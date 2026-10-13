<!-- 现场摄像机与音量操作台；缺绑定保持不可操作，播放受理不冒充出画。 -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { NAlert, NButton, NCard, NTag } from 'naive-ui';
import { microphoneVolume, venueApi, type VenueAudioResult, type VenueAudioSettings, type VenueCamera, type VenueState } from '@/services/venueApi';
import { useRuntimeStore } from '@/stores/runtime';
import { useSourceStore } from '@/stores/sources';
import { useAuthStore } from '@/stores/auth';
import SourceOpenCard from '@/features/sources/SourceOpenCard.vue';
import type { MediaSourceItem } from '@/services/api';
import CameraControlPad from './CameraControlPad.vue';
import VenueAudioCard from './VenueAudioCard.vue';
import VenueSourceCard from './VenueSourceCard.vue';

const runtime = useRuntimeStore();
const sources = useSourceStore();
const auth = useAuthStore();
const state = ref<VenueState | null>(null);
const busy = ref(false);
const error = ref('');
const volume = ref<number | null>(null);
const audioSource = ref<VenueAudioSettings['source']>(null);
const computer = ref<number | null>(null); const handheld = ref<number | null>(null); const headset = ref<number | null>(null);
const cameraId = ref('center1'); const clockOffset = ref(0);
const controlled = computed(() => state.value?.cameras.find(camera => camera.id === cameraId.value && camera.movable));
const result = ref<VenueAudioResult | null>(null);
const selected = ref<MediaSourceItem | null>(null);
const open = ref(false);
let alive = true;

/** :param action: 只读或显式发送。:returns: 当前连接的结果；切主机或离页后不接收旧响应。 */
async function run(action: 'load' | 'read' | 'apply', settings?: VenueAudioSettings): Promise<void> {
  if (busy.value) return;
  const generation = runtime.connectionGeneration;
  const user = auth.user?.id;
  const current = () => alive && generation === runtime.connectionGeneration && user === auth.user?.id;
  busy.value = true;
  error.value = '';
  try {
    if (action !== 'load') {
      if (action === 'apply' && !settings) return;
      const response = action === 'read' ? await venueApi.readAudio() : await venueApi.applyAudio(settings!);
      if (!current()) return;
      result.value = response.audio;
    }
    const response = await venueApi.get();
    if (!current()) return;
    state.value = response.venue;
    if (action === 'load' || action === 'read' || settings?.screen_volume != null) volume.value = response.venue.audio.screen_volume ?? null;
    if (action === 'load' || settings?.computer_volume != null) computer.value = response.venue.audio.computer_volume ?? null;
    if (action === 'load' || settings?.handheld_volume != null) handheld.value = response.venue.audio.handheld_volume ?? null;
    if (action === 'load' || settings?.headset_volume != null) headset.value = microphoneVolume(response.venue.audio);
    if (action === 'load' || settings?.source != null) audioSource.value = response.venue.audio.source ?? null;
    clockOffset.value = response.venue.server_time_ms == null ? 0 : response.venue.server_time_ms - Date.now();
    if (action === 'load') await sources.refresh();
  } catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : '读取失败'; }
  finally {
    if (alive) {
      busy.value = false;
      if (!current()) void run('load');
    }
  }
}

/** :param camera: 只读内置项。:returns: 选窗小卡片，实际播放以输出确认。 */
function openCamera(camera: VenueCamera): void {
  selected.value = sources.sources.find(source => source.id === camera.source_id) ?? null;
  if (selected.value) open.value = true;
  else error.value = '摄像机目录尚未同步，请刷新。';
}
onMounted(() => { void run('load'); });
watch(() => [runtime.connectionGeneration, auth.user?.id], () => {
  state.value = null; volume.value = null; computer.value = null; handheld.value = null; headset.value = null; result.value = null;
  audioSource.value = null;
  error.value = ''; selected.value = null; open.value = false;
  if (alive && !busy.value && auth.user) void run('load');
});
onBeforeUnmount(() => { alive = false; });
</script>

<template>
  <section class="venue-page">
    <header><div><h1>摄像机与音量</h1><p>选择画面与机位，再调整现场声音。</p></div>
      <n-button :loading="busy" :disabled="busy" @click="run('load')">刷新</n-button></header>
    <n-alert v-if="error" type="error">{{ error }}</n-alert>
    <n-alert v-if="state?.simulation" type="warning">当前为仿真模式，所有音量结果均非实体设备状态。</n-alert>
    <div class="camera-grid">
      <n-card v-for="camera in state?.cameras" :key="camera.id" :title="camera.name" size="small" :class="{ selected: controlled?.id === camera.id }">
        <template #header-extra><n-tag size="small" :type="camera.stream_configured ? 'default' : 'warning'">{{ camera.stream_configured ? '内置 · 只读' : '播放地址未配置' }}</n-tag></template>
        <code>{{ camera.path }}</code>
        <n-button :disabled="busy || !camera.stream_configured || !camera.source_id" @click="openCamera(camera)">打开到…</n-button>
        <n-button v-if="camera.movable" :disabled="!camera.control_available" :type="controlled?.id === camera.id ? 'primary' : 'default'" :aria-pressed="controlled?.id === camera.id" @click="cameraId = camera.id">{{ controlled?.id === camera.id ? '正在控制' : '选择控制' }}</n-button>
        <p v-if="camera.unavailable_reason" class="unavailable">{{ camera.unavailable_reason }}</p>
        <p v-if="camera.motion_intent === 'stop_pending'" class="unavailable">停止发送未确认，请检查设备连接。</p>
      </n-card>
    </div>
    <n-card v-if="controlled" size="small"><CameraControlPad :camera="controlled" :clock-offset="clockOffset" /></n-card>
    <n-alert v-if="result" :type="['confirmed', 'simulated', 'unchanged'].includes(result.status) ? 'info' : 'warning'">{{ result.detail }}</n-alert>
    <div class="audio-grid" v-if="state">
      <VenueAudioCard v-model="computer" title="大屏音量" caption="原工程电脑音频 · 百分比换算20档，非设备读回" :busy="busy" :disabled="!state.capabilities.computer_volume" @apply="run('apply', { computer_volume: computer })" />
      <VenueAudioCard v-model="handheld" title="手持话筒" caption="原工程手持分组 · 百分比换算20档" :busy="busy" :disabled="!state.capabilities.handheld_volume" @apply="run('apply', { handheld_volume: handheld })" />
      <VenueAudioCard v-model="headset" title="悬挂（头戴）话筒" caption="原工程头戴1/2 · 同一分组，百分比换算20档" :busy="busy" :disabled="!state.capabilities.headset_volume" @apply="run('apply', { headset_volume: headset })" />
    </div>
    <div class="audio-extra" v-if="state">
      <VenueSourceCard v-model="audioSource" :busy="busy" :capabilities="state.capabilities" :requested="state.audio.source" @apply="run('apply', { source: audioSource })" />
      <details><summary>高级：分布式输出增益</summary><VenueAudioCard v-model="volume" title="输出增益" caption="155输出节点 · 可读回，与上述中控滑杆独立" :busy="busy" readback @read="run('read')" @apply="run('apply', { screen_volume: volume })" /></details>
    </div>
    <SourceOpenCard v-model:open="open" :source="selected" />
  </section>
</template>

<style scoped>
.venue-page{display:grid;gap:var(--spacingVerticalL)}
header{display:flex;justify-content:space-between;gap:16px;align-items:center}
h1{font-size:var(--fontSizeHero700);margin:0}
p{color:var(--colorNeutralForeground3);line-height:1.6}
code{display:block;margin-bottom:16px;color:var(--colorNeutralForeground2)}
.camera-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}
.audio-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}
.audio-extra{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}
.camera-grid .n-button{margin-right:8px;margin-bottom:4px}.camera-grid .selected{border-color:var(--colorBrandStroke1)}
.audio-extra details{border:1px solid var(--colorNeutralStroke1);border-radius:var(--borderRadiusMedium);padding:16px}.audio-extra summary{cursor:pointer;font-weight:600;margin-bottom:12px}
.unavailable{font-size:var(--fontSizeBase200)}
.audio-actions{display:flex;gap:8px;margin:16px 0}
@media(max-width:1000px){.camera-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:640px){
  .camera-grid,.audio-grid,.audio-extra{grid-template-columns:1fr}header{align-items:flex-start}
  /* 原生方向键已有48px；其余现场操作采用同一44px触控下限，不让内层label溢出外框。 */
  .venue-page :deep(.n-button){min-height:44px;min-width:44px}
  .venue-page :deep(.n-input){--n-height:44px!important}
  .audio-extra summary{min-height:44px;display:flex;align-items:center}
}
</style>
