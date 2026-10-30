<script setup lang="ts">
/** 文件描述：使用PDF.js呈现真实PDF并支持触摸端翻页。项目：SCP-cv；文件：PdfSourcePreview.vue；作者：Qintsg；日期：2026-10-30。 */
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import AButton from 'ant-design-vue/es/button';
import { getDocument, GlobalWorkerOptions, type PDFDocumentLoadingTask, type PDFDocumentProxy, type RenderTask } from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
GlobalWorkerOptions.workerSrc = workerUrl;
const props = defineProps<{ url: string }>();
const canvas = ref<HTMLCanvasElement | null>(null), stage = ref<HTMLElement | null>(null), page = ref(1), total = ref(0), error = ref(''), loading = ref(true);
let document: PDFDocumentProxy | undefined, task: PDFDocumentLoadingTask | undefined, render: RenderTask | undefined;
let sequence = 0, drawing = 0, alive = true, observer: ResizeObserver | undefined;
let renderTail: Promise<void> = Promise.resolve();
/** :returns: 串行替换同一画布绘制，关闭或翻页取消旧绘制。 */
function draw(): void {
  render?.cancel(); const own = ++drawing;
  renderTail = renderTail.then(async () => {
    if (!alive || own !== drawing || !document || !canvas.value || !stage.value) return;
    try {
      const current = await document.getPage(page.value);
      if (!alive || own !== drawing) return;
      const base = current.getViewport({ scale: 1 }), width = Math.max(1, stage.value.clientWidth);
      const scale = Math.min(2, devicePixelRatio || 1) * Math.min(width / base.width, 1200 / base.width);
      const viewport = current.getViewport({ scale });
      canvas.value.width = Math.ceil(viewport.width); canvas.value.height = Math.ceil(viewport.height);
      render = current.render({ canvas: canvas.value, viewport });
      await render.promise;
      if (alive && own === drawing) { error.value = ''; loading.value = false; }
    } catch (failure) {
      if (alive && own === drawing && !(failure instanceof Error && failure.name === 'RenderingCancelledException')) {
        loading.value = false; error.value = 'PDF预览读取失败';
      }
    }
  });
}
/** :returns: 只读取当前来源PDF；释放旧文档、工作线程及绘制资源。 */
async function load(): Promise<void> {
  const own = ++sequence; drawing++; document = undefined; render?.cancel(); await task?.destroy();
  if (!alive || own !== sequence) return;
  total.value = 0; page.value = 1; loading.value = true; error.value = '';
  task = getDocument({ url: props.url, withCredentials: true, useSystemFonts: true });
  try { const loaded = await task.promise; if (alive && own === sequence) { document = loaded; total.value = loaded.numPages; draw(); } }
  catch { if (alive && own === sequence) { loading.value = false; error.value = 'PDF预览读取失败'; } }
}
watch(() => props.url, () => { void load(); }); watch(page, draw);
onMounted(() => { void load(); observer = new ResizeObserver(draw); if (stage.value) observer.observe(stage.value); });
onBeforeUnmount(() => { alive = false; sequence++; drawing++; observer?.disconnect(); render?.cancel(); void task?.destroy(); });
</script>
<template>
  <section class="pdf-preview" aria-label="PDF预览">
    <div ref="stage" class="pdf-preview__stage" :aria-busy="loading"><canvas ref="canvas" v-show="total > 0" :aria-label="`第${page}页`" /><span v-if="loading">正在读取预览…</span></div>
    <div class="pdf-preview__controls"><a-button :disabled="page <= 1" @click="page--">上一页</a-button><span>{{ total ? `${page} / ${total}` : '—' }}</span><a-button :disabled="page >= total" @click="page++">下一页</a-button></div>
    <p class="pdf-preview__error" role="status">{{ error }}</p>
  </section>
</template>
<style scoped>
.pdf-preview{display:grid;gap:12px}.pdf-preview__stage{min-height:200px;max-height:65dvh;overflow:auto;display:grid;justify-items:center;align-items:start;background:var(--console-subtle)}.pdf-preview__stage canvas{max-width:100%;height:auto}.pdf-preview__controls{display:flex;align-items:center;justify-content:center;gap:12px}.pdf-preview__error{margin:0;min-height:22px;font-size:12px;color:var(--colorStatusDangerForeground1)}
@media(max-width:767px){.pdf-preview__controls :deep(button){min-height:44px}}
</style>
