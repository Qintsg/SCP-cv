<!-- 二选一声音来源；编辑只是草稿，显式应用，能力缺失拒绝，不冒充实体路由读回。 -->
<script setup lang="ts">
import { computed } from 'vue';
import { NButton, NCard, NRadio, NRadioGroup } from 'naive-ui';
import type { VenueAudioSettings, VenueState } from '@/services/venueApi';
const props = defineProps<{ busy: boolean; capabilities: VenueState['capabilities']; requested?: VenueAudioSettings['source'] }>();
const value = defineModel<VenueAudioSettings['source']>();
defineEmits<{ apply: [] }>();
const available = computed(() => value.value === 'ip81' ? props.capabilities.source_ip81 === true
  : value.value === 'scp_cv' && props.capabilities.source_scp_cv === true);
</script>
<template>
  <n-card title="声音来源" size="small" class="source-card">
    <n-radio-group v-model:value="value" :disabled="busy" aria-label="声音来源">
      <n-radio value="ip81" :disabled="capabilities.source_ip81 !== true">IP81</n-radio>
      <n-radio value="scp_cv" :disabled="capabilities.source_scp_cv !== true">SCP-cv</n-radio>
    </n-radio-group>
    <p>最后发送请求：{{ requested === 'ip81' ? 'IP81' : requested === 'scp_cv' ? 'SCP-cv' : '未配置' }}。实际声音来源需现场确认。</p>
    <p v-if="!capabilities.source_scp_cv" class="unavailable">SCP-cv：{{ capabilities.reason }}</p>
    <n-button type="primary" :disabled="busy || !available" @click="$emit('apply')">应用声音来源</n-button>
  </n-card>
</template>
<style scoped>
p{color:var(--colorNeutralForeground3);line-height:1.6}.unavailable{font-size:var(--fontSizeBase200)}
.source-card :deep(.n-radio){min-height:44px;align-items:center;margin-right:24px}
</style>
