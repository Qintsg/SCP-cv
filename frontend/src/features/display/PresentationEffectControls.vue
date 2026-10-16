<script setup lang="ts">
/** 动画步与翻页分开；在真实在线文稿上才提交操作。 */
import AButton from 'ant-design-vue/es/button';
import AAlert from 'ant-design-vue/es/alert';
import { onScopeDispose, ref } from 'vue';
import { useSessionStore } from '@/stores/sessions';
import { useToast } from '@/composables/useToast';
import { usePresentationCommandFeedback } from './usePresentationCommandFeedback';
const props = defineProps<{ windowId: number; disabled: boolean }>();
const sessions = useSessionStore();
const toast = useToast();
const feedback = usePresentationCommandFeedback(() => sessions.byWindowId(props.windowId),
  () => sessions.commandFeedbackRevisions[props.windowId] ?? 0);
const pending = ref(false);
let active = true;
// 页面离开后旧HTTP不再发布全局错误；已受理操作不会因此撤销或重放。
onScopeDispose(() => { active = false; });
/** :param action: 单个动画步。:returns: 等待真实控制面响应。 */
async function step(action: string): Promise<void> {
  if (props.disabled || pending.value) return;
  pending.value = true;
  try { await feedback.submit(() => sessions.control(props.windowId, action), action.toUpperCase()); }
  catch (error) {
    if (active) toast.error('动画步操作失败', error instanceof Error ? error.message : '请检查播放主机');
  }
  finally { pending.value = false; }
}
</script>
<template>
  <div class="presentation-effects">
    <div class="presentation-effects__actions">
    <a-button :disabled="disabled || pending" @click="step('previous_effect')">上一步动画</a-button>
    <a-button :disabled="disabled || pending" @click="step('next_effect')">下一步动画</a-button>
    </div>
    <a-alert type="info"><template #description>统一媒体暂停、恢复和定位暂不支持。</template></a-alert>
  </div>
</template>
<style scoped>
/** 动画与能力提示在桌面/手机中保持换行空间。 */
.presentation-effects { display: grid; gap: 12px; }
.presentation-effects__actions { display: flex; flex-wrap: wrap; gap: 8px; }
</style>
