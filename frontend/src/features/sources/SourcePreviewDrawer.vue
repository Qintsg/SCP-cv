<script setup lang="ts">
/** 文件描述：全类型媒体详情预览，关闭即卸载媒体资源。项目：SCP-cv；文件：SourcePreviewDrawer.vue；作者：Qintsg；日期：2026-10-30。 */
import { computed, defineAsyncComponent, onBeforeUnmount, ref, watch } from 'vue';
import AButton from 'ant-design-vue/es/button';
import ControlDrawer from '@/design-system/ControlDrawer.vue';
import { buildBackendUrl, type MediaSourceItem } from '@/services/api';
import { useRequestContext } from '@/composables/useRequestContext';
import { resolveSourceCategory, sourceCategoryLabel } from './sourcePresentation';
import ClientStreamPreview from '@/features/preview/ClientStreamPreview.vue';
import PresentationSourcePreview from './PresentationSourcePreview.vue';
const PdfSourcePreview = defineAsyncComponent(() => import('./PdfSourcePreview.vue'));
const props = defineProps<{ source: MediaSourceItem | null }>();
const open = defineModel<boolean>('open', { required: true });
const error = ref(''), revision = ref(0);
const media = ref<HTMLMediaElement | null>(null);
/** :returns: 关闭、换源和刷新时停止原音视频，不依赖垃圾回收。 */
function releaseMedia(): void { if (media.value) { media.value.pause(); media.value.removeAttribute('src'); media.value.load(); } }
watch(revision, releaseMedia, { flush: 'sync' });
onBeforeUnmount(releaseMedia);
const category = computed(() => props.source ? resolveSourceCategory(props.source) : 'all');
const fileUrl = computed(() => props.source ? buildBackendUrl(`/api/sources/${props.source.id}/preview/`) : '');
const pdf = computed(() => props.source?.playback_mode === 'pdf');
const webUrl = computed(() => {
  if (category.value !== 'web' || !props.source) return '';
  const url = new URL(props.source.uri);
  return ['http:', 'https:'].includes(url.protocol) && !url.username && !url.password ? url.href : '';
});
useRequestContext(() => { open.value = false; });
watch(() => [props.source?.id, open.value], () => { error.value = ''; revision.value++; });
/** :returns: 媒体加载错误仅显示在当前预览反馈区域。 */
function failed(): void { error.value = '无法预览此媒体，请检查文件或浏览器支持的格式。'; }
</script>
<template>
  <ControlDrawer v-model:open="open" :title="source?.name || '媒体预览'" width="min(1000px, 100vw)" placement="right" :destroy-on-close="true" class="source-preview-drawer">
    <section v-if="open && source" class="source-preview" :data-source-id="source.id" :data-source-type="source.source_type">
      <header class="source-preview__heading"><span>{{ category === 'stream' ? '直播流' : sourceCategoryLabel(source) }}</span><a-button v-if="category !== 'ppt' && category !== 'stream'" @click="revision++; error = ''">刷新预览</a-button><a v-if="webUrl" :href="webUrl" target="_blank" rel="noopener noreferrer">在新标签打开</a></header>
      <PdfSourcePreview v-if="pdf" :key="source.id" :url="fileUrl" />
      <PresentationSourcePreview v-else-if="category === 'ppt'" :key="source.id" :source="source" />
      <ClientStreamPreview v-else-if="category === 'stream'" class="source-preview__stream" :source-id="source.id" quality="high" :explicit="true" :label="source.name" />
      <div v-else class="source-preview__stage" :class="{ 'source-preview__stage--audio': category === 'audio' }">
        <img v-if="category === 'image'" :key="revision" :src="fileUrl" :alt="source.name" @error="failed" />
        <video v-else-if="category === 'video'" ref="media" :key="revision" :src="fileUrl" controls playsinline preload="metadata" :aria-label="source.name" @error="failed" />
        <audio v-else-if="category === 'audio'" ref="media" :key="revision" :src="fileUrl" controls preload="metadata" :aria-label="source.name" @error="failed" />
        <iframe v-else-if="webUrl" :key="revision" :src="webUrl" :title="source.name" sandbox="allow-scripts allow-same-origin allow-forms allow-presentation" referrerpolicy="no-referrer" @error="error = '网页无法嵌入，请在新标签打开。'" />
        <span v-else>暂无可用预览</span>
      </div>
      <p v-if="category !== 'ppt'" class="source-preview__error" role="status">{{ error }}</p>
    </section>
  </ControlDrawer>
</template>
<style scoped>
.source-preview{display:grid;gap:16px;min-width:0}.source-preview__heading{display:flex;align-items:center;gap:12px;flex-wrap:wrap;color:var(--console-muted);font-size:13px}.source-preview__heading>span{margin-right:auto}
.source-preview__stage{aspect-ratio:16/9;display:grid;place-items:center;overflow:hidden;background:var(--console-subtle);min-width:0}.source-preview__stage img,.source-preview__stage video,.source-preview__stage iframe{width:100%;height:100%;object-fit:contain;min-height:0;border:0}.source-preview__stage--audio{aspect-ratio:auto;min-height:200px}.source-preview__stage audio{max-width:100%;width:420px}.source-preview__stream{aspect-ratio:16/9!important;height:auto!important;min-height:180px}.source-preview__error{min-height:24px;margin:0;font-size:12px;color:var(--colorStatusDangerForeground1);overflow-wrap:anywhere}
@media(max-width:767px){.source-preview__heading :deep(button),.source-preview__heading a{min-height:44px;display:inline-flex;align-items:center}.source-preview__stage{min-height:160px}}
</style>
