/**
 * 将有效播放器心跳与实际状态共同投影为播出标签，选中源不等于已播出。
 * @Project : SCP-cv
 * @File : playbackObservation.ts
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import type { SessionSnapshot } from '@/services/api';

/**
 * 判断当前会话是否具备前端可展示的实际播出确认。
 * :param session: REST/SSE 返回的会话观测，尚未加载时可为空。
 * :returns: 仅有效源、在线 Worker 且实际 playing 时为真；不证明实体出帧。
 */
export function isConfirmedPlayback(
  session: Pick<SessionSnapshot, 'source_id' | 'player_online' | 'playback_state'> | null | undefined,
): boolean {
  return Boolean(session?.source_id && session.player_online && session.playback_state === 'playing');
}
