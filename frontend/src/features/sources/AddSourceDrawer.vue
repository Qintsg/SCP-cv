<script setup lang="ts">
/**
 * 添加源 Drawer / Sheet：上传文件、网页与直播流采用同一文件夹语义。
 * 文件上传统一保存为可管理媒体源；临时上传只在显示控制页“上传并打开”使用。
 */
import { tabsKeyboard as vTabsKeyboard } from '@/design-system/tabsKeyboard';
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';

import { computed, ref, type CSSProperties } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ControlDrawer from '@/design-system/ControlDrawer.vue';
import { FormItem as AFormItem } from 'ant-design-vue/es/form';
import AInput from 'ant-design-vue/es/input';
import AProgress from 'ant-design-vue/es/progress';
import ASelect from 'ant-design-vue/es/select';
import ASwitch from 'ant-design-vue/es/switch';
import ATabs from 'ant-design-vue/es/tabs';
import { TabPane as ATabPane } from 'ant-design-vue/es/tabs';

import FIcon from '@/design-system/FIcon.vue';
import { useToast } from '@/composables/useToast';
import { useWindowSizeClass } from '@/composables/useWindowSizeClass';
import { useSourceStore } from '@/stores/sources';
import type { StreamSourceCreate } from '@/services/api';
import { pickUploadFile } from '@/platform/files';
import { getNativePlatformAdapter } from '@/platform/native';
import { directoryPath } from '../directories/directoryPath';

const props = defineProps<{ open: boolean; folderId?: number | null }>();
const emit = defineEmits<{
  (event: 'update:open', value: boolean): void;
  (event: 'added'): void;
}>();

const { t } = useI18n();
const sourceStore = useSourceStore();
const destinationPath = computed(() => directoryPath(props.folderId ?? null, sourceStore.folders));
const toast = useToast();
const { isCompact } = useWindowSizeClass();
/** :returns: 手机确认/取消使用既有44px触控令牌，桌面保持紧凑尺寸。 */
const footerButtonStyle = computed<CSSProperties | undefined>(() => isCompact.value
  ? { minHeight: 'var(--touch-target-min, 44px)' } : undefined);

type TabId = 'file' | 'web' | 'stream';

const acceptedFileTypes = [
  '.pdf', '.pptx', '.ppt', '.pps', '.ppsx', '.pptm', '.ppsm', '.pot', '.potx', '.potm', '.odp',
  '.mp4', '.mkv', '.avi', '.mov', '.wmv', '.flv', '.webm', '.m4v',
  '.mp3', '.wav', '.flac', '.aac', '.ogg', '.wma', '.m4a',
  '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.webp', '.svg',
] as const;

const activeTab = ref<TabId>('file');

const fileToUpload = ref<File | null>(null);
const fileDisplayName = ref('');
const filePreheatEnabled = ref(true);
const webUrl = ref('');
const webName = ref('');
const webPreheatEnabled = ref(true);
const streamType = ref<StreamSourceCreate['source_type']>('rtsp_stream');
const streamUrl = ref('');
const streamName = ref('');
const streamPreheatEnabled = ref(false);
const uploadProgress = ref(0);
const uploading = ref(false);
const uploadPhase = ref<'uploading' | 'processing'>('uploading');
const errorMessage = ref('');

const fileLabel = computed(() => fileToUpload.value?.name ?? t('sources.add.noFile'));
const fileSize = computed(() => {
  if (!fileToUpload.value) return '';
  const bytes = fileToUpload.value.size;
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unitIdx = 0;
  while (value >= 1024 && unitIdx < units.length - 1) {
    value /= 1024;
    unitIdx += 1;
  }
  return `${value.toFixed(unitIdx === 0 ? 0 : 1)} ${units[unitIdx]}`;
});

const isOpen = computed({
  get: () => props.open,
  set: (value: boolean) => emit('update:open', value),
});

const uploadStatusText = computed(() =>
  uploadPhase.value === 'processing'
    ? t('sources.add.processing')
    : t('sources.add.uploading'),
);
const streamProtocolOptions = computed(() => [
  { label: t('sources.add.streamRtsp'), value: 'rtsp_stream' },
  { label: t('sources.add.streamSrt'), value: 'srt_stream' },
  { label: t('sources.add.streamCustom'), value: 'custom_stream' },
]);
const streamUrlPlaceholder = computed(() => streamType.value === 'srt_stream'
  ? 'srt://192.168.5.194:8890?streamid=read:demo'
  : streamType.value === 'custom_stream' ? 'http://192.168.5.10:8080/live.ts' : 'rtsp://192.168.5.194:8554/demo');

function handleUploadProgress(percent: number): void {
  uploadProgress.value = percent >= 99 ? 100 : percent;
  uploadPhase.value = percent >= 99 ? 'processing' : 'uploading';
}

function close(): void {
  emit('update:open', false);
}

function reset(): void {
  fileToUpload.value = null;
  fileDisplayName.value = '';
  filePreheatEnabled.value = true;
  webUrl.value = '';
  webName.value = '';
  webPreheatEnabled.value = true;
  streamType.value = 'rtsp_stream';
  streamUrl.value = '';
  streamName.value = '';
  streamPreheatEnabled.value = false;
  uploadProgress.value = 0;
  uploading.value = false;
  uploadPhase.value = 'uploading';
  errorMessage.value = '';
  activeTab.value = 'file';
}

const fileInputEl = ref<HTMLInputElement | null>(null);

function onFileSelect(event: Event): void {
  const target = event.target as HTMLInputElement;
  fileToUpload.value = target.files?.[0] ?? null;
}

async function triggerFilePicker(): Promise<void> {
  const adapter = getNativePlatformAdapter();
  if (!adapter) {
    fileInputEl.value?.click();
    return;
  }
  try {
    const selected = await pickUploadFile(adapter, acceptedFileTypes);
    if (!selected) return;
    fileToUpload.value = new File([selected.data], selected.name, { type: selected.mimeType });
    errorMessage.value = '';
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('sources.add.pickFileFirst');
  }
}

async function uploadFile(): Promise<void> {
  if (!fileToUpload.value) {
    errorMessage.value = t('sources.add.pickFileFirst');
    return;
  }
  uploading.value = true;
  uploadPhase.value = 'uploading';
  uploadProgress.value = 0;
  errorMessage.value = '';
  try {
    const result = await sourceStore.upload(fileToUpload.value, {
      name: fileDisplayName.value.trim() || undefined,
      isTemporary: false,
      preheatEnabled: filePreheatEnabled.value,
      folderId: props.folderId,
      onProgress: handleUploadProgress,
    });
    if (result.source_type === 'ppt' && result.preparation_state === 'queued') {
      toast.info(t('sources.add.pptQueued'), t('sources.add.pptQueuedDetail', { name: result.name }));
    } else {
      toast.success(t('sources.add.uploadedSaved'), t('sources.add.sourceNameDetail', { name: result.name }));
    }
    emit('added');
    reset();
    close();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('sources.add.uploadFail');
  } finally {
    uploading.value = false;
    uploadPhase.value = 'uploading';
  }
}

async function addWebSource(): Promise<void> {
  const url = webUrl.value.trim();
  if (!url) {
    errorMessage.value = t('sources.add.urlRequired');
    return;
  }
  uploading.value = true;
  errorMessage.value = '';
  try {
    await sourceStore.addWebSource(url, webName.value.trim() || undefined, webPreheatEnabled.value, props.folderId ?? null);
    toast.success(t('sources.add.webAddedOk'));
    emit('added');
    reset();
    close();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('sources.add.webAddFail');
  } finally {
    uploading.value = false;
  }
}

async function addStreamSource(): Promise<void> {
  const url = streamUrl.value.trim();
  if (!url) {
    errorMessage.value = t('sources.add.streamUrlRequired');
    return;
  }
  uploading.value = true;
  errorMessage.value = '';
  try {
    await sourceStore.addStreamSource({
      source_type: streamType.value,
      url,
      name: streamName.value.trim() || undefined,
      folder_id: props.folderId ?? null,
      preheat_enabled: streamPreheatEnabled.value,
    });
    toast.info(t('sources.add.streamAddedOk'), t('sources.add.streamAddedDetail'));
    emit('added');
    reset();
    close();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('sources.add.streamAddFail');
  } finally {
    uploading.value = false;
  }
}
</script>

<template>
  <!-- 使用统一Ant弹层层级，避免Select下拉层被过高的抽屉遮挡。 -->
  <ControlDrawer role="dialog" aria-modal="true" :aria-label="t('sources.add.title')" :title="t('sources.add.title')" v-model:open="isOpen" width="min(480px, 100vw)" placement="right">

      <p class="source-add-destination">保存位置：<strong>{{ destinationPath }}</strong></p>
      <p class="add-source__desc">{{ t('sources.add.desc') }}</p>

      <a-tabs v-tabs-keyboard v-model:activeKey="activeTab" type="line" :aria-label="t('sources.add.typeAria')">
        <a-tab-pane key="file" :tab="t('sources.add.tabFile')">
          <a-form-item :label="t('sources.add.file')" required>
            <div class="add-source__file-row">
              <label class="add-source__file">
                <input ref="fileInputEl" type="file" class="visually-hidden" :disabled="uploading"
                  :accept="acceptedFileTypes.join(',')"
                  @change="onFileSelect" />
                <span class="add-source__file-info">
                  <FIcon name="arrow_upload_24_regular" />
                  <span>{{ fileLabel }}</span>
                  <span v-if="fileSize" class="add-source__file-size">{{ fileSize }}</span>
                </span>
                <a-button @click.stop="triggerFilePicker">{{ t('sources.add.chooseFile') }}</a-button>
              </label>
              <p class="add-source__pdf-hint">{{ t('sources.add.pdfSuggestion') }}</p>
            </div>
          </a-form-item>
          <a-form-item :label="t('sources.add.displayName')">
            <a-input v-model:value="fileDisplayName" :placeholder="t('sources.add.displayNamePlaceholder')" v-bind="{ 'aria-label': t('sources.add.displayName') }" />
          </a-form-item>
          <a-form-item :label="t('sources.add.preheat')">
            <a-switch v-model:checked="filePreheatEnabled" :aria-label="t('sources.add.preheat')">
              <template #checkedChildren>{{ t('sources.add.preheatSwitch') }}</template>
              <template #unCheckedChildren>{{ t('sources.add.preheatSwitchOff') }}</template>
            </a-switch>
          </a-form-item>

          <template v-if="uploading">
            <a-progress type="line" :percent="uploadProgress" :show-info="false" />
            <p class="add-source__upload-state">{{ uploadStatusText }}</p>
          </template>
        </a-tab-pane>

        <a-tab-pane key="web" :tab="t('sources.add.tabWeb')">
          <a-form-item :label="t('sources.add.url')" required>
            <a-input v-model:value="webUrl" placeholder="https://" v-bind="{ 'aria-label': t('sources.add.url') }" />
          </a-form-item>
          <a-form-item :label="t('sources.add.webName')">
            <a-input v-model:value="webName" :placeholder="t('sources.add.webNamePlaceholder')" v-bind="{ 'aria-label': t('sources.add.webName') }" />
          </a-form-item>
          <a-form-item :label="t('sources.add.preheat')">
            <a-switch v-model:checked="webPreheatEnabled" :aria-label="t('sources.add.preheat')">
              <template #checkedChildren>{{ t('sources.add.preheatSwitch') }}</template>
              <template #unCheckedChildren>{{ t('sources.add.preheatSwitchOff') }}</template>
            </a-switch>
          </a-form-item>
        </a-tab-pane>

        <a-tab-pane key="stream" :tab="t('sources.add.tabStream')">
          <a-form-item :label="t('sources.add.streamProtocol')" required>
            <a-select v-select-aria-label="t('sources.add.streamProtocol')" v-model:value="streamType" :options="streamProtocolOptions"
              :aria-label="t('sources.add.streamProtocol')" :disabled="uploading" />
          </a-form-item>
          <a-form-item :label="t('sources.add.streamUrl')" required>
            <a-input v-model:value="streamUrl" :placeholder="streamUrlPlaceholder"
              v-bind="{ 'aria-label': t('sources.add.streamUrl') }" :disabled="uploading" />
          </a-form-item>
          <a-form-item :label="t('sources.add.displayName')">
            <a-input v-model:value="streamName" :placeholder="t('sources.add.streamNamePlaceholder')"
              v-bind="{ 'aria-label': t('sources.add.displayName') }" :disabled="uploading" />
          </a-form-item>
          <a-form-item :label="t('sources.add.preheat')">
            <a-switch v-model:checked="streamPreheatEnabled" :disabled="uploading" aria-label="直播流预热" />
          </a-form-item>
          <a-alert type="info" :show-icon="true"><template #description>{{ t('sources.add.streamHint') }}</template></a-alert>
        </a-tab-pane>
      </a-tabs>

      <a-alert v-if="errorMessage" type="error" :message="t('sources.add.cantComplete')"><template #description>
        {{ errorMessage }}
      </template></a-alert>

      <template #footer>
        <div class="add-source__actions">
          <a-button :style="footerButtonStyle" :disabled="uploading" @click="close">{{ t('common.cancel') }}</a-button>
          <template v-if="activeTab === 'file'">
            <a-button :style="footerButtonStyle" type="primary" :disabled="uploading || !fileToUpload" :loading="uploading"
              @click="uploadFile">
              {{ t('sources.add.uploadSave') }}
            </a-button>
          </template>
          <a-button v-else-if="activeTab === 'web'" :style="footerButtonStyle" type="primary" :disabled="uploading" :loading="uploading" @click="addWebSource">
            {{ t('sources.add.addWeb') }}
          </a-button>
          <a-button v-else :style="footerButtonStyle" type="primary" :disabled="uploading" :loading="uploading" @click="addStreamSource">
            {{ t('sources.add.addStream') }}
          </a-button>
        </div>
      </template>

  </ControlDrawer>
</template>

<style scoped>
.source-add-destination { padding:var(--spacingVerticalS) var(--spacingHorizontalM); margin:0 0 var(--spacingVerticalM); border-radius:var(--borderRadiusMedium); background:var(--colorBrandBackground2); color:var(--colorNeutralForeground2); overflow-wrap:anywhere; }
.source-add-destination strong { font-family:Consolas,monospace; color:var(--colorNeutralForeground1); }
.add-source__desc {
  margin: 0 0 var(--spacingVerticalM);
  color: var(--colorNeutralForeground2);
  font-size: var(--fontSizeBase200);
}

.add-source__file {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--spacingHorizontalM);
  padding: var(--spacingVerticalM);
  border: 1px dashed var(--colorNeutralStroke1);
  border-radius: var(--borderRadiusMedium);
  cursor: pointer;
  background: var(--colorNeutralBackground2);
}

.add-source__file:hover {
  border-color: var(--colorBrandStroke1);
}

.add-source__file-row {
  display: flex;
  align-items: center;
  gap: var(--spacingHorizontalM);
  width: 100%;
}

.add-source__file-row .add-source__file {
  flex: 1 1 auto;
  min-width: 0;
}

.add-source__pdf-hint {
  flex: 0 0 auto;
  margin: 0;
  max-width: 220px;
  color: var(--colorBrandForeground1);
  font-size: var(--fontSizeBase200);
  line-height: var(--lineHeightBase200);
}

.add-source__file-info {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalS);
  color: var(--colorNeutralForeground2);
  flex: 1 1 auto;
  min-width: 0;
}

.add-source__file-size {
  color: var(--colorNeutralForeground3);
  font-size: var(--fontSizeBase200);
}

.add-source__actions {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalS);
  justify-content: flex-end;
  width: 100%;
}

.add-source__upload-state {
  margin: 0;
  color: var(--colorNeutralForeground2);
  font-size: var(--fontSizeBase200);
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@media (max-width: 767px) {
  .add-source__file {
    flex-direction: column;
    align-items: stretch;
  }

  .add-source__file-row {
    flex-direction: column;
    align-items: stretch;
  }

  .add-source__pdf-hint {
    max-width: none;
  }
}
</style>
