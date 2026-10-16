/** 为Ant Select真实可聚焦的组合框同步名称，不能只给外层装饰容器设置ARIA。 */
import type { Directive } from 'vue';

/** :param root: 真实Select根节点。:param label: 业务可读名称。:returns: 焦点输入与外层名称一致。 */
function apply(root: HTMLElement, label: string): void {
  root.querySelectorAll<HTMLInputElement>('input[role="combobox"]').forEach(input => input.setAttribute('aria-label', label));
}
export const selectAriaLabel: Directive<HTMLElement, string> = {
  mounted(element, binding): void { apply(element, binding.value); },
  updated(element, binding): void { apply(element, binding.value); },
};
