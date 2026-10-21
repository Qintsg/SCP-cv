/** 私有原码FLV共享分发：完整tag边界，有界关键帧缓存，慢读取者不阻塞其它订阅。 */
import type { PreviewMediaProcess } from './helperProcess.js';
import { NativePreviewError } from './contracts.js';

const MAX_TAG_BYTES = 8 * 1024 * 1024;
const MAX_CACHE_BYTES = 16 * 1024 * 1024;
const HEADER = Uint8Array.from([70, 76, 86, 1, 1, 0, 0, 0, 9, 0, 0, 0, 0]);
export class NativeFlvFeed {
  private readonly viewers = new Set<ReadableStreamDefaultController<Uint8Array>>();
  private configuration: Uint8Array | undefined;
  private metadata: Uint8Array | undefined;
  private cache: Uint8Array[] = [];
  private cacheSize = 0;
  private pending = Buffer.alloc(0);
  private header = false;
  private ready = false;
  private stopped = false;
  private pumping: Promise<void> | undefined;
  /** :param producer: 单个自有宿主。:param failed: 固定失败回调，不传原文。 */
  constructor(private readonly producer: PreviewMediaProcess, private readonly failed: () => void) {}
  /** :returns: 只启动一轮stdout读取；监听者无独立上游。 */
  start(): void {
    if (this.pumping) return;
    this.pumping = (async () => {
      try {
        for await (const chunk of this.producer.output) this.accept(Buffer.from(chunk));
        if (!this.stopped) throw new NativePreviewError('native_preview_stream_ended');
      } catch { if (!this.stopped) { this.stopped = true; this.end(); try { await this.producer.stop(); } catch { /* stop保留失败门禁供显式清理，不能无界重开。 */ } finally { this.failed(); } } }
    })();
  }
  /** :param signal: 原协议GET取消。:param closed: 消费者关闭释放回调。:returns: 从配置和有效IDR起始的只读流。 */
  subscribe(signal: AbortSignal, closed: () => void): ReadableStream<Uint8Array> {
    let own: ReadableStreamDefaultController<Uint8Array> | undefined, released = false;
    const release = (): void => {
      if (released) return; released = true;
      if (own) this.viewers.delete(own);
      signal.removeEventListener('abort', abort); closed();
    };
    const abort = (): void => { if (own) { try { own.close(); } catch {} } release(); };
    return new ReadableStream<Uint8Array>({
      start: controller => {
        own = controller;
        if (this.stopped || signal.aborted) { controller.close(); release(); return; }
        controller.enqueue(HEADER);
        if (this.ready) {
          if (this.metadata) controller.enqueue(this.metadata);
          if (this.configuration) controller.enqueue(this.configuration);
          for (const tag of this.cache) controller.enqueue(tag);
        }
        this.viewers.add(controller); signal.addEventListener('abort', abort, { once: true });
      }, cancel: release,
    }, { highWaterMark: MAX_CACHE_BYTES, size: value => value.byteLength });
  }
  /** :returns: 停所有本人输出；确认Job/进程退出再完成。 */
  async stop(): Promise<void> { this.stopped = true; this.end(); await this.producer.stop(); await this.pumping; }
  /** :returns: 关闭协议body，不保留最近帧或带凭据元数据。 */
  private end(): void {
    for (const controller of this.viewers) { try { controller.close(); } catch {} }
    this.viewers.clear(); this.cache = []; this.cacheSize = 0; this.pending = Buffer.alloc(0);
  }
  /** :param chunk: FFmpeg输出。:returns: 只接收有界标准FLV，不暴露原始媒体错误。 */
  private accept(chunk: Buffer): void {
    if (this.stopped) return;
    this.pending = Buffer.concat([this.pending, chunk]);
    if (this.pending.length > MAX_TAG_BYTES * 2) throw new NativePreviewError('native_preview_invalid_stream');
    if (!this.header) {
      if (this.pending.length < 13) return;
      if (this.pending.toString('ascii', 0, 3) !== 'FLV' || this.pending.readUInt32BE(5) !== 9) throw new NativePreviewError('native_preview_invalid_stream');
      this.pending = this.pending.subarray(13); this.header = true;
    }
    while (this.pending.length >= 15) {
      const payload = this.pending.readUIntBE(1, 3), length = 15 + payload;
      if (length > MAX_TAG_BYTES) throw new NativePreviewError('native_preview_invalid_stream');
      if (this.pending.length < length) return;
      const tag = Uint8Array.from(this.pending.subarray(0, length)); this.pending = this.pending.subarray(length);
      if (tag[0] === 18) { this.metadata = tag; continue; }
      if (tag[0] !== 9 || payload < 5) continue;
      const codec = tag[11]! & 15, keyframe = (tag[11]! >> 4) === 1;
      if (codec !== 7) throw new NativePreviewError('native_preview_incompatible_codec');
      if (tag[12] === 0) { this.configuration = tag; this.ready = false; this.cache = []; this.cacheSize = 0; continue; }
      if (tag[12] !== 1 || !this.configuration) continue;
      if (keyframe) {
        this.ready = true; this.cache = []; this.cacheSize = 0;
        if (this.metadata) this.publish(this.metadata);
        this.publish(this.configuration);
      }
      if (!this.ready) continue;
      if (this.cacheSize + tag.byteLength > MAX_CACHE_BYTES) throw new NativePreviewError('native_preview_invalid_stream');
      this.cache.push(tag); this.cacheSize += tag.byteLength; this.publish(tag);
    }
  }
  /** :param tag: 完整独立tag。:returns: 慢消费者有界失败，不积累无限数据。 */
  private publish(tag: Uint8Array): void {
    for (const controller of this.viewers) {
      try {
        if ((controller.desiredSize ?? 0) < tag.byteLength) { controller.error(new NativePreviewError('native_preview_slow_consumer')); this.viewers.delete(controller); }
        else controller.enqueue(tag);
      } catch { this.viewers.delete(controller); }
    }
  }
}
