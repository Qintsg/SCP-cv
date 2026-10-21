/** 原生预览的受限平台合同；共享业务不依赖Electron、原始流地址或进程对象。 */
export type NativePreviewInputKind = 'window_1' | 'window_2' | 'window_3' | 'window_4' | 'laptop' | 'ip_stream';
export interface NativePreviewRequest {
  source_id?: number;
  input?: { kind: NativePreviewInputKind; ip_address?: string };
  quality: 'low' | 'high';
}
export interface NativePreviewLease { id: string; sharingKey: string; streamUrl: string; expiresAt: string }
export interface NativePreviewAdapter {
  acquire(request: NativePreviewRequest): Promise<NativePreviewLease>;
  heartbeat(id: string, aliases?: readonly string[]): Promise<void>;
  release(id: string): Promise<void>;
  invalidate(): Promise<void>;
  /** :param id: 自己的租约。:returns: 确认原码进程退出后，至多一次兼容重开。 */
  fallback?(id: string): Promise<void>;
}
