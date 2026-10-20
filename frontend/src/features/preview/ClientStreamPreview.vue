<!-- 用户本机无声流预览：只连接loopback桥；无能力/失败就地展示，绝不代理到D4。 -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { VideoWallMappingItem } from '@/services/apiTypes';
import { PREVIEW_BRIDGE, previewAddress, previewToken } from './previewAddress';
import FIcon from '@/design-system/FIcon.vue';
import { getClientBuildTarget } from '@/platform';
const props = withDefaults(defineProps<{ input?: VideoWallMappingItem['input']; address?: string;
  quality?: 'low' | 'high'; enabled?: boolean; label?: string }>(), { quality: 'low', enabled: true, label: '画面预览', address: '' });
const url = computed(() => props.address || previewAddress(props.input, props.quality));
const frame = ref(''), status = ref(''), detail = ref('');
let alive = true, generation = 0, timer: ReturnType<typeof setTimeout> | undefined, controller: AbortController | undefined;
let session = '', token = '', seen = true, observer: IntersectionObserver | undefined;
const root = ref<HTMLElement | null>(null);
/** :returns: 撤销本轮订阅与帧，只释放本组件拥有的本机桥会话。 */
function stop(): void {
  generation++; clearTimeout(timer); controller?.abort(); controller = undefined;
  const previous = session, previousToken = token; session = '';
  if (previous) void fetch(`${PREVIEW_BRIDGE}/sessions/${previous}`, { method: 'DELETE', headers: { Authorization: `Bearer ${previousToken}` } }).catch(() => {});
  if (frame.value) URL.revokeObjectURL(frame.value); frame.value = '';
}
/** :returns: 创建本机订阅并轮询最新JPEG；所有错误固定文案，不回显源地址/凭据。 */
async function start(): Promise<void> {
  stop(); detail.value = ''; status.value = '';
  if (!url.value || !props.enabled || !alive || !seen || document.hidden) return;
  if (getClientBuildTarget() !== 'web' || !['http:', 'https:'].includes(window.location.protocol)) { status.value = '当前客户端未接入本机预览'; detail.value = '请使用浏览器连接本机预览服务'; return; }
  token = previewToken();
  if (!token) { status.value = '本机预览未启用'; detail.value = '在设置中连接本机预览服务'; return; }
  const ticket = generation, currentToken = token;
  const ownerController = new AbortController(); controller = ownerController; const signal = ownerController.signal;
  status.value = '正在连接预览';
  const timeout = setTimeout(() => ownerController.abort(), 12000);
  try {
    const response = await fetch(`${PREVIEW_BRIDGE}/sessions`, { method: 'POST', signal,
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${currentToken}` },
      body: JSON.stringify({ url: url.value, quality: props.quality }) });
    if (!response.ok) throw new Error('bridge');
    const created: { id: string } = await response.json();
    if (!/^[a-f0-9]{32}$/.test(created.id)) throw new Error('bridge');
    if (!alive || ticket !== generation) {
      void fetch(`${PREVIEW_BRIDGE}/sessions/${created.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${currentToken}` } }).catch(() => {}); return;
    }
    session = created.id; clearTimeout(timeout);
    await readFrame(ticket, signal, currentToken);
  } catch {
    if (alive && ticket === generation) { stop(); status.value = '暂时无法预览'; detail.value = '本机服务不可用、未授权，或流连接失败'; }
  } finally { clearTimeout(timeout); }
}
/** :param ticket: 订阅代次。:param signal: 本轮取消。:param currentToken: 本轮本机令牌。:returns: 帧更新后安排下一次读取，避免并发堆积。 */
async function readFrame(ticket: number, signal: AbortSignal, currentToken: string): Promise<void> {
  if (!alive || ticket !== generation) return;
  try {
    const response = await fetch(`${PREVIEW_BRIDGE}/sessions/${session}/frame`, { signal: AbortSignal.any([signal, AbortSignal.timeout(10000)]), headers: { Authorization: `Bearer ${currentToken}` }, cache: 'no-store' });
    if (!response.ok) throw new Error('frame');
    const image = await response.blob();
    if (!alive || ticket !== generation) return;
    const next = URL.createObjectURL(image), previous = frame.value; frame.value = next;
    if (previous) URL.revokeObjectURL(previous); status.value = '本机预览'; detail.value = '';
    timer = setTimeout(() => { void readFrame(ticket, signal, currentToken); }, props.quality === 'high' ? 250 : 500);
  } catch {
    if (alive && ticket === generation) { stop(); status.value = '预览已中断'; detail.value = '未收到有效画面，请检查本机连接与输入设备'; }
  }
}
/** :param event: 当前img的解码失败。:returns: 释放本轮订阅并就地说明，不留下空白或弹窗。 */
function frameFailed(event: Event): void {
  if (!alive || !(event.currentTarget instanceof Element) || event.currentTarget.getAttribute('src') !== frame.value) return;
  stop(); status.value = '无法显示预览画面'; detail.value = '本机服务返回的画面无法解码，请检查输入流';
}
watch(() => [url.value, props.quality, props.enabled], () => { void start(); });
onMounted(() => {
  document.addEventListener('visibilitychange', start); window.addEventListener('scp-client-preview-change', start);
  if (typeof IntersectionObserver !== 'undefined' && root.value) {
    observer = new IntersectionObserver(entries => { seen = !!entries[0]?.isIntersecting; void start(); }); observer.observe(root.value);
  } else void start();
});
onBeforeUnmount(() => { alive = false; stop(); observer?.disconnect(); document.removeEventListener('visibilitychange', start); window.removeEventListener('scp-client-preview-change', start); });
</script>
<template>
  <figure ref="root" class="client-preview" :aria-label="label">
    <div class="client-preview__surface">
      <img v-if="frame" :src="frame" :alt="label" @error="frameFailed" />
      <div v-else class="client-preview__empty"><FIcon name="desktop_24_regular" :size="24" /><span>{{ url ? status || '预览已暂停' : '暂无预览流' }}</span></div>
    </div>
    <figcaption v-if="detail" class="client-preview__detail">{{ detail }}</figcaption>
  </figure>
</template>
<style scoped>
.client-preview{margin:0;min-width:0;height:100%;display:flex;flex-direction:column;overflow:hidden;background:var(--console-subtle)}
.client-preview__surface{flex:1;min-height:0;display:grid;place-items:center;overflow:hidden;position:relative}
.client-preview__surface img{width:100%;height:100%;object-fit:contain;position:absolute;inset:0}
.client-preview__empty{display:flex;align-items:center;justify-content:center;gap:8px;padding:8px;color:var(--console-muted);font-size:12px;text-align:center}
.client-preview__detail{padding:3px 8px;font-size:11px;line-height:1.4;color:var(--console-muted);overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
</style>
