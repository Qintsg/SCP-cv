<!-- 仪表盘：大屏与四路预览为主，右侧集中音量、电源与两电视选源。 -->
<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import ACard from 'ant-design-vue/es/card';
import ASlider from 'ant-design-vue/es/slider';
import ASwitch from 'ant-design-vue/es/switch';
import FPageHeader from '@/design-system/FPageHeader.vue';
import DashboardWindows from './DashboardWindows.vue';
import DashboardPowerCard from './DashboardPowerCard.vue';
import TelevisionRoutingCard from './TelevisionRoutingCard.vue';
import { sliderAriaLabel as vSliderAriaLabel } from '@/design-system/sliderAriaLabel';
import VideoWallMappingPanel from '@/features/runtime/VideoWallMappingPanel.vue';
import { useDashboardAudio } from './useDashboardAudio';
import { useDeviceStore } from '@/stores/devices';
const { t } = useI18n(), device = useDeviceStore();
const { volume, muteToggle, muteBusy, volumeBlocked, transportBusy, recoveryBusy } = useDashboardAudio();
const hasDeviceError = computed(() => Object.values(device.lastActionResult).some(status => status === 'error'));
</script>
<template>
  <div class="dashboard">
    <FPageHeader :title="t('nav.dashboard')" />
    <a-alert v-if="hasDeviceError" type="error" :message="t('dashboard.deviceErrorTitle')"><template #description>{{ t('dashboard.deviceErrorBody') }}</template></a-alert>
    <section class="dashboard__grid" :aria-label="t('dashboard.topCommands')">
      <div class="dashboard__main"><DashboardWindows /><VideoWallMappingPanel /></div>
      <aside class="dashboard__side" aria-label="声音与设备">
        <a-card :title="t('dashboard.volumeTitle')">
          <template #extra><span class="dashboard__eyebrow">{{ t('dashboard.volumeEyebrow') }}</span></template>
          <a-slider v-slider-aria-label="t('dashboard.volumeAria')" :value="volume.value.value" :min="0" :max="100" :disabled="volumeBlocked" :aria-label="t('dashboard.volumeAria')" @change="value => { if (typeof value === 'number') volume.handleInput(value); }" @afterChange="volume.handleChange(volume.value.value)" />
          <div class="dashboard__mute-row"><span>{{ t('dashboard.enableSystemMute') }}</span><a-switch v-model:checked="muteToggle" :disabled="muteBusy || volume.busy.value || transportBusy || recoveryBusy" :loading="muteBusy || recoveryBusy" :aria-label="t('dashboard.enableSystemMute')" /></div>
        </a-card>
        <DashboardPowerCard />
        <TelevisionRoutingCard />
      </aside>
    </section>
  </div>
</template>
<style scoped>
.dashboard{display:flex;flex-direction:column;gap:16px;max-width:1440px}
.dashboard__grid{display:grid;grid-template-columns:minmax(0,1fr) 286px;gap:16px}
.dashboard__main,.dashboard__side{display:grid;align-content:start;gap:16px;min-width:0}
.dashboard__eyebrow{font-size:11px;color:var(--console-muted)}
.dashboard__mute-row{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-top:12px;font-size:12px}
.dashboard__side :deep(.ant-card-head){min-height:46px;padding:0 16px}
.dashboard__side :deep(.ant-card-body){padding:16px}
@media(max-width:1099px){.dashboard__grid{grid-template-columns:minmax(0,1fr)}.dashboard__side{grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}}
@media(max-width:767px){.dashboard__main>.wall-map{order:-1}.dashboard__side{grid-template-columns:minmax(0,1fr)}}
@media(min-width:1100px) and (max-height:800px){.dashboard{gap:14px}.dashboard__main,.dashboard__side{gap:12px}.dashboard__side :deep(.ant-card-body){padding:12px}.dashboard__side :deep(.ant-card-head){min-height:42px}.dashboard :deep(.wall-map .ant-card-body){padding:12px}.dashboard :deep(.wall-map .ant-card-head){min-height:46px}.dashboard :deep(.wall-map__actions){padding-block:0;margin-top:8px}}
</style>
