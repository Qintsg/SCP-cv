/** 临时共享发布者HTTP合同，复用登录/CSRF/主机代次；不公开接收凭据。SCP-cv 2026-10-11。 */
import { requestJson } from './api';
import type { PlaybackCommandSnapshot } from './apiTypes';

export interface ScreenShareSdp { type: 'offer' | 'answer'; sdp: string }
export interface ScreenShareSnapshot {
  id: string;
  window_id: number;
  state: 'allocated' | 'negotiating' | 'receiving' | 'stopping' | 'closed' | 'failed';
  has_audio: boolean;
  receiver_has_audio: boolean;
  source_generation: number | null;
  expires_at: string;
  actual_frames: number;
  last_frame_at: string | null;
  answer: ScreenShareSdp | null;
  stop_reason: string;
  actual_volume: number | null;
  actual_muted: boolean | null;
}
export interface ScreenShareAudioSubmission {
  submitted_command_id: string;
  window_id: number;
  share_id: string;
  source_generation: number;
}
export interface ScreenSharingApi {
  create(window: number, hasAudio: boolean, signal?: AbortSignal): Promise<ScreenShareSnapshot>;
  offer(id: string, offer: ScreenShareSdp, signal?: AbortSignal): Promise<ScreenShareSnapshot>;
  get(id: string, signal?: AbortSignal): Promise<ScreenShareSnapshot>;
  heartbeat(id: string, signal?: AbortSignal): Promise<ScreenShareSnapshot>;
  stop(id: string, signal?: AbortSignal): Promise<ScreenShareSnapshot>;
  setAudio(id: string, volume: number, muted: boolean, signal?: AbortSignal): Promise<ScreenShareAudioSubmission>;
  getAudioCommand(window: number, command: string, signal?: AbortSignal): Promise<PlaybackCommandSnapshot>;
}

/** :param path: 本模块固定路由。:param init: 当前尝试的请求。:returns: 原认证封装的非缓存共享快照。 */
async function shareRequest(path: string, init: RequestInit): Promise<ScreenShareSnapshot> {
  return (await requestJson<{ success: boolean; share: ScreenShareSnapshot }>(path, { ...init, cache: 'no-store' })).share;
}
/** :param id: 非秘密会话标识。:returns: 当前模块的固定会话路由，不接受任意URL。 */
function sharePath(id: string): string {
  if (!/^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i.test(id)) throw new Error('share_identity_invalid');
  return `/api/screen-shares/${id}/`;
}

export const screenSharingApi: ScreenSharingApi = {
  create: (window, hasAudio, signal) => shareRequest('/api/screen-shares/', { method: 'POST', body: JSON.stringify({ window_id: window, has_audio: hasAudio }), signal }),
  offer: (id, offer, signal) => shareRequest(`${sharePath(id)}offer/`, { method: 'POST', body: JSON.stringify(offer), signal }),
  get: (id, signal) => shareRequest(sharePath(id), { signal }),
  heartbeat: (id, signal) => shareRequest(`${sharePath(id)}heartbeat/`, { method: 'POST', signal }),
  stop: (id, signal) => shareRequest(sharePath(id), { method: 'DELETE', signal }),
  setAudio: (id, volume, muted, signal) => requestJson<ScreenShareAudioSubmission>(`${sharePath(id)}audio/`, {
    method: 'PATCH', body: JSON.stringify({ volume, muted }), signal, cache: 'no-store',
  }),
  getAudioCommand: async (window, command, signal) => {
    if (![1, 2].includes(window) || !/^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i.test(command)) throw new Error('share_identity_invalid');
    return (await requestJson<{ command: PlaybackCommandSnapshot }>(`/api/playback/${window}/commands/${command}/`, { signal, cache: 'no-store' })).command;
  },
};
