<!-- 显示块来源编辑；预留3/4可配置，但服务端仍拒绝实体应用。 -->
<script setup lang="ts">
import { ref, watch } from 'vue';
import AModal from 'ant-design-vue/es/modal';
import ASelect from 'ant-design-vue/es/select';
import AInput from 'ant-design-vue/es/input';
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import type { VideoWallMappingItem } from '@/services/apiTypes';
import { validIp } from './layoutModel';
const props = defineProps<{ open: boolean; value?: VideoWallMappingItem['input'] }>();
const emit = defineEmits<{ 'update:open': [value: boolean]; choose: [value: VideoWallMappingItem['input']] }>();
const kind = ref<VideoWallMappingItem['input']['kind']>('window_1'), ip = ref('');
const options = [{ value: 'window_1', label: '窗口 1' }, { value: 'window_2', label: '窗口 2' },
  { value: 'window_3', label: '窗口 3 · 预留' }, { value: 'window_4', label: '窗口 4 · 预留' },
  { value: 'laptop', label: 'IP81 · 笔记本' }, { value: 'ip_stream', label: '自定义 IP' }];
watch(() => props.open, open => { if (open) { kind.value = props.value?.kind || 'window_1'; ip.value = props.value?.ip_address || ''; } });
/** :returns: 只返回本地选择，不发送任何API。 */
function choose(): void {
  if (kind.value === 'ip_stream' && !validIp(ip.value.trim())) return;
  emit('choose', { kind: kind.value, ip_address: kind.value === 'ip_stream' ? ip.value.trim() : '' }); emit('update:open', false);
}
</script>
<template>
  <a-modal :open="open" title="选择显示来源" ok-text="确定" cancel-text="取消" :ok-button-props="{ disabled: kind === 'ip_stream' && !validIp(ip.trim()) }" @ok="choose" @cancel="emit('update:open', false)">
    <a-select v-select-aria-label="'显示来源'" v-model:value="kind" :options="options" aria-label="显示来源" style="width:100%" />
    <a-input v-if="kind === 'ip_stream'" v-model:value="ip" aria-label="输入设备IPv4" placeholder="192.168.5.…" style="margin-top:12px" />
    <p v-if="kind === 'window_3' || kind === 'window_4'" class="wall-picker-note">此窗口仅预留，可编辑布局，暂不能应用到实体大屏。</p>
  </a-modal>
</template>
<style scoped>.wall-picker-note{font-size:12px;color:var(--console-muted);margin:12px 0 0}</style>
