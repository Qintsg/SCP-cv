<!-- 一个真实云台手控块：方向与变焦按住，位置调用显式，保存需二次确认。 -->
<script setup lang="ts">
import { nullableNumber } from '@/design-system/controlValue';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import AInputNumber from 'ant-design-vue/es/input-number';
import APopconfirm from 'ant-design-vue/es/popconfirm';
import { venueApi, type CameraDirection, type VenueCamera } from '@/services/venueApi';
import { useRuntimeStore } from '@/stores/runtime';
import { useAuthStore } from '@/stores/auth';
import { CameraMotionLease } from './motionLease';
const props = defineProps<{ camera: VenueCamera; clockOffset?: number }>();
const runtime = useRuntimeStore(); const auth = useAuthStore();
const active = ref<CameraDirection | null>(null); const error = ref(''); const feedback = ref('');
const position = ref<number | null>(1); const busy = ref(false); let alive = true;
const disabled = computed(() => !props.camera.control_available || busy.value);
let lease: CameraMotionLease | null = null;
const directions: { value: CameraDirection; label: string; symbol: string }[] = [
  { value: 'up', label: '向上', symbol: '↑' }, { value: 'left', label: '向左', symbol: '←' },
  { value: 'right', label: '向右', symbol: '→' }, { value: 'down', label: '向下', symbol: '↓' },
  { value: 'zoom_in', label: '拉近', symbol: '＋' }, { value: 'zoom_out', label: '拉远', symbol: '−' },
];

/** :param direction: 方向。:returns: 身份、主机及摄像机围栏内的新租约，跨主机后只依赖旧服务器超时释放。 */
function start(direction: CameraDirection): void {
  if (disabled.value) return;
  lease?.stop(); error.value = ''; feedback.value = '';
  const generation = runtime.connectionGeneration; const user = auth.user?.id; const camera = props.camera.id;
  const current = () => alive && generation === runtime.connectionGeneration && user === auth.user?.id && camera === props.camera.id;
  lease = new CameraMotionLease(request => {
    if (generation !== runtime.connectionGeneration || user !== auth.user?.id) return Promise.resolve();
    return venueApi.motion(camera, request);
  }, failure => { if (current()) error.value = failure instanceof Error ? failure.message : '运动发送失败，请检查连接。'; },
  direction => { if (current()) active.value = direction; }, () => Date.now() + (props.clockOffset ?? 0));
  lease.start(direction);
}
function stop(): void { lease?.stop(); active.value = null; }
function pointerStart(event: PointerEvent, direction: CameraDirection): void {
  if (event.button !== 0 || disabled.value) return;
  (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId); start(direction);
}
function keyStart(event: KeyboardEvent, direction: CameraDirection): void {
  if (![' ', 'Enter'].includes(event.key)) return; event.preventDefault();
  if (!event.repeat) start(direction);
}
function keyStop(event: KeyboardEvent): void { if ([' ', 'Enter'].includes(event.key)) { event.preventDefault(); stop(); } }
function hidden(): void { if (document.visibilityState !== 'visible') stop(); }
/** :param action: 保存会覆盖所选现场位置。:returns: 不自动重复的调用及明确发送反馈。 */
async function applyPosition(action: 'call' | 'save'): Promise<void> {
  if (disabled.value || position.value === null || position.value > (props.camera.position_count ?? 6)) return;
  stop(); busy.value = true; error.value = '';
  const camera = props.camera.id; const generation = runtime.connectionGeneration; const user = auth.user?.id;
  const current = () => alive && camera === props.camera.id && generation === runtime.connectionGeneration && user === auth.user?.id;
  try { const result = await venueApi.position(camera, position.value, action); if (current()) feedback.value = result.camera.detail; }
  catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : '位置操作未确认'; }
  finally { if (alive) busy.value = false; }
}
watch(() => [props.camera.id, runtime.connectionGeneration, auth.user?.id], () => { stop(); feedback.value = ''; error.value = ''; });
watch(() => props.camera.control_available, available => { if (!available) stop(); });
onMounted(() => { window.addEventListener('blur', stop); document.addEventListener('visibilitychange', hidden); });
onBeforeUnmount(() => { stop(); alive = false; window.removeEventListener('blur', stop); document.removeEventListener('visibilitychange', hidden); });
</script>
<template>
  <div class="camera-pad">
    <div class="pad-copy"><h3>{{ camera.name }}</h3><p>按住转动或变焦，松开即停止。断线后自动释放。</p></div>
    <div class="pad-buttons" aria-label="云台方向与变焦">
      <button v-for="direction in directions" :key="direction.value" type="button" :class="[direction.value, { held: active === direction.value }]"
        :disabled="disabled" :aria-label="camera.name + direction.label" :aria-pressed="active === direction.value"
        @pointerdown="pointerStart($event, direction.value)" @pointerup="stop" @pointercancel="stop" @lostpointercapture="stop"
        @keydown="keyStart($event, direction.value)" @keyup="keyStop" @blur="stop">{{ direction.symbol }}</button>
      <button class="stop" type="button" :disabled="!camera.control_available" @click="stop" aria-label="停止当前云台运动">停止</button>
    </div>
    <div class="positions"><label>位置<a-input-number :value="position ?? undefined" @update:value="position = nullableNumber($event)" :min="1" :max="camera.position_count ?? 6" :precision="0" :disabled="disabled" :controls="false"
      v-bind="{ 'aria-label': camera.name + '位置' }" /></label>
      <a-button :disabled="disabled || position === null" :loading="busy" @click="applyPosition('call')">调用位置</a-button>
      <a-popconfirm :disabled="disabled || position === null" :ok-button-props="{ style: { minHeight: '44px' } }" :cancel-button-props="{ style: { minHeight: '44px' } }" @confirm="applyPosition('save')">
        <a-button :disabled="disabled || position === null">保存当前位置…</a-button>
        <template #title>将覆盖{{ camera.name }}的位置{{ position }}，确认保存？</template>
      </a-popconfirm><small>位置1–{{ camera.position_count ?? 6 }} · 操作后不自动重试</small>
    </div>
    <a-alert v-if="error" type="error"><template #description>{{ error }}</template></a-alert><a-alert v-if="feedback" type="info"><template #description>{{ feedback }}</template></a-alert>
  </div>
</template>
<style scoped>
.camera-pad{display:grid;grid-template-columns:minmax(0,1fr);gap:20px;align-items:start;min-width:0}
h3{margin:0 0 8px;font-size:var(--fontSizeBase500)}p,small{color:var(--colorNeutralForeground3);line-height:1.6}
.pad-buttons{display:grid;grid-template-columns:repeat(3,48px) 48px;grid-template-rows:repeat(3,48px);gap:6px;justify-self:center}
.pad-buttons button{border:1px solid var(--colorNeutralStroke1);border-radius:var(--borderRadiusMedium);background:var(--colorNeutralBackground1);color:var(--colorNeutralForeground1);font:inherit;font-size:24px;touch-action:none;user-select:none;cursor:pointer}
.pad-buttons button:hover:not(:disabled),.pad-buttons button.held{background:var(--colorBrandBackground2);border-color:var(--colorBrandStroke1);color:var(--colorBrandForeground1)}
.pad-buttons button:focus-visible{outline:2px solid var(--colorBrandStroke1);outline-offset:2px}.pad-buttons button:disabled{opacity:.4;cursor:not-allowed}
.up{grid-area:1/2}.left{grid-area:2/1}.right{grid-area:2/3}.down{grid-area:3/2}.zoom_in{grid-area:1/4}.zoom_out{grid-area:3/4}.pad-buttons .stop{grid-area:2/2;font-size:12px;font-weight:600}
.positions{display:flex;flex-wrap:wrap;gap:8px;align-items:center}.positions label{display:flex;align-items:center;gap:12px;width:100%;margin-bottom:4px}.positions .ant-input-number{width:100px}.positions small{width:100%}
.camera-pad>.ant-alert{grid-column:1/-1}.pad-copy{min-width:0}.positions{min-width:0}
</style>
