<!-- 一个真实云台手控块：方向与变焦按住，位置调用显式，保存需二次确认。 -->
<script setup lang="ts">
import { nullableNumber } from '@/design-system/controlValue';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import AInputNumber from 'ant-design-vue/es/input-number';
import APopconfirm from 'ant-design-vue/es/popconfirm';
import type { CameraDirection, VenueCamera } from '@/services/venueApi';
import { useCameraControls } from './useCameraControls';
const props = defineProps<{ camera: VenueCamera; clockOffset?: number }>();
const { active, error, feedback, position, busy, disabled, saveDisabled, confirmation, stop, pointerStart, keyStart, keyStop, applyPosition, openConfirmation, saveConfirmed } = useCameraControls(props);
const directions: { value: CameraDirection; label: string; symbol: string }[] = [
  { value: 'up', label: '向上', symbol: '↑' }, { value: 'left', label: '向左', symbol: '←' },
  { value: 'right', label: '向右', symbol: '→' }, { value: 'down', label: '向下', symbol: '↓' },
  { value: 'zoom_in', label: '拉近', symbol: '＋' }, { value: 'zoom_out', label: '拉远', symbol: '−' },
];

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
      <a-popconfirm :open="confirmation !== null" :disabled="saveDisabled" :ok-button-props="{ danger: true, style: { minHeight: '44px' } }" :cancel-button-props="{ style: { minHeight: '44px' } }" @open-change="openConfirmation" @confirm="saveConfirmed">
        <a-button :disabled="saveDisabled">保存当前位置…</a-button>
        <template #title>将覆盖{{ confirmation?.name }}的位置{{ confirmation?.position }}，确认保存？</template>
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
