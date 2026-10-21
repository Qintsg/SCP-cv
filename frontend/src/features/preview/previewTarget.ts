/** 浏览器仅提交来源身份，不生成或传递原始RTSP／RTMP地址。 */
import type { VideoWallMappingItem } from '@/services/apiTypes';
import { validIp } from '../runtime/wall/layoutModel';
export interface PreviewTarget { source_id?: number; input?: VideoWallMappingItem['input']; quality: 'low' | 'high' }
/** :param input: 墙面输入。:param sourceId: 已登记媒体。:param quality: 观看意图。:returns: 单一受限来源；预留／无效输入不请求。 */
export function previewTarget(input: VideoWallMappingItem['input'] | undefined, sourceId: number | undefined, quality: 'low' | 'high'): PreviewTarget | undefined {
  if (sourceId !== undefined) return !input && Number.isSafeInteger(sourceId) && sourceId > 0 ? { source_id: sourceId, quality } : undefined;
  if (!input) return undefined;
  if (input.kind === 'ip_stream') return validIp(input.ip_address || '') ? { input: { kind: input.kind, ip_address: input.ip_address }, quality } : undefined;
  return ['window_1', 'window_2', 'window_3', 'window_4', 'laptop'].includes(input.kind) ? { input: { kind: input.kind }, quality } : undefined;
}
