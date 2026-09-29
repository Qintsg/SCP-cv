<script setup lang="ts">
/**
 * 显示控制页左侧「切换源」面板：
 *   - 顶部搜索 + 类型筛选 Pill；
 *   - List/Detail 风格列表，点击行直接打开到当前窗口；
 *   - 折叠的「上传并打开」区域：创建临时源并立即打开，结束后由后端清理。
 * @Project : SCP-cv
 * @File : SourcePicker.vue
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import {
  NButton,
  NCard,
  NFormItem,
  NInput,
  NProgress,
  NSpin,
  NTabs,
  NTabPane,
  NTag,
} from 'naive-ui';

import FIcon from '@/design-system/FIcon.vue';
import { useToast } from '@/composables/useToast';
import { useBackgroundAudioStore } from '@/stores/backgroundAudio';
import { useSessionStore } from '@/stores/sessions';
import { useSourceStore, type SourceCategory } from '@/stores/sources';
import type { MediaSourceItem } from '@/services/api';
import { pickUploadFile } from '@/platform/files';
import { getNativePlatformAdapter } from '@/platform/native';
import SourceThumbnail from '../sources/SourceThumbnail.vue';
import { sourceAvailabilityLabel, sourceCategoryLabel } from '../sources/sourcePresentation';
import { isConfirmedPlayback } from './playbackObservation';

const props = defineProps<{ windowId: number }>();
const emit = defineEmits<{ (event: 'opened'): void }>();

const { t } = useI18n();
const sourceStore = useSourceStore();
const backgroundAudioStore = useBackgroundAudioStore();
const sessionStore = useSessionStore();
const toast = useToast();
const currentSession = computed(() => sessionStore.byWindowId(props.windowId));
const controlsDisabled = computed(() => !currentSession.value?.player_online);
const currentSourceIsPlaying = computed(() => isConfirmedPlayback(currentSession.value));
const currentSourceStatus = computed(() => {
  if (controlsDisabled.value) return t('playback.playerOfflineTitle');
  return currentSourceIsPlaying.value
    ? t('sourcePicker.onAir')
    : currentSession.value?.playback_state_label || currentSession.value?.playback_state || t('playback.idle');
});

const filterValue = ref<SourceCategory>('all');
const searchKeyword = ref('');
const expanded = ref(false);
const fileToUpload = ref<File | null>(null);
const fileDisplayName = ref('');
const uploadProgress = ref(0);
const uploading = ref(false);
const uploadPhase = ref<'uploading' | 'processing'>('uploading');
const switchingSourceId = ref<number | null>(null);
const uploadError = ref('');
const fileInputRef = ref<HTMLInputElement | null>(null);
const acceptedFileTypes = [
  '.pdf', '.pptx', '.ppt', '.pps', '.ppsx', '.pptm', '.ppsm', '.pot', '.potx', '.potm', '.odp',
  '.mp4', '.mkv', '.avi', '.mov', '.wmv', '.flv', '.webm', '.m4v',
  '.mp3', '.wav', '.flac', '.aac', '.ogg', '.wma', '.m4a',
  '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.webp', '.svg',
] as const;

const filteredSources = computed<MediaSourceItem[]>(() => {
  const keyword = searchKeyword.value.trim().toLowerCase();
  return sourceStore.sources.filter((source) => {
    if (source.source_type === 'audio') return false;
    if (filterValue.value !== 'all' && sourceStore.resolveCategory(source.source_type) !== filterValue.value) {
      return false;
    }
    if (!keyword) return true;
    const hay = `${source.name} ${source.uri ?? ''} ${source.original_filename ?? ''}`.toLowerCase();
    return hay.includes(keyword);
  });
});

const totalLabel = computed(() => t('sourcePicker.count', { n: filteredSources.value.length }));
const uploadStatusText = computed(() =>
  uploadPhase.value === 'processing'
    ? t('sourcePicker.processing')
    : t('sourcePicker.uploading'),
);

function handleUploadProgress(percent: number): void {
  uploadProgress.value = percent >= 99 ? 100 : percent;
  uploadPhase.value = percent >= 99 ? 'processing' : 'uploading';
}

/**
 * 在线时选择源，受理与真正播出分别由会话呈现。
 * :param source: 当前媒体源记录。
 * :returns: 打开请求完成，失败通过通知反馈。
 */
async function selectSource(source: MediaSourceItem): Promise<void> {
  if (controlsDisabled.value) return;
  if (!source.is_available || switchingSourceId.value !== null) {
    if (!source.is_available) toast.warning(t('sourcePicker.offline'), t('sourcePicker.offlineHint'));
    return;
  }
  switchingSourceId.value = source.id;
  try {
    await sessionStore.openSource(props.windowId, source.id, true);
    toast.success(t('sourcePicker.openedOk', { name: source.name }));
    emit('opened');
  } catch (error) {
    toast.error(t('sourcePicker.openFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    switchingSourceId.value = null;
  }
}

function isCurrentSource(source: MediaSourceItem): boolean {
  return sessionStore.byWindowId(props.windowId)?.source_id === source.id;
}

function onFileSelect(event: Event): void {
  const target = event.target as HTMLInputElement;
  fileToUpload.value = target.files?.[0] ?? null;
}

async function triggerFilePicker(): Promise<void> {
  const adapter = getNativePlatformAdapter();
  if (!adapter) {
    fileInputRef.value?.click();
    return;
  }
  try {
    const selected = await pickUploadFile(adapter, acceptedFileTypes);
    if (!selected) return;
    fileToUpload.value = new File([selected.data], selected.name, { type: selected.mimeType });
    uploadError.value = '';
  } catch (error) {
    uploadError.value = error instanceof Error ? error.message : t('sourcePicker.pickFileFirst');
  }
}

/**
 * 在线时上传并显式打开，不为离线恢复缓存动作。
 * :returns: 上传和打开请求完成，失败保留可读原因。
 */
async function uploadAndOpen(): Promise<void> {
  if (controlsDisabled.value) return;
  if (!fileToUpload.value) {
    uploadError.value = t('sourcePicker.pickFileFirst');
    return;
  }
  uploading.value = true;
  uploadPhase.value = 'uploading';
  uploadProgress.value = 0;
  uploadError.value = '';
  try {
    const result = await sourceStore.upload(fileToUpload.value, {
      name: fileDisplayName.value.trim() || undefined,
      isTemporary: true,
      onProgress: handleUploadProgress,
    });
    if (result.source_type === 'audio') {
      await backgroundAudioStore.playSource(result.id);
      toast.success(t('backgroundAudio.playingOk', { name: result.name }));
    } else {
      await sessionStore.openSource(props.windowId, result.id, true);
      toast.success(t('sourcePicker.uploadedOpened'), t('sourcePicker.sourceNameDetail', { name: result.name }));
      emit('opened');
    }
    fileToUpload.value = null;
    fileDisplayName.value = '';
    if (fileInputRef.value) fileInputRef.value.value = '';
  } catch (error) {
    uploadError.value = error instanceof Error ? error.message : t('sourcePicker.uploadFail');
  } finally {
    uploading.value = false;
    uploadProgress.value = 0;
    uploadPhase.value = 'uploading';
  }
}

async function uploadOnly(): Promise<void> {
  if (!fileToUpload.value) {
    uploadError.value = t('sourcePicker.pickFileFirst');
    return;
  }
  uploading.value = true;
  uploadPhase.value = 'uploading';
  uploadProgress.value = 0;
  uploadError.value = '';
  try {
    const result = await sourceStore.upload(fileToUpload.value, {
      name: fileDisplayName.value.trim() || undefined,
      isTemporary: false,
      onProgress: handleUploadProgress,
    });
    toast.success(t('sourcePicker.uploadedSaved'), t('sourcePicker.sourceNameDetail', { name: result.name }));
    fileToUpload.value = null;
    fileDisplayName.value = '';
    if (fileInputRef.value) fileInputRef.value.value = '';
  } catch (error) {
    uploadError.value = error instanceof Error ? error.message : t('sourcePicker.uploadFail');
  } finally {
    uploading.value = false;
    uploadProgress.value = 0;
    uploadPhase.value = 'uploading';
  }
}
</script>

<template>
  <n-card class="source-picker" :title="t('sourcePicker.title')" size="small">
    <template #header-extra>
      <span class="source-picker__count">{{ totalLabel }}</span>
    </template>

    <n-input
      v-model:value="searchKeyword"
      :placeholder="t('sourcePicker.searchPlaceholder')"
      :aria-label="t('sourcePicker.searchAria')"
      clearable
    >
      <template #prefix>
        <FIcon name="search_20_regular" />
      </template>
    </n-input>

    <div class="source-picker__filter-scroll">
      <n-tabs v-model:value="filterValue" type="segment" :aria-label="t('sourcePicker.filterAria')">
        <n-tab-pane name="all" :tab="t('sourcePicker.filter.all')" />
        <n-tab-pane name="ppt" :tab="t('sourcePicker.filter.ppt')" />
        <n-tab-pane name="video" :tab="t('sourcePicker.filter.video')" />
        <n-tab-pane name="image" :tab="t('sourcePicker.filter.image')" />
        <n-tab-pane name="web" :tab="t('sourcePicker.filter.web')" />
        <n-tab-pane name="stream" :tab="t('sourcePicker.filter.stream')" />
      </n-tabs>
    </div>

    <ul class="source-picker__list">
      <li v-if="filteredSources.length === 0" class="source-picker__empty">
        {{ t('sourcePicker.empty') }}
      </li>
      <li
        v-for="source in filteredSources"
        :key="source.id"
        class="source-picker__item"
        :class="{
          'source-picker__item--unavailable': controlsDisabled || !source.is_available,
          'source-picker__item--active': isCurrentSource(source),
          'source-picker__item--switching': switchingSourceId === source.id,
        }"
      >
        <button
          type="button"
          class="source-picker__item-button"
          :disabled="controlsDisabled || !source.is_available || switchingSourceId !== null"
          :aria-current="isCurrentSource(source) ? 'true' : undefined"
          @click="selectSource(source)"
        >
          <SourceThumbnail :source="source" />
          <div class="source-picker__meta">
            <p class="source-picker__name">{{ source.name }}</p>
            <p class="source-picker__sub">
              <n-tag v-if="isCurrentSource(source)" :type="currentSourceIsPlaying ? 'success' : 'warning'" size="small" round>{{ currentSourceStatus }}</n-tag>
              <n-tag :type="source.is_available ? 'default' : source.preparation_state === 'queued' || source.preparation_state === 'running' ? 'warning' : 'error'" size="small" round>
                {{ source.is_available ? sourceCategoryLabel(source) : sourceAvailabilityLabel(source) }}
              </n-tag>
            </p>
          </div>
          <n-spin v-if="switchingSourceId === source.id" :size="18" />
        </button>
      </li>
    </ul>

    <details class="source-picker__upload" :open="expanded"
      @toggle="expanded = ($event.target as HTMLDetailsElement).open">
      <summary class="source-picker__upload-summary">
        <FIcon name="arrow_upload_24_regular" />
        <span>{{ t('sourcePicker.uploadNew') }}</span>
      </summary>
      <div class="source-picker__upload-body">
        <n-form-item :label="t('sourcePicker.file')" required>
          <div class="source-picker__file-row">
            <label class="source-picker__file">
              <input ref="fileInputRef" type="file" class="visually-hidden" :disabled="uploading"
                :accept="acceptedFileTypes.join(',')"
                @change="onFileSelect" />
              <span>{{ fileToUpload ? fileToUpload.name : t('sourcePicker.noFile') }}</span>
              <n-button @click.stop="triggerFilePicker">
                {{ t('sourcePicker.chooseFile') }}
              </n-button>
            </label>
            <p class="source-picker__pdf-hint">{{ t('sourcePicker.pdfSuggestion') }}</p>
          </div>
        </n-form-item>
        <n-form-item :label="t('sourcePicker.displayName')">
          <n-input v-model:value="fileDisplayName" :placeholder="t('sourcePicker.displayNamePlaceholder')" />
        </n-form-item>
        <p v-if="uploadError" class="source-picker__upload-error">{{ uploadError }}</p>
        <template v-if="uploading">
          <n-progress type="line" :percentage="uploadProgress" :show-indicator="false" />
          <p class="source-picker__upload-state">{{ uploadStatusText }}</p>
        </template>

        <div class="source-picker__upload-actions">
          <n-button :disabled="uploading || !fileToUpload" :loading="uploading" @click="uploadOnly">
            {{ t('sourcePicker.uploadOnly') }}
          </n-button>
          <n-button type="primary" :disabled="controlsDisabled || uploading || !fileToUpload" :loading="uploading"
            @click="uploadAndOpen">
            {{ t('sourcePicker.uploadOpen') }}
          </n-button>
        </div>
      </div>
    </details>

    <p v-if="uploading && !expanded" class="source-picker__upload-state">
      <n-spin :size="16" /> {{ uploadStatusText }}
    </p>
  </n-card>
</template>

<style scoped src="./SourcePicker.css"></style>
