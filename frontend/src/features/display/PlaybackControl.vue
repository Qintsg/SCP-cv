<script setup lang="ts">
/**
 * 单窗口播放控制条：按 source_type 渲染不同分支
 *  - PPT：两按钮步进 / 页码 / 当前页媒体子表
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
import { tagColor } from '@/design-system/tagColor';
import { displayMediaAddress } from '@/services/mediaAddress';

import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { RouterLink } from 'vue-router';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import AProgress from 'ant-design-vue/es/progress';
import ASlider from 'ant-design-vue/es/slider';
import ASwitch from 'ant-design-vue/es/switch';
import ATag from 'ant-design-vue/es/tag';

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
import { isConfirmedPlayback, isRequestedScreenShare, playbackStateTagType } from './playbackObservation';
import { resolveActualOutputSource, resolveActualScreenShare } from './playbackOutput';
import ScreenShareOutputStatus from '../screen-share/ScreenShareOutputStatus.vue';
import PresentationNavigation from './PresentationNavigation.vue';
import { useMobileControls } from '@/composables/useMobileControls';

const props = defineProps<{ session: SessionSnapshot }>();

const { t } = useI18n();
const toast = useToast();
const sessionStore = useSessionStore();
const sourceStore = useSourceStore();
const mobileControls = useMobileControls();

const category = computed(() => sourceStore.resolveCategory(props.session.source_type));
const isStaticSlideMode = computed(() => props.session.playback_mode === 'pdf' || props.session.playback_mode === 'slide_images');
const currentSource = computed(() => sourceStore.findById(props.session.source_id) ?? null);
const { context: previewContext, resources: pptResources, error: pptError,
  pending: previewPending,
  refresh: refreshPreview } = usePresentationPreview(() => mobileControls.value ? undefined : props.session);
const usesPresentationPreview = computed(() => isPresentationPreviewAdapter(props.session.actual_adapter_kind)
  || category.value === 'ppt' && !['image', 'web', 'vlc'].includes(props.session.actual_adapter_kind ?? ''));
const actualSource = computed(() => resolveActualOutputSource(props.session, sourceStore.findById));
const actualShare = computed(() => resolveActualScreenShare(props.session));
const requestedShare = computed(() => isRequestedScreenShare(props.session));
const hasContent = computed(() => Boolean(props.session.source_id) || requestedShare.value || Boolean(actualShare.value));
const shareStopPending = computed(() => !props.session.source_id && !requestedShare.value && Boolean(actualShare.value));
const sharedStateLabel = computed(() => shareStopPending.value ? t('screenShare.output.stopPending')
  : !props.session.player_online ? t('playback.playerOfflineTitle') : props.session.playback_state === 'error'
    ? props.session.playback_state_label : isConfirmedPlayback(props.session) ? t('screenShare.phases.sharing') : t('screenShare.output.waiting'));
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

const stateType = computed(() => playbackStateTagType(props.session));

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
    toast.info(t('playback.reopenOk'));
  } catch (error) {
    toast.error(t('playback.reopenFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

async function refreshWebSource(): Promise<void> {
  if (!props.session.source_id) return;
  try {
    await sessionStore.control(props.session.window_id, category.value === 'web' ? 'refresh' : 'play');
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
          <a-tag :color="tagColor(stateType)"  >
            {{ requestedShare || shareStopPending ? sharedStateLabel : session.source_id ? (session.playback_state_label || session.playback_state) : t('playback.idle') }}
          </a-tag>
          <h3 class="playback-control__source-name">
            {{ requestedShare || shareStopPending ? t('screenShare.title') : session.source_name && session.source_id ? session.source_name : t('playback.notOpened') }}
          </h3>
          <p v-if="session.source_id" class="playback-control__caption">
            {{ session.source_type_label || t('playback.idle') }}
            <a-tag v-if="category === 'ppt' && session.playback_mode === 'pdf'" color="processing"  >
              {{ t('playback.pdfBadge') }}
            </a-tag>
            <a-tag v-else-if="category === 'ppt' && session.playback_mode === 'slide_images'" color="processing"  >
              {{ t('playback.slideImagesBadge') }}
            </a-tag>
            <a-tag v-else-if="category === 'ppt' && session.playback_mode === 'libreoffice'" color="processing"  >
              LibreOffice
            </a-tag>
            <a-tag v-if="session.actual_fit_text !== null && session.actual_fit_text !== undefined" color="processing"  >
              {{ t(session.actual_fit_text ? 'playback.fittedTextBadge' : 'playback.originalTextBadge') }}
            </a-tag>
          </p>
        </div>
      </div>
      <RouterLink v-if="category === 'ppt' && session.source_id" :to="`/ppt-focus/${session.window_id}`"
        class="playback-control__focus-link">
        <FIcon name="arrow_maximize_24_regular" />
        <span>{{ t('playback.focusLink') }}</span>
      </RouterLink>
    </header>

    <PresentationNavigation v-if="category === 'ppt'" :window-id="session.window_id" :mode="session.playback_mode" :disabled="presentationControlsDisabled" />

    <ScreenShareOutputStatus v-if="actualShare" :audio="actualShare.audio" :online="session.player_online" />
    <section v-else-if="outputReferenceVisible && !mobileControls" class="playback-control__monitor" aria-live="polite">
      <SourceThumbnail v-if="monitorSource" :source="monitorSource" size="stage" :image-url="currentPagePreviewUrl"
        :allow-source-preview="!usesPresentationPreview" />
      <div class="playback-control__monitor-copy">
        <span class="playback-control__monitor-eyebrow">{{ monitorSource ? t(session.player_online ? 'playback.currentOutput' : 'playback.lastOutput') : missingOutputLabel }}</span>
        <strong>{{ monitorSource?.name || missingOutputLabel }}</strong>
        <span v-if="outputMismatch">{{ t('playback.outputMismatch') }}</span>
        <a-button v-if="previewContext" size="small" :loading="previewPending" :disabled="previewPending"
          @click="refreshPreview">刷新预览</a-button>
      </div>
    </section>

    <a-alert v-if="session.actual_source_id && !supportsRequestedPlaybackControls(session)" type="warning"><template #description>
      {{ t('playback.controlsTypeMismatch') }}
    </template></a-alert>

    <a-alert v-if="showErrorBar" type="error" :message="errorBarTitle" closable @close="dismissErrorBar"><template #description>
      <div class="playback-control__alert-body">
        <span>{{ errorBarDescription }}</span>
        <a-button size="small" :disabled="!reopenSourceId || controlsDisabled" @click="reopenCurrentSource">
          {{ t('playback.reopen') }}
        </a-button>
      </div>
    </template></a-alert>

    <section v-if="category === 'ppt'" class="playback-control__section">
      <div v-if="session.total_slides > 0" class="playback-control__row playback-control__row--progress">
        <a-progress
          type="line"
          :percent="pptProgressPercentage"
          :show-info="false"
          class="playback-control__progress"
        />
        <span class="playback-control__progress-label">{{ pptProgressLabel }}</span>
      </div>

      <a-alert v-if="pptError" type="error" :message="t('playback.pptLoadFail')"><template #description>
        {{ pptError }}
      </template></a-alert>

      <div v-if="!isStaticSlideMode && session.playback_mode !== 'libreoffice' && currentResource && currentResource.media_items.length > 0" class="playback-control__media">
        <h4 class="playback-control__media-title">{{ t('playback.currentMedia') }}</h4>
        <ul class="playback-control__media-list">
          <li v-for="media in currentResource.media_items" :key="media.id" class="playback-control__media-item">
            <span class="playback-control__media-name">{{ media.name }}</span>
            <span class="playback-control__media-actions">
              <a-button size="small" shape="circle" :disabled="playbackControlsDisabled" :aria-label="t('playback.playMedia')"
                @click="pptMediaAction(media.id, media.media_index, 'play')">
                <template #icon><FIcon name="play_24_regular" /></template>
              </a-button>
              <a-button size="small" shape="circle" :disabled="playbackControlsDisabled" :aria-label="t('playback.pauseMedia')"
                @click="pptMediaAction(media.id, media.media_index, 'pause')">
                <template #icon><FIcon name="pause_24_regular" /></template>
              </a-button>
              <a-button size="small" shape="circle" danger :disabled="playbackControlsDisabled" :aria-label="t('playback.stopMedia')"
                @click="pptMediaAction(media.id, media.media_index, 'stop')">
                <template #icon><FIcon name="stop_24_regular" /></template>
              </a-button>
            </span>
          </li>
        </ul>
      </div>
    </section>

    <section v-else-if="category === 'video'" class="playback-control__section">
      <div class="playback-control__row">
        <a-button v-if="!isPlaying" type="primary" :disabled="playbackControlsDisabled" @click="onPlay">
          <template #icon><FIcon name="play_24_regular" /></template>
          {{ t('playback.play') }}
        </a-button>
        <a-button v-else type="primary" :disabled="playbackControlsDisabled" @click="onPause">
          <template #icon><FIcon name="pause_24_regular" /></template>
          {{ t('playback.pause') }}
        </a-button>
        <a-button :disabled="playbackControlsDisabled" @click="onStop">
          <template #icon><FIcon name="stop_24_regular" /></template>
          {{ t('playback.stop') }}
        </a-button>
        <div class="playback-control__switch">
          <span>{{ t('playback.loop') }}</span>
          <a-switch :checked="session.loop_enabled" :disabled="playbackControlsDisabled" :aria-label="t('playback.loop')"
            @update:checked="value => onLoopToggle(value === true)" />
        </div>
      </div>
      <div v-if="(session.duration_ms ?? 0) > 0" class="playback-control__row playback-control__row--seek">
        <a-slider
          v-slider-aria-label="t('playback.seekAria')"
          :value="videoSeek.value.value"
          :min="0"
          :max="session.duration_ms ?? 0"
          :step="1000"
          :disabled="playbackControlsDisabled"
          :aria-label="t('playback.seekAria')"
          class="playback-control__seek"
          @change="value => { if (typeof value === 'number') videoSeek.handleInput(value); }"
          @afterChange="videoSeek.handleChange(videoSeek.value.value)"

        />
        <span class="playback-control__progress-label">
          {{ formatDuration(session.position_ms ?? 0) }} / {{ formatDuration(session.duration_ms ?? 0) }}
        </span>
      </div>
    </section>

    <section v-else-if="category === 'image' || category === 'web'" class="playback-control__section">
      <p v-if="session.source_uri" class="playback-control__uri">{{ displayMediaAddress(session.source_uri) }}</p>
      <p v-else class="playback-control__uri">{{ t('playback.uriMissing') }}</p>
      <a-button v-if="category === 'web'" size="small" :disabled="playbackControlsDisabled" @click="refreshWebSource">
        <template #icon><FIcon name="arrow_clockwise_24_regular" /></template>
        {{ t('playback.refresh') }}
      </a-button>
    </section>

    <section v-else-if="category === 'stream'" class="playback-control__section">
      <a-tag :color="tagColor(controlsDisabled ? 'warning' : stateType)"  >
        {{ streamStatusLabel }}
      </a-tag>
      <p v-if="session.source_uri" class="playback-control__uri">{{ displayMediaAddress(session.source_uri) }}</p>
      <a-button size="small" :disabled="playbackControlsDisabled" @click="refreshWebSource">
        <template #icon><FIcon name="arrow_clockwise_24_regular" /></template>
        {{ t('playback.refresh') }}
      </a-button>
    </section>

    <section v-else-if="!requestedShare && !actualShare" class="playback-control__section">
      <p class="playback-control__caption">{{ t('playback.noSource') }}</p>
    </section>

    <section class="playback-control__section">
      <div class="playback-control__row">
        <span class="playback-control__field-label">{{ t('playback.windowVolume') }}</span>
        <a-slider
          v-slider-aria-label="t('playback.windowVolumeAria')"
          :value="windowVolume.value.value"
          :min="0"
          :max="100"
          :aria-label="t('playback.windowVolumeAria')"
          :disabled="!canAdjustWindowAudio || playbackControlsDisabled"
          class="playback-control__seek"
          @change="value => { if (typeof value === 'number') windowVolume.handleInput(value); }"
          @afterChange="windowVolume.handleChange(windowVolume.value.value)"

        />
      </div>
      <div class="playback-control__row">
        <div class="playback-control__switch">
          <span>{{ t('playback.windowMute') }}</span>
          <a-switch
            :checked="session.is_muted"
            :aria-label="t('playback.windowMute')"
            :disabled="!canAdjustWindowAudio || playbackControlsDisabled"
            :aria-disabled="!canAdjustWindowAudio || playbackControlsDisabled"
            @update:checked="value => onMuteToggle(value === true)"
          />
        </div>
        <a-button v-if="hasContent" danger :disabled="controlsDisabled || shareStopPending" @click="onClose">
          <template #icon><FIcon name="dismiss_24_regular" /></template>
          {{ t(requestedShare || shareStopPending ? 'screenShare.stop' : 'playback.closeDisplay') }}
        </a-button>
      </div>
    </section>
  </div>
</template>

<style scoped src="./PlaybackControl.css"></style>
