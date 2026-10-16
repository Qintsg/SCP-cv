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
import { tabsKeyboard as vTabsKeyboard } from '@/design-system/tabsKeyboard';
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import { tagColor } from '@/design-system/tagColor';

import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import { FormItem as AFormItem } from 'ant-design-vue/es/form';
import AInput from 'ant-design-vue/es/input';
import AProgress from 'ant-design-vue/es/progress';
import ASelect from 'ant-design-vue/es/select';
import ASwitch from 'ant-design-vue/es/switch';
import ASpin from 'ant-design-vue/es/spin';
import ATabs from 'ant-design-vue/es/tabs';
import { TabPane as ATabPane } from 'ant-design-vue/es/tabs';
import ATag from 'ant-design-vue/es/tag';

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
import { resolveActualOutputSource } from './playbackOutput';
import DirectoryNavigator from '../directories/DirectoryNavigator.vue';
import { inDirectory, sourcePath } from '../directories/directoryPath';

const props = defineProps<{ windowId: number }>();
const emit = defineEmits<{ (event: 'opened'): void }>();

const { t } = useI18n();
const sourceStore = useSourceStore();
const backgroundAudioStore = useBackgroundAudioStore();
const sessionStore = useSessionStore();
const toast = useToast();
const currentSession = computed(() => sessionStore.byWindowId(props.windowId));
const actualSource = computed(() => currentSession.value
  ? resolveActualOutputSource(currentSession.value, sourceStore.findById) : null);
const controlsDisabled = computed(() => !currentSession.value?.player_online);
const currentSourceIsPlaying = computed(() => isConfirmedPlayback(currentSession.value));
const requestedSourceStatus = computed(() => {
  if (controlsDisabled.value) return '请求源（播放器离线）';
  if (currentSession.value?.error_message) return '请求失败';
  if (currentSession.value?.pending_command) return '请求中';
  return '请求源（输出未确认）';
});

const filterValue = ref<SourceCategory>('all');
const currentFolderId = ref<number | null>(null);
const presentationMode = ref<'libreoffice' | 'slide_images' | 'pdf'>('libreoffice');
const fitText = ref(false);
const presentationOptions = [
  { label: 'LibreOffice 动态放映', value: 'libreoffice' },
  { label: '静态逐页图片（无动画／媒体）', value: 'slide_images' },
  { label: '静态 PDF（无动画／媒体）', value: 'pdf' },
];
const searchKeyword = ref('');
const expanded = ref(false);
const fileToUpload = ref<File | null>(null);
// 仅保存上传接口已明确返回的源；OPEN失败后显式重试不再重复上传。
const uploadedSource = ref<MediaSourceItem | null>(null);
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
    if (!inDirectory(source.folder_id, currentFolderId.value, sourceStore.folders, !!keyword)) return false;
    if (source.source_type === 'audio') return false;
    if (filterValue.value !== 'all' && sourceStore.resolveCategory(source.source_type) !== filterValue.value) {
      return false;
    }
    if (!keyword) return true;
    const hay = `${source.name} ${sourcePath(source, sourceStore.folders)}`.toLowerCase();
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
    await openWindowSource(source);
    toast.success(t('sourcePicker.openedOk', { name: source.name }), '实际播放状态以窗口输出报告为准');
    emit('opened');
  } catch (error) {
    toast.error(t('sourcePicker.openFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    switchingSourceId.value = null;
  }
}

/**
 * 普通选源和上传后打开共用文稿决策，PDF原源与非文稿不携带布局参数。
 * :param source: 服务端已经登记的媒体源。
 * :param requestedMode: 点击时选定的播放模式。
 * :param requestedFit: 点击时选定的文字布局。
 * :returns: OPEN请求完成，实际出画仍由会话观测确认。
 */
async function openWindowSource(source: MediaSourceItem, requestedMode = presentationMode.value,
  requestedFit = fitText.value): Promise<void> {
  const mode = source.source_type === 'ppt' && source.playback_mode !== 'pdf' ? requestedMode : undefined;
  await sessionStore.openSource(props.windowId, source.id, true, 0, mode, !!mode && requestedFit);
}

/** :param source: 列表元数据。:returns: 仅实际源与适配器/类型一致时标当前项。 */
function isCurrentSource(source: MediaSourceItem): boolean {
  return actualSource.value?.id === source.id;
}

/** :param source: 列表源。:returns: 只标记与实际输出不同的明确请求；受理不等于出画。 */
function isRequestedSource(source: MediaSourceItem): boolean {
  return currentSession.value?.source_id === source.id && !isCurrentSource(source);
}

/**
 * 新的文件选择开始新的上传意图，不复用此前已确认源。
 * :param event: 原生文件输入的change事件。
 * :returns: 更新所选文件并清除旧上传源缓存。
 */
function onFileSelect(event: Event): void {
  const target = event.target as HTMLInputElement;
  fileToUpload.value = target.files?.[0] ?? null;
  uploadedSource.value = null;
}

/** :returns: 明确选到新文件才清除旧上传源，取消选择保留当前恢复入口。 */
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
    uploadedSource.value = null;
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
  if (controlsDisabled.value || uploading.value) return;
  if (!fileToUpload.value) {
    uploadError.value = t('sourcePicker.pickFileFirst');
    return;
  }
  uploading.value = true;
  uploadPhase.value = uploadedSource.value ? 'processing' : 'uploading';
  uploadProgress.value = 0;
  uploadError.value = '';
  // 上传在途的开关变化只影响后续点击，不改写本次已提交的意图。
  const requestedMode = presentationMode.value;
  const requestedFit = fitText.value;
  try {
    const result = uploadedSource.value ?? await sourceStore.upload(fileToUpload.value, {
      name: fileDisplayName.value.trim() || undefined,
      isTemporary: true,
      folderId: null,
      onProgress: handleUploadProgress,
    });
    uploadedSource.value = result;
    if (result.source_type === 'audio') {
      await backgroundAudioStore.playSource(result.id);
      toast.success(t('backgroundAudio.playRequested', { name: result.name }));
    } else {
      await openWindowSource(result, requestedMode, requestedFit);
      toast.success(t('sourcePicker.uploadedOpened'), t('sourcePicker.sourceNameDetail', { name: result.name }));
      emit('opened');
    }
    uploadedSource.value = null;
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
      folderId: null,
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
  <a-card class="source-picker" :title="t('sourcePicker.title')" size="small">
    <template #extra>
      <span class="source-picker__count">{{ totalLabel }}</span>
    </template>

    <a-input
      v-model:value="searchKeyword"
      :placeholder="t('sourcePicker.searchPlaceholder')"
      :aria-label="t('sourcePicker.searchAria')"
      allow-clear
    >
      <template #prefix>
        <FIcon name="search_20_regular" />
      </template>
    </a-input>
    <DirectoryNavigator v-model:folder-id="currentFolderId" :folders="sourceStore.folders" compact />

    <div class="source-picker__filter-scroll">
      <a-tabs v-tabs-keyboard v-model:activeKey="filterValue" type="card" :aria-label="t('sourcePicker.filterAria')">
        <a-tab-pane key="all" :tab="t('sourcePicker.filter.all')" />
        <a-tab-pane key="ppt" :tab="t('sourcePicker.filter.ppt')" />
        <a-tab-pane key="video" :tab="t('sourcePicker.filter.video')" />
        <a-tab-pane key="image" :tab="t('sourcePicker.filter.image')" />
        <a-tab-pane key="web" :tab="t('sourcePicker.filter.web')" />
        <a-tab-pane key="stream" :tab="t('sourcePicker.filter.stream')" />
      </a-tabs>
    </div>

    <a-form-item v-if="filterValue === 'ppt'" label="文稿打开方式">
      <a-select v-select-aria-label="'文稿打开方式'" v-model:value="presentationMode" :options="presentationOptions" aria-label="文稿打开方式" />
    </a-form-item>

    <a-form-item v-if="filterValue === 'ppt'" label="文字适配（不修改原件）">
      <a-switch v-model:checked="fitText" aria-label="文字适配（不修改原件）" />
    </a-form-item>

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
            <p class="source-picker__sub">{{ sourcePath(source, sourceStore.folders) }}</p>
            <p class="source-picker__sub">
              <a-tag v-if="isCurrentSource(source)" :color="tagColor(currentSourceIsPlaying ? 'success' : 'warning')"  >
                当前输出{{ controlsDisabled ? '（离线，最后报告）' : '' }}
              </a-tag>
              <a-tag v-if="isRequestedSource(source)" color="warning"  >
                {{ requestedSourceStatus }}
              </a-tag>
              <a-tag :color="tagColor(source.is_available ? 'default' : source.preparation_state === 'queued' || source.preparation_state === 'running' ? 'warning' : 'error')"  >
                {{ source.is_available ? sourceCategoryLabel(source) : sourceAvailabilityLabel(source) }}
              </a-tag>
            </p>
          </div>
          <a-spin v-if="switchingSourceId === source.id" size="large" />
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
        <p class="source-picker__pdf-hint">此处上传默认保存到 / 根目录；不会跟随浏览目录。</p>
        <a-form-item :label="t('sourcePicker.file')" required>
          <div class="source-picker__file-row">
            <label class="source-picker__file">
              <input ref="fileInputRef" type="file" class="visually-hidden" :disabled="uploading"
                :accept="acceptedFileTypes.join(',')"
                @change="onFileSelect" />
              <span>{{ fileToUpload ? fileToUpload.name : t('sourcePicker.noFile') }}</span>
              <a-button @click.stop="triggerFilePicker">
                {{ t('sourcePicker.chooseFile') }}
              </a-button>
            </label>
            <p class="source-picker__pdf-hint">{{ t('sourcePicker.pdfSuggestion') }}</p>
          </div>
        </a-form-item>
        <a-form-item :label="t('sourcePicker.displayName')">
          <a-input v-model:value="fileDisplayName" :disabled="!!uploadedSource" :placeholder="t('sourcePicker.displayNamePlaceholder')" />
        </a-form-item>
        <p v-if="uploadedSource" class="source-picker__upload-state">
          文件已上传为“{{ uploadedSource.name }}”；重试仅打开该源，重新选文件才会再次上传。
        </p>
        <p v-if="uploadError" class="source-picker__upload-error">{{ uploadError }}</p>
        <template v-if="uploading">
          <a-progress type="line" :percent="uploadProgress" :show-info="false" />
          <p class="source-picker__upload-state">{{ uploadStatusText }}</p>
        </template>

        <div class="source-picker__upload-actions">
          <a-button :disabled="uploading || !fileToUpload || !!uploadedSource" :loading="uploading" @click="uploadOnly">
            {{ t('sourcePicker.uploadOnly') }}
          </a-button>
          <a-button type="primary" :disabled="controlsDisabled || uploading || !fileToUpload" :loading="uploading"
            @click="uploadAndOpen">
            {{ uploadedSource ? '重试打开已上传源' : t('sourcePicker.uploadOpen') }}
          </a-button>
        </div>
      </div>
    </details>

    <p v-if="uploading && !expanded" class="source-picker__upload-state">
      <a-spin size="large" /> {{ uploadStatusText }}
    </p>
  </a-card>
</template>

<style scoped src="./SourcePicker.css"></style>
