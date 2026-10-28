<script setup lang="ts">
/**
 * 文件描述：当前页、下一页与备注组成的演示控制工作区。
 * 项目：SCP-cv；文件：PptFocusView.vue；作者：Qintsg；日期：2026-10-27。
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AButton from 'ant-design-vue/es/button';
import FIcon from '@/design-system/FIcon.vue';
import { useControlLayout } from '@/composables/useControlLayout';
import { useTheme } from '@/composables/useTheme';
import { useToast } from '@/composables/useToast';
import { useSessionStore } from '@/stores/sessions';
import { useSourceStore } from '@/stores/sources';
import { buildBackendUrl } from '@/services/api';
import { supportsRequestedPlaybackControls } from '../display/playbackCapabilities';
import { windowIdToSlug } from '../display/displayTargets';
import { usePresentationPreview } from '../display/usePresentationPreview';
import { usePresentationCommandFeedback } from '../display/usePresentationCommandFeedback';
import { presentationStep } from '../display/presentationStep';
import PresentationNavigation from '../display/PresentationNavigation.vue';
import PptSlideRail from './PptSlideRail.vue';
import { sanitizeSpeakerNotes } from './speakerNotes';

const route = useRoute(), router = useRouter(), sessions = useSessionStore(), sources = useSourceStore(), toast = useToast();
const { mobileControls, tabletControls, touchNavigation } = useControlLayout();
const { pushOverride, popOverride } = useTheme();
const windowId = computed(() => Number(route.params.windowId));
const session = computed(() => sessions.byWindowId(windowId.value));
const disabled = computed(() => !session.value?.player_online || !supportsRequestedPlaybackControls(session.value));
const { context, resources, error, loading, unavailable, pending, refresh } = usePresentationPreview(() => session.value, true);
const feedback = usePresentationCommandFeedback(() => session.value, () => sessions.commandFeedbackRevisions[windowId.value] ?? 0);
const fullscreen = ref(false);
const current = computed(() => session.value?.current_slide ?? 0);
const total = computed(() => session.value?.total_slides ?? 0);
const title = computed(() => sources.findById(context.value?.sourceId ?? null)?.name || session.value?.source_name || '演示文稿');
const currentResource = computed(() => resources.value.find(item => item.page_index === current.value));
const image = computed(() => currentResource.value?.slide_image ? buildBackendUrl(currentResource.value.slide_image) : '');
const nextImage = computed(() => {
  const path = resources.value.find(item => item.page_index === current.value + 1)?.slide_image;
  return path ? buildBackendUrl(path) : '';
});
const thumbnails = computed(() => resources.value.map(item => ({ pageIndex: item.page_index,
  imageUrl: item.slide_image ? buildBackendUrl(item.slide_image) : '', hasMedia: Boolean(item.has_media) })));
const notes = computed(() => sanitizeSpeakerNotes(currentResource.value?.speaker_notes ?? '', current.value, total.value));
const status = computed(() => !session.value?.player_online ? '播放器离线' : error.value || (!mobileControls.value && unavailable.value ? '预览暂不可用' : ''));

/** :param direction: 按钮和键盘的步进方向。:returns: 对应动画步或静态翻页。 */
async function step(direction: 'prev' | 'next'): Promise<void> {
  if (disabled.value || !session.value) return;
  const selected = presentationStep(session.value.playback_mode, direction);
  try { await feedback.submit(() => selected.kind === 'effect' ? sessions.control(windowId.value, selected.action)
    : sessions.navigate(windowId.value, selected.action), selected.command); }
  catch (failure) { toast.error('步进失败', failure instanceof Error ? failure.message : '请检查连接'); }
}
/** :param page: 缩略图选中的页码。:returns: 直接定位该页。 */
async function jump(page: number): Promise<void> {
  if (disabled.value || page === current.value) return;
  try { await sessions.navigate(windowId.value, 'goto', page); }
  catch (failure) { toast.error('跳页失败', failure instanceof Error ? failure.message : '请检查连接'); }
}
/** :param event: 原生键盘事件。:returns: 文稿区域的方向键与按钮使用相同动作。 */
function keydown(event: KeyboardEvent): void {
  const direction = ({ ArrowLeft: 'prev', PageUp: 'prev', ArrowRight: 'next', PageDown: 'next' } as Record<string, 'prev' | 'next'>)[event.key];
  if (!direction || event.defaultPrevented || event.repeat || event.altKey || event.ctrlKey || event.metaKey || session.value?.source_type !== 'ppt') return;
  if (event.target instanceof Element && event.target.closest('input,textarea,select,[contenteditable="true"],[role="textbox"],[role="combobox"],[role="listbox"],[role="option"],.ant-select-dropdown')) return;
  event.preventDefault(); void step(direction);
}
/** :returns: 记录浏览器的真实全屏状态。 */
function syncFullscreen(): void { fullscreen.value = Boolean(document.fullscreenElement); }
/** :returns: 切换当前控制台的全屏显示。 */
async function toggleFullscreen(): Promise<void> {
  try { if (document.fullscreenElement) await document.exitFullscreen(); else await document.documentElement.requestFullscreen(); }
  catch { syncFullscreen(); }
}
onMounted(() => {
  pushOverride('dark');
  document.addEventListener('keydown', keydown); document.addEventListener('fullscreenchange', syncFullscreen);
  void sessions.refresh().catch(failure => toast.error('无法读取文稿状态', failure instanceof Error ? failure.message : '请检查连接'));
});
onBeforeUnmount(() => { popOverride(); document.removeEventListener('keydown', keydown); document.removeEventListener('fullscreenchange', syncFullscreen); });
</script>
<template>
  <div class="ppt-focus" :class="{ 'ppt-focus--mobile': mobileControls, 'ppt-focus--tablet': tabletControls, 'ppt-focus--touch': touchNavigation }">
    <header class="ppt-focus__topbar">
      <a-button @click="router.push(`/display/${windowIdToSlug(windowId)}`)"><template #icon><FIcon name="arrow_left_24_regular" /></template>返回</a-button>
      <div class="ppt-focus__identity"><span>窗口 {{ windowId }}</span><h1>{{ title }}</h1></div>
      <div class="ppt-focus__top-actions"><span class="ppt-focus__page">{{ current || '—' }} / {{ total || '—' }}</span>
        <a-button v-if="!mobileControls" :loading="pending" :disabled="pending || !context" aria-label="刷新预览" @click="refresh"><template #icon><FIcon name="arrow_clockwise_24_regular" /></template></a-button>
        <a-button v-if="!mobileControls" :aria-label="fullscreen ? '退出全屏' : '全屏'" @click="toggleFullscreen"><template #icon><FIcon name="arrow_maximize_24_regular" /></template></a-button>
      </div>
    </header>
    <main class="ppt-focus__stage">
      <div class="ppt-focus__status" role="status"><span :class="{ 'ppt-focus__status--empty': !status }">{{ status || '状态正常' }}</span></div>
      <div class="ppt-focus__layout" :class="{ 'ppt-focus__layout--remote': mobileControls }">
        <PptSlideRail v-if="!mobileControls" class="ppt-focus__slide-rail" :items="thumbnails" :current-page="current" :total-pages="total" @jump="jump" />
        <figure v-if="!mobileControls" class="ppt-focus__current">
          <figcaption>当前页 <span>{{ current || '—' }}</span></figcaption>
          <img v-if="image" :src="image" :alt="`第${current}页`" />
          <div v-else class="ppt-focus__placeholder"><FIcon name="document_24_regular" :size="32" /><span>{{ loading ? '正在读取' : '暂无预览' }}</span></div>
        </figure>
        <aside class="ppt-focus__side">
          <figure v-if="!mobileControls" class="ppt-focus__next"><figcaption>下一页</figcaption><img v-if="nextImage" :src="nextImage" alt="下一页预览" /><div v-else class="ppt-focus__placeholder">{{ total && current >= total ? '最后一页' : '—' }}</div></figure>
          <section class="ppt-focus__notes"><header>演讲备注<span>第 {{ current || '—' }} 页</span></header><p>{{ notes || '暂无备注' }}</p></section>
        </aside>
      </div>
    </main>
    <footer class="ppt-focus__controls"><PresentationNavigation :window-id="windowId" :mode="session?.playback_mode" :disabled="disabled" /></footer>
  </div>
</template>
<style scoped src="./PptFocusView.css"></style>
