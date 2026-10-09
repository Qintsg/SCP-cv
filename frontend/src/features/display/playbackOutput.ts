/**
 * 输出参考只取服务端实际源，期望源元数据不能填补缺失的观测。
 * @Project : SCP-cv
 * @File : playbackOutput.ts
 * @Author : Qintsg
 * @Date : 2026-10-04
 */
import type { MediaSourceItem, SessionSnapshot } from '@/services/api';
import { adapterSupportsSourceType } from './playbackCapabilities';

/**
 * 解析最后报告的实际源元数据；不将图片、在线或playing当实体出帧证明。
 * :param session: 当前HTTP/SSE会话。
 * :param findSource: 媒体库按ID查询，不读取播放器原生对象。
 * :returns: 实际源有明确正整数ID、列表存在且类型与实际适配器对应时返回该源，否则为空。
 */
export function resolveActualOutputSource(
  session: Pick<SessionSnapshot, 'actual_source_id' | 'actual_adapter_kind'>,
  findSource: (id: number) => MediaSourceItem | undefined,
): MediaSourceItem | null {
  const id = session.actual_source_id;
  if (typeof id !== 'number' || !Number.isSafeInteger(id) || id <= 0) return null;
  const source = findSource(id);
  return source && adapterSupportsSourceType(session.actual_adapter_kind, source.source_type) ? source : null;
}
