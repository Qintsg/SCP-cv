<script setup lang="ts">
/** 文件描述：媒体库文稿逐页预览及明确准备操作。项目：SCP-cv；文件：PresentationSourcePreview.vue；作者：Qintsg；日期：2026-10-30。 */
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import AButton from 'ant-design-vue/es/button';
import { api, buildBackendUrl, type MediaSourceItem, type PptResourceItem } from '@/services/api';
import { useRequestContext } from '@/composables/useRequestContext';
import { pptPreparationSummary } from './sourcePresentation';
const props = defineProps<{ source: MediaSourceItem }>();
const pages = ref<PptResourceItem[]>([]), page = ref(1), loading = ref(false), preparing = ref(false), error = ref('');
const imageFailed = ref(false);
const image = computed(() => { const selected = pages.value[page.value - 1]; return selected?.slide_image ? buildBackendUrl(selected.slide_image) : ''; });
let sequence = 0, timer: ReturnType<typeof setTimeout> | undefined, attempts = 0;
const capture = useRequestContext(() => { sequence++; clearTimeout(timer); });

/** :param current: 当前来源与身份。:param source: 当前准备状态。:returns: 仅排队或转换期间继续有限查询。 */
function schedule(current: () => boolean, source: MediaSourceItem): void {
  if (!current()) return;
  preparing.value = ['queued', 'running'].includes(pptPreparationSummary(source, false).state ?? '');
  if (preparing.value && attempts++ < 120) timer = setTimeout(() => { void read(current, true); }, 2000);
  else if (preparing.value) { preparing.value = false; error.value = '转换仍在进行，可稍后刷新预览。'; }
}
/** :param current: 请求归属。:param status: 是否读取转换状态。:returns: 读取实际已发布页图，不改变播出窗口。 */
async function read(current: () => boolean, status = false): Promise<void> {
  try {
    const payload = await api.listPptResources(props.source.id);
    if (!current()) return;
    pages.value = payload.resources.filter(resource => !!resource.slide_image);
    if (pages.value.length) { preparing.value = false; error.value = ''; return; }
    let source = props.source;
    if (status) {
      const catalog = await api.listSources('ppt');
      if (!current()) return;
      const item = catalog.sources.find(item => item.id === props.source.id);
      if (!item) throw new Error('媒体文件已移除。');
      source = item;
    }
    const state = pptPreparationSummary(source, false).state;
    if (state === 'failed' || state === 'uncertain') { preparing.value = false; error.value = '文稿预览准备失败，可以重新准备。'; }
    else schedule(current, source);
  } catch (failure) { if (current()) { preparing.value = false; error.value = failure instanceof Error ? failure.message : '预览读取失败'; } }
  finally { if (current()) loading.value = false; }
}
/** :returns: 明确重新读取当前文稿预览。 */
function refresh(): void {
  clearTimeout(timer); attempts = 0; const own = ++sequence, scope = capture();
  loading.value = true; error.value = ''; imageFailed.value = false;
  void read(() => own === sequence && scope(), true);
}
/** :returns: 用户明确请求转换当前文稿，等待已发布制品。 */
async function prepare(): Promise<void> {
  if (preparing.value || loading.value) return;
  clearTimeout(timer); attempts = 0; const own = ++sequence, scope = capture(), current = () => own === sequence && scope();
  preparing.value = true; error.value = '';
  try {
    const result = await api.preparePptImages(props.source.id);
    if (current()) { schedule(current, result.source); if (!preparing.value) await read(current, true); }
  } catch (failure) { if (current()) { preparing.value = false; error.value = failure instanceof Error ? failure.message : '文稿准备失败'; } }
}
watch(() => props.source.id, () => { pages.value = []; page.value = 1; refresh(); }, { immediate: true });
watch(page, () => { imageFailed.value = false; });
onBeforeUnmount(() => { sequence++; clearTimeout(timer); });
</script>
<template>
  <section class="presentation-preview" aria-label="文稿预览">
    <div class="presentation-preview__stage" :aria-busy="loading || preparing">
      <img v-if="image && !imageFailed" :src="image" :alt="`${source.name}，第${page}页`" @error="imageFailed = true" />
      <span v-else role="status">{{ loading ? '正在读取预览…' : preparing ? '正在准备预览…' : imageFailed ? '页图读取失败' : '预览尚未准备' }}</span>
    </div>
    <div class="presentation-preview__controls">
      <a-button :disabled="page <= 1" @click="page--">上一页</a-button><span>{{ pages.length ? `${page} / ${pages.length}` : '—' }}</span>
      <a-button :disabled="page >= pages.length" @click="page++">下一页</a-button>
      <a-button :loading="loading" :disabled="loading || preparing" @click="refresh">刷新预览</a-button>
      <a-button v-if="!pages.length" type="primary" :loading="preparing" :disabled="preparing || loading" @click="prepare">准备预览</a-button>
    </div>
    <p class="presentation-preview__error" role="status">{{ error }}</p>
  </section>
</template>
<style scoped>
.presentation-preview{display:grid;gap:12px}.presentation-preview__stage{aspect-ratio:16/9;min-height:160px;display:grid;place-items:center;background:var(--console-subtle);overflow:hidden;color:var(--console-muted)}
.presentation-preview__stage img{width:100%;height:100%;object-fit:contain;min-height:0}.presentation-preview__controls{display:flex;align-items:center;gap:8px;flex-wrap:wrap}.presentation-preview__controls span{font-variant-numeric:tabular-nums;min-width:60px;text-align:center}
.presentation-preview__error{min-height:22px;margin:0;color:var(--colorStatusDangerForeground1);font-size:12px;overflow-wrap:anywhere}
@media(max-width:767px){.presentation-preview__controls :deep(button){min-height:44px}}
</style>
