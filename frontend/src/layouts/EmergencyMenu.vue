<script setup lang="ts">
// 文件描述：右上角维护菜单及实际窗口隐藏、显示控制。
// 项目：SCP-cv；文件：EmergencyMenu.vue；作者：Qintsg；日期：2026-10-29。
import type { DropdownOption } from '@/design-system/dropdownActions';

import ActionMenu from '@/design-system/ActionMenu.vue';

import { computed, h, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AButton from 'ant-design-vue/es/button';

import FIcon from '@/design-system/FIcon.vue';
import { runDropdownAction, type ActionDropdownOption } from '@/design-system/dropdownActions';
import type { FluentIconName } from '@/design-system';
import { useDialog } from '@/composables/useDialog';
import { useToast } from '@/composables/useToast';
import { useSessionStore } from '@/stores/sessions';
import { useWindowForeground } from '@/composables/useWindowForeground';

const { t } = useI18n();
const session = useSessionStore();
const dialog = useDialog();
const toast = useToast();
const systemActionPending = ref(false);
const windowForeground = useWindowForeground();
const busy = computed(() => systemActionPending.value || windowForeground.pending.value);

/**
 * 重置全部播放窗口。
 * :return: Promise 完成时表示请求已处理
 */
async function onResetAll(): Promise<void> {
  try {
    await session.resetAll();
    toast.success(t('emergency.resetAllOk'));
  } catch (error) {
    toast.error(t('emergency.resetAllFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

/**
 * 在物理输出窗口显示窗口编号。
 * :return: Promise 完成时表示请求已处理
 */
async function onShowWindowIds(): Promise<void> {
  try {
    await session.showWindowIds();
    toast.info(t('emergency.showIdsOk'));
  } catch (error) {
    toast.error(t('emergency.showIdsFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

/**
 * 确认后请求关闭全部服务。
 * :return: Promise 完成时表示请求已处理或用户已取消
 */
async function onShutdown(): Promise<void> {
  if (systemActionPending.value) return;
  const current = dialog.captureContext();
  const confirmed = await dialog.danger({
    title: t('emergency.shutdownTitle'),
    description: t('emergency.shutdownDesc'),
    confirmLabel: t('emergency.shutdownConfirm'),
    cancelLabel: t('common.cancel'),
  });
  if (!confirmed || !current()) return;
  systemActionPending.value = true;
  try {
    const result = await session.shutdownSystem();
    toast.warning(t('emergency.shutdownOk'), result.detail ?? t('emergency.shutdownSent'));
  } catch (error) {
    toast.error(t('emergency.shutdownFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    systemActionPending.value = false;
  }
}

/**
 * 确认后请求重启全部服务，并阻止重复系统动作。
 * :return: Promise 完成时表示请求已处理或用户已取消
 */
async function onRestartAll(): Promise<void> {
  if (systemActionPending.value) return;
  const current = dialog.captureContext();
  const confirmed = await dialog.danger({
    title: t('emergency.restartAllTitle'),
    description: t('emergency.restartAllDesc'),
    confirmLabel: t('emergency.restartAllConfirm'),
    cancelLabel: t('common.cancel'),
  });
  if (!confirmed || !current()) return;
  systemActionPending.value = true;
  try {
    const result = await session.restartAll();
    toast.warning(t('emergency.restartAllOk'), result.detail ?? t('emergency.restartAllSent'));
  } catch (error) {
    toast.error(t('emergency.restartAllFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    systemActionPending.value = false;
  }
}

/**
 * 构造下拉菜单使用的 Fluent 图标渲染函数。
 * :param name: 设计系统图标名称
 * :return: Ant Design Vue 图标渲染函数
 */
function renderIcon(name: FluentIconName): () => ReturnType<typeof h> {
  return () => h(FIcon, { name, size: 18 });
}

const options = computed<ActionDropdownOption[]>(() => [
  {
    type: 'group',
    label: t('emergency.groupLabel'),
    key: 'group-main',
    children: [
      { label: windowForeground.label.value, key: 'window-foreground', disabled: !windowForeground.available.value || busy.value,
        icon: renderIcon('eye_24_regular'), action: windowForeground.toggle },
      { label: t('emergency.resetAll'), key: 'reset', icon: renderIcon('arrow_reset_24_regular'), action: onResetAll },
      { label: t('emergency.showIds'), key: 'show-ids', icon: renderIcon('eye_24_regular'), action: onShowWindowIds },
    ],
  },
  { type: 'divider', key: 'divider-1' },
  {
    label: t('emergency.restartAll'),
    key: 'restart-all',
    disabled: systemActionPending.value,
    icon: renderIcon('arrow_repeat_all_24_regular'),
    action: onRestartAll,
  },
  {
    label: t('emergency.shutdown'),
    key: 'shutdown',
    disabled: systemActionPending.value,
    icon: renderIcon('plug_disconnected_24_regular'),
    action: onShutdown,
    props: { style: 'color: var(--colorStatusDangerForeground1);' },
  },
]);

/**
 * 执行下拉选项绑定的动作。
 * :param _key: Ant Design Vue 选项键，本处理器不使用
 * :param option: 被选择的下拉选项
 * :return: None
 */
function handleSelect(_key: string | number, option: DropdownOption): void {
  if (busy.value) return;
  runDropdownAction(option);
}
</script>

<template>
  <ActionMenu

    placement="bottomRight"
    :options="options"
    @select="handleSelect"
  >
    <a-button
      type="text"
      shape="circle"
      :loading="busy"
      :disabled="busy"
      :aria-label="t('emergency.triggerAria')"
    >
      <template #icon>
        <FIcon name="alert_urgent_24_regular" />
      </template>
    </a-button>
  </ActionMenu>
</template>
