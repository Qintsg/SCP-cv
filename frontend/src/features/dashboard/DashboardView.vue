<script setup lang="ts">
/**
 * 仪表盘：概览 + 紧凑大屏交互块 + 系统音量 + 设备电源。
 * 仅承载顶层、无需进入子页即可完成的指令。
 *  - 不放预案调用、上传、窗口状态等明细能力；
 *  - 关机统一走 useDialog 二次确认。
 */
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ASlider from 'ant-design-vue/es/slider';
import ASwitch from 'ant-design-vue/es/switch';

import FIcon from '@/design-system/FIcon.vue';
import FPageHeader from '@/design-system/FPageHeader.vue';
import DashboardWindows from './DashboardWindows.vue';
import { sliderAriaLabel as vSliderAriaLabel } from '@/design-system/sliderAriaLabel';
import VideoWallMappingPanel from '@/features/runtime/VideoWallMappingPanel.vue';
import { useDevicePowerActions } from '@/composables/useDevicePowerActions';
import { useDashboardAudio } from './useDashboardAudio';
import { useRuntimeStore } from '@/stores/runtime';
import { useDeviceStore } from '@/stores/devices';

const { t } = useI18n();
const runtime = useRuntimeStore();
const device = useDeviceStore();
const { volume, muteToggle, muteBusy, volumeBlocked, transportBusy, recoveryBusy } = useDashboardAudio();
const { powerOnSplice, powerOffSplice, toggleTv, isPending, isLoading } = useDevicePowerActions('dashboard');

const heroSubtitle = computed(() => {
  return t('dashboard.heroCaption');
});

const hasDeviceError = computed(() =>
  Object.values(device.lastActionResult).some((status) => status === 'error'),
);
</script>

<template>
  <div class="dashboard">
    <FPageHeader :title="t('nav.dashboard')" />

    <a-alert v-if="hasDeviceError" type="error" :message="t('dashboard.deviceErrorTitle')"><template #description>
      {{ t('dashboard.deviceErrorBody') }}
    </template></a-alert>

    <section class="dashboard__grid" :aria-label="t('dashboard.topCommands')">
      <div class="dashboard__main">
        <DashboardWindows />
        <VideoWallMappingPanel class="dashboard__card" />
      </div>
      <aside class="dashboard__side" aria-label="声音与设备">

      <a-card class="dashboard__card" :title="t('dashboard.volumeTitle')">
        <template #extra>
          <span class="dashboard__eyebrow">{{ t('dashboard.volumeEyebrow') }}</span>
        </template>
        <a-slider
          v-slider-aria-label="t('dashboard.volumeAria')"
          :value="volume.value.value"
          :min="0"
          :max="100"
          :disabled="volumeBlocked"
          :aria-label="t('dashboard.volumeAria')"
          @change="value => { if (typeof value === 'number') volume.handleInput(value); }"
          @afterChange="volume.handleChange(volume.value.value)"
        />
        <div class="dashboard__mute-row">
          <span>{{ t('dashboard.enableSystemMute') }}</span>
          <a-switch v-model:checked="muteToggle" :disabled="muteBusy || volume.busy.value || transportBusy || recoveryBusy" :loading="muteBusy || recoveryBusy" :aria-label="t('dashboard.enableSystemMute')" />
        </div>
      </a-card>

      <a-card class="dashboard__card" :title="t('dashboard.powerTitle')">
        <template #extra>
          <span class="dashboard__eyebrow">{{ t('dashboard.powerEyebrow') }}</span>
        </template>
        <div class="dashboard__power-row">
          <span class="dashboard__power-label">{{ t('dashboard.splice') }}</span>
          <a-button type="primary" :disabled="isPending('splice_screen')" :loading="isLoading('splice_screen', 'on')" @click="powerOnSplice">
            <template #icon><FIcon name="power_24_regular" /></template>
            {{ t('dashboard.powerOn') }}
          </a-button>
          <!-- 本次关机loading保留原生焦点；aria-disabled、Ant loading及意图锁共同防重。 -->
          <a-button danger :disabled="isPending('splice_screen') && !isLoading('splice_screen', 'off')" :aria-disabled="isPending('splice_screen')" :loading="isLoading('splice_screen', 'off')" @click="powerOffSplice">
            <template #icon><FIcon name="plug_disconnected_24_regular" /></template>
            {{ t('dashboard.powerOff') }}
          </a-button>
        </div>
        <div class="dashboard__power-row">
          <span class="dashboard__power-label">{{ t('dashboard.tvLeft') }}</span>
          <a-button :disabled="isPending('tv_left')" :loading="isLoading('tv_left', 'toggle')" @click="toggleTv('tv_left', t('dashboard.tvLeft'))">
            <template #icon><FIcon name="arrow_swap_24_regular" /></template>
            {{ t('dashboard.toggleState') }}
          </a-button>
        </div>
        <div class="dashboard__power-row">
          <span class="dashboard__power-label">{{ t('dashboard.tvRight') }}</span>
          <a-button :disabled="isPending('tv_right')" :loading="isLoading('tv_right', 'toggle')" @click="toggleTv('tv_right', t('dashboard.tvRight'))">
            <template #icon><FIcon name="arrow_swap_24_regular" /></template>
            {{ t('dashboard.toggleState') }}
          </a-button>
        </div>
      </a-card>
      </aside>
    </section>
  </div>
</template>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalL);
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
  grid-template-columns: minmax(0, 1fr) 300px;
  gap: var(--spacingHorizontalL);
}

.dashboard__card {
  min-height: 0;
}
.dashboard__main,.dashboard__side { display:grid; align-content:start; gap:20px; min-width:0; }
.dashboard__main { gap:16px; }

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
  display: grid;
  grid-template-columns: minmax(0,1fr) auto auto;
  align-items: center;
  gap: var(--spacingHorizontalS);
  margin-bottom: var(--spacingVerticalS);
}
.dashboard__power-row:not(:first-child) .ant-btn { grid-column:2/4; justify-self:stretch; }

.dashboard__power-label {
  flex: 1 1 100px;
  font-weight: 600;
}

@media (max-width: 1199px) {
  .dashboard__grid { grid-template-columns:minmax(0,1fr); }
  .dashboard__side { grid-template-columns:repeat(2,minmax(0,1fr)); }
}
@media (max-width: 767px) {
  .dashboard__main > .wall-map { order:-1; }
  .dashboard__side { grid-template-columns:minmax(0,1fr); }
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
