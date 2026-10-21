/** 文件描述：成熟FLV组件及有界完整性检查；项目：SCP-cv；文件：flvLibrary.ts；作者：Qintsg；日期：2026-10-21。 */
import flvee, { type FlvObject, type Packet } from 'flvee';
import { readVideo, setOptions, type as mediaTypes } from '@mediafish/flv';
import { NativePreviewError } from './contracts.js';

setOptions({ strictMode: true });
export interface NativeFlvTag { bytes: Uint8Array; kind: 'metadata' | 'configuration' | 'keyframe' | 'video' | 'end'; timestamp: number }
const MAX_TAG_BYTES = 8 * 1024 * 1024;

export class NativeFlvLibrary {
  private readonly parser = new flvee.Parser({ highWaterMark: 4 }, true);
  private readonly writer = new flvee.Serialiser({ highWaterMark: 4 });
  private received = 0;
  private accounted = 0;
  private packets = 0;
  private footers = 0;
  private originalHeader = Buffer.alloc(0);
  private generatedHeader: Uint8Array | undefined;
  private waiting: Packet | undefined;
  private current: Omit<NativeFlvTag, 'bytes'> | undefined;
  private failure: NativePreviewError | undefined;

  /** :param header: 成熟库生成并核对的文件头。:param tag: 已校验完整标签。:param failed: 同一读取过程的错误通知。 */
  constructor(private readonly header: (bytes: Uint8Array) => void, private readonly tag: (value: NativeFlvTag) => void,
    failed: (error: NativePreviewError) => void) {
    this.parser.on('data', (event: FlvObject) => this.object(event));
    this.writer.on('data', (bytes: Buffer) => this.written(bytes));
    for (const stream of [this.parser, this.writer]) stream.on('error', () => {
      this.failure = new NativePreviewError('native_preview_invalid_stream'); failed(this.failure);
    });
  }

  /** :param chunk: 自有FFmpeg输出。:returns: 仅将受限字节交给成熟解析器，不读取容器字段。 */
  accept(chunk: Buffer): void {
    if (this.failure) throw this.failure;
    if (chunk.length > 128 * 1024) throw new NativePreviewError('native_preview_invalid_stream');
    this.received += chunk.length;
    if (this.received - this.accounted > MAX_TAG_BYTES + 128 * 1024) throw new NativePreviewError('native_preview_invalid_stream');
    if (this.originalHeader.length < 13) this.originalHeader = Buffer.concat([this.originalHeader, chunk]).subarray(0, 13);
    this.parser.write(chunk);
    if (this.failure) throw this.failure;
  }

  /** :returns: EOF必须包含完整标签及其长度校验，不接受截断画面。 */
  finish(): void {
    if (this.failure) throw this.failure;
    if (this.received !== this.accounted || this.footers !== this.packets + 1 || this.waiting || !this.generatedHeader)
      throw new NativePreviewError('native_preview_invalid_stream');
    this.parser.end(); this.writer.end();
  }

  /** :returns: 停止本实例解析与序列化，解除应用持有的数据引用。 */
  dispose(): void { this.parser.destroy(); this.writer.destroy(); this.waiting = undefined; this.originalHeader = Buffer.alloc(0); }

  /** :param event: 成熟解析器对象。:returns: 仅在其footer严格校验完成后发布对应标签。 */
  private object(event: FlvObject): void {
    if (event.header) {
      const value = event.header;
      if (this.accounted || value.version !== 1 || value.headerSize !== 9 || Object.keys(value.flags).join() !== '0' || !value.flags['0'])
        throw new NativePreviewError('native_preview_invalid_stream');
      this.accounted += value.headerSize; this.writer.write(event);
    }
    if (event.packet) {
      if (this.waiting || event.packet.length > MAX_TAG_BYTES - 15 || event.packet.streamId !== 0 || event.packet.data.length !== event.packet.length)
        throw new NativePreviewError('native_preview_invalid_stream');
      this.packets++; this.accounted += 11 + event.packet.length; this.waiting = event.packet;
    }
    if (event.footer !== undefined) {
      this.footers++; this.accounted += 4;
      if (this.waiting) {
        this.current = this.classify(this.waiting); this.writer.write({ packet: this.waiting }); this.waiting = undefined;
      }
    }
  }

  /** :param packet: 库校验的完整包。:returns: 由成熟AVC解析器提供的媒体分类。 */
  private classify(packet: Packet): Omit<NativeFlvTag, 'bytes'> {
    if (packet.type === 18) return { kind: 'metadata', timestamp: packet.time };
    if (packet.type !== 9 || packet.length < 5) throw new NativePreviewError('native_preview_invalid_stream');
    const [end, video] = readVideo(packet.data, 0, packet.length);
    if (end !== packet.length || video.codec !== mediaTypes.Video.Codec.AVC) throw new NativePreviewError('native_preview_incompatible_codec');
    if (!Number.isInteger(video.frameType) || video.frameType < 1 || video.frameType > 5) throw new NativePreviewError('native_preview_invalid_stream');
    const kinds = mediaTypes.AVC.PacketType;
    if (!Object.values(kinds).includes(video.packetType)) throw new NativePreviewError('native_preview_invalid_stream');
    const kind = video.packetType === kinds.SequenceHeader ? 'configuration' : video.packetType === kinds.EndOfSequence ? 'end'
      : video.frameType === mediaTypes.Video.FrameType.keyframe ? 'keyframe' : 'video';
    return { kind, timestamp: packet.time };
  }

  /** :param bytes: 成熟writer生成的数据。:returns: 校验文件头并复制自有完整标签。 */
  private written(bytes: Buffer): void {
    if (!this.generatedHeader) {
      if (bytes.length !== 13 || !bytes.equals(this.originalHeader)) throw new NativePreviewError('native_preview_invalid_stream');
      this.generatedHeader = Uint8Array.from(bytes); this.header(this.generatedHeader); return;
    }
    if (!this.current || bytes.length > MAX_TAG_BYTES) throw new NativePreviewError('native_preview_invalid_stream');
    this.tag({ ...this.current, bytes: Uint8Array.from(bytes) });
  }
}
