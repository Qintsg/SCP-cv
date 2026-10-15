/** 首页两窗实际状态投影；在线、受理和代次匹配分别判断，不发送命令。 */
import type { SessionSnapshot } from '@/services/api';
import { isConfirmedPlayback } from '../display/playbackObservation';
/** :param session: 原REST/SSE快照。:returns: 可读状态与颜色，不证明现场墙面出画。 */
export function windowPresentation(session?: Partial<SessionSnapshot>): { label: string; tone: 'default' | 'success' | 'warning' | 'error' } {
  if (!session) return { label: '等待连接', tone: 'default' };
  if (!session.player_online) return { label: '播放器离线', tone: 'error' };
  if (session.playback_state === 'error') return { label: '播放失败', tone: 'error' };
  if (session.pending_command) return { label: '正在处理', tone: 'warning' };
  if (isConfirmedPlayback(session as SessionSnapshot)) return { label: '播放中', tone: 'success' };
  if (session.playback_state === 'playing') return { label: '正在切换', tone: 'warning' };
  if (session.playback_state === 'paused') return { label: '已暂停', tone: 'warning' };
  if (session.playback_state === 'loading') return { label: '正在加载', tone: 'warning' };
  return { label: '空闲', tone: 'default' };
}
