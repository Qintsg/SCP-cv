<script setup lang="ts">
/**
 * 仪表盘：概览 + 紧凑大屏交互块 + 系统音量 + 设备电源。
 * 仅承载顶层、无需进入子页即可完成的指令。
 *  - 不放预案调用、上传、窗口状态等明细能力；
 *  - 关机统一走 useDialog 二次确认。
 */
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import {
  NAlert,
  NButton,
  NCard,
  NSlider,
  NSwitch,
} from 'naive-ui';

import FIcon from '@/design-system/FIcon.vue';
import FPageHeader from '@/design-system/FPageHeader.vue';
import DashboardWindows from './DashboardWindows.vue';
import { sliderAriaLabel as vSliderAriaLabel } from '@/design-system/sliderAriaLabel';
import VideoWallMappingPanel from '@/features/runtime/VideoWallMappingPanel.vue';
import { useDialog } from '@/composables/useDialog';
import { useThrottledSlider } from '@/composables/useThrottledSlider';
import { useToast } from '@/composables/useToast';
import { useRuntimeStore } from '@/stores/runtime';
import { useDeviceStore } from '@/stores/devices';

const { t } = useI18n();
const runtime = useRuntimeStore();
const device = useDeviceStore();
const toast = useToast();
const dialog = useDialog();

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

const heroSubtitle = computed(() => {
  return t('dashboard.heroCaption');
});


async function powerOnSplice(): Promise<void> {
  try {
    await device.power('splice_screen', 'on');
    toast.success(t('dashboard.spliceOnOk'));
  } catch (error) {
    toast.error(t('dashboard.spliceOnFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

async function powerOffSplice(): Promise<void> {
  const confirmed = await dialog.danger({
    title: t('dashboard.spliceOffTitle'),
    description: t('dashboard.spliceOffDesc'),
    confirmLabel: t('dashboard.spliceOffConfirm'),
    cancelLabel: t('common.cancel'),
  });
  if (!confirmed) return;
  try {
    await device.power('splice_screen', 'off');
    toast.warning(t('dashboard.spliceOffOk'), t('dashboard.spliceOffOkDetail'));
  } catch (error) {
    toast.error(t('dashboard.spliceOffFail'), error instanceof Error ? error.message : t('dashboard.spliceOffFailDetail'));
  }
}

async function toggleTv(deviceType: 'tv_left' | 'tv_right', label: string): Promise<void> {
  try {
    await device.toggle(deviceType);
    toast.success(t('dashboard.tvToggleOk', { label }), t('dashboard.tvToggleOkDetail'));
  } catch (error) {
    toast.error(t('dashboard.tvToggleFail', { label }), error instanceof Error ? error.message : t('common.retry'));
  }
}

const hasDeviceError = computed(() =>
  Object.values(device.lastActionResult).some((status) => status === 'error'),
);
</script>

<template>
  <div class="dashboard">
    <FPageHeader :title="t('nav.dashboard')" :description="heroSubtitle" />

    <DashboardWindows />

    <n-alert v-if="hasDeviceError" type="error" :title="t('dashboard.deviceErrorTitle')">
      {{ t('dashboard.deviceErrorBody') }}
    </n-alert>

    <section class="dashboard__grid" :aria-label="t('dashboard.topCommands')">
      <VideoWallMappingPanel class="dashboard__card" />

      <n-card class="dashboard__card" :title="t('dashboard.volumeTitle')">
        <template #header-extra>
          <span class="dashboard__eyebrow">{{ t('dashboard.volumeEyebrow') }}</span>
        </template>
        <n-slider
          v-slider-aria-label="t('dashboard.volumeAria')"
          :value="volume.value.value"
          :min="0"
          :max="100"
          :aria-label="t('dashboard.volumeAria')"
          @update:value="volume.handleInput"
          @dragend="volume.handleChange(volume.value.value)"
        />
        <div class="dashboard__mute-row">
          <span>{{ t('dashboard.enableSystemMute') }}</span>
          <n-switch v-model:value="muteToggle" :aria-label="t('dashboard.enableSystemMute')" />
        </div>
      </n-card>

      <n-card class="dashboard__card" :title="t('dashboard.powerTitle')">
        <template #header-extra>
          <span class="dashboard__eyebrow">{{ t('dashboard.powerEyebrow') }}</span>
        </template>
        <div class="dashboard__power-row">
          <span class="dashboard__power-label">{{ t('dashboard.splice') }}</span>
          <n-button type="primary" @click="powerOnSplice">
            <template #icon><FIcon name="power_24_regular" /></template>
            {{ t('dashboard.powerOn') }}
          </n-button>
          <n-button type="error" @click="powerOffSplice">
            <template #icon><FIcon name="plug_disconnected_24_regular" /></template>
            {{ t('dashboard.powerOff') }}
          </n-button>
        </div>
        <div class="dashboard__power-row">
          <span class="dashboard__power-label">{{ t('dashboard.tvLeft') }}</span>
          <n-button @click="toggleTv('tv_left', t('dashboard.tvLeft'))">
            <template #icon><FIcon name="arrow_swap_24_regular" /></template>
            {{ t('dashboard.toggleState') }}
          </n-button>
        </div>
        <div class="dashboard__power-row">
          <span class="dashboard__power-label">{{ t('dashboard.tvRight') }}</span>
          <n-button @click="toggleTv('tv_right', t('dashboard.tvRight'))">
            <template #icon><FIcon name="arrow_swap_24_regular" /></template>
            {{ t('dashboard.toggleState') }}
          </n-button>
        </div>
      </n-card>
    </section>
  </div>
</template>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalXXL);
  max-width: 1280px;
}

.dashboard__hero {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalXS);
  padding: var(--spacingVerticalXL) var(--spacingHorizontalXXXL);
  border-radius: var(--borderRadius2XLarge);
  background: linear-gradient(135deg, var(--colorBrandBackground2) 0%, var(--colorNeutralBackground1) 100%);
  border: 1px solid var(--colorNeutralStroke2);
  box-shadow: var(--shadow4);
  overflow: hidden;
}

.dashboard__hero::after {
  content: '';
  position: absolute;
  right: -100px;
  bottom: -100px;
  width: 240px;
  height: 240px;
  border-radius: var(--borderRadiusCircular);
  background: radial-gradient(circle at center,
      color-mix(in srgb, var(--colorBrandBackground) 18%, transparent) 0%,
      transparent 70%);
  pointer-events: none;
}

.dashboard__hero-eyebrow {
  position: relative;
  z-index: 1;
  margin: 0;
  font-size: var(--fontSizeBase200);
  letter-spacing: 0.12em;
  font-weight: 600;
  color: var(--colorBrandForeground1);
  text-transform: uppercase;
}

.dashboard__hero-title {
  position: relative;
  z-index: 1;
  margin: 0;
  font-size: var(--fontSizeHero700);
  line-height: var(--lineHeightHero700);
  font-weight: 600;
  color: var(--colorNeutralForeground1);
}

.dashboard__hero-caption {
  position: relative;
  z-index: 1;
  margin: 0;
  color: var(--colorNeutralForeground2);
  font-size: var(--fontSizeBase200);
  max-width: 720px;
}

.dashboard__hero-status {
  position: relative;
  z-index: 1;
  display: flex;
  flex-wrap: wrap;
  gap: var(--spacingHorizontalS);
  margin-top: var(--spacingVerticalM);
}

.dashboard__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: var(--spacingHorizontalL);
}

.dashboard__card {
  min-height: 220px;
}

.dashboard__eyebrow {
  font-size: var(--fontSizeBase200);
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--colorNeutralForeground3);
}

.dashboard__mute-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--spacingVerticalM);
}

.dashboard__power-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--spacingHorizontalS);
  margin-bottom: var(--spacingVerticalS);
}

.dashboard__power-label {
  flex: 1 1 100px;
  font-weight: 600;
}

@media (max-width: 767px) {
  .dashboard__hero {
    padding: var(--spacingVerticalL) var(--spacingHorizontalL) var(--spacingVerticalXL);
  }

  .dashboard__hero-title {
    font-size: var(--fontSizeHero700);
    line-height: var(--lineHeightHero700);
  }

  .dashboard__grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
