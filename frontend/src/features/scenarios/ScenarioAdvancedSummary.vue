<!-- 预设高级意图的只读摘要，未知绑定不当作已执行，空项明确保持。 -->
<script setup lang="ts">
import { computed } from 'vue';
import AAlert from 'ant-design-vue/es/alert';
import type { ScenarioAdvanced } from '@/services/apiTypes';
import { microphoneAliasesConflict, microphoneVolume } from '@/services/venueApi';
import { televisionLabel } from './scenarioTelevisions';
const props = defineProps<{ advanced?: ScenarioAdvanced }>();
const names: Record<string, string> = { rear: '后摄像机', center1: '中摄像机1', center2: '中摄像机2', left: '左摄像机', right: '右摄像机' };
const positions = computed(() => Object.entries(props.advanced?.camera_positions ?? {}).filter(([, value]) => value != null && value > 0));
const audio = computed(() => props.advanced?.audio);
const microphone = computed(() => microphoneVolume(audio.value));
const microphoneConflict = computed(() => microphoneAliasesConflict(audio.value));
const unbound = computed(() => positions.value.some(([, value]) => value! > 6));
</script>
<template>
  <section class="advanced-summary" aria-label="预设高级配置摘要">
    <h3>电视显示</h3>
    <p>电视左：{{ televisionLabel(advanced?.televisions?.tv_left) }} · 电视右：{{ televisionLabel(advanced?.televisions?.tv_right) }}</p>
    <h3>摄像机与现场声音</h3>
    <p v-if="!positions.length">摄像机位置：保持</p>
    <p v-for="[id, position] in positions" :key="id">{{ names[id] ?? id }}：位置 {{ position }}</p>
    <p>大屏音量：{{ audio?.computer_volume == null ? '保持' : audio.computer_volume + '%' }} · 输出增益：{{ audio?.screen_volume == null ? '保持' : audio.screen_volume + '%' }}</p>
    <p>声音来源：{{ audio?.source == null ? '保持' : audio.source === 'ip81' ? 'IP81' : 'SCP-cv' }}</p>
    <p v-if="audio?.source === 'scp_cv'">SCP-cv要求调用时软件输出就绪；不满足时在任何动作前拒绝。</p>
    <p>手持话筒：{{ audio?.handheld_volume == null ? '保持' : audio.handheld_volume + '%' }} · 悬挂（头戴）话筒：{{ microphoneConflict ? '配置冲突' : microphone == null ? '保持' : microphone + '%' }}</p>
    <a-alert v-if="microphoneConflict" type="error"><template #description>同一话筒配置冲突：头戴 {{ audio?.headset_volume }}% / 旧悬挂 {{ audio?.hanging_volume }}%；请编辑后再调用，任何设备动作前会拒绝。</template></a-alert>
    <a-alert v-if="unbound" type="warning"><template #description>包含尚未绑定的设备配置，调用时会在任何设备动作前拒绝。请先改为保持，或完成现场绑定。</template></a-alert>
  </section>
</template>
<style scoped>
.advanced-summary{margin-top:16px;border-top:1px solid var(--colorNeutralStrokeDivider);padding-top:12px}
h3{font-size:var(--fontSizeBase300);margin:0 0 8px}h3:not(:first-child){margin-top:14px}p{color:var(--colorNeutralForeground2);font-size:var(--fontSizeBase200);line-height:1.6;margin:4px 0}
</style>
