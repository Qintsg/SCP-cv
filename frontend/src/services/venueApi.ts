/* 现场设备合同：来源和话筒只有后端能力允许后才可编辑。 */
import { exclusiveWrite, readAfterWrite, requestJson } from './api';

export interface VenueAudioSettings {
  screen_volume?: number | null;
  computer_volume?: number | null;
  handheld_volume?: number | null;
  headset_volume?: number | null;
  hanging_volume?: number | null;
  source?: 'ip81' | 'scp_cv' | null;
}
/** :param audio: 新旧保存字段。:returns: 同一话筒是否存在不同请求值，界面不得隐藏冲突。 */
export function microphoneAliasesConflict(audio?: VenueAudioSettings | null): boolean {
  return audio?.headset_volume != null && audio.hanging_volume != null && audio.headset_volume !== audio.hanging_volume;
}
/** :param audio: 已确认的同组别名。:returns: 唯一显示值；冲突时留空要求明确编辑。 */
export function microphoneVolume(audio?: VenueAudioSettings | null): number | null {
  return microphoneAliasesConflict(audio) ? null : audio?.headset_volume ?? audio?.hanging_volume ?? null;
}
export interface VenueAudioResult { status: string; detail: string; screen_volume?: number | null; source?: VenueAudioSettings['source']; sent_fields?: string[]; failed_field?: string | null }
export type CameraDirection = 'up' | 'down' | 'left' | 'right' | 'zoom_in' | 'zoom_out';
export interface CameraMotionRequest { direction: CameraDirection; phase: 'start' | 'hold' | 'stop'; motion_id: string; issued_at_ms: number }
export interface VenueCameraResult { status: string; detail: string; motion_id?: string | null }
export interface VenueCamera {
  id: string; name: string; path: string; source_id: number;
  stream_configured: boolean; movable: boolean; control_available: boolean; unavailable_reason: string;
  position_count?: number; motion_intent?: 'idle' | 'held' | 'stop_pending';
}
export interface VenueState {
  cameras: VenueCamera[]; audio: VenueAudioSettings;
  capabilities: { screen_volume: boolean; computer_volume?: boolean; handheld_volume: boolean; headset_volume?: boolean; hanging_volume: boolean; source: boolean; source_ip81?: boolean; source_scp_cv?: boolean; reason: string; hanging_label?: string; headset_label?: string };
  simulation: boolean;
  server_time_ms?: number;
}
export const venueApi = {
  get: (canConsume: () => boolean = () => true) => readAfterWrite('venue-audio', () => requestJson<{ success: boolean; venue: VenueState }>('/api/venue/', {}, undefined, { canConsume }), canConsume),
  readAudio: () => exclusiveWrite('venue-audio', () => requestJson<{ success: boolean; audio: VenueAudioResult }>('/api/venue/audio/read/', { method: 'POST' })),
  applyAudio: (audio: VenueAudioSettings) => exclusiveWrite('venue-audio', () => requestJson<{ success: boolean; audio: VenueAudioResult }>('/api/venue/audio/',
    { method: 'PUT', body: JSON.stringify(audio) })),
  motion: (id: string, request: CameraMotionRequest) => requestJson<{ success: boolean; camera: VenueCameraResult }>(`/api/venue/cameras/${encodeURIComponent(id)}/motion/`,
    { method: 'POST', body: JSON.stringify(request) }),
  position: (id: string, position: number, action: 'call' | 'save') => exclusiveWrite(`camera-position:${id}`, () => requestJson<{ success: boolean; camera: VenueCameraResult }>(`/api/venue/cameras/${encodeURIComponent(id)}/positions/${position}/${action}/`, { method: 'POST', body: '{}' })),
};
