<!-- 服务端共享直播预览状态：仅只读诊断，不再要求观看端安装工具或填写本机令牌。 -->
<script setup lang="ts">
import { onMounted, ref } from 'vue';
import ACard from 'ant-design-vue/es/card';
import AButton from 'ant-design-vue/es/button';
import { requestJson } from '@/services/api';
import { useRequestContext } from '@/composables/useRequestContext';
interface PreviewStatus { lease_count: number; upstream_count: number; max_upstreams: number; egress_kbps: number }
const status = ref<PreviewStatus>(), busy = ref(false), detail = ref('');
const capture = useRequestContext(() => { status.value = undefined; detail.value = ''; });
/** :returns: 只读取原主机诊断，不探测或启动媒体流。 */
async function refresh(): Promise<void> {
  if (busy.value) return; const current = capture(); busy.value = true; detail.value = '';
  try { const result = await requestJson<PreviewStatus>('/api/stream-previews/status/', {}, 8000, { canConsume: current }); if (current()) status.value = result; }
  catch { if (current()) detail.value = '暂时无法读取预览状态'; }
  finally { busy.value = false; }
}
onMounted(refresh);
</script>
<template><a-card title="直播预览"><div class="client-preview-settings">
  <p>由播放主机按需提供，观看电脑无需安装 Node 或 FFmpeg。相同流共享拉取，离开页面后释放；预览没有声音。</p>
  <p>网络拥塞时降低预览画质与帧率，优先保留正式播放资源。</p>
  <div v-if="status" class="client-preview-settings__status"><span>{{ status.upstream_count }} / {{ status.max_upstreams }} 路预览</span><span>{{ status.lease_count }} 个观看会话</span><span>出站预算 {{ status.egress_kbps }} kbps</span></div>
  <p v-if="detail">{{ detail }}</p><a-button :loading="busy" @click="refresh">刷新预览状态</a-button>
</div></a-card></template>
<style scoped>.client-preview-settings{display:grid;gap:12px}.client-preview-settings p{font-size:12px;color:var(--console-muted);margin:0;line-height:1.7}.client-preview-settings__status{display:flex;gap:8px 16px;flex-wrap:wrap;font-size:12px}.client-preview-settings>.ant-btn{justify-self:start}</style>
