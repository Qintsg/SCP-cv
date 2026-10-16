<script setup lang="ts">
/**
 * 设置中心设备电源 Tab。
 * 复用设备 store 发送拼接屏开关机与电视切换 TCP 指令。
 */
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';

import FIcon from '@/design-system/FIcon.vue';
import { useDialog } from '@/composables/useDialog';
import { useToast } from '@/composables/useToast';
import { useDeviceStore } from '@/stores/devices';

const { t } = useI18n();
const device = useDeviceStore();
const toast = useToast();
const dialog = useDialog();

async function powerOnSplice(): Promise<void> {
  try {
    await device.power('splice_screen', 'on');
    toast.success(t('settings.spliceOnOk'));
  } catch (error) {
    toast.error(t('settings.spliceOnFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

async function powerOffSplice(): Promise<void> {
  const current = dialog.captureContext();
  const confirmed = await dialog.danger({
    title: t('settings.spliceOffTitle'),
    description: t('settings.spliceOffDesc'),
    confirmLabel: t('settings.spliceOffConfirm'),
  });
  if (!confirmed || !current()) return;
  try {
    await device.power('splice_screen', 'off');
    toast.warning(t('settings.spliceOffOk'), t('settings.spliceOffOkDetail'));
  } catch (error) {
    toast.error(t('settings.spliceOffFail'), error instanceof Error ? error.message : t('settings.spliceOffFailDetail'));
  }
}

async function toggleTv(deviceType: 'tv_left' | 'tv_right', label: string): Promise<void> {
  try {
    await device.toggle(deviceType);
    toast.success(t('settings.tvToggleOk', { label }));
  } catch (error) {
    toast.error(t('settings.tvToggleFail', { label }), error instanceof Error ? error.message : t('common.retry'));
  }
}

function lastActionLabel(deviceType: string): string {
  const at = device.lastActionAt[deviceType];
  if (!at) return t('settings.notOperated');
  const date = new Date(at);
  const detail = device.lastActionDetail[deviceType] ?? '';
  return `${date.toLocaleTimeString()} · ${detail}`;
}
</script>

<template>
  <section class="settings-view__grid">
    <a-card :title="t('settings.splice')">
      <p class="settings-view__hint">{{ t('settings.spliceHint') }}</p>
      <div class="settings-view__row">
        <a-button type="primary" @click="powerOnSplice">
          <template #icon><FIcon name="power_24_regular" /></template>
          {{ t('settings.powerOn') }}
        </a-button>
        <a-button danger @click="powerOffSplice">
          <template #icon><FIcon name="plug_disconnected_24_regular" /></template>
          {{ t('settings.powerOff') }}
        </a-button>
      </div>
      <p class="settings-view__hint">{{ t('settings.lastAction', { detail: lastActionLabel('splice_screen') }) }}</p>
      <a-alert v-if="device.lastActionResult.splice_screen === 'error'" type="error" :message="t('settings.spliceTcpFail')"><template #description>
        {{ device.lastActionDetail.splice_screen }}
      </template></a-alert>
    </a-card>

    <a-card :title="t('settings.tv')">
      <p class="settings-view__hint">{{ t('settings.tvHint') }}</p>
      <div class="settings-view__row">
        <a-button @click="toggleTv('tv_left', t('settings.devLeft'))">
          <template #icon><FIcon name="arrow_swap_24_regular" /></template>
          {{ t('settings.tvLeftToggle') }}
        </a-button>
        <a-button @click="toggleTv('tv_right', t('settings.devRight'))">
          <template #icon><FIcon name="arrow_swap_24_regular" /></template>
          {{ t('settings.tvRightToggle') }}
        </a-button>
      </div>
      <p class="settings-view__hint">
        {{ t('settings.tvLeftLast', { detail: lastActionLabel('tv_left') }) }}<br />
        {{ t('settings.tvRightLast', { detail: lastActionLabel('tv_right') }) }}
      </p>
    </a-card>
  </section>
</template>
