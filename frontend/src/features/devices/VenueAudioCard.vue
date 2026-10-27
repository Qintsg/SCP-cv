<!-- 单路现场音量显式应用；空值不写，滑动不自动下发，不将请求值当DSP读回。 -->
<script setup lang="ts">
import { nullableNumber } from '@/design-system/controlValue';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import AInputNumber from 'ant-design-vue/es/input-number';
import ASlider from 'ant-design-vue/es/slider';
defineProps<{ title: string; busy: boolean; disabled?: boolean; readback?: boolean }>();
const value = defineModel<number | null>({ required: true });
defineEmits<{ apply: []; read: [] }>();
</script>
<template>
  <a-card :title="title" size="small" class="audio-card">
    <a-input-number :value="value ?? undefined" @update:value="value = nullableNumber($event)" :min="0" :max="100" :precision="0" :disabled="busy || disabled" :controls="false" placeholder="保持" addon-after="%"
      v-bind="{ 'aria-label': title }" />
    <a-slider v-if="value !== null" v-model:value="value" :min="0" :max="100" :disabled="busy || disabled" :aria-label="title + '滑块'" />
    <div class="actions"><a-button v-if="readback" :disabled="busy || disabled" @click="$emit('read')">读取实际值</a-button>
      <a-button type="primary" :disabled="busy || disabled || value === null" @click="$emit('apply')">应用{{ title }}</a-button></div>
  </a-card>
</template>
<style scoped>
.ant-slider{margin-top:12px}.actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:16px}
</style>
