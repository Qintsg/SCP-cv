<!-- 运行维护工作区：危险动作确认且受页面/账号/主机围栏约束，不重复首页布局/音量。 -->
<script setup lang="ts">
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ATag from 'ant-design-vue/es/tag';
import FIcon from '@/design-system/FIcon.vue';
import { useDialog } from '@/composables/useDialog';
import { useRequestContext } from '@/composables/useRequestContext';
import { useToast } from '@/composables/useToast';
import { useRuntimeStore } from '@/stores/runtime';
import { useSessionStore } from '@/stores/sessions';
import ClientPreviewSettings from './ClientPreviewSettings.vue';
const { t } = useI18n(), router = useRouter(), runtime = useRuntimeStore(), session = useSessionStore(), toast = useToast(), dialog = useDialog();
const pending = ref(''); const capture = useRequestContext(() => { pending.value = ''; });
const sseLastUpdateLabel = computed(() => runtime.sseLastUpdate ? t('settings.sseLastUpdate', { time: new Date(runtime.sseLastUpdate).toLocaleTimeString() }) : t('settings.sseNoUpdate'));
/** :returns: 只重建当前控制台SSE，不重启任何播放进程。 */
function refreshSse(): void { runtime.disconnectEvents(); runtime.connectEvents(); }
/** :param action: 明确维护动作。:returns: 确认后仅向原归属发送一次，不自动重试。 */
async function maintenance(action: 'reset' | 'restart'): Promise<void> {
  if (pending.value) return;
  const current = capture(); pending.value = action;
  try {
    const accepted = await dialog.danger({ title: action === 'restart' ? '重启播放运行组？' : '重置窗口 1／2？',
      description: action === 'restart' ? '会短暂中断播放并重启项目运行组，不重启 Windows。' : '会关闭两窗当前媒体，回到待机；不删除媒体和预案。', confirmLabel: action === 'restart' ? '重启运行组' : '重置窗口' });
    if (!accepted || !current()) return;
    if (action === 'restart') { const result = await session.restartAll(); if (current()) toast.warning('重启请求已处理', result.detail); }
    else { await session.resetAll(); if (current()) toast.success(t('settings.resetAllOk')); }
  } catch (failure) { if (current()) toast.error('维护操作失败', failure instanceof Error ? failure.message : '请检查连接'); }
  finally { if (current()) pending.value = ''; }
}
</script>
<template><section class="runtime-maintenance">
  <a-card title="维护工具"><div class="runtime-maintenance__tools">
    <div><strong>播放运行组</strong><p>重新启动播放器及相关项目服务。</p><a-button danger :disabled="!!pending" :loading="pending === 'restart'" @click="maintenance('restart')"><template #icon><FIcon name="arrow_repeat_all_24_regular" /></template>重启运行组</a-button></div>
    <div><strong>窗口 1／2</strong><p>清理当前播放内容，恢复待机。</p><a-button :disabled="!!pending" :loading="pending === 'reset'" @click="maintenance('reset')"><template #icon><FIcon name="arrow_reset_24_regular" /></template>重置窗口</a-button></div>
  </div></a-card>
  <a-card title="控制连接"><div class="runtime-maintenance__connection"><a-tag :color="runtime.sseStatus === 'connected' ? 'green' : 'orange'">{{ t(`app.sse.${runtime.sseStatus}`) }}</a-tag><span>{{ sseLastUpdateLabel }}</span></div><div class="runtime-maintenance__buttons"><a-button @click="refreshSse"><template #icon><FIcon name="arrow_clockwise_24_regular" /></template>重新建立连接</a-button><a-button @click="router.push('/connect')">更换播放主机</a-button></div></a-card>
  <ClientPreviewSettings />
</section></template>
<style scoped>.runtime-maintenance{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px;align-items:start}.runtime-maintenance>.ant-card:first-child{grid-column:1/-1}.runtime-maintenance__tools{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}.runtime-maintenance p{font-size:12px;color:var(--console-muted);margin:8px 0 16px}.runtime-maintenance__connection,.runtime-maintenance__buttons{display:flex;gap:8px;align-items:center;flex-wrap:wrap}.runtime-maintenance__connection{font-size:12px;color:var(--console-muted);margin-bottom:18px}@media(max-width:999px){.runtime-maintenance{grid-template-columns:minmax(0,1fr)}}@media(max-width:767px){.runtime-maintenance__tools{grid-template-columns:minmax(0,1fr)}}</style>
