/** 预案电视纯值工具：深拷贝两个独立来源，缺省保持；不发送网络或设备命令。 */
import type { ScenarioAdvanced, ScenarioTelevision, VideoWallMappingItem } from '@/services/apiTypes';

/** :param values: 可能来自响应式预案的两台配置。:returns: 完全独立的嵌套输入，缺省仍省略以兼容旧预案。 */
export function copyTelevisions(values: ScenarioAdvanced['televisions']): ScenarioAdvanced['televisions'] {
  if (!values) return undefined;
  const copy = (value?: ScenarioTelevision): ScenarioTelevision => value?.state === 'set'
    ? { state: 'set', ...(value.input ? { input: { ...value.input } } : {}) } : { state: 'keep' };
  return { tv_left: copy(values.tv_left), tv_right: copy(values.tv_right) };
}

/** :param value: 单台输入意图。:returns: 只显示用户认识的窗口/末段IP，不堆协议说明。 */
export function televisionLabel(value?: ScenarioTelevision): string {
  if (!value || value.state === 'keep') return '保持';
  const input = value.input;
  if (!input) return '未选择输入';
  if (input.kind === 'laptop') return 'IP81';
  return input.kind === 'ip_stream' ? `IP${input.ip_address?.split('.').at(-1) || '…'}` : `窗口 ${input.kind.slice(-1)}`;
}

/** :param values: 表单值。:returns: 错误或空串；无输入的切换不得提交为默认来源。 */
export function televisionError(values?: ScenarioAdvanced['televisions']): string {
  for (const [target, name] of [['tv_left', '电视左'], ['tv_right', '电视右']] as const) {
    if (values?.[target]?.state === 'set' && !values[target]?.input) return `请选择${name}的输入`;
  }
  return '';
}

/** :param values: 原配置。:param target: 单台电视。:param input: 明确选择。:returns: 只更新当前目标，不借用另一台输入引用。 */
export function setTelevisionInput(values: ScenarioAdvanced['televisions'], target: 'tv_left' | 'tv_right', input: VideoWallMappingItem['input']): NonNullable<ScenarioAdvanced['televisions']> {
  return { ...copyTelevisions(values), [target]: { state: 'set', input: { ...input } } };
}
