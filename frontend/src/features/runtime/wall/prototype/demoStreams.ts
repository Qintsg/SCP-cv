/** 仅本机Canvas视频池：同IP一条MediaStream；不是RTMP/FLV的正式共享池。 */
import { reactive } from 'vue';
import { inputFor } from './catalog';
interface Feed { ip: number; stream: MediaStream; canvas: HTMLCanvasElement; refs: number; serial: number; timer: ReturnType<typeof setInterval> }
const feeds = new Map<number, Feed>();
export const streamStats = reactive({ producers: 0, consumers: 0, created: 0, closed: 0 });
/** :param canvas: 自有画布。:param ip: 规范源。:param serial: 创建序号。:returns: 本机动态桌面示意，绝非现场截图。 */
function draw(canvas: HTMLCanvasElement, ip: number, serial: number): void {
  const ctx = canvas.getContext('2d')!; const clock = new Date().toLocaleTimeString('zh-CN', { hour12: false });
  ctx.fillStyle = '#15243a'; ctx.fillRect(0, 0, 640, 360);
  ctx.fillStyle = '#213650'; ctx.fillRect(0, 0, 640, 27); ctx.fillRect(14, 43, 130, 282);
  ctx.fillStyle = '#9fb1cc'; ctx.font = '12px sans-serif'; ctx.fillText('SCP-cv / SIGNAL PREVIEW', 14, 18);
  ctx.fillStyle = '#dae6f6'; ctx.font = 'bold 32px sans-serif'; ctx.fillText(inputFor(ip).name, 173, 113);
  ctx.fillStyle = '#78b2ff'; ctx.font = '24px Consolas, monospace'; ctx.fillText(`IP${ip}`, 174, 150);
  ctx.fillStyle = '#99adc8'; ctx.font = '15px sans-serif'; ctx.fillText('本机生成的演示画面', 175, 186);
  ctx.font = '18px Consolas, monospace'; ctx.fillText(clock, 175, 248);
  ctx.fillStyle = '#718ba9'; ctx.font = '12px Consolas, monospace'; ctx.fillText(`SOURCE #${serial}  /  SHARED STREAM`, 175, 280);
  for (let i = 0; i < 8; i++) { ctx.fillStyle = i === 2 ? '#3b6fa7' : '#30445f'; ctx.fillRect(26, 64 + i * 26, 101, 14); }
  ctx.fillStyle = '#1677ff'; ctx.fillRect(0, 348, Math.round((performance.now() % 8000) / 8000 * 640), 4);
}
/** :param ip: 规范源，与几何/别名无关。:returns: 引用句柄，仅释放自己的引用。 */
export function acquire(ip: number): { stream: MediaStream; serial: number; reused: boolean; release: () => void } {
  let feed = feeds.get(ip); const reused = !!feed;
  if (!feed) {
    const canvas = document.createElement('canvas'); canvas.width = 640; canvas.height = 360;
    const serial = ++streamStats.created; draw(canvas, ip, serial);
    const stream = canvas.captureStream(8);
    const timer = setInterval(() => { if (!document.hidden) draw(canvas, ip, serial); }, 125);
    feed = { ip, canvas, stream, refs: 0, serial, timer }; feeds.set(ip, feed); streamStats.producers++;
    draw(canvas, ip, serial);
  }
  const owned = feed; owned.refs++; streamStats.consumers++; let released = false;
  return { stream: owned.stream, serial: owned.serial, reused, release() {
    if (released) return; released = true; owned.refs--; streamStats.consumers--;
    if (!owned.refs) { clearInterval(owned.timer); owned.stream.getTracks().forEach(track => track.stop()); feeds.delete(ip); streamStats.producers--; streamStats.closed++; }
  } };
}
/** :returns: 窗口退出回收所有原型媒体，不触碰外部资源。 */
export function disposeFeeds(): void { for (const feed of feeds.values()) { clearInterval(feed.timer); feed.stream.getTracks().forEach(track => track.stop()); } feeds.clear(); }
window.addEventListener('pagehide', disposeFeeds, { once: true });
