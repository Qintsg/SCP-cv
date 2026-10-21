<!-- 现场摄像机与音量操作台；缺绑定保持不可操作，播放受理不冒充出画。 -->
<script setup lang="ts">
import { tabsKeyboard as vTabsKeyboard } from '@/design-system/tabsKeyboard';
import { tagColor } from '@/design-system/tagColor';
import { computed, ref } from 'vue';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ATag from 'ant-design-vue/es/tag';
import ATabs from 'ant-design-vue/es/tabs';
import { TabPane as ATabPane } from 'ant-design-vue/es/tabs';
import type { VenueCamera } from '@/services/venueApi';
import { useSourceStore } from '@/stores/sources';
import { useVenueState } from './useVenueState';
import SourceOpenCard from '@/features/sources/SourceOpenCard.vue';
import FPageHeader from '@/design-system/FPageHeader.vue';
import type { MediaSourceItem } from '@/services/api';
import CameraControlPad from './CameraControlPad.vue';
import VenueAudioCard from './VenueAudioCard.vue';
import VenueSourceCard from './VenueSourceCard.vue';
import ClientStreamPreview from '@/features/preview/ClientStreamPreview.vue';

const sources = useSourceStore();
const cameraId = ref('center1');
const selected = ref<MediaSourceItem | null>(null);
const open = ref(false);
const workspace = ref('camera');
const { state, result, error, volume, computer, handheld, headset, audioSource, clockOffset, busy, run } = useVenueState(() => { selected.value = null; open.value = false; });
const previewed = computed(() => state.value?.cameras.find(camera => camera.id === cameraId.value));
const controlled = computed(() => previewed.value?.movable ? previewed.value : undefined);

/** :param camera: 只读内置项。:returns: 选窗小卡片，实际播放以输出确认。 */
function openCamera(camera: VenueCamera): void {
  selected.value = sources.sources.find(source => source.id === camera.source_id) ?? null;
  if (selected.value) open.value = true;
  else error.value = '摄像机目录尚未同步，请刷新。';
}
</script>

<template>
  <section class="venue-page">
    <FPageHeader title="摄像机与音量" description="打开摄像机画面、控制机位，或调整现场声音。">
      <template #actions><a-button :loading="busy" :disabled="busy" @click="run('load')">刷新</a-button></template>
    </FPageHeader>
    <a-alert v-if="error" type="error"><template #description>{{ error }}</template></a-alert>
    <a-alert v-if="state?.simulation" type="warning"><template #description>当前为仿真模式，所有音量结果均非实体设备状态。</template></a-alert>
    <a-tabs v-tabs-keyboard v-model:activeKey="workspace" destroy-inactive-tab-pane class="venue-workspaces">
      <a-tab-pane key="camera" tab="摄像机控制">
      <div class="venue-camera-workspace"><div class="camera-grid">
      <a-card v-for="camera in state?.cameras" :key="camera.id" :title="camera.name" size="small" :class="{ selected: previewed?.id === camera.id }">
        <template #extra><a-tag  :color="tagColor(camera.stream_configured ? 'default' : 'warning')">{{ camera.stream_configured ? '内置 · 只读' : '播放地址未配置' }}</a-tag></template>
        <code>{{ camera.path }}</code>
        <a-button :disabled="busy || !camera.stream_configured || !camera.source_id" @click="openCamera(camera)">打开到…</a-button>
        <a-button :type="previewed?.id === camera.id ? 'primary' : 'default'" :aria-pressed="previewed?.id === camera.id" @click="cameraId = camera.id">{{ previewed?.id === camera.id ? '已选择' : camera.movable ? '预览与控制' : '预览画面' }}</a-button>
        <p v-if="camera.unavailable_reason" class="unavailable">{{ camera.unavailable_reason }}</p>
        <p v-if="camera.motion_intent === 'stop_pending'" class="unavailable">停止发送未确认，请检查设备连接。</p>
      </a-card>
    </div>
      <a-card v-if="previewed" :title="previewed.name" class="venue-camera-console"><div class="venue-camera-preview"><ClientStreamPreview :source-id="previewed.source_id || undefined" :enabled="previewed.stream_configured" quality="high" :label="previewed.name" /></div><CameraControlPad v-if="controlled" :camera="controlled" :clock-offset="clockOffset" /></a-card>
      </div></a-tab-pane>
      <a-tab-pane key="audio" tab="音量与来源">
    <a-alert v-if="result" :type="['confirmed', 'simulated', 'unchanged'].includes(result.status) ? 'info' : 'warning'"><template #description>{{ result.detail }}</template></a-alert>
    <div class="audio-grid" v-if="state">
      <VenueAudioCard v-model="computer" title="大屏音量" caption="电脑音频通道 · 百分比换算20档，显示的是请求值" :busy="busy" :disabled="!state.capabilities.computer_volume" @apply="run('apply', { computer_volume: computer })" />
      <VenueAudioCard v-model="handheld" title="手持话筒" caption="手持话筒分组 · 百分比换算20档" :busy="busy" :disabled="!state.capabilities.handheld_volume" @apply="run('apply', { handheld_volume: handheld })" />
      <VenueAudioCard v-model="headset" title="悬挂（头戴）话筒" caption="头戴话筒1/2为同一分组 · 百分比换算20档" :busy="busy" :disabled="!state.capabilities.headset_volume" @apply="run('apply', { headset_volume: headset })" />
    </div>
    <div class="audio-extra" v-if="state">
      <VenueSourceCard v-model="audioSource" :busy="busy" :capabilities="state.capabilities" :requested="state.audio.source" @apply="run('apply', { source: audioSource })" />
      <details><summary>高级：分布式输出增益</summary><VenueAudioCard v-model="volume" title="输出增益" caption="155输出节点 · 可读回，与上述中控滑杆独立" :busy="busy" readback @read="run('read')" @apply="run('apply', { screen_volume: volume })" /></details>
    </div>
      </a-tab-pane>
    </a-tabs>
    <SourceOpenCard v-model:open="open" :source="selected" />
  </section>
</template>

<style scoped>
.venue-page{display:grid;gap:var(--spacingVerticalL)}
.venue-camera-workspace{display:grid;grid-template-columns:minmax(260px,1fr) minmax(320px,1.2fr);gap:20px;align-items:start}
.venue-camera-workspace .camera-grid{grid-template-columns:1fr;gap:10px}
.venue-camera-console{position:sticky;top:0}
.venue-camera-preview{aspect-ratio:16/9;margin-bottom:16px;overflow:hidden;border-radius:var(--borderRadiusSmall)}
@media(max-width:1023px){.venue-camera-workspace{grid-template-columns:minmax(0,1fr)}.venue-camera-console{position:static;grid-row:1}.venue-camera-workspace .camera-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:599px){.venue-camera-workspace .camera-grid{grid-template-columns:minmax(0,1fr)}}
header{display:flex;justify-content:space-between;gap:16px;align-items:center}
h1{font-size:var(--fontSizeHero700);margin:0}
p{color:var(--colorNeutralForeground3);line-height:1.6}
code{display:block;margin-bottom:16px;color:var(--colorNeutralForeground2)}
.camera-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}
.audio-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}
.audio-extra{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}
.camera-grid .ant-btn{margin-right:8px;margin-bottom:4px}.camera-grid .selected{border-color:var(--colorBrandStroke1)}
.audio-extra details{border:1px solid var(--colorNeutralStroke1);border-radius:var(--borderRadiusMedium);padding:16px}.audio-extra summary{cursor:pointer;font-weight:600;margin-bottom:12px}
.unavailable{font-size:var(--fontSizeBase200)}
.audio-actions{display:flex;gap:8px;margin:16px 0}
@media(max-width:1000px){.camera-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:640px){
  .camera-grid,.audio-grid,.audio-extra{grid-template-columns:1fr}header{align-items:flex-start}
  /* 原生方向键已有48px；其余现场操作采用同一44px触控下限，不让内层label溢出外框。 */
  .venue-page :deep(.ant-btn){min-height:44px;min-width:44px}
  .venue-page :deep(.ant-input){min-height:44px!important}
  .audio-extra summary{min-height:44px;display:flex;align-items:center}
}
</style>
