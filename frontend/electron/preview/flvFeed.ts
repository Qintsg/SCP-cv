/** 文件描述：成熟FLV媒体的单来源共享分发；项目：SCP-cv；文件：flvFeed.ts；作者：Qintsg；日期：2026-10-21。 */
import type { PreviewMediaProcess } from './helperProcess.js';
import { NativePreviewError } from './contracts.js';
import { NativeFlvLibrary, type NativeFlvTag } from './flvLibrary.js';

const MAX_CACHE_BYTES = 16 * 1024 * 1024;
export class NativeFlvFeed {
  private readonly viewers = new Set<ReadableStreamDefaultController<Uint8Array>>();
  private configuration: Uint8Array | undefined;
  private metadata: Uint8Array | undefined;
  private cache: Uint8Array[] = [];
  private cacheSize = 0;
  private header: Uint8Array | undefined;
  private readonly library: NativeFlvLibrary;
  private ready = false;
  private stopped = false;
  private pumping: Promise<void> | undefined;
  /** :param producer: 单个自有宿主。:param failed: 固定失败回调，不传原文。 */
  constructor(private readonly producer: PreviewMediaProcess, private readonly failed: (error: NativePreviewError) => void) {
    this.library = new NativeFlvLibrary(bytes => { this.header = bytes; this.publish(bytes); },
      tag => this.accept(tag), error => { this.producer.output.destroy(error); });
  }
  /** :returns: 只启动一轮stdout读取；监听者无独立上游。 */
  start(): void {
    if (this.pumping) return;
    this.pumping = (async () => {
      try {
        for await (const chunk of this.producer.output) this.library.accept(Buffer.from(chunk));
        this.library.finish();
        if (!this.stopped) throw new NativePreviewError('native_preview_stream_ended');
      } catch (error) {
        if (this.stopped) return;
        this.stopped = true;
        let failure = error instanceof NativePreviewError ? error : new NativePreviewError('native_preview_invalid_stream');
        this.end(failure);
        try { await this.producer.stop(); }
        catch { failure = new NativePreviewError('native_preview_cleanup_pending'); }
        this.failed(failure);
      }
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
        if (this.header) controller.enqueue(this.header);
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
  private end(error?: NativePreviewError): void {
    for (const controller of this.viewers) { try { if (error) controller.error(error); else controller.close(); } catch {} }
    this.viewers.clear(); this.cache = []; this.cacheSize = 0; this.header = undefined;
    this.configuration = undefined; this.metadata = undefined; this.library.dispose();
  }
  /** :param value: 成熟组件校验的标签。:returns: 配置与关键帧起始的有界共享输出。 */
  private accept(value: NativeFlvTag): void {
    if (this.stopped) return;
    const tag = value.bytes;
    if (value.kind === 'metadata') { this.metadata = tag; return; }
    if (value.kind === 'configuration') { this.configuration = tag; this.ready = false; this.cache = []; this.cacheSize = 0; return; }
    if (value.kind === 'end' || !this.configuration) return;
    if (value.kind === 'keyframe') {
      this.ready = true; this.cache = []; this.cacheSize = 0;
      if (this.metadata) this.publish(this.metadata);
      this.publish(this.configuration);
    }
    if (!this.ready) return;
    if (this.cacheSize + tag.byteLength > MAX_CACHE_BYTES) throw new NativePreviewError('native_preview_invalid_stream');
    this.cache.push(tag); this.cacheSize += tag.byteLength; this.publish(tag);
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
