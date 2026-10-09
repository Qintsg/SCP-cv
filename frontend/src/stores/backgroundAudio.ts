/*
 * 背景音乐 Store：维护全局音频状态、播放列表与远端控制动作。
 * @Project : SCP-cv
 * @File : backgroundAudio.ts
 * @Author : Qintsg
 * @Date : 2026-10-04
 */
import { defineStore } from 'pinia';

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
    async refresh(): Promise<void> {
      const owner = Symbol('background-audio-refresh');
      refreshOwners.set(this, owner);
      this.loading = true;
      try {
        this.applyRemoteSnapshot((await api.getBackgroundAudio()).background_audio);
      } finally {
        if (refreshOwners.get(this) === owner) {
          refreshOwners.delete(this);
          this.loading = false;
        }
      }
    },
    async addSource(sourceId: number): Promise<void> {
      this.applyRemoteSnapshot((await api.addBackgroundAudioSource(sourceId)).background_audio);
    },
    async playSource(sourceId: number): Promise<void> {
      this.applyRemoteSnapshot((await api.playBackgroundAudioSource(sourceId)).background_audio);
    },
    async playItem(itemId: number): Promise<void> {
      this.applyRemoteSnapshot((await api.playBackgroundAudioItem(itemId)).background_audio);
    },
    async removeItem(itemId: number): Promise<void> {
      this.applyRemoteSnapshot((await api.removeBackgroundAudioItem(itemId)).background_audio);
    },
    async clearPlaylist(): Promise<void> {
      this.applyRemoteSnapshot((await api.clearBackgroundAudioPlaylist()).background_audio);
    },
    async control(action: 'play' | 'pause' | 'stop' | 'next' | 'prev' | 'seek', positionMs = 0): Promise<void> {
      this.applyRemoteSnapshot((await api.controlBackgroundAudio(action, positionMs)).background_audio);
    },
    async setVolume(volume: number): Promise<void> {
      this.applyRemoteSnapshot((await api.setBackgroundAudioVolume(volume)).background_audio);
    },
    async setMute(muted: boolean): Promise<void> {
      this.applyRemoteSnapshot((await api.setBackgroundAudioMute(muted)).background_audio);
    },
    async setLoop(enabled: boolean): Promise<void> {
      this.applyRemoteSnapshot((await api.setBackgroundAudioLoop(enabled)).background_audio);
    },
  },
});
