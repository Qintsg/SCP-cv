/** 服务器预览短租约：沿用认证／CSRF／主机围栏，不接受响应中的任意跨站流URL。 */
import { buildBackendUrl, requestJson } from '@/services/api';
import type { PreviewTarget } from './previewTarget';
export interface PreviewLease { id: string; sharingKey: string; httpUrl: string; wsUrl: string; expiresAt: string }
/** :param target: 受限来源。:param current: 冻结归属。:param signal: 本轮取消。:returns: 仅绑定原Host的预览地址。 */
export async function openPreview(target: PreviewTarget, current: () => boolean, signal: AbortSignal): Promise<PreviewLease> {
  const result = await requestJson<{ id: string; sharing_key: string; expires_at: string }>('/api/stream-previews/', { method: 'POST', body: JSON.stringify(target), signal }, 10000, { canConsume: current });
  if (!/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(result.id)) throw new Error('预览会话格式无效');
  if (!/^[\da-f]{64}$/i.test(result.sharing_key) || !Number.isFinite(Date.parse(result.expires_at))) throw new Error('预览共享身份无效');
  // mpegts的Blob Worker没有页面相对URL上下文，正式流地址必须在主线程变成绝对URL。
  const root = new URL(buildBackendUrl(`/api/stream-previews/${result.id}`), window.location.href).href;
  const ws = new URL(`${root}/stream.ws`, window.location.href); ws.protocol = ws.protocol === 'https:' ? 'wss:' : 'ws:';
  return { id: result.id, sharingKey: result.sharing_key, httpUrl: `${root}/stream.flv`, wsUrl: ws.href, expiresAt: result.expires_at };
}
/** :param lease: 本轮原租约。:param current: 同一归属。:param signal: 本轮取消。:returns: 已认证续期；无新流创建。 */
export async function heartbeatPreview(lease: PreviewLease, current: () => boolean, signal: AbortSignal, aliases?: readonly string[]): Promise<void> {
  await requestJson(`/api/stream-previews/${lease.id}/heartbeat/`, { method: 'POST', signal,
    ...(aliases ? { body: JSON.stringify({ aliases }) } : {}) }, 8000, { canConsume: current });
}
/** :param lease: 共享池验证或消费租约。:param current: 必须仍是原主机／账号。:returns: 释放；失效归属不向新目标发旧DELETE，服务端租约负责到期。 */
export async function closePreview(lease: PreviewLease, current: () => boolean): Promise<void> {
  if (!current()) return;
  await requestJson(`/api/stream-previews/${lease.id}/`, { method: 'DELETE' }, 5000, { canConsume: current });
}
