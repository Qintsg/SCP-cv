/** 控件输入边界：清空保持null，合法数字含0保留，不把字符串/数组隐式转成设备参数。 */
/** :param value: 组件输入。:returns: 有限数字或明确的未配置null。 */
export function nullableNumber(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}
