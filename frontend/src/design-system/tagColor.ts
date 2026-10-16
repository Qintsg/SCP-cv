/** 将业务状态映射为Ant Tag预设色；业务层无需持有组件库枚举。 */
/** :param value: 业务语义。:returns: Ant预设色或默认无强调色。 */
export function tagColor(value?: string): string | undefined {
  if (!value || value === 'default') return undefined;
  return value === 'info' ? 'processing' : value === 'primary' ? 'blue' : value;
}
