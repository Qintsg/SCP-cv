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
import { useDevicePowerActions } from '@/composables/useDevicePowerActions';
import { useDeviceStore } from '@/stores/devices';

const { t } = useI18n();
const device = useDeviceStore();
const { powerOnSplice, powerOffSplice, toggleTv, isPending, isLoading } = useDevicePowerActions('settings');

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
      <div class="settings-view__row">
        <a-button type="primary" :disabled="isPending('splice_screen')" :loading="isLoading('splice_screen', 'on')" @click="powerOnSplice">
          <template #icon><FIcon name="power_24_regular" /></template>
          {{ t('settings.powerOn') }}
        </a-button>
        <!-- 保留本次关机入口供Modal回焦；loading与意图锁仍拒绝重复发送。 -->
        <a-button danger :disabled="isPending('splice_screen') && !isLoading('splice_screen', 'off')" :aria-disabled="isPending('splice_screen')" :loading="isLoading('splice_screen', 'off')" @click="powerOffSplice">
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
      <div class="settings-view__row">
        <a-button :disabled="isPending('tv_left')" :loading="isLoading('tv_left', 'toggle')" @click="toggleTv('tv_left', t('settings.devLeft'))">
          <template #icon><FIcon name="arrow_swap_24_regular" /></template>
          {{ t('settings.tvLeftToggle') }}
        </a-button>
        <a-button :disabled="isPending('tv_right')" :loading="isLoading('tv_right', 'toggle')" @click="toggleTv('tv_right', t('settings.devRight'))">
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
