<!-- 业务操作菜单：真实Ant Dropdown/Menu，选择仅经一个事件派发，不使用DOM onClick。 -->
<script lang="ts">
import { cloneVNode, defineComponent, h, nextTick, onBeforeUnmount, ref, useId } from 'vue';
import type { PropType, VNode } from 'vue';
import ADropdown from 'ant-design-vue/es/dropdown';
import AMenu, { Item as MenuItem, SubMenu, ItemGroup, Divider } from 'ant-design-vue/es/menu';
import type { DropdownOption } from './dropdownActions';

export default defineComponent({
  name: 'ActionMenu',
  props: {
    options: { type: Array as PropType<DropdownOption[]>, required: true },
    disabled: { type: Boolean, default: false },
    placement: { type: String as PropType<'bottomLeft' | 'bottomRight'>, default: 'bottomRight' },
  },
  emits: { select: (key: string | number, item: DropdownOption) => (typeof key === 'string' || typeof key === 'number') && !!item },
  setup(props, { slots, emit }) {
    const marker = 'scp-action-menu-' + useId().replaceAll(':', '-');
    let observer: MutationObserver | null = null;
    const opened = ref(false);
    const openedKeys = ref<string[]>([]);
    const listening = new Set<HTMLElement>();
    let opener: HTMLElement | null = null;
    let pendingMenu = '';
    let focusRoot = false;
    let accepted = false;

    /** :param items: 当前操作。:returns: 隐藏项移除后的Ant菜单结构。 */
    function menuNodes(items: DropdownOption[]): VNode[] {
      return items.filter(item => item.show !== false).map((item, index) => {
        const key = item.key ?? `item-${index}`;
        const label = () => typeof item.label === 'function' ? item.label() : item.label;
        if (item.type === 'divider') return h(Divider, { key });
        if (item.type === 'group') return h(ItemGroup, { key, title: label() }, () => menuNodes(item.children ?? []));
        const icon = item.icon ? () => h('span', { class: 'action-menu__icon' }, [item.icon!()]) : undefined;
        if (item.children) return h(SubMenu, { key, disabled: item.disabled, popupClassName: marker }, {
          title: label, icon, default: () => menuNodes(item.children!),
        });
        return h(MenuItem, { key, disabled: item.disabled,
          danger: item.props?.class === 'text-error' || String(item.props?.style || '').includes('Danger'),
        }, { default: label, icon });
      });
    }

    /** :returns: 为当前菜单的原生子菜单标题补齐角色，保留Ant的展开、焦点和命令处理。 */
    function syncSemantics(): void {
      for (const root of document.getElementsByClassName(marker)) {
        if (root instanceof HTMLElement && !listening.has(root)) {
          root.addEventListener('keydown', navigateKeyboard);
          listening.add(root);
        }
        root.querySelectorAll<HTMLElement>('[data-menu-id][aria-haspopup]').forEach(title => {
          title.setAttribute('role', 'menuitem');
          if (!title.hasAttribute('tabindex')) title.setAttribute('tabindex', '-1');
        });
        root.querySelectorAll<HTMLElement>('[data-menu-list]').forEach(menu => {
          menu.setAttribute('role', 'menu');
          if (!menu.hasAttribute('tabindex')) menu.setAttribute('tabindex', '-1');
        });
        root.querySelectorAll<HTMLElement>('[role="menuitem"]').forEach(item => {
          if (!item.hasAttribute('tabindex')) item.setAttribute('tabindex', '-1');
        });
        if (focusRoot) {
          const menu = root.querySelector<HTMLElement>('[role="menu"]');
          if (menu && visible(menu)) { menu.focus({ preventScroll: true }); focusRoot = false; }
        }
      }
      if (pendingMenu) {
        const menu = document.getElementById(pendingMenu);
        const first = menu && enabledItems(menu)[0];
        if (first && visible(first)) { pendingMenu = ''; first.focus({ preventScroll: true }); }
      }
    }
    /** :param element: 当前弹层对象。:returns: CSS隐藏的缓存弹层不可接收键盘焦点。 */
    function visible(element: HTMLElement): boolean {
      for (let node: HTMLElement | null = element; node; node = node.parentElement) {
        const style = getComputedStyle(node);
        if (style.display === 'none' || style.visibility === 'hidden') return false;
      }
      return true;
    }
    /** :param menu: 当前层菜单。:returns: 同层、可用的公开菜单项，不跨入子弹层。 */
    function enabledItems(menu: Element): HTMLElement[] {
      return [...menu.querySelectorAll<HTMLElement>('[role="menuitem"]')].filter(item =>
        item.closest('[role="menu"]') === menu && item.getAttribute('aria-disabled') !== 'true'
        && !item.className.includes('-disabled') && visible(item));
    }
    /** :param title: Ant的原生子菜单标题。:returns: 通过公开openKeys展开，不调用业务动作。 */
    function enterSubmenu(title: HTMLElement): void {
      const key = title.getAttribute('data-menu-id');
      if (!key) return;
      openedKeys.value = [...new Set([...openedKeys.value, key])];
      pendingMenu = title.getAttribute('aria-controls') || '';
      void nextTick(syncSemantics);
    }
    /** :param event: 当前菜单内的键盘事件。:returns: 补齐Ant4缺少的方向键；Enter叶项仍由Ant派发一次。 */
    function navigateKeyboard(event: KeyboardEvent): void {
      if (!opened.value || event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) return;
      const target = event.target instanceof HTMLElement ? event.target : null;
      if (!target || target.closest('input,textarea,select,[contenteditable="true"]')) return;
      if (event.key === 'Tab') { openChange(false); opener?.focus({ preventScroll: true }); return; }
      if (event.key === 'Escape') {
        event.preventDefault(); event.stopPropagation(); openChange(false); opener?.focus({ preventScroll: true }); return;
      }
      const menu = target.closest<HTMLElement>('[role="menu"]');
      if (!menu) return;
      const entries = enabledItems(menu);
      const current = target.closest<HTMLElement>('[role="menuitem"]');
      if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key) && entries.length) {
        event.preventDefault(); event.stopPropagation();
        const index = current ? entries.indexOf(current) : -1;
        const next = event.key === 'Home' ? 0 : event.key === 'End' ? entries.length - 1
          : event.key === 'ArrowDown' ? (index + 1) % entries.length : (index + entries.length - 1) % entries.length;
        entries[next].focus({ preventScroll: true });
      } else if (event.key === 'ArrowRight' || (['Enter', ' '].includes(event.key) && current?.hasAttribute('aria-haspopup'))) {
        const title = current?.hasAttribute('aria-haspopup') ? current
          : menu.querySelector<HTMLElement>('[role="menuitem"][aria-haspopup][aria-expanded="true"]');
        if (title) { event.preventDefault(); event.stopPropagation(); enterSubmenu(title); }
      } else if (event.key === 'ArrowLeft' && menu.id) {
        const title = [...document.querySelectorAll<HTMLElement>('[data-menu-id][aria-controls]')]
          .find(item => item.getAttribute('aria-controls') === menu.id);
        if (title) {
          event.preventDefault(); event.stopPropagation();
          openedKeys.value = openedKeys.value.filter(key => key !== title.getAttribute('data-menu-id'));
          title.focus({ preventScroll: true });
        }
      } else if (event.key === ' ' && current && entries.includes(current)) {
        event.preventDefault(); event.stopPropagation(); if (!event.repeat) current.click();
      }
    }
    /** :returns: 当前弹层关闭或卸载后释放观察器与监听，不留下全局快捷键。 */
    function disconnect(): void {
      observer?.disconnect(); observer = null;
      for (const element of listening) element.removeEventListener('keydown', navigateKeyboard);
      listening.clear(); pendingMenu = ''; focusRoot = false;
    }
    /** :param open: 真实菜单开合。:returns: 只在当前弹层寿命内观察DOM，不常驻订阅或发送业务动作。 */
    function openChange(open: boolean): void {
      disconnect(); opened.value = open;
      if (!open) { openedKeys.value = []; return; }
      accepted = false; focusRoot = true;
      opener = document.querySelector<HTMLElement>(`[data-scp-menu-trigger="${marker}"]`)
        || (document.activeElement instanceof HTMLElement ? document.activeElement : null);
      observer = new MutationObserver(syncSemantics);
      observer.observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['style', 'class'] });
      void nextTick(syncSemantics);
    }
    onBeforeUnmount(() => { disconnect(); opener = null; });

    /** :param key: Ant实际选中键。:returns: 按当前选项复核后只派发一次。 */
    function select({ key }: { key: string | number }): void {
      if (accepted) return;
      const find = (options: DropdownOption[]): DropdownOption | undefined => {
        for (const item of options) {
          if (item.disabled || item.show === false) continue;
          if (String(item.key) === String(key)) return item;
          const nested = item.children && find(item.children);
          if (nested) return nested;
        }
      };
      const item = find(props.options);
      if (item && !props.disabled) { accepted = true; emit('select', key, item); openChange(false); }
    }

    /** :returns: Ant只克隆首个触发器；直接传递原Slot，避免template的slot占位产生Fragment而丢失点击事件。 */
    return () => h(ADropdown, {
      trigger: ['click'], disabled: props.disabled, placement: props.placement, autofocus: false, open: opened.value,
      overlayClassName: marker, onOpenChange: openChange,
    }, {
      default: () => slots.default?.().map((node, index) => index === 0 ? cloneVNode(node, { 'data-scp-menu-trigger': marker }) : node),
      overlay: () => h(AMenu, { role: 'menu', onClick: select, openKeys: openedKeys.value,
        onOpenChange: (keys: (string | number)[]) => { openedKeys.value = keys.map(String); },
      }, () => menuNodes(props.options)),
    });
  },
});
</script>
