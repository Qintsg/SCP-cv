/**
 * 文件描述：按窗口持有共享采集，控制页面仅观察状态。
 * 项目：SCP-cv；文件：screenShareStore.ts；作者：Qintsg；日期：2026-10-30。
 */
import { computed, onScopeDispose, shallowReactive, shallowRef, watch } from 'vue';
import { defineStore } from 'pinia';
import { clientConnection } from '@/platform/connection';
import { getClientBuildTarget } from '@/platform';
import { useAuthStore } from '@/stores/auth';
import { useSessionStore } from '@/stores/sessions';
import { screenSharingApi } from '@/services/screenSharing';
import { captureAuthenticationScope } from '@/services/authenticationScope';
import { screenShareAvailability } from './availability';
import { createScreenSharePeer } from './rtcPeer';
import { ScreenSharePublisher } from './ScreenSharePublisher';

/** :param windowId: 实际逻辑窗口。:returns: 与当前账号及主机绑定的共享所有者。 */
function createOwner(windowId: number) {
  const auth = useAuthStore(), sessions = useSessionStore();
  const generation = clientConnection.captureGeneration(), account = auth.user, authentication = captureAuthenticationScope();
  const expired = shallowRef(false);
  const isCurrent = () => !expired.value && generation === clientConnection.captureGeneration() && auth.user === account && authentication();
  const online = () => sessions.byWindowId(windowId)?.player_online === true;
  const upgradeAvailable = computed(() => isCurrent() && getClientBuildTarget() === 'web'
    && window.location.protocol === 'http:' && auth.isAuthenticated && online());
  const availability = computed(() => expired.value ? 'share_context_changed' : screenShareAvailability({
    secure: window.isSecureContext && window.location.protocol === 'https:', web: getClientBuildTarget() === 'web',
    capture: typeof navigator.mediaDevices?.getDisplayMedia === 'function', peer: typeof RTCPeerConnection === 'function',
    authenticated: auth.isAuthenticated, online: online(),
  }));
  const publisher = new ScreenSharePublisher({ windowId: () => windowId, availability: () => availability.value,
    isCurrent, capture: options => navigator.mediaDevices.getDisplayMedia(options), peer: createScreenSharePeer,
    api: screenSharingApi, changed: next => { view.value = next; } });
  const view = shallowRef(publisher.view);
  const active = computed(() => ['selecting', 'connecting', 'sharing', 'stopping'].includes(view.value.phase));
  return { windowId, view, availability, active, upgradeAvailable, isCurrent,
    start: (audio: boolean) => publisher.start(audio), stop: () => publisher.stop(),
    refreshStop: () => publisher.refreshStop(), setAudio: (volume: number, muted: boolean) => publisher.setAudio(volume, muted),
    offline: () => publisher.stop('share_player_offline'),
    dispose: () => { void publisher.dispose(); expired.value = true; } };
}
export type SharedScreenOwner = ReturnType<typeof createOwner>;

export const useScreenShareStore = defineStore('screenShares', () => {
  const auth = useAuthStore(), sessions = useSessionStore();
  const owners = shallowReactive(new Map<number, SharedScreenOwner>());
  /** :returns: 终止当前采集并清除旧身份的可见条目。 */
  function disposeAll(): void { for (const owner of owners.values()) owner.dispose(); owners.clear(); }
  const unsubscribe = clientConnection.onGenerationChanged(disposeAll);
  watch(() => [auth.user, auth.user?.id, captureAuthenticationScope()], disposeAll, { flush: 'sync' });
  watch(() => sessions.sessions.map(session => [session.window_id, session.player_online] as const), values => {
    for (const [windowId, online] of values) if (!online) void owners.get(windowId)?.offline();
  });
  onScopeDispose(() => { unsubscribe(); disposeAll(); });
  /** :param windowId: 控制目标。:returns: 同窗口复用当前发布者，导航不重新采集。 */
  function forWindow(windowId: number): SharedScreenOwner {
    if (![1, 2].includes(windowId)) throw new Error('无效的共享窗口。');
    let owner = owners.get(windowId);
    if (!owner) { owner = createOwner(windowId); owners.set(windowId, owner); }
    return owner;
  }
  const activeOwners = computed(() => [...owners.values()].filter(owner => owner.active.value));
  return { forWindow, activeOwners, disposeAll };
});
