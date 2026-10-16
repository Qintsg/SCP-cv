/*
 * 背景音乐 Store：维护全局音频状态、播放列表与远端控制动作。
 * @Project : SCP-cv
 * @File : backgroundAudio.ts
 * @Author : Qintsg
 * @Date : 2026-10-04
 */
import { defineStore } from 'pinia';
import { captureStoreRequestScope, assertStoreRequestScope } from './requestScope';

import {
  api,
  type BackgroundAudioPlaylistItem,
  type BackgroundAudioSnapshot,
  type BackgroundAudioStateSnapshot,
} from '@/services/api';

interface BackgroundAudioStoreState {
  snapshot: BackgroundAudioSnapshot | null;
  loading: boolean;
}

// 刷新寿命属于store实例，不进入持久/可重置状态；主机切换后的新读取不会与旧数字代次重名。
const refreshOwners = new WeakMap<BackgroundAudioStoreState, symbol>();

/** :param store: 音频投影。:param receive: 原归属HTTP。:returns: 只向当前认证投影，保留原时间戳围栏。 */
async function applyOwnedSnapshot(store: { applyRemoteSnapshot: (value: BackgroundAudioSnapshot) => void },
  receive: () => Promise<{ background_audio: BackgroundAudioSnapshot }>): Promise<void> {
  const current = captureStoreRequestScope();
  const response = await receive();
  assertStoreRequestScope(current);
  store.applyRemoteSnapshot(response.background_audio);
}

export const useBackgroundAudioStore = defineStore('backgroundAudio', {
  state: (): BackgroundAudioStoreState => ({
    snapshot: null,
    loading: false,
  }),
  getters: {
    state: (store): BackgroundAudioStateSnapshot | null => store.snapshot?.state ?? null,
    playlist: (store): BackgroundAudioPlaylistItem[] => store.snapshot?.playlist ?? [],
    isPlaying: (store): boolean => store.snapshot?.state.playback_state === 'playing',
    hasCurrentSource: (store): boolean => Boolean(store.snapshot?.state.source_id),
  },
  actions: {
    /**
     * REST与SSE共用观测围栏；相同时间保留列表等非播放状态更新的兼容语义。
     * :param snapshot: 当前主机返回的音频快照，不把HTTP受理当实体播放证明。
     * :returns: 较旧或未知时间不能覆盖已有明确新观测；首次旧格式仍可初始化。
     */
    applyRemoteSnapshot(snapshot: BackgroundAudioSnapshot): void {
      const currentTime = Date.parse(this.snapshot?.state?.updated_at ?? '');
      const incomingTime = Date.parse(snapshot.state?.updated_at ?? '');
      if (this.snapshot && Number.isFinite(currentTime)
        && (!Number.isFinite(incomingTime) || incomingTime < currentTime)) return;
      this.snapshot = snapshot;
    },
    /** :returns: 当前归属音频快照；旧刷新不能解除新读取的loading。 */
    async refresh(): Promise<void> {
      const owner = Symbol('background-audio-refresh');
      refreshOwners.set(this, owner);
      this.loading = true;
      try {
        await applyOwnedSnapshot(this, () => api.getBackgroundAudio());
      } finally {
        if (refreshOwners.get(this) === owner) {
          refreshOwners.delete(this);
          this.loading = false;
        }
      }
    },
    /** :param sourceId: 明确音频源。:returns: 当前归属列表更新，不表示已播放。 */
    async addSource(sourceId: number): Promise<void> {
      await applyOwnedSnapshot(this, () => api.addBackgroundAudioSource(sourceId));
    },
    /** :param sourceId: 明确音频源。:returns: 当前归属的受理/实际快照。 */
    async playSource(sourceId: number): Promise<void> {
      await applyOwnedSnapshot(this, () => api.playBackgroundAudioSource(sourceId));
    },
    /** :param itemId: 列表项目。:returns: 当前归属快照。 */
    async playItem(itemId: number): Promise<void> {
      await applyOwnedSnapshot(this, () => api.playBackgroundAudioItem(itemId));
    },
    /** :param itemId: 列表项目。:returns: 当前归属列表更新。 */
    async removeItem(itemId: number): Promise<void> {
      await applyOwnedSnapshot(this, () => api.removeBackgroundAudioItem(itemId));
    },
    /** :returns: 清空列表的当前归属回执，不操作其它账号投影。 */
    async clearPlaylist(): Promise<void> {
      await applyOwnedSnapshot(this, () => api.clearBackgroundAudioPlaylist());
    },
    /** :param action: 明确控制动作。:param positionMs: 定位值。:returns: 当前归属快照。 */
    async control(action: 'play' | 'pause' | 'stop' | 'next' | 'prev' | 'seek', positionMs = 0): Promise<void> {
      await applyOwnedSnapshot(this, () => api.controlBackgroundAudio(action, positionMs));
    },
    /** :param volume: 音量。:returns: 当前归属回执。 */
    async setVolume(volume: number): Promise<void> {
      await applyOwnedSnapshot(this, () => api.setBackgroundAudioVolume(volume));
    },
    /** :param muted: 静音选择。:returns: 当前归属回执。 */
    async setMute(muted: boolean): Promise<void> {
      await applyOwnedSnapshot(this, () => api.setBackgroundAudioMute(muted));
    },
    /** :param enabled: 循环选择。:returns: 当前归属回执。 */
    async setLoop(enabled: boolean): Promise<void> {
      await applyOwnedSnapshot(this, () => api.setBackgroundAudioLoop(enabled));
    },
  },
});
