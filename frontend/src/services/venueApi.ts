/* 现场设备合同：来源和话筒只有后端能力允许后才可编辑。 */
import { requestJson } from './api';

export interface VenueAudioSettings {
  screen_volume?: number | null;
  handheld_volume?: number | null;
  hanging_volume?: number | null;
  source?: 'ip81' | 'scp_cv' | null;
}
export interface VenueAudioResult { status: string; detail: string; screen_volume?: number | null }
export interface VenueCamera {
  id: string; name: string; path: string; source_id: number;
  stream_configured: boolean; movable: boolean; control_available: boolean; unavailable_reason: string;
}
export interface VenueState {
  cameras: VenueCamera[]; audio: VenueAudioSettings;
  capabilities: { screen_volume: boolean; handheld_volume: boolean; hanging_volume: boolean; source: boolean; reason: string };
  simulation: boolean;
}
export const venueApi = {
  get: () => requestJson<{ success: boolean; venue: VenueState }>('/api/venue/'),
  readAudio: () => requestJson<{ success: boolean; audio: VenueAudioResult }>('/api/venue/audio/read/', { method: 'POST' }),
  applyAudio: (audio: VenueAudioSettings) => requestJson<{ success: boolean; audio: VenueAudioResult }>('/api/venue/audio/',
    { method: 'PUT', body: JSON.stringify(audio) }),
};
