<script setup lang="ts">
/** 文稿专注控制页；动态动画步与静态翻页分开，不暴露未支持的媒体控制。 */
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import { tagColor } from '@/design-system/tagColor';

import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import AEmpty from 'ant-design-vue/es/empty';
import AProgress from 'ant-design-vue/es/progress';
import ASelect from 'ant-design-vue/es/select';
import ASpin from 'ant-design-vue/es/spin';
import ATag from 'ant-design-vue/es/tag';

import FIcon from '@/design-system/FIcon.vue';
import { useBreakpoint } from '@/composables/useBreakpoint';
import { useTheme, type ThemeMode } from '@/composables/useTheme';
import { useToast } from '@/composables/useToast';
import { buildBackendUrl, type PptMediaItem } from '@/services/api';
import { useRuntimeStore } from '@/stores/runtime';
import { useSessionStore } from '@/stores/sessions';
import { useSourceStore } from '@/stores/sources';
import { windowIdToSlug } from '@/features/display/displayTargets';

import PptSlideRail from './PptSlideRail.vue';
import PresentationEffectControls from '../display/PresentationEffectControls.vue';
import { sanitizeSpeakerNotes } from './speakerNotes';
import { usePresentationPreview } from '../display/usePresentationPreview';
import { usePresentationCommandFeedback } from '../display/usePresentationCommandFeedback';
import { supportsRequestedPlaybackControls } from '../display/playbackCapabilities';

type KnownMediaType = 'audio' | 'video' | 'image';
const KNOWN_MEDIA_TYPES = new Set<KnownMediaType>(['audio', 'video', 'image']);
const PRESENTATION_KEY_ACTIONS: Record<string, 'prev' | 'next'> = {
  ArrowLeft: 'prev',
  ArrowRight: 'next',
  PageUp: 'prev',
  PageDown: 'next',
};
const KEYBOARD_NAVIGATION_SKIP_SELECTOR = [
  'input',
  'textarea',
  'select',
  '[contenteditable="true"]',
  '[role="textbox"]',
  '[role="combobox"]',
  '[role="listbox"]',
  '[role="option"]',
  '.ant-select-selector',
  '.ant-select-dropdown',
].join(',');

interface PptSlideRailItem {
  pageIndex: number;
  imageUrl: string;
  hasMedia: boolean;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const { isLandscape } = useBreakpoint();
const runtimeStore = useRuntimeStore();
const sessionStore = useSessionStore();
const sourceStore = useSourceStore();
const toast = useToast();
const { pushOverride, popOverride } = useTheme();

const isFullscreen = ref(false);
const selectedMediaKey = ref<string | null>(null);
const _restoreTheme = ref<ThemeMode | null>(null);

const windowId = computed(() => Number.parseInt(String(route.params.windowId ?? '0'), 10));
const session = computed(() => sessionStore.byWindowId(windowId.value));
const controlsDisabled = computed(() => !session.value?.player_online || !supportsRequestedPlaybackControls(session.value));
const feedback = usePresentationCommandFeedback(() => session.value,
  () => sessionStore.commandFeedbackRevisions[windowId.value] ?? 0);
const { context: previewContext, resources, error: loadError, loading: isLoading,
  unavailable: previewUnavailable, pending: previewPending,
  refresh: refreshPreview } = usePresentationPreview(() => session.value, true);
const actualPresentationTitle = computed(() => previewContext.value
  ? sourceStore.findById(previewContext.value.sourceId)?.name || `实际文稿源${previewContext.value.sourceId}`
  : '实际文稿预览未确认');
const isStaticSlideMode = computed(() => session.value?.playback_mode === 'pdf' || session.value?.playback_mode === 'slide_images');
const isLibreOffice = computed(() => session.value?.playback_mode === 'libreoffice');
const orientationKey = computed<'landscape' | 'portrait'>(() => (isLandscape.value ? 'landscape' : 'portrait'));

const slidesProgress = computed(() => ({
  total: session.value?.total_slides ?? 0,
  current: session.value?.current_slide ?? 0,
}));

const currentResource = computed(() =>
  resources.value.find((resource) => resource.page_index === (session.value?.current_slide ?? 1)),
);

const thumbnailItems = computed<PptSlideRailItem[]>(() =>
  [...resources.value]
    .sort((left, right) => left.page_index - right.page_index)
    .map((resource) => ({
      pageIndex: resource.page_index,
      imageUrl: resource.slide_image ? buildBackendUrl(resource.slide_image) : '',
      hasMedia: resource.has_media,
    })),
);

const slideImage = computed(() => {
  const path = currentResource.value?.slide_image;
  return path ? buildBackendUrl(path) : '';
});

const nextSlideImage = computed(() => {
  const path = resources.value.find(
    (resource) => resource.page_index === (session.value?.current_slide ?? 1) + 1,
  )?.slide_image;
  return path ? buildBackendUrl(path) : '';
});

const nextSlideNumber = computed(() => {
  if (slidesProgress.value.total <= 0 || slidesProgress.value.current >= slidesProgress.value.total) return null;
  return slidesProgress.value.current + 1;
});

const currentMediaItems = computed(() => currentResource.value?.media_items ?? []);

const currentMediaOptions = computed(() =>
  currentMediaItems.value.map((media) => ({
    value: mediaSelectionValue(media),
    label: media.name || t('pptFocus.mediaFallback', { n: media.media_index }),
  })),
);

function mediaTypeHint(rawType: string): string {
  const key = rawType?.toLowerCase() as KnownMediaType;
  if (KNOWN_MEDIA_TYPES.has(key)) return t(`pptFocus.mediaType.${key}`);
  return t('pptFocus.mediaType.other');
}
void mediaTypeHint;

const selectedMedia = computed(() =>
  currentMediaItems.value.find((media) => mediaSelectionValue(media) === selectedMediaKey.value) ?? null,
);

const canControlSelectedMedia = computed(() => !controlsDisabled.value && !!selectedMedia.value && !isLibreOffice.value);
const isMediaPickerDisabled = computed(() => currentMediaOptions.value.length === 0);
const mediaSelectPlaceholder = computed(() => {
  if (!currentMediaOptions.value.length) return t('pptFocus.noMedia');
  return currentMediaOptions.value.length === 1 ? t('pptFocus.autoSelectMedia') : t('pptFocus.selectMedia');
});

const teleprompterText = computed(() => sanitizeSpeakerNotes(
  currentResource.value?.speaker_notes ?? '',
  slidesProgress.value.current,
  slidesProgress.value.total,
));

const windowLabel = computed(() => {
  switch (windowId.value) {
    case 1:
      return runtimeStore.runtime?.big_screen_mode === 'double' ? t('pptFocus.winBigLeft') : t('pptFocus.winBig');
    case 2:
      return t('pptFocus.winBigRight');
    default:
      return t('pptFocus.winFallback', { id: windowId.value });
  }
});

const slidesProgressPercentage = computed(() => {
  if (slidesProgress.value.total <= 0) return 0;
  return Math.round((slidesProgress.value.current / slidesProgress.value.total) * 100);
});

watch(currentMediaItems, (items) => {
  if (!items.length) {
    selectedMediaKey.value = null;
    return;
  }
  if (items.length === 1) {
    selectedMediaKey.value = mediaSelectionValue(items[0]);
    return;
  }
  if (!items.some((media) => mediaSelectionValue(media) === selectedMediaKey.value)) {
    selectedMediaKey.value = null;
  }
}, { immediate: true });

function syncFullscreen(): void {
  isFullscreen.value = !!document.fullscreenElement;
}

function shouldSkipKeyboardNavigation(target: EventTarget | null): boolean {
  if (!(target instanceof Element)) return false;
  if (target instanceof HTMLElement && target.isContentEditable) return true;
  return !!target.closest(KEYBOARD_NAVIGATION_SKIP_SELECTOR);
}

function handlePresentationKeydown(event: KeyboardEvent): void {
  const action = PRESENTATION_KEY_ACTIONS[event.key];
  if (!action || event.defaultPrevented || event.repeat || event.altKey || event.ctrlKey || event.metaKey) return;
  if (session.value?.source_type !== 'ppt' || controlsDisabled.value) return;
  if (shouldSkipKeyboardNavigation(event.target)) return;
  event.preventDefault();
  void nav(action);
}

onMounted(() => {
  pushOverride('dark');
  document.addEventListener('fullscreenchange', syncFullscreen);
  document.addEventListener('keydown', handlePresentationKeydown);
  void sessionStore.refresh();
  if (!runtimeStore.runtime) {
    void runtimeStore.refresh().catch(() => undefined);
  }
});

onBeforeUnmount(() => {
  popOverride();
  document.removeEventListener('fullscreenchange', syncFullscreen);
  document.removeEventListener('keydown', handlePresentationKeydown);
});

function mediaSelectionValue(media: PptMediaItem): string {
  return `${media.id}:${media.media_index}`;
}

async function nav(action: 'prev' | 'next'): Promise<void> {
  if (!session.value || controlsDisabled.value) return;
  try {
    await feedback.submit(() => sessionStore.navigate(session.value!.window_id, action), action === 'next' ? 'NEXT' : 'PREV');
  } catch (error) {
    toast.error(t('pptFocus.navFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

async function jumpToSlide(pageIndex: number): Promise<void> {
  if (!session.value || controlsDisabled.value || pageIndex === session.value.current_slide) return;
  try {
    await sessionStore.navigate(session.value.window_id, 'goto', pageIndex);
  } catch (error) {
    toast.error(t('pptFocus.jumpFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

async function controlSelectedMedia(action: 'play' | 'pause' | 'stop'): Promise<void> {
  if (controlsDisabled.value) return;
  if (!session.value || !selectedMedia.value) {
    if (currentMediaItems.value.length > 1) {
      toast.info(t('pptFocus.pickMediaFirst'));
    }
    return;
  }
  try {
    await sessionStore.controlPptMedia(
      session.value.window_id,
      action,
      selectedMedia.value.id,
      selectedMedia.value.media_index,
    );
  } catch (error) {
    toast.error(t('pptFocus.mediaFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

async function toggleFullscreen(): Promise<void> {
  try {
    if (document.fullscreenElement) {
      await document.exitFullscreen();
    } else if (document.documentElement.requestFullscreen) {
      await document.documentElement.requestFullscreen();
    }
  } catch {
    syncFullscreen();
  }
}

function exitFocus(): void {
  void router.push(`/display/${windowIdToSlug(windowId.value)}`);
}
</script>

<template>
  <div class="ppt-focus">
    <header class="ppt-focus__topbar">
      <a-button  @click="exitFocus">
        <template #icon><FIcon name="arrow_left_24_regular" /></template>
        {{ t('pptFocus.back') }}
      </a-button>
      <div class="ppt-focus__topbar-center">
        <a-tag color="processing"  >{{ windowLabel }}</a-tag>
        <h1 class="ppt-focus__topbar-title">{{ actualPresentationTitle }}</h1>
        <a-tag :color="tagColor(session?.playback_state === 'playing' ? 'success' : 'default')"  >
          {{ session?.playback_state_label || session?.playback_state || t('pptFocus.unknown') }}
        </a-tag>
        <span v-if="slidesProgress.total > 0" class="ppt-focus__topbar-progress">
          {{ slidesProgress.current }} / {{ slidesProgress.total }}
        </span>
      </div>
      <a-button  @click="toggleFullscreen">
        <template #icon>
          <FIcon :name="isFullscreen ? 'full_screen_minimize_24_regular' : 'full_screen_maximize_24_regular'" />
        </template>
        {{ isFullscreen ? t('pptFocus.exitFullscreen') : t('pptFocus.fullscreen') }}
      </a-button>
    </header>

    <main class="ppt-focus__stage" :data-orientation="orientationKey">
      <a-alert v-if="session?.actual_source_id && !supportsRequestedPlaybackControls(session)" type="warning"><template #description>
        {{ t('playback.controlsTypeMismatch') }}
      </template></a-alert>
      <a-button v-if="previewContext" :loading="previewPending" :disabled="previewPending"
        @click="refreshPreview">刷新预览</a-button>
      <a-alert v-if="previewUnavailable" type="info"><template #description>
        所选布局的预览暂不可用。
      </template></a-alert>
      <a-alert v-if="loadError" type="error" :message="t('pptFocus.loadFailTitle')"><template #description>
        {{ loadError }}
      </template></a-alert>

      <div v-if="isLoading" class="ppt-focus__loading">
        <a-spin size="large" />
        <span>{{ t('pptFocus.loadingPages') }}</span>
      </div>

      <template v-else-if="!previewContext">
        <a-empty description="实际文稿源或文字布局尚未确认，不能显示默认预览。">
          <template #image>
            <FIcon name="document_24_regular" />
          </template>
          <template #default>
            <a-button type="primary" @click="exitFocus">{{ t('pptFocus.back') }}</a-button>
          </template>
        </a-empty>
      </template>

      <template v-else>
        <div class="ppt-focus__layout">
          <PptSlideRail
            v-if="isLandscape"
            class="ppt-focus__slide-rail"
            :items="thumbnailItems"
            :current-page="slidesProgress.current"
            :total-pages="slidesProgress.total"
            @jump="jumpToSlide"
          />

          <figure class="ppt-focus__current">
            <img v-if="slideImage" :src="slideImage" :alt="t('pptFocus.pageAlt', { n: slidesProgress.current })" />
            <div v-else class="ppt-focus__current-fallback">
              <FIcon name="document_24_regular" />
              <span>{{ t('pptFocus.pageAlt', { n: slidesProgress.current }) }}</span>
            </div>
          </figure>

          <aside class="ppt-focus__side">
            <div class="ppt-focus__next">
              <p class="ppt-focus__side-eyebrow">{{ t('pptFocus.nextEyebrow') }}</p>
              <img v-if="nextSlideImage" :src="nextSlideImage" :alt="t('pptFocus.nextAlt')" />
              <div v-else class="ppt-focus__next-fallback">{{ nextSlideNumber ?? '—' }}</div>
            </div>

            <div class="ppt-focus__progress">
              <p class="ppt-focus__side-eyebrow">{{ t('pptFocus.progressEyebrow') }}</p>
              <template v-if="slidesProgress.total > 0">
                <a-progress type="line" :percent="slidesProgressPercentage" />
                <p class="ppt-focus__progress-page">{{ t('pptFocus.progressPage', { current: slidesProgress.current, total: slidesProgress.total }) }}</p>
              </template>
              <p v-else class="ppt-focus__progress-page">{{ t('pptFocus.progressUnknown') }}</p>
            </div>

            <section class="ppt-focus__teleprompter">
              <header class="ppt-focus__teleprompter-head">
                <p class="ppt-focus__side-eyebrow">{{ t('pptFocus.prompterEyebrow') }}</p>
                <span class="ppt-focus__teleprompter-page">{{ t('pptFocus.prompterPage', { n: slidesProgress.current }) }}</span>
              </header>
              <div class="ppt-focus__teleprompter-body">
                <p v-if="teleprompterText" class="ppt-focus__teleprompter-text">{{ teleprompterText }}</p>
                <p v-else class="ppt-focus__teleprompter-empty">{{ t('pptFocus.prompterEmpty') }}</p>
              </div>
            </section>
          </aside>
        </div>

        <div class="ppt-focus__controls" role="group" :aria-label="t('pptFocus.controlsAria')">
          <PresentationEffectControls v-if="isLibreOffice" :window-id="windowId" :disabled="controlsDisabled || !!session?.pending_command" />
          <template v-if="!isStaticSlideMode && !isLibreOffice">
            <div class="ppt-focus__media-picker">
              <a-select v-select-aria-label="t('pptFocus.mediaAria')"
                :value="selectedMediaKey ?? undefined" @update:value="selectedMediaKey = typeof $event === 'string' ? $event : null"
                :options="currentMediaOptions"
                :placeholder="mediaSelectPlaceholder"
                :disabled="isMediaPickerDisabled"
                show-search option-filter-prop="label"
                :aria-label="t('pptFocus.mediaAria')"
                size="large"
              />
            </div>
            <a-button :disabled="!canControlSelectedMedia" :aria-label="t('pptFocus.pauseMedia')" @click="controlSelectedMedia('pause')">
              <template #icon><FIcon name="pause_24_regular" /></template>
              <span class="ppt-focus__control-label">{{ t('pptFocus.pauseMedia') }}</span>
            </a-button>
            <a-button type="primary" :disabled="!canControlSelectedMedia" :aria-label="t('pptFocus.playMedia')" @click="controlSelectedMedia('play')">
              <template #icon><FIcon name="play_24_regular" /></template>
              <span class="ppt-focus__control-label">{{ t('pptFocus.playMedia') }}</span>
            </a-button>
            <a-button danger :disabled="!canControlSelectedMedia" :aria-label="t('pptFocus.stopMedia')" @click="controlSelectedMedia('stop')">
              <template #icon><FIcon name="stop_24_regular" /></template>
              <span class="ppt-focus__control-label">{{ t('pptFocus.stopMedia') }}</span>
            </a-button>
          </template>
          <a-button :disabled="controlsDisabled" :aria-label="t('pptFocus.prevPage')" @click="nav('prev')">
            <template #icon><FIcon name="previous_24_regular" /></template>
            <span class="ppt-focus__control-label">{{ t('pptFocus.prevPage') }}</span>
          </a-button>
          <a-button :disabled="controlsDisabled" :aria-label="t('pptFocus.nextPage')" @click="nav('next')">
            <template #icon><FIcon name="next_24_regular" /></template>
            <span class="ppt-focus__control-label">{{ t('pptFocus.nextPage') }}</span>
          </a-button>
        </div>
      </template>
    </main>
  </div>
</template>

<style scoped src="./PptFocusView.css"></style>
<style scoped src="./PptFocusResponsive.css"></style>
