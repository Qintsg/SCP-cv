/** 应用级预览池：每实际流一连接/decoder，多画布共享；每target独立修订围栏与有界引用回收。 */
import type { InjectionKey } from 'vue';
import { ApiRequestError } from '@/services/api';
import { createPreviewPlayer, type PreviewPlayer, type PreviewPlayerFactory } from './previewPlayer';
import { createPreviewTransport, type PoolLease, type PreviewTransport } from './previewTransport';
import type { PreviewTarget } from './previewTarget';

export type PreviewState = 'idle' | 'connecting' | 'live' | 'reconnecting' | 'unavailable' | 'empty';
export type PreviewFrame = HTMLVideoElement | HTMLCanvasElement;
export interface PreviewSink { frame(video: PreviewFrame): void; state(state: PreviewState): void; clear(): void }
interface Request {
  key: string; target: PreviewTarget; current: () => boolean; sinks: Set<PreviewSink>; abort: AbortController;
  lease?: PoolLease; entry?: Entry; idleTimer?: ReturnType<typeof setTimeout>; retryTimer?: ReturnType<typeof setTimeout>; pending?: Promise<void>; closed: boolean;
}
interface Entry {
  key: string; requests: Set<Request>; current: () => boolean; abort: AbortController; state: PreviewState;
  lease?: PoolLease; player?: PreviewPlayer; video?: HTMLVideoElement; paintedVideo?: HTMLVideoElement; pending?: Promise<void>; generation: number;
  timer?: ReturnType<typeof setTimeout>; monitor?: ReturnType<typeof setInterval>; lastFrame: number;
  attempts: number; hasFrame: boolean; fallback: boolean; closed: boolean; failureSince: number; still?: HTMLCanvasElement;
}
interface PoolTiming { idleGrace: number; heartbeat: number; monitor: number; stale: number; failed: number; retry: number }
const defaults: PoolTiming = { idleGrace: 1000, heartbeat: 10000, monitor: 1000, stale: 3000, failed: 20000, retry: 1000 };
export const PREVIEW_POOL: InjectionKey<PreviewPool> = Symbol('preview-pool');
const pools = new WeakMap<PreviewPlayerFactory, PreviewPool>();

/** :param factory: 正式/测试播放器边界。:returns: 同一应用播放器工厂共享同一池，不归第一个组件所有。 */
export function getPreviewPool(factory: PreviewPlayerFactory = createPreviewPlayer): PreviewPool {
  let pool = pools.get(factory); if (!pool) { pool = new PreviewPool(factory, createPreviewTransport()); pools.set(factory, pool); }
  return pool;
}

export class PreviewPool {
  private readonly requests = new Map<string, Request>();
  private readonly entries = new Map<string, Entry>();
  private readonly timing: PoolTiming;

  constructor(private readonly factory: PreviewPlayerFactory, private readonly transport: PreviewTransport,
    timing: Partial<PoolTiming> = {}) { this.timing = { ...defaults, ...timing }; }

  /** :returns: 只读计数，不暴露来源/凭据；请求数是不同target而非画布数。 */
  get diagnostics(): { targets: number; streams: number; decoders: number } {
    return { targets: this.requests.size, streams: this.entries.size, decoders: [...this.entries.values()].filter(entry => !!entry.player).length };
  }

  /** :param target: 受限来源。:param sink: 当前画布。:param current: 独立应用认证scope。:returns: 幂等引用释放，不将几何/清晰度提示纳入连接键。 */
  subscribe(target: PreviewTarget, sink: PreviewSink, current: () => boolean): () => void {
    this.invalidateObsolete();
    const key = JSON.stringify(target.source_id ? { source_id: target.source_id } : { input: target.input });
    let request = this.requests.get(key);
    if (!request) {
      request = { key, target, current, sinks: new Set(), abort: new AbortController(), closed: false };
      this.requests.set(key, request);
    }
    clearTimeout(request.idleTimer); clearTimeout(request.retryTimer); request.sinks.add(sink);
    if (request.entry) { sink.state(request.entry.state); const frame = request.entry.paintedVideo ?? request.entry.still; if (request.entry.hasFrame && frame) sink.frame(frame); }
    else { sink.state('connecting'); void this.resolve(request); }
    let released = false;
    return () => {
      if (released) return; released = true; request!.sinks.delete(sink);
      if (!request!.sinks.size) request!.idleTimer = setTimeout(() => this.drop(request!), this.timing.idleGrace);
    };
  }

  /** :returns: 只清理失效认证/主机scope；某画布卸载不影响其它画布。 */
  invalidateObsolete(): void { for (const request of [...this.requests.values()]) if (!request.current()) this.drop(request, true); }

  /** :returns: 页面隐藏/应用结束立即清需求，不保温、不重放；原归属释放由transport围栏保护。 */
  dispose(): void { for (const request of [...this.requests.values()]) this.drop(request, true); }

  private valid(request: Request): boolean { return !request.closed && request.current() && this.requests.get(request.key) === request; }
  /** :param lease: 自有验证/消费租约。:param current: 原归属围栏。:returns: 幂等尽力释放，失联由租约到期兜底。 */
  private release(lease: PoolLease | undefined, current: () => boolean): void {
    if (lease) void this.transport.release(lease, current).catch(() => { /* 服务端TTL或原生所有者租约兜底。 */ });
  }
  /** :param entry: 唯一实际流。:param state: 实际帧/寿命状态。:returns: 同步广播，不携带原始异常或凭据。 */
  private state(entry: Entry, state: PreviewState): void {
    if (entry.closed) return; entry.state = state;
    for (const request of entry.requests) for (const sink of request.sinks) sink.state(state);
  }

  /** :param request: 去重target。:returns: 合并进行中的解析，同target不为每画布POST。 */
  private resolve(request: Request): Promise<void> {
    if (request.pending) return request.pending;
    const pending = this.resolveOnce(request).finally(() => { if (request.pending === pending) request.pending = undefined; });
    request.pending = pending; return pending;
  }
  /** :param request: 固定来源及应用scope。:returns: 只取得非消费验证租约，不启动GET。 */
  private async resolveOnce(request: Request): Promise<void> {
    if (!this.valid(request)) return;
    try {
      const lease = await this.transport.acquire(request.target, request.current, request.abort.signal);
      if (!this.valid(request)) { this.release(lease, request.current); return; }
      this.attach(request, lease);
    } catch (failure) {
      if (!this.valid(request)) return;
      const empty = failure instanceof ApiRequestError && failure.code === 'preview_no_stream';
      for (const sink of request.sinks) sink.state(empty ? 'empty' : 'unavailable');
      if (!empty) request.retryTimer = setTimeout(() => { void this.resolve(request); }, this.timing.retry * 2);
    }
  }

  /** :param request: 明确别名。:param lease: 已解析HMAC身份。:returns: 原子合并相同实际流，源变更只迁本别名。 */
  private attach(request: Request, lease: PoolLease): void {
    const previous = request.entry;
    if (previous && previous.key !== lease.sharingKey) {
      previous.requests.delete(request); if (!previous.requests.size) this.close(previous);
      for (const sink of request.sinks) sink.clear();
    }
    this.release(request.lease, request.current); request.lease = lease;
    let entry = this.entries.get(lease.sharingKey);
    if (!entry) {
      entry = { key: lease.sharingKey, requests: new Set(), current: request.current, abort: new AbortController(),
        state: 'connecting', generation: 0, lastFrame: 0, attempts: 0, hasFrame: false, fallback: false, closed: false, failureSince: 0 };
      this.entries.set(entry.key, entry);
    }
    if (!entry.requests.has(request) && entry.requests.size >= 64) {
      this.release(lease, request.current); request.lease = undefined; request.entry = undefined;
      for (const sink of request.sinks) sink.state('unavailable'); return;
    }
    entry.requests.add(request); request.entry = entry;
    for (const sink of request.sinks) { sink.state(entry.state); const frame = entry.paintedVideo ?? entry.still; if (entry.hasFrame && frame) sink.frame(frame); }
    if (!entry.pending && !entry.player && !entry.timer) void this.start(entry);
  }

  /** :param entry: 实际流占位。:returns: 同组只有一份在途消费租约/decoder创建。 */
  private start(entry: Entry): Promise<void> {
    if (entry.pending) return entry.pending;
    const pending = this.startOnce(entry).finally(() => { if (entry.pending === pending) entry.pending = undefined; });
    entry.pending = pending; return pending;
  }
  /** :param entry: 已有有效验证别名。:returns: 获取独立消费租约，不把首个别名寿命当整组唯一寿命。 */
  private async startOnce(entry: Entry): Promise<void> {
    const request = [...entry.requests].find(item => this.valid(item) && item.lease);
    if (!request || entry.closed || !entry.current()) return;
    const generation = ++entry.generation;
    this.state(entry, entry.failureSince && Date.now() - entry.failureSince > this.timing.failed ? 'unavailable' : entry.hasFrame ? 'reconnecting' : 'connecting');
    try {
      const lease = await this.transport.acquire(request.target, entry.current, entry.abort.signal);
      if (entry.closed || generation !== entry.generation || !entry.current()) { this.release(lease, entry.current); return; }
      if (lease.sharingKey !== entry.key) {
        this.release(lease, entry.current); await this.resolve(request);
        if (!entry.closed && entry.requests.size) this.failed(entry); return;
      }
      entry.lease = lease; entry.fallback = false;
      await this.player(entry, generation);
      if (!entry.closed && generation === entry.generation) this.schedule(entry);
    } catch { if (!entry.closed && generation === entry.generation) this.failed(entry); }
  }

  /** :param entry: 共享消费。:param generation: 本轮decoder代次。:returns: 一个video广播真实解码帧。 */
  private async player(entry: Entry, generation: number): Promise<void> {
    if (!entry.lease) return;
    const video = document.createElement('video'); video.muted = true; video.volume = 0; video.autoplay = true; video.playsInline = true;
    video.setAttribute('aria-hidden', 'true'); video.dataset.previewDecoder = 'shared';
    video.style.cssText = 'position:fixed;left:-10000px;top:0;width:1px;height:1px;opacity:0;pointer-events:none';
    document.body.append(video); entry.video = video; entry.lastFrame = Date.now();
    const managed = await this.factory(video, entry.lease.streamUrl, () => {
      if (entry.closed || generation !== entry.generation || !entry.current()) return;
      entry.hasFrame = true; entry.paintedVideo = video; entry.lastFrame = Date.now(); entry.attempts = 0; entry.failureSince = 0; entry.still = undefined; this.state(entry, 'live');
      for (const request of entry.requests) for (const sink of request.sinks) sink.frame(video);
    }, () => { if (!entry.closed && generation === entry.generation) void this.decodeFailed(entry); });
    if (entry.closed || generation !== entry.generation || !entry.current()) { managed.destroy(); video.remove(); return; }
    entry.player = managed;
    clearInterval(entry.monitor);
    entry.monitor = setInterval(() => {
      if (!entry.current()) { this.invalidateObsolete(); return; }
      const gap = Date.now() - entry.lastFrame;
      if (gap > this.timing.failed) this.failed(entry);
      else if (gap > this.timing.stale) { entry.failureSince ||= Date.now(); this.state(entry, 'reconnecting'); }
    }, this.timing.monitor);
  }

  /** :param entry: 当前解码器故障。:returns: 原生至多一次受限兼容切换；普通Web走有限退避。 */
  private async decodeFailed(entry: Entry): Promise<void> {
    if (!entry.fallback && this.transport.fallback && entry.lease) {
      entry.fallback = true; const generation = ++entry.generation; this.stopPlayer(entry); this.state(entry, 'reconnecting');
      try {
        await this.transport.fallback(entry.lease);
        if (!entry.closed && generation === entry.generation) { await this.player(entry, generation); this.schedule(entry); }
        return;
      }
      catch { /* 兼容切换也失败时进入同一有界重连，不伪装原码直连成功。 */ }
    }
    if (!entry.closed) this.failed(entry);
  }

  /** :param entry: 原流。:param delay: 有界心跳间隔。:returns: 同一流只保留一个心跳timer。 */
  private schedule(entry: Entry, delay = this.timing.heartbeat): void {
    clearTimeout(entry.timer); entry.timer = setTimeout(() => { entry.timer = undefined; void this.renew(entry); }, delay);
  }
  /** :param entry: 共享流。:returns: 先逐target校验修订，再用剩余同URI别名续消费租约。 */
  private async renew(entry: Entry): Promise<void> {
    if (entry.closed || !entry.current()) { this.invalidateObsolete(); return; }
    await Promise.all([...entry.requests].map(async request => {
      if (!request.lease || !this.valid(request)) return;
      try { await this.transport.heartbeat(request.lease, request.current, request.abort.signal); }
      catch {
        if (!this.valid(request)) return;
        const old = request.lease; request.lease = undefined; this.release(old, request.current);
        entry.requests.delete(request); request.entry = undefined;
        for (const sink of request.sinks) { sink.clear(); sink.state('reconnecting'); }
        await this.resolve(request);
      }
    }));
    if (entry.closed) return;
    const aliases = [...entry.requests].filter(request => this.valid(request) && request.lease).map(request => request.lease!.id);
    if (!aliases.length) { this.close(entry); return; }
    try { if (entry.lease) await this.transport.heartbeat(entry.lease, entry.current, entry.abort.signal, aliases); this.schedule(entry); }
    catch {
      if (!entry.closed) { this.state(entry, 'reconnecting'); this.schedule(entry, Math.min(1000, this.timing.heartbeat)); }
    }
  }

  /** :param entry: 唯一自有decoder。:returns: 保留有界最后帧并尽力完成全部销毁阶段。 */
  private stopPlayer(entry: Entry): void {
    clearInterval(entry.monitor); entry.monitor = undefined;
    if (entry.hasFrame && entry.paintedVideo?.videoWidth && entry.paintedVideo.videoHeight) {
      const last = entry.paintedVideo, scale = Math.min(1, 1280 / last.videoWidth, 720 / last.videoHeight);
      const still = document.createElement('canvas'); still.width = Math.max(1, Math.round(last.videoWidth * scale));
      still.height = Math.max(1, Math.round(last.videoHeight * scale));
      try { const context = still.getContext('2d'); if (context) { context.drawImage(last, 0, 0, still.width, still.height); entry.still = still; } }
      catch { /* 旧画布继续保留各自最后帧，不使其它消费者失败。 */ }
    }
    try { entry.player?.destroy(); } catch { /* 仍清理自有video与租约。 */ }
    entry.player = undefined; entry.video?.remove(); entry.video = undefined; entry.paintedVideo = undefined;
  }
  /** :param entry: 当前流。:returns: 只重连此实际流，其它组与canvas不受影响。 */
  private failed(entry: Entry): void {
    if (entry.closed) return;
    entry.generation++; clearTimeout(entry.timer); this.stopPlayer(entry);
    const lease = entry.lease; entry.lease = undefined; this.release(lease, entry.current);
    entry.failureSince ||= Date.now();
    this.state(entry, entry.hasFrame && Date.now() - entry.failureSince < this.timing.failed ? 'reconnecting' : 'unavailable');
    entry.timer = setTimeout(() => { entry.timer = undefined; void this.start(entry); }, Math.min(10000, this.timing.retry * ++entry.attempts));
  }
  /** :param request: 最后引用释放/归属失效的target。:param clear: 失效时清像素。:returns: 原子移除，不让旧timer杀死新target。 */
  private drop(request: Request, clear = false): void {
    if (request.closed) return;
    request.closed = true; clearTimeout(request.idleTimer); clearTimeout(request.retryTimer); request.abort.abort();
    if (this.requests.get(request.key) === request) this.requests.delete(request.key);
    if (clear) for (const sink of request.sinks) { sink.clear(); sink.state('idle'); }
    request.sinks.clear(); this.release(request.lease, request.current); request.lease = undefined;
    if (request.entry) { request.entry.requests.delete(request); if (!request.entry.requests.size) this.close(request.entry); }
    request.entry = undefined;
  }
  /** :param entry: 已无有效别名的实际流。:returns: 幂等关闭自有资源，释放过程中不创建第二份decoder。 */
  private close(entry: Entry): void {
    if (entry.closed) return;
    entry.closed = true; entry.generation++; entry.abort.abort(); clearTimeout(entry.timer); this.stopPlayer(entry);
    this.release(entry.lease, entry.current); entry.lease = undefined;
    if (this.entries.get(entry.key) === entry) this.entries.delete(entry.key);
  }
}
