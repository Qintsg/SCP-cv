/**
 * 将有效播放器心跳与实际状态共同投影为播出标签，选中源不等于已播出。
 * @Project : SCP-cv
 * @File : playbackObservation.ts
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import type { SessionSnapshot } from '@/services/api';

const SHARE_ID = /^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i;
/** :param session: 当前请求。:returns: 有效临时共享意图，不把空媒体库ID等同共享。 */
export function isRequestedScreenShare(session: Partial<SessionSnapshot> | null | undefined): boolean {
  return session?.source_id === null && session.source_type === 'screen_share'
    && typeof session.share_id === 'string' && SHARE_ID.test(session.share_id);
}
/** :param session: 原Worker实际观测。:returns: 非文件资源身份明确，帧数可为零但不代表出画。 */
export function isActualScreenShare(session: Partial<SessionSnapshot> | null | undefined): boolean {
  return session?.actual_source_id === null && session.actual_adapter_kind === 'screen_share'
    && typeof session.actual_share_id === 'string' && SHARE_ID.test(session.actual_share_id)
    && Number.isSafeInteger(session.observed_generation) && (session.observed_generation ?? 0) > 0
    && Number.isSafeInteger(session.actual_frames) && (session.actual_frames ?? -1) >= 0;
}

/**
 * 判断当前会话是否具备前端可展示的实际播出确认。
 * :param session: REST/SSE 返回的会话观测，尚未加载时可为空。
 * :returns: 仅有效源、在线 Worker 且实际 playing 时为真；不证明实体出帧。
 */
export function isConfirmedPlayback(
  session: (Pick<SessionSnapshot, 'source_id' | 'player_online' | 'playback_state'>
    & Partial<SessionSnapshot>) | null | undefined,
): boolean {
  if (isRequestedScreenShare(session)) {
    return isActualScreenShare(session) && session!.actual_share_id!.toLowerCase() === session!.share_id!.toLowerCase()
      && session!.desired_generation === session!.observed_generation
      && (session!.actual_frames ?? 0) > 0 && session!.player_online && session!.playback_state === 'playing';
  }
  if (session?.actual_source_id !== undefined && session.actual_source_id !== session.source_id) return false;
  if (session?.desired_generation !== undefined && session.observed_generation !== undefined
    && session.observed_generation !== session.desired_generation) return false;
  return Boolean(session?.source_id && session.player_online && session.playback_state === 'playing');
}

/** :param session: 当前请求和实际输出。:returns: 共享零帧/不匹配不显示成功色，其余保持既有媒体标签规则。 */
export function playbackStateTagType(session: SessionSnapshot): 'default' | 'success' | 'warning' | 'error' {
  if (!session.source_id && !isRequestedScreenShare(session) && !isActualScreenShare(session)) return 'default';
  if (session.playback_state === 'error') return 'error';
  if (isRequestedScreenShare(session)) return isConfirmedPlayback(session) ? 'success' : 'warning';
  if (!session.source_id && isActualScreenShare(session)) return 'warning';
  if (session.playback_state === 'playing') return 'success';
  return ['paused', 'loading'].includes(session.playback_state) ? 'warning' : 'default';
}
