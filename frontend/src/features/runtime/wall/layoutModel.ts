/** 10×5墙面纯模型；编辑与解析不产生任何请求或设备动作。 */
import type { VideoWallInputKind, VideoWallMappingItem, VideoWallRect } from '@/services/apiTypes';
export type WallBlock = VideoWallMappingItem & { region: 'grid'; rect: VideoWallRect };
export const INPUT_KINDS: VideoWallInputKind[] = ['window_1', 'window_2', 'laptop', 'ip_stream', 'window_3', 'window_4'];

/** :param mapping: 兼容全屏/左右映射。:returns: 对应的网格坐标副本。 */
export function rectOf(mapping: VideoWallMappingItem): VideoWallRect {
  if (mapping.region === 'grid') {
    if (!mapping.rect) throw new Error('布局缺少矩形坐标');
    return { ...mapping.rect };
  }
  return { column: mapping.region === 'right' ? 5 : 0, row: 0, columns: mapping.region === 'fullscreen' ? 10 : 5, rows: 5 };
}
/** :param first: 起点格索引。:param last: 终点格索引。:returns: 正向矩形；支持逆向拖选。 */
export function selectionRect(first: number, last: number): VideoWallRect {
  if (![first, last].every(n => Number.isInteger(n) && n >= 0 && n < 50)) throw new Error('请选择墙面内的单元格');
  const column = Math.min(first % 10, last % 10), row = Math.min(Math.floor(first / 10), Math.floor(last / 10));
  return { column, row, columns: Math.abs(first % 10 - last % 10) + 1, rows: Math.abs(Math.floor(first / 10) - Math.floor(last / 10)) + 1 };
}
/** :param a: 一个矩形。:param b: 另一个矩形。:returns: 是否有非零面积重叠。 */
export function overlaps(a: VideoWallRect, b: VideoWallRect): boolean {
  return a.column < b.column + b.columns && b.column < a.column + a.columns && a.row < b.row + b.rows && b.row < a.row + a.rows;
}
/** :param value: IPv4文本。:returns: 只接受规范十进制设备IP，不接受URL/凭据。 */
export function validIp(value: string): boolean {
  return /^(0|[1-9]\d{0,2})(\.(0|[1-9]\d{0,2})){3}$/.test(value) && value.split('.').every(p => Number(p) <= 255);
}
/** :param mappings: 旧区域或新矩形。:returns: 验证、拷贝并按坐标规范排序的块，可为空以供编辑。 */
export function validateBlocks(mappings: readonly VideoWallMappingItem[]): WallBlock[] {
  if (!Array.isArray(mappings) || mappings.length > 50) throw new Error('布局最多包含50个显示块');
  const blocks: WallBlock[] = [];
  for (const mapping of mappings) {
    if (!['grid', 'fullscreen', 'left', 'right'].includes(mapping.region)) throw new Error('未知的显示区域');
    const rect = rectOf(mapping);
    const { column, row, columns, rows } = rect;
    if (![column, row, columns, rows].every(Number.isInteger) || column < 0 || row < 0 || columns < 1 || rows < 1 || column + columns > 10 || row + rows > 5)
      throw new Error('矩形必须位于10列5行网格内');
    if (!mapping.input || !INPUT_KINDS.includes(mapping.input.kind)) throw new Error('未知的输入来源');
    const ip = mapping.input.ip_address || '';
    if (mapping.input.kind === 'ip_stream' ? !validIp(ip) : !!ip.trim()) throw new Error('自定义输入需要有效IPv4地址');
    if (blocks.some(b => overlaps(rect, b.rect))) throw new Error('显示块不能重叠');
    blocks.push({ region: 'grid', rect, input: { kind: mapping.input.kind, ip_address: mapping.input.kind === 'ip_stream' ? ip : '' } });
  }
  return blocks.sort((a, b) => a.rect.row - b.rect.row || a.rect.column - b.rect.column);
}
/** :param input: 来源。:returns: 不含内部实现细节的名称。 */
export function inputLabel(input: VideoWallMappingItem['input']): string {
  return input.kind === 'laptop' ? 'IP81' : input.kind === 'ip_stream' ? input.ip_address || '自定义IP' : `窗口 ${input.kind.slice(-1)}`;
}
