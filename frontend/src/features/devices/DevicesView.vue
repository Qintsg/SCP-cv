<!-- 现场摄像机与音量操作台；缺绑定保持不可操作，播放受理不冒充出画。 -->
<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { NAlert, NButton, NCard, NInputNumber, NRadio, NRadioGroup, NSlider, NTag } from 'naive-ui';
import { venueApi, type VenueAudioResult, type VenueCamera, type VenueState } from '@/services/venueApi';
import { useRuntimeStore } from '@/stores/runtime';
import { useSourceStore } from '@/stores/sources';
import { useAuthStore } from '@/stores/auth';
import SourceOpenCard from '@/features/sources/SourceOpenCard.vue';
import type { MediaSourceItem } from '@/services/api';

const runtime = useRuntimeStore();
const sources = useSourceStore();
const auth = useAuthStore();
const state = ref<VenueState | null>(null);
const busy = ref(false);
const error = ref('');
const volume = ref<number | null>(null);
const result = ref<VenueAudioResult | null>(null);
const selected = ref<MediaSourceItem | null>(null);
const open = ref(false);
let alive = true;

/** :param action: 只读或显式发送。:returns: 当前连接的结果；切主机或离页后不接收旧响应。 */
async function run(action: 'load' | 'read' | 'apply'): Promise<void> {
  if (busy.value) return;
  const generation = runtime.connectionGeneration;
  const user = auth.user?.id;
  const current = () => alive && generation === runtime.connectionGeneration && user === auth.user?.id;
  busy.value = true;
  error.value = '';
  try {
    if (action !== 'load') {
      if (action === 'apply' && volume.value === null) return;
      const response = action === 'read' ? await venueApi.readAudio() : await venueApi.applyAudio({ screen_volume: volume.value });
      if (!current()) return;
      result.value = response.audio;
    }
    const response = await venueApi.get();
    if (!current()) return;
    state.value = response.venue;
    volume.value = response.venue.audio.screen_volume ?? null;
    await sources.refresh();
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
  state.value = null; volume.value = null; result.value = null;
  error.value = ''; selected.value = null; open.value = false;
  if (alive && !busy.value && auth.user) void run('load');
});
onBeforeUnmount(() => { alive = false; });
</script>

<template>
  <section class="venue-page">
    <header><div><h1>摄像机与音量</h1><p>选择画面，调整现场声音。未绑定的控制不会发送到设备。</p></div>
      <n-button :loading="busy" :disabled="busy" @click="run('load')">刷新</n-button></header>
    <n-alert v-if="error" type="error">{{ error }}</n-alert>
    <n-alert v-if="state?.simulation" type="warning">当前为仿真模式，所有音量结果均非实体设备状态。</n-alert>
    <div class="camera-grid">
      <n-card v-for="camera in state?.cameras" :key="camera.id" :title="camera.name" size="small">
        <template #header-extra><n-tag size="small" :type="camera.stream_configured ? 'default' : 'warning'">{{ camera.stream_configured ? '内置 · 只读' : '播放地址未配置' }}</n-tag></template>
        <code>{{ camera.path }}</code>
        <n-button :disabled="busy || !camera.stream_configured || !camera.source_id" @click="openCamera(camera)">打开到…</n-button>
        <div v-if="camera.movable" class="camera-controls" :title="camera.unavailable_reason">
          <n-button disabled aria-label="摄像机上转">↑</n-button>
          <div><n-button disabled aria-label="摄像机左转">←</n-button><n-button disabled>停止</n-button><n-button disabled aria-label="摄像机右转">→</n-button></div>
          <n-button disabled aria-label="摄像机下转">↓</n-button>
          <div><n-input-number :value="1" :min="1" :max="255" disabled aria-label="摄像机位置" /><n-button disabled>调用位置</n-button><n-button disabled>保存位置</n-button></div>
        </div>
        <p class="unavailable">{{ camera.unavailable_reason }}</p>
      </n-card>
    </div>
    <div class="audio-grid" v-if="state">
      <n-card title="大屏音量" size="small">
        <p>输出节点 155 · 与电脑系统音量独立</p>
        <n-input-number v-model:value="volume" :min="0" :max="100" :precision="0" :disabled="busy" placeholder="尚未读回" :input-props="{ 'aria-label': '大屏音量' }" />
        <n-slider v-if="volume !== null" v-model:value="volume" :min="0" :max="100" :disabled="busy" aria-label="大屏音量滑块" />
        <div class="audio-actions"><n-button :disabled="busy" @click="run('read')">读取实际音量</n-button>
          <n-button type="primary" :disabled="busy || volume === null" @click="run('apply')">应用</n-button></div>
        <n-alert v-if="result" :type="['confirmed', 'simulated'].includes(result.status) ? 'info' : 'warning'">{{ result.detail }}</n-alert>
      </n-card>
      <n-card title="声音来源" size="small">
        <n-radio-group :value="state.audio.source ?? null" disabled aria-label="声音来源"><n-radio value="ip81">IP81</n-radio><n-radio value="scp_cv">SCP-cv</n-radio></n-radio-group>
        <p class="unavailable">{{ state.capabilities.reason }}</p>
        <p>手持话筒、悬挂话筒：通道未绑定，暂不可调整。</p>
      </n-card>
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
.audio-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}
.camera-controls{display:grid;gap:6px;justify-items:center;margin-top:16px}
.camera-controls>div{display:flex;gap:6px;flex-wrap:wrap;justify-content:center}
.camera-controls .n-input-number{width:88px}
.unavailable{font-size:var(--fontSizeBase200)}
.audio-actions{display:flex;gap:8px;margin:16px 0}
@media(max-width:1000px){.camera-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:640px){.camera-grid,.audio-grid{grid-template-columns:1fr}header{align-items:flex-start}}
</style>
