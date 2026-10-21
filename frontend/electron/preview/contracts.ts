/** 主进程预览合同与边界校验；拒绝renderer提供原始URI或运行参数。 */
export interface NativeRequest {
  source_id?: number;
  input?: { kind: string; ip_address?: string };
  quality: 'low' | 'high';
}
export interface NativeDescriptor {
  uri: string; sharing_key: string; source_id: number | null; source_revision: number; expires_at: string; quality: 'low' | 'high';
}
export interface NativeLease { id: string; sharingKey: string; streamUrl: string; expiresAt: string }
export class NativePreviewError extends Error {
  /** :param code: 固定公开错误，不能携带FFmpeg原文或URI。 */
  constructor(readonly code: string) { super(code); this.name = 'NativePreviewError'; }
}
/** :param raw: 不可信IPC对象。:returns: 新建受限输入；多余字段拒绝。 */
export function validateNativeRequest(raw: unknown): NativeRequest {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) throw new NativePreviewError('native_preview_invalid_request');
  const body = raw as Record<string, unknown>;
  if (Object.keys(body).some(key => !['source_id', 'input', 'quality'].includes(key)) || !['low', 'high'].includes(String(body.quality))) throw new NativePreviewError('native_preview_invalid_request');
  if ((body.source_id === undefined) === (body.input === undefined)) throw new NativePreviewError('native_preview_invalid_request');
  if (body.source_id !== undefined) {
    if (!Number.isSafeInteger(body.source_id) || (body.source_id as number) <= 0) throw new NativePreviewError('native_preview_invalid_request');
    return { source_id: body.source_id as number, quality: body.quality as 'low' | 'high' };
  }
  const input = body.input as Record<string, unknown>;
  if (!input || typeof input !== 'object' || Array.isArray(input) || Object.keys(input).some(key => !['kind', 'ip_address'].includes(key))) throw new NativePreviewError('native_preview_invalid_request');
  if (!['window_1', 'window_2', 'window_3', 'window_4', 'laptop', 'ip_stream'].includes(String(input.kind))) throw new NativePreviewError('native_preview_invalid_request');
  if (input.kind === 'ip_stream') {
    const address = input.ip_address;
    if (typeof address !== 'string' || !/^192\.168\.5\.(?:[1-9]\d?|1\d\d|2[0-4]\d|25[0-4])$/.test(address)) throw new NativePreviewError('native_preview_invalid_request');
    return { input: { kind: 'ip_stream', ip_address: address }, quality: body.quality as 'low' | 'high' };
  }
  if (input.ip_address !== undefined) throw new NativePreviewError('native_preview_invalid_request');
  return { input: { kind: String(input.kind) }, quality: body.quality as 'low' | 'high' };
}
/** :param raw: 当前认证主机的响应。:returns: 有界且只读的原生描述符，不允许其它协议。 */
export function validateNativeDescriptor(raw: unknown): NativeDescriptor {
  const descriptor = raw as NativeDescriptor;
  if (!descriptor || typeof descriptor.uri !== 'string' || descriptor.uri.length > 8192 || /[\x00-\x20\x7f]/.test(descriptor.uri)) throw new NativePreviewError('native_preview_invalid_descriptor');
  let uri: URL;
  try { uri = new URL(descriptor.uri); } catch { throw new NativePreviewError('native_preview_invalid_descriptor'); }
  if (!['rtmp:', 'rtsp:'].includes(uri.protocol) || !uri.hostname || uri.hash || !/^[\w-]{16,160}$/.test(descriptor.sharing_key)
    || !Number.isSafeInteger(descriptor.source_revision) || descriptor.source_revision < 0 || !Number.isFinite(Date.parse(descriptor.expires_at))
    || !['low', 'high'].includes(descriptor.quality) || !(descriptor.source_id === null || Number.isSafeInteger(descriptor.source_id) && descriptor.source_id > 0)) throw new NativePreviewError('native_preview_invalid_descriptor');
  return { uri: uri.href, sharing_key: descriptor.sharing_key, source_id: descriptor.source_id,
    source_revision: descriptor.source_revision, expires_at: descriptor.expires_at, quality: descriptor.quality };
}
/** :param raw: 租约ID。:returns: 无路径／控制字符的固定UUID。 */
export function validateNativeId(raw: unknown): string {
  if (typeof raw !== 'string' || !/^[\da-f]{8}-(?:[\da-f]{4}-){3}[\da-f]{12}$/i.test(raw)) throw new NativePreviewError('native_preview_invalid_lease');
  return raw;
}
