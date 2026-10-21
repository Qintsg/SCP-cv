/** 共享池仅依赖受限租约；Web保持服务器FLV，Electron只使用已安装的原生适配。 */
import { getNativePlatformAdapter } from '@/platform/native';
import type { NativePreviewAdapter, NativePreviewLease } from '@/platform/previewTypes';
import type { PreviewTarget } from './previewTarget';
import { openPreview, heartbeatPreview, closePreview, type PreviewLease } from './previewClient';

export type PoolLease = NativePreviewLease;
export interface PreviewTransport {
  acquire(target: PreviewTarget, current: () => boolean, signal: AbortSignal): Promise<PoolLease>;
  heartbeat(lease: PoolLease, current: () => boolean, signal: AbortSignal, aliases?: readonly string[]): Promise<void>;
  release(lease: PoolLease, current: () => boolean): Promise<void>;
  fallback?(lease: PoolLease): Promise<void>;
}

/** :returns: 按当前平台选择一次传输；Web绝不尝试IPC，Electron缺能力不伪装直连。 */
export function createPreviewTransport(): PreviewTransport {
  // main.ts在app.mount后才安装平台Adapter；preload的受限bridge早已就绪，不能把首屏永久缓存为无能力。
  const native: NativePreviewAdapter | undefined = getNativePlatformAdapter()?.preview
    ?? (typeof window === 'undefined' ? undefined : window.scpCvElectron?.preview);
  if (native) return {
    async acquire(target, current, signal) {
      if (!current() || signal.aborted) throw new Error('预览归属已失效');
      const lease = await native.acquire(target);
      if (!current() || signal.aborted) { await native.release(lease.id).catch(() => {}); throw new Error('预览归属已失效'); }
      return lease;
    },
    async heartbeat(lease, current, signal, aliases) { if (!current() || signal.aborted) throw new Error('预览归属已失效'); await native.heartbeat(lease.id, aliases); },
    async release(lease) { await native.release(lease.id); },
    ...(native.fallback ? { fallback: async (lease: PoolLease) => { await native.fallback!(lease.id); } } : {}),
  };
  if (typeof window !== 'undefined' && window.scpCvElectron) return {
    async acquire() { throw new Error('原生预览组件不可用'); }, async heartbeat() { throw new Error('原生预览组件不可用'); }, async release() {},
  };
  const originals = new Map<string, PreviewLease>();
  return {
    async acquire(target, current, signal) { const lease = await openPreview(target, current, signal); originals.set(lease.id, lease);
      return { id: lease.id, sharingKey: lease.sharingKey, streamUrl: lease.httpUrl, expiresAt: lease.expiresAt }; },
    async heartbeat(lease, current, signal, aliases) {
      const original = originals.get(lease.id); if (!original) throw new Error('预览租约已释放');
      await heartbeatPreview(original, current, signal, aliases);
    },
    async release(lease, current) { const original = originals.get(lease.id); originals.delete(lease.id); if (original) await closePreview(original, current); },
  };
}
