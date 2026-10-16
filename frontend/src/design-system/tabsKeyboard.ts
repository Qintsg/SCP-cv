/** Ant页签的原生键盘导航；通过真实页签点击选择，保留组件状态与嵌套工作区边界。 */
import type { Directive } from 'vue';
const handlers = new WeakMap<HTMLElement, (event: KeyboardEvent) => void>();

export const tabsKeyboard: Directive<HTMLElement> = {
  mounted(root): void {
    const keydown = (event: KeyboardEvent): void => {
      if (event.ctrlKey || event.metaKey || event.altKey || !(event.target instanceof Element)) return;
      const current = event.target.closest<HTMLElement>('[role="tab"]');
      if (!current || current.closest('.ant-tabs') !== root) return;
      const list = current.closest('[role="tablist"]');
      if (!list) return;
      const tabs = [...list.querySelectorAll<HTMLElement>('[role="tab"]')].filter(tab =>
        tab.closest('.ant-tabs') === root && tab.getAttribute('aria-disabled') !== 'true');
      if (!tabs.length) return;
      const vertical = root.classList.contains('ant-tabs-left') || root.classList.contains('ant-tabs-right');
      const forward = vertical ? 'ArrowDown' : 'ArrowRight';
      const backward = vertical ? 'ArrowUp' : 'ArrowLeft';
      const index = tabs.indexOf(current);
      const target = event.key === forward ? tabs[(index + 1) % tabs.length]
        : event.key === backward ? tabs[(index + tabs.length - 1) % tabs.length]
          : event.key === 'Home' ? tabs[0] : event.key === 'End' ? tabs.at(-1)
            : ['Enter', ' '].includes(event.key) ? current : null;
      if (!target) return;
      event.preventDefault(); event.stopPropagation();
      target.click(); target.focus({ preventScroll: true });
    };
    handlers.set(root, keydown); root.addEventListener('keydown', keydown, true);
  },
  beforeUnmount(root): void {
    const handler = handlers.get(root);
    if (handler) root.removeEventListener('keydown', handler, true);
    handlers.delete(root);
  },
};
