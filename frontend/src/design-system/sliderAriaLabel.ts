/**
 * Slider把外层aria-label留在根节点，而实际可聚焦的
 * role="slider" handle 不会继承。该指令把可读名称同步到真正的焦点节点。
 */
import type { Directive } from 'vue';

/** :param root: 滑杆根节点。:param label: 业务名称。:returns: 同步原生焦点节点名称。 */
function applySliderAriaLabel(root: HTMLElement, label: string): void {
  root.querySelectorAll<HTMLElement>('[role="slider"]').forEach((handle) => {
    handle.setAttribute('aria-label', label);
  });
}

export const sliderAriaLabel: Directive<HTMLElement, string> = {
  mounted(element, binding): void {
    applySliderAriaLabel(element, binding.value);
  },
  updated(element, binding): void {
    applySliderAriaLabel(element, binding.value);
  },
};
