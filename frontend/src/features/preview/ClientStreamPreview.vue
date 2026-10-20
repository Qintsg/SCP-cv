<!-- 服务端共享FLV预览：按可见性订阅，原始凭据只留Host，失败内联且保留最后画面。 -->
<script setup lang="ts">
import { computed, inject, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { VideoWallMappingItem } from '@/services/apiTypes';
import { ApiRequestError } from '@/services/api';
import FIcon from '@/design-system/FIcon.vue';
import { useRequestContext } from '@/composables/useRequestContext';
import { useAuthStore } from '@/stores/auth';
import { previewTarget } from './previewTarget';
import { openPreview, heartbeatPreview, closePreview, type PreviewLease } from './previewClient';
import { PREVIEW_PLAYER_FACTORY, createPreviewPlayer, PreviewUnsupportedError, type PreviewPlayer } from './previewPlayer';
const props = withDefaults(defineProps<{ input?: VideoWallMappingItem['input']; sourceId?: number;
  quality?: 'low' | 'high'; enabled?: boolean; label?: string }>(), { quality: 'low', enabled: true, label: '画面预览' });
const auth = useAuthStore(), factory = inject(PREVIEW_PLAYER_FACTORY, createPreviewPlayer);
const target = computed(() => previewTarget(props.input, props.sourceId, props.quality));
const root = ref<HTMLElement | null>(null), video = ref<HTMLVideoElement | null>(null), canvas = ref<HTMLCanvasElement | null>(null);
const status = ref(''), detail = ref(''), hasFrame = ref(false), hasStill = ref(false);
let active = true, mounted = false, seen = true, generation = 0, attempts = 0, lastFrame = 0;
let observer: IntersectionObserver | undefined, player: PreviewPlayer | undefined, lease: PreviewLease | undefined;
let controller: AbortController | undefined, scope: (() => boolean) | undefined;
let retry: ReturnType<typeof setTimeout> | undefined, heartbeat: ReturnType<typeof setTimeout> | undefined, monitor: ReturnType<typeof setInterval> | undefined;
const capture = useRequestContext(() => { stop(false); if (active && mounted) queueMicrotask(() => { void start(); }); });
/** :returns: 仅在原来源/账号不变且重连时保留最近画面，绝不把它标成实时。 */
function freeze(): void {
  if (!hasFrame.value || !canvas.value || !video.value || !video.value.videoWidth) return;
  try {
    const width = Math.min(1280, video.value.videoWidth); canvas.value.width = width;
    canvas.value.height = Math.max(1, Math.round(width * video.value.videoHeight / video.value.videoWidth));
    const context = canvas.value.getContext('2d'); if (!context) return;
    context.drawImage(video.value, 0, 0, canvas.value.width, canvas.value.height); hasStill.value = true;
  } catch { hasStill.value = false; }
}
/** :param keepFrame: 同来源重连可保留最后帧。:returns: 取消本轮、销毁播放器、仅释放原归属租约。 */
function stop(keepFrame: boolean): void {
  generation++; clearTimeout(retry); clearTimeout(heartbeat); clearInterval(monitor); controller?.abort();
  controller = undefined;
  if (keepFrame) freeze(); else hasStill.value = false;
  try { player?.destroy(); } catch { /* 播放器失败仍须释放租约，服务器到期回收兜底。 */ }
  player = undefined; hasFrame.value = false;
  const old = lease, owner = scope; lease = undefined;
  if (old && owner?.()) void closePreview(old, owner).catch(() => {});
}
/** :param ticket: 原连接代次。:param failure: 固定分层异常。:returns: 失败只内联，正常无流不报错，不启动弹窗。 */
function failed(ticket: number, failure?: unknown): void {
  if (!active || ticket !== generation) return;
  stop(true);
  if (failure instanceof ApiRequestError && failure.code === 'preview_no_stream') { status.value = '暂无预览流'; detail.value = ''; return; }
  status.value = hasStill.value ? '画面已暂停' : '暂时无法预览';
  if (failure instanceof PreviewUnsupportedError) { detail.value = '请使用支持MediaSource的新版浏览器'; return; }
  detail.value = hasStill.value ? '正在重连，当前为最后画面' : '服务器或输入流暂不可用，正在重试';
  const delay = Math.min(10000, 2000 * ++attempts);
  retry = setTimeout(() => { void start(true); }, delay);
}
/** :returns: 当前可见且已认证才订阅；迟到结果不更改新画面/新主机。 */
async function start(keepFrame = false): Promise<void> {
  stop(keepFrame); status.value = hasStill.value ? '画面已暂停' : ''; detail.value = hasStill.value ? '正在重连，当前为最后画面' : '';
  if (!mounted || !active || !seen || !props.enabled || document.hidden || !target.value || !auth.user) return;
  const ticket = generation, current = capture(), abort = new AbortController(); scope = current; controller = abort;
  status.value = '正在连接预览';
  try {
    const created = await openPreview(target.value, current, abort.signal);
    if (!active || ticket !== generation || !current()) { if (current()) void closePreview(created, current).catch(() => {}); return; }
    lease = created;
    /** :returns: 续租失败停止本轮；不会用新账号给旧会话续租。 */
    const renew = async (): Promise<void> => {
      if (ticket !== generation || !current()) return;
      try { await heartbeatPreview(created, current, abort.signal); if (ticket === generation) heartbeat = setTimeout(() => { void renew(); }, 10000); }
      catch (error) { failed(ticket, error); }
    };
    heartbeat = setTimeout(() => { void renew(); }, 10000); lastFrame = Date.now();
    const createdPlayer = await factory(video.value!, created.httpUrl, () => {
      if (ticket !== generation || !current()) return;
      hasFrame.value = true; hasStill.value = false; status.value = '实时预览'; detail.value = ''; lastFrame = Date.now(); attempts = 0;
    }, () => failed(ticket));
    if (!active || ticket !== generation || !current()) { createdPlayer.destroy(); return; }
    player = createdPlayer;
    monitor = setInterval(() => { if (Date.now() - lastFrame > 20000) failed(ticket); }, 1000);
  } catch (error) { if (current()) failed(ticket, error); }
}
watch(() => [JSON.stringify(target.value), props.enabled], () => { if (mounted) void start(); });
/** :returns: 隐藏时停止，不持续拉所有流。 */
function visibility(): void { if (document.hidden) { stop(false); status.value = '预览已暂停'; detail.value = ''; } else void start(); }
onMounted(() => {
  mounted = true; document.addEventListener('visibilitychange', visibility);
  if (typeof IntersectionObserver !== 'undefined' && root.value) {
    seen = false;
    observer = new IntersectionObserver(entries => { const next = !!entries[0]?.isIntersecting; if (seen === next) return; seen = next; if (seen) void start(); else stop(false); });
    observer.observe(root.value);
  } else void start();
});
onBeforeUnmount(() => { active = false; stop(false); observer?.disconnect(); document.removeEventListener('visibilitychange', visibility); });
</script>
<template>
  <figure ref="root" class="client-preview" :aria-label="label">
    <div class="client-preview__surface">
      <video ref="video" muted autoplay playsinline disablepictureinpicture :class="{ 'client-preview__video--hidden': !hasFrame }" />
      <canvas ref="canvas" v-show="hasStill" class="client-preview__still" />
      <div v-if="!hasFrame && !hasStill" class="client-preview__empty"><FIcon name="desktop_24_regular" :size="24" /><span>{{ target ? status || '预览已暂停' : '暂无预览流' }}</span></div>
      <span v-if="hasStill" class="client-preview__paused">最后画面 · 非实时</span>
    </div>
    <figcaption v-if="detail" class="client-preview__detail">{{ detail }}</figcaption>
  </figure>
</template>
<style scoped>
.client-preview{margin:0;min-width:0;height:100%;display:flex;flex-direction:column;overflow:hidden;background:var(--console-subtle)}
.client-preview__surface{flex:1;min-height:0;display:grid;place-items:center;overflow:hidden;position:relative}
.client-preview__surface video,.client-preview__still{width:100%;height:100%;object-fit:contain;position:absolute;inset:0;pointer-events:none}
.client-preview__video--hidden{visibility:hidden}
.client-preview__empty{display:flex;align-items:center;justify-content:center;gap:8px;padding:8px;color:var(--console-muted);font-size:12px;text-align:center}
.client-preview__detail{padding:3px 8px;font-size:11px;line-height:1.4;color:var(--console-muted);overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.client-preview__paused{position:absolute;top:5px;right:5px;background:var(--console-surface);color:var(--console-muted);font-size:10px;padding:2px 4px}
</style>
