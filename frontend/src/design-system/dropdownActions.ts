/*
 * 下拉菜单语义动作：只经 select 派发，避免 DOM props 再次执行或绕过禁用态。
 */
import type { VNodeChild } from 'vue';

/** 业务菜单项；只描述操作，不依赖某个组件库的私有DOM或事件。 */
export interface ActionDropdownOption {
  key?: string | number;
  label?: string | (() => VNodeChild);
  icon?: () => VNodeChild;
  disabled?: boolean;
  show?: boolean;
  type?: string;
  props?: Record<string, unknown>;
  action?: () => void | Promise<void>;
  children?: ActionDropdownOption[];
}
export type DropdownOption = ActionDropdownOption;

/**
 * 执行选项的独立语义动作；DOM props 仅保留展示属性。
 * :param option: 选中的业务菜单项。
 * :returns: 同步完成派发，异步错误由业务动作处理。
 */
export function runDropdownAction(option: DropdownOption): void {
  if (option.disabled || option.show === false) return;
  const action = option.action;
  if (typeof action === 'function') void action();
}
