<!-- 预设高级意图的只读摘要，未知绑定不当作已执行，空项明确保持。 -->
<script setup lang="ts">
import { computed } from 'vue';
import { NAlert } from 'naive-ui';
import type { ScenarioAdvanced } from '@/services/apiTypes';
const props = defineProps<{ advanced?: ScenarioAdvanced }>();
const names: Record<string, string> = { rear: '后摄像机', center1: '中摄像机1', center2: '中摄像机2', left: '左摄像机', right: '右摄像机' };
const positions = computed(() => Object.entries(props.advanced?.camera_positions ?? {}).filter(([, value]) => value != null));
const audio = computed(() => props.advanced?.audio);
const unbound = computed(() => positions.value.length > 0 || audio.value?.source != null || audio.value?.handheld_volume != null || audio.value?.hanging_volume != null);
</script>
<template>
  <section class="advanced-summary" aria-label="预设高级配置摘要">
    <h3>摄像机与现场声音</h3>
    <p v-if="!positions.length">摄像机位置：保持</p>
    <p v-for="[id, position] in positions" :key="id">{{ names[id] ?? id }}：位置 {{ position }}</p>
    <p>大屏音量：{{ audio?.screen_volume == null ? '保持' : audio.screen_volume + '%' }}</p>
    <p>声音来源：{{ audio?.source == null ? '保持' : audio.source === 'ip81' ? 'IP81' : 'SCP-cv' }}</p>
    <p>手持话筒：{{ audio?.handheld_volume == null ? '保持' : audio.handheld_volume + '%' }} · 悬挂话筒：{{ audio?.hanging_volume == null ? '保持' : audio.hanging_volume + '%' }}</p>
    <n-alert v-if="unbound" type="warning">包含尚未绑定的设备配置，调用时会在任何设备动作前拒绝。请先改为保持，或完成现场绑定。</n-alert>
  </section>
</template>
<style scoped>
.advanced-summary{margin-top:16px;border-top:1px solid var(--colorNeutralStrokeDivider);padding-top:12px}
h3{font-size:var(--fontSizeBase300);margin:0 0 8px}p{color:var(--colorNeutralForeground2);font-size:var(--fontSizeBase200);line-height:1.6;margin:4px 0}
</style>
