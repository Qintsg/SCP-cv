<!-- 高级现场项默认保持；未绑定项可保存意图但不能执行，明确提示限制。 -->
<script setup lang="ts">
import { computed } from 'vue';
import { NAlert, NButton, NInputNumber, NSelect } from 'naive-ui';
import type { ScenarioAdvanced } from '@/services/apiTypes';
import { microphoneAliasesConflict, microphoneVolume } from '@/services/venueApi';
const value = defineModel<ScenarioAdvanced>({ required: true });
const microphoneConflict = computed(() => microphoneAliasesConflict(value.value.audio));
const cameras = [['rear','后摄像机'],['center1','中摄像机1'],['center2','中摄像机2'],['left','左摄像机'],['right','右摄像机']];
/** :param id: 受信摄像机。:param position: null保持。:returns: 新对象避免只读引用和遗留值丢失。 */
function setPosition(id: string, position: number | null): void { value.value = { ...value.value, camera_positions: { ...value.value.camera_positions, [id]: position } }; }
/** :param key: 音频字段。:param setting: null保持。:returns: 不改变其它高级项。 */
function setAudio(key: 'screen_volume' | 'computer_volume' | 'handheld_volume' | 'headset_volume' | 'hanging_volume' | 'source', setting: number | string | null): void {
  const audio = { ...value.value.audio, [key]: setting };
  // 编辑唯一话筒项时同时清除旧别名，清空后也不能重新激活旧悬挂值。
  if (key === 'headset_volume') audio.hanging_volume = null;
  value.value = { ...value.value, audio };
}
</script>
<template>
  <details class="advanced-settings"><summary>高级：摄像机位置与现场音量</summary>
    <p>留空表示调用预设时保持当前状态。</p>
    <n-alert type="info">摄像机位置1–6和中控音量可发送，实际机位与话筒增益无读回。悬挂与原头戴1/2为同一组；IP81可独立选用，SCP-cv需软件输出就绪。调用前全部预检，不可用时不执行任何动作。</n-alert>
    <n-alert v-if="microphoneConflict" type="error">悬挂（头戴）话筒配置冲突：头戴 {{ value.audio?.headset_volume }}% / 旧悬挂 {{ value.audio?.hanging_volume }}%。请在下面重新输入一个值或清空；保存与调用前会拒绝全部动作。</n-alert>
    <label v-for="[id,label] in cameras" :key="id">{{ label }}
      <n-input-number :value="value.camera_positions?.[id] ?? null" :show-button="false" :min="1" :max="6" :precision="0" clearable placeholder="保持" :input-props="{ 'aria-label': label+'预设位置' }" @update:value="setPosition(id,$event)" /></label>
    <label>大屏音量（电脑音频）<n-input-number :value="value.audio?.computer_volume ?? null" :show-button="false" :min="0" :max="100" :precision="0" clearable placeholder="保持" :input-props="{ 'aria-label': '预设大屏音量' }" @update:value="setAudio('computer_volume',$event)" /></label>
    <label>输出155增益<n-input-number :value="value.audio?.screen_volume ?? null" :show-button="false" :min="0" :max="100" :precision="0" clearable placeholder="保持" :input-props="{ 'aria-label': '预设输出增益' }" @update:value="setAudio('screen_volume',$event)" /></label>
    <label>手持话筒<n-input-number :value="value.audio?.handheld_volume ?? null" :show-button="false" :min="0" :max="100" :precision="0" clearable placeholder="保持" :input-props="{ 'aria-label': '预设手持话筒音量' }" @update:value="setAudio('handheld_volume',$event)" /></label>
    <label>悬挂（头戴）话筒<n-input-number :value="microphoneVolume(value.audio)" :show-button="false" :min="0" :max="100" :precision="0" clearable placeholder="保持" :input-props="{ 'aria-label': '预设悬挂（头戴）话筒音量' }" @update:value="setAudio('headset_volume',$event)" /></label>
    <label>声音来源<n-select :value="value.audio?.source ?? null" clearable placeholder="保持" :options="[{label:'IP81',value:'ip81'},{label:'SCP-cv（需输出就绪）',value:'scp_cv'}]" aria-label="预设声音来源" @update:value="setAudio('source',$event)" /></label>
    <n-button @click="value = {}">全部保持</n-button>
  </details>
</template>
<style scoped>
.advanced-settings{margin:16px 0}.advanced-settings summary{cursor:pointer;font-weight:600}.advanced-settings label{display:flex;align-items:center;gap:12px;justify-content:space-between;margin:12px 0}.advanced-settings label>:last-child{width:220px}
@media(max-width:640px){
  .advanced-settings label{display:grid;grid-template-columns:minmax(0,1fr);justify-content:stretch;gap:6px;margin:16px 0}.advanced-settings label>:last-child{width:100%}
  .advanced-settings :deep(.n-input),.advanced-settings :deep(.n-base-selection){--n-height:44px!important}
  .advanced-settings summary{min-height:44px;display:flex;align-items:center}
  .advanced-settings :deep(.n-button){min-height:44px;min-width:44px}
}
</style>
