<!-- 文件描述：可见引用共享实时画布，细边框表达连接状态。项目：SCP-cv；文件：ClientStreamPreview.vue；作者：Qintsg；日期：2026-10-30。 -->
<script setup lang="ts">
import { computed, inject, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { VideoWallMappingItem } from '@/services/apiTypes';
import FIcon from '@/design-system/FIcon.vue';
import { useRequestContext } from '@/composables/useRequestContext';
import { useAuthStore } from '@/stores/auth';
import { previewTarget } from './previewTarget';
import { PREVIEW_PLAYER_FACTORY, createPreviewPlayer } from './previewPlayer';
import { getPreviewPool, PREVIEW_POOL, type PreviewState } from './previewPool';
import { createPreviewCanvas, type PreviewCanvas } from './previewCanvas';
import { capturePreviewScope } from './previewScope';
import { useMobileControls } from '@/composables/useMobileControls';
const mobileControls = useMobileControls();
const props = withDefaults(defineProps<{ input?: VideoWallMappingItem['input']; sourceId?: number;
  quality?: 'low' | 'high'; enabled?: boolean; explicit?: boolean; label?: string }>(), { quality: 'low', enabled: true, explicit: false, label: '画面预览' });
const auth = useAuthStore(), factory = inject(PREVIEW_PLAYER_FACTORY, createPreviewPlayer);
const pool = inject(PREVIEW_POOL, undefined) ?? getPreviewPool(factory);
const target = computed(() => previewTarget(props.input, props.sourceId, props.quality));
const root = ref<HTMLElement | null>(null), canvas = ref<HTMLCanvasElement | null>(null);
const state = ref<PreviewState>('idle'), hasFrame = ref(false);
const stateText = computed(() => ({ idle: '预览已暂停', connecting: '正在连接预览', live: '实时预览',
  reconnecting: hasFrame.value ? '正在重连，当前为最后画面' : '正在重新连接预览', unavailable: '无法连接预览', empty: '暂无预览流' })[state.value]);
let active = true, mounted = false, seen = true;
let observer: IntersectionObserver | undefined, surface: PreviewCanvas | undefined, release: (() => void) | undefined;
useRequestContext(() => { pool.invalidateObsolete(); stop(true); if (active && mounted) queueMicrotask(start); });

/** :param clear: 来源/认证改变时清空像素，不能保留其它来源或账号画面。:returns: 仅释放当前引用。 */
function stop(clear = false): void {
  release?.(); release = undefined; state.value = target.value ? 'idle' : 'empty';
  if (clear) { surface?.clear(); hasFrame.value = false; }
}
/** :returns: 可见且已认证时接收共享帧；尺寸/比例/标签和quality提示改变不重新订阅。 */
function start(): void {
  if (!mounted || !active) return;
  if (mobileControls.value && !props.explicit || !seen || !props.enabled || document.hidden || !target.value || !auth.user) { stop(); return; }
  if (release) return;
  release = pool.subscribe(target.value, {
    frame(video) { surface?.draw(video); hasFrame.value = true; },
    state(next) { state.value = next; }, clear() { surface?.clear(); hasFrame.value = false; },
  }, capturePreviewScope());
}
// 清晰度是首次观看意图，不让自定义几何跨阈值时切换chn0/1；真正换来源才重取。
watch(() => [props.sourceId, props.input?.kind, props.input?.ip_address, props.enabled, props.explicit, mobileControls.value], () => { stop(true); start(); });
/** :returns: 整页隐藏立即清需求，恢复后按可见引用重新接入。 */
function visibility(): void { if (document.hidden) { stop(); pool.dispose(); } else start(); }
onMounted(() => {
  mounted = true; if (canvas.value) surface = createPreviewCanvas(canvas.value);
  document.addEventListener('visibilitychange', visibility);
  if (typeof IntersectionObserver !== 'undefined' && root.value) {
    seen = false;
    observer = new IntersectionObserver(entries => { const next = !!entries[0]?.isIntersecting; if (seen === next) return;
      seen = next; if (seen) start(); else stop(); });
    observer.observe(root.value);
  } else start();
});
onBeforeUnmount(() => { active = false; stop(); observer?.disconnect(); surface?.dispose(); document.removeEventListener('visibilitychange', visibility); });
</script>
<template>
  <figure ref="root" class="client-preview" :class="`client-preview--${state}`" :data-preview-state="state" :aria-label="`${label}：${stateText}`" :title="stateText">
    <canvas ref="canvas" v-show="hasFrame" class="client-preview__canvas" />
    <FIcon v-if="!hasFrame" class="client-preview__empty" name="desktop_24_regular" :size="24" />
    <span class="client-preview__accessible" role="status">{{ stateText }}</span>
  </figure>
</template>
<style scoped>
.client-preview{margin:0;min-width:0;height:100%;box-sizing:border-box;display:grid;place-items:center;overflow:hidden;position:relative;background:var(--console-subtle);border:1px solid var(--console-border)}
.client-preview--live{border-color:var(--colorStatusSuccessForeground1)}
.client-preview--reconnecting{border-color:var(--colorPaletteMarigoldBorderActive)}
.client-preview--unavailable{border-color:var(--colorStatusDangerForeground1)}
.client-preview__canvas{width:100%;height:100%;position:absolute;inset:0;pointer-events:none}
.client-preview__empty{color:var(--console-muted);pointer-events:none}
.client-preview__accessible{position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip-path:inset(50%);white-space:nowrap;border:0}
</style>
