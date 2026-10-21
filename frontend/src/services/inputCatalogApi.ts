/** 受信现场输入目录；仅请求固定API，不接受原始预览URL或控制帧。 */
import { requestJson } from './api';
import type { VideoWallMappingItem } from './apiTypes';
export interface SiteInputItem {
  id: string; name: string; group: 'workstation' | 'camera' | 'site'; ip_suffix: number;
  preview_input: VideoWallMappingItem['input']; source_id: number | null;
  availability: 'unknown' | 'reachable' | 'unreachable'; is_available: boolean; checked_at: string | null; aliases: string[];
}
export const inputCatalogApi = {
  get: (current: () => boolean) => requestJson<{success: boolean; inputs: SiteInputItem[]}>('/api/inputs/', {}, undefined, { canConsume: current }),
  refresh: (force: boolean, current: () => boolean) => requestJson<{success: boolean; inputs: SiteInputItem[]}>('/api/inputs/refresh/', { method: 'POST', body: JSON.stringify({ force }) }, 15000, { canConsume: current }),
};
