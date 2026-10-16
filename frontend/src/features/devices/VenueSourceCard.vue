<!-- 二选一声音来源；编辑只是草稿，显式应用，能力缺失拒绝，不冒充实体路由读回。 -->
<script setup lang="ts">
import { computed } from 'vue';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ARadio from 'ant-design-vue/es/radio';
import { RadioGroup as ARadioGroup } from 'ant-design-vue/es/radio';
import type { VenueAudioSettings, VenueState } from '@/services/venueApi';
const props = defineProps<{ busy: boolean; capabilities: VenueState['capabilities']; requested?: VenueAudioSettings['source'] }>();
const value = defineModel<VenueAudioSettings['source']>();
defineEmits<{ apply: [] }>();
const available = computed(() => value.value === 'ip81' ? props.capabilities.source_ip81 === true
  : value.value === 'scp_cv' && props.capabilities.source_scp_cv === true);
</script>
<template>
  <a-card title="声音来源" size="small" class="source-card">
    <a-radio-group v-model:value="value" :disabled="busy" aria-label="声音来源">
      <a-radio value="ip81" :disabled="capabilities.source_ip81 !== true">IP81</a-radio>
      <a-radio value="scp_cv" :disabled="capabilities.source_scp_cv !== true">SCP-cv</a-radio>
    </a-radio-group>
    <p>最后发送请求：{{ requested === 'ip81' ? 'IP81' : requested === 'scp_cv' ? 'SCP-cv' : '未配置' }}。实际声音来源需现场确认。</p>
    <p v-if="!capabilities.source_scp_cv" class="unavailable">SCP-cv：{{ capabilities.reason }}</p>
    <a-button type="primary" :disabled="busy || !available" @click="$emit('apply')">应用声音来源</a-button>
  </a-card>
</template>
<style scoped>
p{color:var(--colorNeutralForeground3);line-height:1.6}.unavailable{font-size:var(--fontSizeBase200)}
.source-card :deep(.ant-radio-wrapper){min-height:44px;align-items:center;margin-right:24px}
</style>
