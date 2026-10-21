/** 原生短租约注册表：主机/完整票据/来源修订归属、延迟创建围栏和同源进程共享。 */
import { randomBytes, randomUUID } from 'node:crypto';
import { NativeFlvFeed } from './flvFeed.js';
import { NativePreviewError, validateNativeDescriptor, validateNativeId, validateNativeRequest,
  type NativeDescriptor, type NativeLease, type NativeRequest } from './contracts.js';
import type { PreviewMediaProcess } from './helperProcess.js';

export interface NativeRegistryDependencies {
  owner(): Promise<string>;
  resolve(request: NativeRequest, signal: AbortSignal): Promise<unknown>;
  produce(uri: string, mode: 'copy' | 'compatible'): PreviewMediaProcess;
  now?(): number;
}
interface LeaseState { lease: NativeLease; nonce: string; owner: string; generation: number; descriptor: NativeDescriptor;
  request: NativeRequest; consumed: boolean; expires: number; feed?: FeedState }
interface FeedState { key: string; feed?: NativeFlvFeed; mode: 'copy' | 'compatible'; leases: Set<string>; stopping?: Promise<void>; error?: boolean }

export class NativePreviewRegistry {
  private readonly leases = new Map<string, LeaseState>();
  private readonly feeds = new Map<string, FeedState>();
  private readonly pending = new Set<AbortController>();
  private generation = 0;
  private disposed = false;
  private readonly maintenance: ReturnType<typeof setInterval>;
  /** :param dependencies: 主进程固定认证与自有进程；测试可注入无设备实现。 */
  constructor(private readonly dependencies: NativeRegistryDependencies) {
    this.maintenance = setInterval(() => { void this.expire(); }, 1000); this.maintenance.unref();
  }
  /** :param raw: 受限renderer请求。:returns: 不含原地址的本人能力租约，创建本身不拉流。 */
  async acquire(raw: unknown): Promise<NativeLease> {
    const request = validateNativeRequest(raw), generation = this.generation, owner = await this.dependencies.owner();
    if (this.disposed || generation !== this.generation || !owner) throw new NativePreviewError('native_preview_stale_owner');
    if (this.leases.size + this.pending.size >= 128) throw new NativePreviewError('native_preview_capacity');
    const abort = new AbortController(); this.pending.add(abort);
    try {
      const descriptor = validateNativeDescriptor(await this.dependencies.resolve(request, abort.signal));
      const completedOwner = await this.dependencies.owner();
      if (generation !== this.generation || owner !== completedOwner || this.disposed) throw new NativePreviewError('native_preview_stale_owner');
      if (this.leases.size >= 128) throw new NativePreviewError('native_preview_capacity');
      const id = randomUUID(), nonce = randomBytes(32).toString('hex'), expires = this.expires(descriptor);
      const lease = { id, sharingKey: descriptor.sharing_key, streamUrl: `app://scp-cv/__preview/${nonce}/stream.flv`, expiresAt: new Date(expires).toISOString() };
      this.leases.set(id, { lease, nonce, owner, generation, descriptor, request, consumed: false, expires });
      return { ...lease };
    } finally { this.pending.delete(abort); }
  }
  /** :param rawId: 本人租约。:returns: 固定API复核源修订后续期，不重复创建上游。 */
  async heartbeat(rawId: unknown, aliases?: unknown): Promise<void> {
    const state = await this.owned(rawId), abort = new AbortController(); this.pending.add(abort);
    try {
      if (aliases !== undefined && (!Array.isArray(aliases) || aliases.length > 64)) throw new NativePreviewError('native_preview_invalid_alias');
      const candidates = aliases === undefined ? [state] : await Promise.all((aliases as unknown[]).map(id => this.owned(id)));
      if (candidates.some(item => item.owner !== state.owner || item.descriptor.sharing_key !== state.descriptor.sharing_key || item.descriptor.uri !== state.descriptor.uri)) throw new NativePreviewError('native_preview_invalid_alias');
      let replacement: { selected: LeaseState; descriptor: NativeDescriptor } | undefined;
      for (const selected of candidates) {
        try {
          const current = validateNativeDescriptor(await this.dependencies.resolve(selected.request, abort.signal));
          if (current.sharing_key === state.descriptor.sharing_key && current.uri === state.descriptor.uri) { replacement = { selected, descriptor: current }; break; }
        } catch { if (abort.signal.aborted) throw new NativePreviewError('native_preview_stale_owner'); }
      }
      const completedOwner = await this.dependencies.owner();
      if (!this.leases.has(state.lease.id) || state.generation !== this.generation || state.owner !== completedOwner) throw new NativePreviewError('native_preview_stale_owner');
      if (!replacement) {
        await this.drop(state); throw new NativePreviewError('native_preview_source_changed');
      }
      state.request = replacement.selected.request; state.descriptor = replacement.descriptor;
      state.expires = this.expires(replacement.descriptor);
    } finally { this.pending.delete(abort); }
  }
  /** :param rawId: 必须仍属当前登录。:returns: 幂等释放，不让旧主机ID操作新归属。 */
  async release(rawId: unknown): Promise<void> {
    const id = validateNativeId(rawId); if (!this.leases.has(id)) return;
    await this.drop(await this.owned(id));
  }
  /** :param rawId: 当前本人租约。:returns: 全部原码进程已退出才换兼容模式；至多一次。 */
  async fallback(rawId: unknown): Promise<void> {
    const state = await this.owned(rawId), selected = state.feed;
    if (!selected || selected.mode !== 'copy' || selected.stopping) throw new NativePreviewError('native_preview_fallback_unavailable');
    selected.stopping = selected.feed?.stop() ?? Promise.resolve();
    await selected.stopping;
    const completedOwner = await this.dependencies.owner();
    if (state.generation !== this.generation || state.owner !== completedOwner) throw new NativePreviewError('native_preview_stale_owner');
    selected.feed = undefined; selected.error = false; selected.mode = 'compatible'; selected.stopping = undefined;
    for (const id of selected.leases) { const lease = this.leases.get(id); if (lease) lease.consumed = false; }
  }
  /** :param request: app自有协议GET。:returns: 只有本人不可猜nonce能取得内存流。 */
  async stream(request: Request): Promise<Response> {
    try {
      const url = new URL(request.url), match = /^\/__preview\/([\da-f]{64})\/stream\.flv$/.exec(url.pathname);
      if (request.method !== 'GET' || url.protocol !== 'app:' || url.hostname !== 'scp-cv' || url.search || url.hash || !match) throw new NativePreviewError('native_preview_invalid_lease');
      const state = [...this.leases.values()].find(item => item.nonce === match[1]);
      if (!state) throw new NativePreviewError('native_preview_invalid_lease');
      await this.owned(state.lease.id);
      if (request.signal.aborted) throw new NativePreviewError('native_preview_cancelled');
      if (state.consumed) throw new NativePreviewError('native_preview_already_consumed');
      const key = `${state.owner}:${state.descriptor.sharing_key}`;
      let selected = this.feeds.get(key);
      if (selected?.stopping || selected?.error) throw new NativePreviewError('native_preview_cleanup_pending');
      if (!selected) {
        if (this.feeds.size >= 6) throw new NativePreviewError('native_preview_capacity');
        selected = { key, mode: 'copy', leases: new Set() }; this.feeds.set(key, selected);
      }
      state.feed = selected; selected.leases.add(state.lease.id); state.consumed = true;
      if (!selected.feed) {
        const own = selected;
        own.feed = new NativeFlvFeed(this.dependencies.produce(state.descriptor.uri, own.mode), () => { own.error = true; });
      }
      const body = selected.feed!.subscribe(request.signal, () => { state.consumed = false; });
      selected.feed!.start();
      return new Response(body, { headers: { 'Content-Type': 'video/x-flv', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' } });
    } catch { return new Response('native_preview_unavailable', { status: 403, headers: { 'Cache-Control': 'no-store' } }); }
  }
  /** :returns: 先同步失效所有旧意图，再确认自有feed退出。 */
  async invalidate(): Promise<void> {
    this.generation++; for (const abort of this.pending) abort.abort(); this.pending.clear();
    this.leases.clear();
    const feeds = [...this.feeds.values()];
    for (const selected of feeds) selected.stopping ??= selected.feed?.stop() ?? Promise.resolve();
    await Promise.all(feeds.map(async selected => { await selected.stopping; if (this.feeds.get(selected.key) === selected) this.feeds.delete(selected.key); }));
  }
  /** :returns: 关闭维护器并回收全部本人资源。 */
  async dispose(): Promise<void> { this.disposed = true; clearInterval(this.maintenance); await this.invalidate(); }
  /** :param rawId: 租约身份。:returns: 认证实际Cookie及租约寿命均仍有效。 */
  private async owned(rawId: unknown): Promise<LeaseState> {
    const id = validateNativeId(rawId), selected = this.leases.get(id);
    const owner = await this.dependencies.owner();
    if (!selected || this.leases.get(id) !== selected || selected.generation !== this.generation || selected.expires <= this.now() || selected.owner !== owner) throw new NativePreviewError('native_preview_stale_owner');
    return selected;
  }
  /** :param descriptor: 服务端短有效期。:returns: 最长30秒，服务端时限不可信时拒绝。 */
  private expires(descriptor: NativeDescriptor): number {
    const expires = Math.min(Date.parse(descriptor.expires_at), this.now() + 30000);
    if (expires <= this.now()) throw new NativePreviewError('native_preview_expired'); return expires;
  }
  /** :returns: 可注入伪时钟。 */
  private now(): number { return this.dependencies.now?.() ?? Date.now(); }
  /** :param state: 原租约对象。:returns: 最后引用退出后才删feed，失败保留门禁。 */
  private async drop(state: LeaseState): Promise<void> {
    if (this.leases.get(state.lease.id) !== state) return;
    this.leases.delete(state.lease.id); const selected = state.feed; if (!selected) return;
    selected.leases.delete(state.lease.id);
    if (selected.leases.size) return;
    selected.stopping ??= selected.feed?.stop() ?? Promise.resolve(); await selected.stopping;
    if (this.feeds.get(selected.key) === selected) this.feeds.delete(selected.key);
  }
  /** :returns: TTL兜底并复核实际账号，不把同ID重登当原票据。 */
  private async expire(): Promise<void> {
    try {
      const owner = await this.dependencies.owner();
      for (const state of [...this.leases.values()]) if (state.expires <= this.now() || state.owner !== owner) await this.drop(state);
    } catch { /* 状态过期仍由下一轮重查；不声称失败清理已完成。 */ }
  }
}
