<!-- 高级现场项默认保持；未绑定项可保存意图但不能执行，明确提示限制。 -->
<script setup lang="ts">
import { NAlert, NButton, NInputNumber, NSelect } from 'naive-ui';
import type { ScenarioAdvanced } from '@/services/apiTypes';
const value = defineModel<ScenarioAdvanced>({ required: true });
const cameras = [['rear','后摄像机'],['center1','中摄像机1'],['center2','中摄像机2'],['left','左摄像机'],['right','右摄像机']];
/** :param id: 受信摄像机。:param position: null保持。:returns: 新对象避免只读引用和遗留值丢失。 */
function setPosition(id: string, position: number | null): void { value.value = { ...value.value, camera_positions: { ...value.value.camera_positions, [id]: position } }; }
/** :param key: 音频字段。:param setting: null保持。:returns: 不改变其它高级项。 */
function setAudio(key: 'screen_volume' | 'handheld_volume' | 'hanging_volume' | 'source', setting: number | string | null): void {
  value.value = { ...value.value, audio: { ...value.value.audio, [key]: setting } };
}
</script>
<template>
  <details class="advanced-settings"><summary>高级：摄像机位置与现场音量</summary>
    <p>留空表示调用预设时保持当前状态。</p>
    <n-alert type="warning">当前仅大屏音量可执行。摄像机位置、话筒与来源绑定尚未核实；配置这些项后，调用预设会在任何设备动作前拒绝，不会部分切换。</n-alert>
    <label v-for="[id,label] in cameras" :key="id">{{ label }}
      <n-input-number :value="value.camera_positions?.[id] ?? null" :min="1" :max="255" :precision="0" clearable placeholder="保持（未绑定）" :input-props="{ 'aria-label': label+'预设位置' }" @update:value="setPosition(id,$event)" /></label>
    <label>大屏音量<n-input-number :value="value.audio?.screen_volume ?? null" :min="0" :max="100" :precision="0" clearable placeholder="保持" :input-props="{ 'aria-label': '预设大屏音量' }" @update:value="setAudio('screen_volume',$event)" /></label>
    <label>手持话筒<n-input-number :value="value.audio?.handheld_volume ?? null" :min="0" :max="100" :precision="0" clearable placeholder="保持（未绑定）" :input-props="{ 'aria-label': '预设手持话筒音量' }" @update:value="setAudio('handheld_volume',$event)" /></label>
    <label>悬挂话筒<n-input-number :value="value.audio?.hanging_volume ?? null" :min="0" :max="100" :precision="0" clearable placeholder="保持（未绑定）" :input-props="{ 'aria-label': '预设悬挂话筒音量' }" @update:value="setAudio('hanging_volume',$event)" /></label>
    <label>声音来源<n-select :value="value.audio?.source ?? null" clearable placeholder="保持（未绑定）" :options="[{label:'IP81',value:'ip81'},{label:'SCP-cv',value:'scp_cv'}]" aria-label="预设声音来源" @update:value="setAudio('source',$event)" /></label>
    <n-button @click="value = {}">全部保持</n-button>
  </details>
</template>
<style scoped>
.advanced-settings{margin:16px 0}.advanced-settings summary{cursor:pointer;font-weight:600}.advanced-settings label{display:flex;align-items:center;gap:12px;justify-content:space-between;margin:12px 0}.advanced-settings label>:last-child{width:220px}
</style>
