/** 文件描述：已验证媒体库公共API类型；项目：SCP-cv；文件：mediaLibraries.d.ts；作者：Qintsg；日期：2026-10-21。 */
declare module 'flvee' {
  import { Transform, type TransformOptions } from 'node:stream';
  export interface Header { version: number; flags: Record<string, boolean>; headerSize: number }
  export interface Packet { type: number; length: number; time: number; streamId: number; data: Buffer; parsed?: unknown }
  export interface FlvObject { header?: Header; packet?: Packet; footer?: number }
  class Parser extends Transform { constructor(options?: TransformOptions | null, strictMode?: boolean) }
  class Serialiser extends Transform { constructor(options?: TransformOptions | null) }
  const flvee: { Parser: typeof Parser; Serialiser: typeof Serialiser };
  export default flvee;
}
declare module '@mediafish/flv' {
  export interface Video { frameType: number; codec: number; packetType: number; compositionTimeOffset: number; data: Uint8Array }
  export function setOptions(options: { strictMode: boolean }): void;
  export function readVideo(buffer: Uint8Array, offset: number, length: number): [number, Video];
  export const type: { Video: { Codec: { AVC: number }; FrameType: { keyframe: number } };
    AVC: { PacketType: { SequenceHeader: number; NALU: number; EndOfSequence: number } } };
}
