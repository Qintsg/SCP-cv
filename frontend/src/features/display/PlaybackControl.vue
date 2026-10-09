<script setup lang="ts">
/**
 * 单窗口播放控制条：按 source_type 渲染不同分支
 *  - PPT：上一页 / 下一页 / 跳页 / PPT 进度条 / 当前页媒体子表
 *  - video：播放暂停停止 / 循环 / Seek
 *  - image / web：仅展示 URI + 关闭按钮
 *  - *_stream：直播状态 Tag + URI（无 Seek）
 *
 * 音量调节走 useThrottledSlider：拖动期间节流上报、抬手再 flush 一次，
 * 避免高频 PATCH 与 SSE 回写在拖动中竞态导致滑块回弹/卡顿。
 * @Project : SCP-cv
 * @File : PlaybackControl.vue
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { RouterLink } from 'vue-router';
import {
  NAlert,
  NButton,
  NInput,
  NProgress,
  NSlider,
  NSwitch,
  NTag,
} from 'naive-ui';

import FIcon from '@/design-system/FIcon.vue';
import { sliderAriaLabel as vSliderAriaLabel } from '@/design-system/sliderAriaLabel';
import SourceThumbnail from '@/features/sources/SourceThumbnail.vue';
import { useToast } from '@/composables/useToast';
import { useThrottledSlider } from '@/composables/useThrottledSlider';
import { useSessionStore } from '@/stores/sessions';
import { useSourceStore } from '@/stores/sources';
import { formatDuration } from '@/design-system/utils';
import { buildBackendUrl, type SessionSnapshot } from '@/services/api';
import { playbackSliderScope, supportsRequestedPlaybackControls, supportsWindowAudioControls } from './playbackCapabilities';
import { isPresentationPreviewAdapter, usePresentationPreview } from './usePresentationPreview';
import { usePlaybackErrorGate } from './usePlaybackErrorGate';
import { isConfirmedPlayback } from './playbackObservation';
import { resolveActualOutputSource } from './playbackOutput';
import PresentationEffectControls from './PresentationEffectControls.vue';
import { usePresentationCommandFeedback } from './usePresentationCommandFeedback';

const props = defineProps<{ session: SessionSnapshot }>();

const { t } = useI18n();
const toast = useToast();
const sessionStore = useSessionStore();
const sourceStore = useSourceStore();
const feedback = usePresentationCommandFeedback(() => sessionStore.byWindowId(props.session.window_id),
  () => sessionStore.commandFeedbackRevisions[props.session.window_id] ?? 0);

const category = computed(() => sourceStore.resolveCategory(props.session.source_type));
const isStaticSlideMode = computed(() => props.session.playback_mode === 'pdf' || props.session.playback_mode === 'slide_images');
const currentSource = computed(() => sourceStore.findById(props.session.source_id) ?? null);
const { context: previewContext, resources: pptResources, error: pptError,
  unavailable: previewUnavailable, pending: previewPending,
  refresh: refreshPreview } = usePresentationPreview(() => props.session);
const usesPresentationPreview = computed(() => isPresentationPreviewAdapter(props.session.actual_adapter_kind)
  || category.value === 'ppt' && !['image', 'web', 'vlc'].includes(props.session.actual_adapter_kind ?? ''));
const actualSource = computed(() => resolveActualOutputSource(props.session, sourceStore.findById));
const monitorSource = computed(() => usesPresentationPreview.value
  ? sourceStore.findById(previewContext.value?.sourceId ?? null) ?? null : actualSource.value);
const outputMismatch = computed(() => typeof props.session.actual_source_id === 'number'
  && props.session.actual_source_id !== props.session.source_id);
const outputReferenceVisible = computed(() => Boolean(props.session.source_id)
  || Boolean(props.session.actual_source_id) || usesPresentationPreview.value);
const missingOutputLabel = computed(() => typeof props.session.actual_source_id === 'number'
  && props.session.actual_source_id > 0 && !actualSource.value
  ? t('playback.actualSourceUnavailable') : t('playback.outputUnconfirmed'));
const reopenSourceId = computed(() => props.session.actual_source_id === undefined
  ? props.session.source_id : props.session.actual_source_id);
const canAdjustWindowAudio = computed(() => (
  Boolean(props.session.source_id)
  && supportsWindowAudioControls(props.session.source_type)
));

type NTagType = 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error';

const stateType = computed<NTagType>(() => {
  if (!props.session.source_id) return 'default';
  switch (props.session.playback_state) {
    case 'playing':
      return 'success';
    case 'paused':
    case 'loading':
      return 'warning';
    case 'error':
      return 'error';
    case 'idle':
    default:
      return 'default';
  }
});

const jumpInput = ref<string>('');

watch(
  () => props.session.current_slide,
  (slide) => {
    if (category.value === 'ppt' && slide > 0) {
      jumpInput.value = String(slide);
    }
  },
  { immediate: true },
);

async function call(action: () => Promise<unknown>, errorTitle: string): Promise<void> {
  try {
    await action();
  } catch (error) {
    toast.error(errorTitle, error instanceof Error ? error.message : t('common.retry'));
  }
}

function onPlay(): void {
  void call(() => sessionStore.control(props.session.window_id, 'play'), t('playback.playFail'));
}
function onPause(): void {
  void call(() => sessionStore.control(props.session.window_id, 'pause'), t('playback.pauseFail'));
}
function onStop(): void {
  void call(() => sessionStore.control(props.session.window_id, 'stop'), t('playback.stopFail'));
}
function onPrev(): void {
  void call(() => sessionStore.navigate(props.session.window_id, 'prev'), t('playback.navFail'));
}
function onNext(): void {
  void call(() => feedback.submit(() => sessionStore.navigate(props.session.window_id, 'next'), 'NEXT'), t('playback.navFail'));
}
/** :returns: 只接受完整十进制正整数文本；非法输入使用既有提示，不发送导航。 */
function onJump(): void {
  const text = jumpInput.value.trim();
  const target = Number(text);
  if (!/^\d+$/.test(text) || !Number.isSafeInteger(target) || target <= 0 || target > 2_147_483_647) {
    toast.warning(t('playback.pageInvalid'), t('playback.pageInvalidDetail'));
    return;
  }
  void call(() => sessionStore.navigate(props.session.window_id, 'goto', target), t('playback.jumpFail'));
}

function onLoopToggle(value: boolean): void {
  void call(() => sessionStore.setLoop(props.session.window_id, value), t('playback.loopFail'));
}

function onMuteToggle(value: boolean): void {
  void call(() => sessionStore.setWindowMute(props.session.window_id, value), t('playback.windowMuteFail'));
}

const windowVolume = useThrottledSlider(
  () => props.session.volume,
  {
    scope: () => playbackSliderScope(props.session),
    isActive: () => props.session.player_online && canAdjustWindowAudio.value && supportsRequestedPlaybackControls(props.session),
    commit: (value: number) => sessionStore.setWindowVolume(props.session.window_id, value),
    onError: (error) => {
      toast.error(t('playback.volumeFail'), error instanceof Error ? error.message : t('common.retry'));
    },
  },
);

const seekValue = computed(() => Math.min(props.session.duration_ms ?? 0, Math.max(0, props.session.position_ms ?? 0)));

const videoSeek = useThrottledSlider(
  () => seekValue.value,
  {
    scope: () => playbackSliderScope(props.session),
    isActive: () => props.session.player_online && category.value === 'video'
      && supportsRequestedPlaybackControls(props.session) && (props.session.duration_ms ?? 0) > 0,
    commit: (positionMs: number) => sessionStore.navigate(props.session.window_id, 'seek', 0, positionMs),
    onError: (error) => toast.error(t('playback.seekFail'), error instanceof Error ? error.message : t('common.retry')),
  },
);

function onClose(): void {
  void call(() => sessionStore.closeSource(props.session.window_id), t('playback.closeFail'));
}

const currentResource = computed(() =>
  pptResources.value.find((res) => res.page_index === props.session.current_slide),
);
const currentPagePreviewUrl = computed(() => {
  if (!previewContext.value || !currentResource.value?.slide_image) return '';
  return buildBackendUrl(currentResource.value.slide_image);
});

async function pptMediaAction(mediaId: string, mediaIndex: number, action: string): Promise<void> {
  try {
    await sessionStore.controlPptMedia(props.session.window_id, action, mediaId, mediaIndex);
  } catch (error) {
    toast.error(t('playback.pptMediaFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

const pptProgressLabel = computed(() => {
  if (props.session.total_slides <= 0) return '';
  return `${props.session.current_slide} / ${props.session.total_slides}`;
});

const pptProgressPercentage = computed(() => {
  if (props.session.total_slides <= 0) return 0;
  return Math.round((props.session.current_slide / props.session.total_slides) * 100);
});

const isPlaying = computed(() => props.session.playback_state === 'playing');
const controlsDisabled = computed(() => !props.session.player_online);
const playbackControlsDisabled = computed(() => controlsDisabled.value || !supportsRequestedPlaybackControls(props.session));
const presentationControlsDisabled = playbackControlsDisabled;
const streamStatusLabel = computed(() => {
  if (controlsDisabled.value) return t('playback.playerOfflineTitle');
  if (!props.session.source_uri) return t('playback.notStreaming');
  if (isConfirmedPlayback(props.session)) return t('playback.live');
  return props.session.playback_state_label || props.session.playback_state;
});

/**
 * 由操作员显式重开当前源，播出结果继续等待 Worker 上报。
 * :returns: 重开请求完成；离线或后端失败通过通知反馈。
 */
async function reopenCurrentSource(): Promise<void> {
  if (!reopenSourceId.value) return;
  try {
    await sessionStore.reopenCurrentSource(props.session.window_id);
    toast.info(t('playback.reopenOk'), t('playback.reopenOkDetail'));
  } catch (error) {
    toast.error(t('playback.reopenFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

async function refreshWebSource(): Promise<void> {
  if (!props.session.source_id) return;
  try {
    await sessionStore.control(props.session.window_id, 'play');
    toast.info(t('playback.refreshOk'));
  } catch (error) {
    toast.error(t('playback.refreshFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

const { showErrorBar, dismissErrorBar } = usePlaybackErrorGate({
  session: () => props.session,
  category: () => category.value,
});

const adapterErrorMessage = computed(() => (props.session.error_message || '').trim());
const errorBarTitle = computed(() => {
  if (!adapterErrorMessage.value && category.value === 'stream') return t('playback.streamNotReady');
  return category.value === 'stream' ? t('playback.streamError') : t('playback.playerError');
});
const errorBarDescription = computed(() => {
  if (adapterErrorMessage.value) return adapterErrorMessage.value;
  if (category.value === 'stream') {
    return t('playback.streamErrorDesc');
  }
  return t('playback.genericErrorDesc');
});
</script>

<template>
  <div class="playback-control">
    <header class="playback-control__heading">
      <div class="playback-control__identity">
        <SourceThumbnail v-if="currentSource" :source="currentSource" size="comfortable" />
        <div>
          <n-tag :type="stateType" round size="small">
            {{ session.source_id ? (session.playback_state_label || session.playback_state) : t('playback.idle') }}
          </n-tag>
          <h3 class="playback-control__source-name">
            {{ session.source_name && session.source_id ? session.source_name : t('playback.notOpened') }}
          </h3>
          <p v-if="outputMismatch" class="playback-control__caption">{{ t('playback.requestedSourceLabel') }}</p>
          <p v-if="session.source_id" class="playback-control__caption">
            {{ session.source_type_label || t('playback.idle') }}
            <n-tag v-if="category === 'ppt' && session.playback_mode === 'pdf'" type="info" round size="small">
              {{ t('playback.pdfBadge') }}
            </n-tag>
            <n-tag v-else-if="category === 'ppt' && session.playback_mode === 'slide_images'" type="info" round size="small">
              {{ t('playback.slideImagesBadge') }}
            </n-tag>
            <n-tag v-else-if="category === 'ppt' && session.playback_mode === 'libreoffice'" type="info" round size="small">
              LibreOffice
            </n-tag>
            <n-tag v-if="session.actual_fit_text !== null && session.actual_fit_text !== undefined" type="info" round size="small">
              {{ t(session.actual_fit_text ? 'playback.fittedTextBadge' : 'playback.originalTextBadge') }}
            </n-tag>
          </p>
        </div>
      </div>
      <RouterLink v-if="category === 'ppt' && session.source_id" :to="`/ppt-focus/${session.window_id}`"
        class="playback-control__focus-link">
        <FIcon name="arrow_maximize_24_regular" />
        <span>{{ t('playback.focusLink') }}</span>
      </RouterLink>
    </header>

    <section v-if="outputReferenceVisible" class="playback-control__monitor" aria-live="polite">
      <SourceThumbnail v-if="monitorSource" :source="monitorSource" size="stage" :image-url="currentPagePreviewUrl"
        :allow-source-preview="!usesPresentationPreview" />
      <div class="playback-control__monitor-copy">
        <span class="playback-control__monitor-eyebrow">{{ monitorSource ? t(session.player_online ? 'playback.currentOutput' : 'playback.lastOutput') : missingOutputLabel }}</span>
        <strong>{{ monitorSource?.name || (usesPresentationPreview ? '实际源或文字布局未知，不显示默认预览' : missingOutputLabel) }}</strong>
        <span v-if="outputMismatch">{{ t('playback.outputMismatch') }}</span>
        <span>{{ t('playback.previewReference') }}</span>
        <n-button v-if="previewContext" size="small" :loading="previewPending" :disabled="previewPending"
          @click="refreshPreview">刷新预览</n-button>
      </div>
    </section>

    <n-alert v-if="previewUnavailable" type="info">
      所选布局预览尚未准备；此提示仅指静态参考图，不表示原生播放失败。
    </n-alert>

    <n-alert v-if="session.actual_source_id && !supportsRequestedPlaybackControls(session)" type="warning">
      {{ t('playback.controlsTypeMismatch') }}
    </n-alert>

    <n-alert v-if="showErrorBar" type="error" :title="errorBarTitle" closable @close="dismissErrorBar">
      <div class="playback-control__alert-body">
        <span>{{ errorBarDescription }}</span>
        <n-button size="small" :disabled="!reopenSourceId || controlsDisabled" @click="reopenCurrentSource">
          {{ t('playback.reopen') }}
        </n-button>
      </div>
    </n-alert>

    <section v-if="category === 'ppt'" class="playback-control__section">
      <div class="playback-control__row playback-control__row--ppt">
        <n-button :disabled="presentationControlsDisabled" @click="onPrev">
          <template #icon><FIcon name="previous_24_regular" /></template>
          {{ t('playback.prevPage') }}
        </n-button>
        <n-button type="primary" :disabled="presentationControlsDisabled" @click="onNext">
          <template #icon><FIcon name="next_24_regular" /></template>
          {{ t('playback.nextPage') }}
        </n-button>
        <div class="playback-control__jump">
          <n-input v-model:value="jumpInput" type="text" :placeholder="t('playback.jumpPlaceholder')" :maxlength="4" />
          <n-button :disabled="presentationControlsDisabled" @click="onJump">{{ t('playback.jump') }}</n-button>
        </div>
      </div>
      <div v-if="session.total_slides > 0" class="playback-control__row playback-control__row--progress">
        <n-progress
          type="line"
          :percentage="pptProgressPercentage"
          :show-indicator="false"
          class="playback-control__progress"
        />
        <span class="playback-control__progress-label">{{ pptProgressLabel }}</span>
      </div>

      <n-alert v-if="pptError" type="error" :title="t('playback.pptLoadFail')">
        {{ pptError }}
      </n-alert>

      <PresentationEffectControls v-if="session.playback_mode === 'libreoffice'" :window-id="session.window_id" :disabled="presentationControlsDisabled" />
      <div v-if="!isStaticSlideMode && session.playback_mode !== 'libreoffice' && currentResource && currentResource.media_items.length > 0" class="playback-control__media">
        <h4 class="playback-control__media-title">{{ t('playback.currentMedia') }}</h4>
        <ul class="playback-control__media-list">
          <li v-for="media in currentResource.media_items" :key="media.id" class="playback-control__media-item">
            <span class="playback-control__media-name">{{ media.name }}</span>
            <span class="playback-control__media-actions">
              <n-button size="small" circle :disabled="playbackControlsDisabled" :aria-label="t('playback.playMedia')"
                @click="pptMediaAction(media.id, media.media_index, 'play')">
                <template #icon><FIcon name="play_24_regular" /></template>
              </n-button>
              <n-button size="small" circle :disabled="playbackControlsDisabled" :aria-label="t('playback.pauseMedia')"
                @click="pptMediaAction(media.id, media.media_index, 'pause')">
                <template #icon><FIcon name="pause_24_regular" /></template>
              </n-button>
              <n-button size="small" circle type="error" :disabled="playbackControlsDisabled" :aria-label="t('playback.stopMedia')"
                @click="pptMediaAction(media.id, media.media_index, 'stop')">
                <template #icon><FIcon name="stop_24_regular" /></template>
              </n-button>
            </span>
          </li>
        </ul>
      </div>
    </section>

    <section v-else-if="category === 'video'" class="playback-control__section">
      <div class="playback-control__row">
        <n-button v-if="!isPlaying" type="primary" :disabled="playbackControlsDisabled" @click="onPlay">
          <template #icon><FIcon name="play_24_regular" /></template>
          {{ t('playback.play') }}
        </n-button>
        <n-button v-else type="primary" :disabled="playbackControlsDisabled" @click="onPause">
          <template #icon><FIcon name="pause_24_regular" /></template>
          {{ t('playback.pause') }}
        </n-button>
        <n-button :disabled="playbackControlsDisabled" @click="onStop">
          <template #icon><FIcon name="stop_24_regular" /></template>
          {{ t('playback.stop') }}
        </n-button>
        <div class="playback-control__switch">
          <span>{{ t('playback.loop') }}</span>
          <n-switch :value="session.loop_enabled" :disabled="playbackControlsDisabled" :aria-label="t('playback.loop')"
            @update:value="onLoopToggle" />
        </div>
      </div>
      <div v-if="(session.duration_ms ?? 0) > 0" class="playback-control__row playback-control__row--seek">
        <n-slider
          v-slider-aria-label="t('playback.seekAria')"
          :value="videoSeek.value.value"
          :min="0"
          :max="session.duration_ms ?? 0"
          :step="1000"
          :disabled="playbackControlsDisabled"
          :aria-label="t('playback.seekAria')"
          class="playback-control__seek"
          @update:value="videoSeek.handleInput"
          @dragend="videoSeek.handleChange(videoSeek.value.value)"
          @keyup="videoSeek.handleChange(videoSeek.value.value)"
        />
        <span class="playback-control__progress-label">
          {{ formatDuration(session.position_ms ?? 0) }} / {{ formatDuration(session.duration_ms ?? 0) }}
        </span>
      </div>
    </section>

    <section v-else-if="category === 'image' || category === 'web'" class="playback-control__section">
      <p v-if="session.source_uri" class="playback-control__uri">{{ session.source_uri }}</p>
      <p v-else class="playback-control__uri">{{ t('playback.uriMissing') }}</p>
      <n-button v-if="category === 'web'" size="small" :disabled="playbackControlsDisabled" @click="refreshWebSource">
        <template #icon><FIcon name="arrow_clockwise_24_regular" /></template>
        {{ t('playback.refresh') }}
      </n-button>
    </section>

    <section v-else-if="category === 'stream'" class="playback-control__section">
      <n-tag :type="controlsDisabled ? 'warning' : stateType" round size="small">
        {{ streamStatusLabel }}
      </n-tag>
      <p v-if="session.source_uri" class="playback-control__uri">{{ session.source_uri }}</p>
      <n-button size="small" :disabled="playbackControlsDisabled" @click="refreshWebSource">
        <template #icon><FIcon name="arrow_clockwise_24_regular" /></template>
        {{ t('playback.refresh') }}
      </n-button>
    </section>

    <section v-else class="playback-control__section">
      <p class="playback-control__caption">{{ t('playback.noSource') }}</p>
    </section>

    <section class="playback-control__section">
      <div class="playback-control__row">
        <span class="playback-control__field-label">{{ t('playback.windowVolume') }}</span>
        <n-slider
          v-slider-aria-label="t('playback.windowVolumeAria')"
          :value="windowVolume.value.value"
          :min="0"
          :max="100"
          :aria-label="t('playback.windowVolumeAria')"
          :disabled="!canAdjustWindowAudio || playbackControlsDisabled"
          class="playback-control__seek"
          @update:value="windowVolume.handleInput"
          @dragend="windowVolume.handleChange(windowVolume.value.value)"
          @keyup="windowVolume.handleChange(windowVolume.value.value)"
        />
      </div>
      <div class="playback-control__row">
        <div class="playback-control__switch">
          <span>{{ t('playback.windowMute') }}</span>
          <n-switch
            :value="session.is_muted"
            :aria-label="t('playback.windowMute')"
            :disabled="!canAdjustWindowAudio || playbackControlsDisabled"
            @update:value="onMuteToggle"
          />
        </div>
        <n-button v-if="session.source_id" type="error" :disabled="controlsDisabled" @click="onClose">
          <template #icon><FIcon name="dismiss_24_regular" /></template>
          {{ t('playback.closeDisplay') }}
        </n-button>
      </div>
    </section>
  </div>
</template>

<style scoped src="./PlaybackControl.css"></style>
