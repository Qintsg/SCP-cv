<!-- 单路现场音量显式应用；空值不写，滑动不自动下发，不将请求值当DSP读回。 -->
<script setup lang="ts">
import { NButton, NCard, NInputNumber, NSlider } from 'naive-ui';
defineProps<{ title: string; caption: string; busy: boolean; disabled?: boolean; readback?: boolean }>();
const value = defineModel<number | null>({ required: true });
defineEmits<{ apply: []; read: [] }>();
</script>
<template>
  <n-card :title="title" size="small" class="audio-card">
    <p>{{ caption }}</p>
    <n-input-number v-model:value="value" :min="0" :max="100" :precision="0" :disabled="busy || disabled" :show-button="false" placeholder="未配置，保持原状"
      :input-props="{ 'aria-label': title }" />
    <n-slider v-if="value !== null" v-model:value="value" :min="0" :max="100" :disabled="busy || disabled" :aria-label="title + '滑块'" />
    <div class="actions"><n-button v-if="readback" :disabled="busy || disabled" @click="$emit('read')">读取实际值</n-button>
      <n-button type="primary" :disabled="busy || disabled || value === null" @click="$emit('apply')">应用{{ title }}</n-button></div>
  </n-card>
</template>
<style scoped>
p{color:var(--colorNeutralForeground3);line-height:1.6;min-height:44px}.n-slider{margin-top:12px}.actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:16px}
</style>
