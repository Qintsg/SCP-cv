/** 输出记账的纯内存原型；没有服务端持久化或真实发包，不声称实体当前画面。 */
import { reactive } from 'vue';
export type OutputTarget = 'wall' | 'left' | 'right';
export const outputMemory = reactive({ last: { wall: null, left: '63', right: '64' } as Record<OutputTarget, string | null>, writes: 0 });
/** :param target: 明确输出。:param desired: 目标源／布局，null表示保持。:param force: 仪表盘允许相同重发。:returns: 模拟发送或跳过，不访问设备。 */
export function dispatchDemo(target: OutputTarget, desired: string | null, force: boolean): string {
  if (desired === null) return '保持';
  if (!force && outputMemory.last[target] !== null && outputMemory.last[target] === desired) return '未变 · 跳过';
  outputMemory.last[target] = desired; outputMemory.writes++; return '模拟已发送';
}
