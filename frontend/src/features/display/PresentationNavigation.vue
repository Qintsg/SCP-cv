<script setup lang="ts">
/** 文件描述：文稿两按钮步进。项目：SCP-cv；文件：PresentationNavigation.vue；作者：Qintsg；日期：2026-10-27。 */
import AButton from 'ant-design-vue/es/button';
import { onScopeDispose, ref } from 'vue';
import { useSessionStore } from '@/stores/sessions';
import { useToast } from '@/composables/useToast';
import { usePresentationCommandFeedback } from './usePresentationCommandFeedback';
import { presentationStep } from './presentationStep';
import FIcon from '@/design-system/FIcon.vue';
const props = defineProps<{ windowId: number; disabled: boolean; mode?: string }>();
const sessions = useSessionStore();
const toast = useToast();
const feedback = usePresentationCommandFeedback(() => sessions.byWindowId(props.windowId),
  () => sessions.commandFeedbackRevisions[props.windowId] ?? 0);
const pending = ref(false);
let active = true;
// 页面离开后旧HTTP不再发布全局错误；已受理操作不会因此撤销或重放。
onScopeDispose(() => { active = false; });
/** :param direction: 步进方向。:returns: 等待真实控制面响应。 */
async function step(direction: 'prev' | 'next'): Promise<void> {
  if (props.disabled || pending.value) return;
  pending.value = true;
  const selected = presentationStep(props.mode, direction);
  try { await feedback.submit(() => selected.kind === 'effect'
    ? sessions.control(props.windowId, selected.action) : sessions.navigate(props.windowId, selected.action), selected.command); }
  catch (error) {
    if (active) toast.error('步进失败', error instanceof Error ? error.message : '请检查播放主机');
  }
  finally { pending.value = false; }
}
</script>
<template>
  <div class="presentation-navigation" role="group" aria-label="演示文稿步进">
    <a-button aria-label="上一步" :disabled="disabled || pending" @click="step('prev')"><template #icon><FIcon name="previous_24_regular" /></template>上一步</a-button>
    <a-button aria-label="下一步" type="primary" :disabled="disabled || pending" @click="step('next')">下一步<template #icon><FIcon name="next_24_regular" /></template></a-button>
  </div>
</template>
<style scoped>
.presentation-navigation { display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;width:100%; }
.presentation-navigation :deep(.ant-btn) { min-height:52px;font-size:16px;border-radius:16px;display:flex;align-items:center;justify-content:center;gap:10px; }
</style>
