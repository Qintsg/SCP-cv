<!-- 显示块来源编辑；统一使用正式A输入菜单，选择只更改本地表单。 -->
<script setup lang="ts">
import { ref, watch } from 'vue';
import AModal from 'ant-design-vue/es/modal';
import InputSourcePicker from '../inputs/InputSourcePicker.vue';
import type { VideoWallMappingItem } from '@/services/apiTypes';
const props = defineProps<{ open: boolean; value?: VideoWallMappingItem['input'] }>();
const emit = defineEmits<{ 'update:open': [value: boolean]; choose: [value: VideoWallMappingItem['input']] }>();
const selected = ref<VideoWallMappingItem['input']>({ kind: 'window_1', ip_address: '' });
watch(() => props.open, open => { if (open) selected.value = { kind: props.value?.kind || 'window_1', ip_address: props.value?.ip_address || '' }; });
/** :returns: 只返回本地明确选择，实体应用由上层按钮执行。 */
function choose(): void { emit('choose', { ...selected.value }); emit('update:open', false); }
</script>
<template>
  <a-modal :open="open" title="选择显示来源" ok-text="确定" cancel-text="取消" @ok="choose" @cancel="emit('update:open', false)">
    <InputSourcePicker :value="selected" aria-label="显示块输入来源" @choose="selected = $event" />
  </a-modal>
</template>
