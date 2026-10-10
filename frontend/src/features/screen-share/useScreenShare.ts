/** Vue只负责共享状态呈现与页面/账号寿命；媒体资源由独占发布者拥有。SCP-cv 2026-10-11。 */
import { computed, onBeforeUnmount, shallowRef, watch } from 'vue';
import { clientConnection } from '@/platform/connection';
import { getClientBuildTarget } from '@/platform';
import { useAuthStore } from '@/stores/auth';
import { screenSharingApi } from '@/services/screenSharing';
import { screenShareAvailability } from './availability';
import { createScreenSharePeer } from './rtcPeer';
import { ScreenSharePublisher } from './ScreenSharePublisher';

/** :param target: 当前逻辑窗口。:param online: 真实Player在线标识。:returns: 一页面实例的一次授权生命周期，离开即停采集。 */
export function useScreenShare(target: () => number, online: () => boolean) {
  const auth = useAuthStore();
  const generation = clientConnection.captureGeneration();
  const ownerId = auth.user?.id;
  const expired = shallowRef(false);
  const availability = computed(() => expired.value ? 'share_context_changed' : screenShareAvailability({
    secure: window.isSecureContext, web: getClientBuildTarget() === 'web',
    capture: typeof navigator.mediaDevices?.getDisplayMedia === 'function',
    peer: typeof RTCPeerConnection === 'function', authenticated: auth.isAuthenticated, online: online(),
  }));
  const publisher = new ScreenSharePublisher({
    windowId: target, availability: () => availability.value,
    isCurrent: () => generation === clientConnection.captureGeneration() && auth.user?.id === ownerId,
    capture: (options) => navigator.mediaDevices.getDisplayMedia(options), peer: createScreenSharePeer,
    api: screenSharingApi, changed: (next) => { view.value = next; },
  });
  const view = shallowRef(publisher.view);
  const active = computed(() => ['selecting', 'connecting', 'sharing', 'stopping'].includes(view.value.phase));
  const stop = (): Promise<void> => publisher.stop();
  const pageEnded = (): void => {
    expired.value = true;
    removeGenerationWatch();
    void publisher.dispose();
  };
  const removeGenerationWatch = clientConnection.onGenerationChanged(pageEnded);
  window.addEventListener('pagehide', pageEnded);
  watch(() => auth.user?.id, pageEnded);
  watch(online, (value) => { if (!value) void publisher.stop('share_player_offline'); });
  onBeforeUnmount(() => { window.removeEventListener('pagehide', pageEnded); pageEnded(); });
  return { view, availability, active, start: (audio: boolean) => publisher.start(audio), stop, refreshStop: () => publisher.refreshStop(),
    setAudio: (volume: number, muted: boolean) => publisher.setAudio(volume, muted) };
}
