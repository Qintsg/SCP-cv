// 文件描述：真实视频采集与正式共享发布者的本机验收。项目：SCP-cv；文件：probe.ts；作者：Qintsg；日期：2026-10-30。
import { createApp, h, ref, nextTick } from 'vue';
import { createPinia } from 'pinia';
import { useAuthStore } from '@/stores/auth';
import { useScreenShare } from '@/features/screen-share/useScreenShare';
import { useScreenShareStore } from '@/features/screen-share/screenShareStore';
import { ScreenSharePublisher } from '@/features/screen-share/ScreenSharePublisher';
import { createScreenSharePeer } from '@/features/screen-share/rtcPeer';
import { waitShareTick } from '@/features/screen-share/shareClock';
import { screenSharingApi } from '@/services/screenSharing';
const pinia = createPinia(), auth = useAuthStore(pinia); await auth.ensureInitialized();
const store = useScreenShareStore(pinia), owner = store.forWindow(1), mounted = ref(true);
const view = ref(null), checks = [], abort = new AbortController();
const Observer = { setup() { const sharing = useScreenShare(() => 1); return () => h('output', sharing.view.value.phase); } };
const app = createApp({ setup: () => () => mounted.value ? h(Observer) : h('main', '控制页面已离开') }).use(pinia); app.mount('#app');
const video = document.createElement('video'); video.muted = true; video.volume = 0; video.loop = true; video.src = '/actual-video.mp4'; video.playsInline = true; document.body.append(video); await video.play();
await new Promise<void>(resolve => video.requestVideoFrameCallback(() => resolve()));
const stream = video.captureStream(); const tracks = stream.getTracks();
// 文件媒体通过原生captureStream实际采集，未替换浏览器getDisplayMedia或网络接口。
const publisher = new ScreenSharePublisher({ windowId: () => 1, availability: () => '', isCurrent: () => auth.isAuthenticated,
  capture: async () => stream, peer: createScreenSharePeer, api: screenSharingApi, changed: next => { view.value = next; } });
window.share030 = { done: false, error: '', checks, ticks: 0, snapshots: [] };
try {
  await publisher.start(false);
  const deadline = Date.now() + 25000;
  while (publisher.view.phase !== 'sharing') { if (Date.now() > deadline || publisher.view.phase === 'failed') throw new Error(publisher.view.errorCode || '共享未出画'); await waitShareTick(abort.signal); }
  const id = publisher.view.snapshot.id, first = publisher.view.snapshot.actual_frames;
  for (let index = 0; index < 10; index++) {
    mounted.value = false; await nextTick();
    await waitShareTick(abort.signal); window.share030.ticks++;
    if (store.forWindow(1) !== owner) throw new Error('共享所有者随组件卸载改变。');
    mounted.value = true; await nextTick(); checks.push({ index, id: publisher.view.snapshot.id, frames: publisher.view.snapshot.actual_frames });
  }
  // 超过原30秒发布租约，核对工作线程心跳确实续期。
  for (let index = 0; index < 25; index++) { await waitShareTick(abort.signal); window.share030.ticks++; }
  if (publisher.view.phase !== 'sharing' || publisher.view.snapshot.id !== id || publisher.view.snapshot.actual_frames <= first + 30) throw new Error('共享连接或实际后帧未保持。');
  window.share030.snapshots.push({ id, first, final: publisher.view.snapshot.actual_frames });
  await publisher.stop();
  for (let index = 0; index < 15 && publisher.view.phase === 'stopping'; index++) { await waitShareTick(abort.signal); await publisher.refreshStop(); }
  if (!['closed', 'failed'].includes(publisher.view.phase) || tracks.some(track => track.readyState !== 'ended')) throw new Error('共享资源未释放。');
  window.share030.phase = publisher.view.phase;
} catch (error) { window.share030.error = error instanceof Error ? error.message : String(error); }
finally { await publisher.dispose(); abort.abort(); store.disposeAll(); app.unmount(); video.pause(); video.removeAttribute('src'); video.load(); video.remove(); window.share030.done = true; }
