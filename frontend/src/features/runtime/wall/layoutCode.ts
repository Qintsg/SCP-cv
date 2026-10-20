/** 无数据库布局短码v1：Base62(版本/块数/矩形14bit/输入/IPv4/CRC16-CCITT)。 */
import { INPUT_KINDS, validateBlocks, type WallBlock } from './layoutModel';
import type { VideoWallMappingItem } from '@/services/apiTypes';
const ALPHABET = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz';
/** :param bytes: 无凭据二进制内容。:returns: CRC16-CCITT-FALSE损坏检测值，不作为认证。 */
function checksum(bytes: number[]): number {
  let crc = 0xffff;
  for (const byte of bytes) {
    crc ^= byte << 8;
    for (let i = 0; i < 8; i++) crc = ((crc << 1) ^ (crc & 0x8000 ? 0x1021 : 0)) & 0xffff;
  }
  return crc;
}
/** :param mappings: 完整布局，名称不影响代码。:returns: 相同布局总是同一码，无外部查找。 */
export function encodeLayout(mappings: readonly VideoWallMappingItem[]): string {
  const blocks = validateBlocks(mappings), bytes = [1, blocks.length];
  for (const { rect: r, input } of blocks) {
    const rect = r.column | r.row << 4 | (r.columns - 1) << 7 | (r.rows - 1) << 11;
    bytes.push(rect >> 8, rect & 255, INPUT_KINDS.indexOf(input.kind));
    if (input.kind === 'ip_stream') bytes.push(...input.ip_address!.split('.').map(Number));
  }
  const crc = checksum(bytes); bytes.push(crc >> 8, crc & 255);
  let value = bytes.reduce((sum, byte) => (sum << 8n) | BigInt(byte), 0n), code = '';
  do { code = ALPHABET[Number(value % 62n)] + code; value /= 62n; } while (value);
  return code;
}
/** :param code: 用户输入。:returns: 整体校验成功后的新布局；错误不部分还原。 */
export function decodeLayout(code: string): WallBlock[] {
  if (!/^[A-Za-z0-9]{1,480}$/.test(code) || code.startsWith('0')) throw new Error('布局代码格式无效');
  let value = 0n;
  for (const ch of code) value = value * 62n + BigInt(ALPHABET.indexOf(ch));
  const bytes: number[] = [];
  while (value) { bytes.unshift(Number(value & 255n)); value >>= 8n; }
  if (bytes.length < 4 || bytes[0] !== 1 || bytes[1]! > 50) throw new Error('不支持的布局代码版本或内容');
  const body = bytes.slice(0, -2), crc = (bytes.at(-2)! << 8) | bytes.at(-1)!;
  if (checksum(body) !== crc) throw new Error('布局代码不完整或已损坏');
  const blocks: WallBlock[] = []; let offset = 2;
  for (let i = 0; i < bytes[1]!; i++) {
    if (offset + 3 > body.length) throw new Error('布局代码不完整');
    const rect = (body[offset++]! << 8) | body[offset++]!, kind = INPUT_KINDS[body[offset++]!];
    if (rect >= 16384 || !kind) throw new Error('布局代码包含未知内容');
    let ip_address = '';
    if (kind === 'ip_stream') {
      if (offset + 4 > body.length) throw new Error('布局代码缺少IP');
      ip_address = body.slice(offset, offset + 4).join('.'); offset += 4;
    }
    blocks.push({ region: 'grid', rect: { column: rect & 15, row: (rect >> 4) & 7, columns: ((rect >> 7) & 15) + 1, rows: ((rect >> 11) & 7) + 1 }, input: { kind, ip_address } });
  }
  if (offset !== body.length) throw new Error('布局代码含多余内容');
  return validateBlocks(blocks);
}
