/**
 * 窗口级音量能力必须与后端 playback_window_controls 的入队前校验保持一致。
 * 使用真实 source_type 判断，避免把能力不同的源聚合为同一 UI 大类后误开放操作。
 * @Project : SCP-cv
 * @File : playbackCapabilities.ts
 * @Author : Qintsg
 * @Date : 2026-10-04
 */
import type { SessionSnapshot } from '@/services/api';

const ADAPTER_TYPES: Readonly<Record<string, readonly string[]>> = {
  image: ['image'], web: ['web'], vlc: ['video', 'audio', 'custom_stream', 'rtsp_stream', 'srt_stream'],
  libreoffice: ['ppt'], pdf: ['ppt'], slide_images: ['ppt'],
};
const WINDOW_AUDIO_SOURCE_TYPES = new Set([
  'video',
  'custom_stream',
  'rtsp_stream',
  'srt_stream',
]);

/** :param sourceType: 请求的媒体类型。:returns: 后台支持窗口音量操作的类型。 */
export function supportsWindowAudioControls(sourceType: string | null | undefined): boolean {
  return sourceType !== null
    && sourceType !== undefined
    && WINDOW_AUDIO_SOURCE_TYPES.has(sourceType);
}

/** :param adapter: 实际适配器。:param type: 媒体类型。:returns: 该类型可由适配器承载，不证明输入推进或能力细项。 */
export function adapterSupportsSourceType(adapter: string | undefined, type: string): boolean {
  return ADAPTER_TYPES[adapter ?? '']?.includes(type) ?? false;
}

/**
 * 只约束源类型能力，不把同类型切换、普通动画Pending或playing当完成证明。
 * :param session: HTTP/SSE会话；老服务缺实际字段时保持既有能力。
 * :returns: 明确空输出、非法实际ID、未知或不对应的实际适配器时拒绝类型相关操作。
 */
export function supportsRequestedPlaybackControls(session: SessionSnapshot | undefined): boolean {
  if (!session?.source_id) return false;
  if (session.actual_source_id === undefined) return true;
  if (!Number.isSafeInteger(session.actual_source_id) || (session.actual_source_id ?? 0) <= 0) return false;
  return adapterSupportsSourceType(session.actual_adapter_kind, session.source_type);
}

/**
 * 为滑块提供按值稳定的目标身份，普通心跳和音量/位置回写不使缓冲失效。
 * :param session: 当前请求与实际观测。
 * :returns: 换窗口、换源、重开代次或实际适配器变化都会改变的值标识。
 */
export function playbackSliderScope(session: SessionSnapshot): string {
  return JSON.stringify([session.window_id, session.source_id, session.source_uri,
    session.actual_source_id, session.actual_adapter_kind, session.desired_generation, session.observed_generation]);
}
