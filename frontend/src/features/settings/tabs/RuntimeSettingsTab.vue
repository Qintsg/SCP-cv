<script setup lang="ts">
/**
 * 设置中心运行态 Tab。
 * 承载紧凑大屏布局、系统音量、SSE 状态与重置全部窗口等全局运行控制。
 */
import { tagColor } from '@/design-system/tagColor';

import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ASlider from 'ant-design-vue/es/slider';
import ASwitch from 'ant-design-vue/es/switch';
import ATag from 'ant-design-vue/es/tag';

import FIcon from '@/design-system/FIcon.vue';
import { sliderAriaLabel as vSliderAriaLabel } from '@/design-system/sliderAriaLabel';
import VideoWallMappingPanel from '@/features/runtime/VideoWallMappingPanel.vue';
import { useThrottledSlider } from '@/composables/useThrottledSlider';
import { useToast } from '@/composables/useToast';
import { useRuntimeStore } from '@/stores/runtime';
import { useSessionStore } from '@/stores/sessions';

const { t } = useI18n();
const router = useRouter();
const runtime = useRuntimeStore();
const session = useSessionStore();
const toast = useToast();
const volume = useThrottledSlider(
  () => runtime.systemVolume.level,
  {
    commit: (level: number) => runtime.setSystemVolume(level, runtime.systemVolume.muted),
    onError: (error) => {
      toast.error(t('dashboard.volumeFail'), error instanceof Error ? error.message : t('common.retry'));
    },
  },
);

const muteToggle = computed({
  get: () => runtime.systemVolume.muted,
  set: async (next: boolean) => {
    try {
      await runtime.setSystemVolume(volume.value.value, next);
    } catch (error) {
      toast.error(t('dashboard.muteFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  },
});

const sseLabel = computed(() => {
  switch (runtime.sseStatus) {
    case 'connected':
      return t('app.sse.connected');
    case 'connecting':
      return t('app.sse.connecting');
    case 'reconnecting':
      return t('app.sse.reconnecting');
    case 'closed':
    default:
      return t('app.sse.closed');
  }
});

const sseLastUpdateLabel = computed(() => {
  if (!runtime.sseLastUpdate) return t('settings.sseNoUpdate');
  const date = new Date(runtime.sseLastUpdate);
  return t('settings.sseLastUpdate', { time: date.toLocaleTimeString() });
});

async function refreshSse(): Promise<void> {
  runtime.disconnectEvents();
  runtime.connectEvents();
  toast.info(t('settings.sseReconnected'));
}

async function resetAll(): Promise<void> {
  try {
    await session.resetAll();
    toast.success(t('settings.resetAllOk'));
  } catch (error) {
    toast.error(t('settings.resetAllFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}
</script>

<template>
  <section class="settings-view__grid">
    <VideoWallMappingPanel />

    <a-card :title="t('settings.systemVolume')">
      <div class="settings-view__volume-row">
        <a-slider
          v-slider-aria-label="t('settings.systemVolumeAria')"
          :value="volume.value.value"
          :min="0"
          :max="100"
          :aria-label="t('settings.systemVolumeAria')"
          :disabled="runtime.systemVolume.muted"
          @change="value => { if (typeof value === 'number') volume.handleInput(value); }"
          @afterChange="volume.handleChange(volume.value.value)"
        />
        <span class="settings-view__volume-value">{{ runtime.systemVolume.muted ? '—' : volume.value.value + '%' }}</span>
      </div>
      <div class="settings-view__row">
        <span>{{ t('settings.enableSystemMute') }}</span>
        <a-switch v-model:checked="muteToggle" />
      </div>
      <a-tag :color="tagColor(runtime.systemVolume.backend === 'windows_core_audio' ? 'default' : 'warning')"  >
        {{ t('settings.backendTag', { backend: runtime.systemVolume.backend }) }}
      </a-tag>
    </a-card>

    <a-card :title="t('settings.sseStatus')">
      <p class="settings-view__row">
        <a-tag :color="tagColor(runtime.sseStatus === 'connected' ? 'success' : 'warning')"  >
          {{ sseLabel }}
        </a-tag>
        <span>{{ sseLastUpdateLabel }}</span>
      </p>
      <a-button @click="refreshSse">
        <template #icon><FIcon name="arrow_clockwise_24_regular" /></template>
        {{ t('settings.reconnect') }}
      </a-button>
      <a-button class="settings-view__connection-button"  @click="router.push('/connect')">
        {{ t('settings.changeServer') }}
      </a-button>
    </a-card>
  </section>

  <section class="settings-view__danger-zone">
    <a-card :title="t('settings.emergencyTools')">
      <a-button danger @click="resetAll">
        <template #icon><FIcon name="arrow_reset_24_regular" /></template>
        {{ t('settings.resetAll') }}
      </a-button>
      <p class="settings-view__hint">{{ t('settings.resetAllHint') }}</p>
    </a-card>
  </section>
</template>
