<script setup lang="ts">
/**
 * 大屏信号映射草稿：用实体墙面比例预览左右/全屏，不为未知帧提供假“应用成功”。
 */
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { NAlert, NButton, NCard, NFormItem, NInput, NRadio, NRadioGroup, NSelect } from 'naive-ui';

import { useToast } from '@/composables/useToast';
import { api, type VideoWallInputKind, type VideoWallLayoutItem, type VideoWallMappingItem } from '@/services/api';
import { useRuntimeStore } from '@/stores/runtime';

type InputChoice = VideoWallInputKind | 'none';
type LayoutKind = 'fullscreen' | 'split';

const { t } = useI18n();
const toast = useToast();
const runtime = useRuntimeStore();
const name = ref(t('videoWall.defaultName'));
const layoutKind = ref<LayoutKind>('split');
const fullInput = ref<InputChoice>('window_1');
const leftInput = ref<InputChoice>('window_1');
const rightInput = ref<InputChoice>('window_2');
const fullIp = ref('');
const leftIp = ref('');
const rightIp = ref('');
const savedSignature = ref('');
const loading = ref(false);
const saving = ref(false);
const applying = ref(false);
const errorMessage = ref('');

const inputOptions = computed(() => [
  { label: t('videoWall.none'), value: 'none' },
  { label: t('videoWall.window1'), value: 'window_1' },
  { label: t('videoWall.window2'), value: 'window_2' },
  { label: t('videoWall.laptop'), value: 'laptop' },
  { label: t('videoWall.ipStream'), value: 'ip_stream' },
]);
const fullOptions = computed(() => inputOptions.value.filter((option) => option.value !== 'none'));

function input(kind: InputChoice, ipAddress: string): VideoWallMappingItem['input'] {
  return { kind: kind as VideoWallInputKind, ip_address: kind === 'ip_stream' ? ipAddress.trim() : '' };
}

const mappings = computed<VideoWallMappingItem[]>(() => {
  if (layoutKind.value === 'fullscreen') {
    return [{ region: 'fullscreen', input: input(fullInput.value, fullIp.value) }];
  }
  const result: VideoWallMappingItem[] = [];
  if (leftInput.value !== 'none') result.push({ region: 'left', input: input(leftInput.value, leftIp.value) });
  if (rightInput.value !== 'none') result.push({ region: 'right', input: input(rightInput.value, rightIp.value) });
  return result;
});

const currentSignature = computed(() => JSON.stringify({ name: name.value.trim(), mappings: mappings.value }));
const isSaved = computed(() => savedSignature.value !== '' && savedSignature.value === currentSignature.value);
const matchesCapturedPreset = computed(() =>
  (layoutKind.value === 'fullscreen' && fullInput.value === 'window_1') ||
  (layoutKind.value === 'split' && leftInput.value === 'window_1' && rightInput.value === 'window_2'));

function inputLabel(kind: InputChoice, ipAddress: string): string {
  const label = kind === 'none' ? t('videoWall.none')
    : kind === 'window_1' ? t('videoWall.window1')
      : kind === 'window_2' ? t('videoWall.window2')
        : kind === 'laptop' ? t('videoWall.laptop') : t('videoWall.ipStream');
  return kind === 'ip_stream' && ipAddress.trim() ? `${label} · ${ipAddress.trim()}` : label;
}

function restoreDraft(draft: VideoWallLayoutItem): void {
  name.value = draft.name;
  const full = draft.mappings.find((mapping) => mapping.region === 'fullscreen');
  layoutKind.value = full ? 'fullscreen' : 'split';
  if (full) {
    fullInput.value = full.input.kind;
    fullIp.value = full.input.ip_address ?? '';
  }
  const left = draft.mappings.find((mapping) => mapping.region === 'left');
  const right = draft.mappings.find((mapping) => mapping.region === 'right');
  leftInput.value = left?.input.kind ?? 'none';
  rightInput.value = right?.input.kind ?? 'none';
  leftIp.value = left?.input.ip_address ?? '';
  rightIp.value = right?.input.ip_address ?? '';
  savedSignature.value = currentSignature.value;
}

async function load(): Promise<void> {
  loading.value = true;
  errorMessage.value = '';
  try {
    const response = await api.getVideoWallLayout();
    if (response.layout.draft) restoreDraft(response.layout.draft);
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('videoWall.loadFail');
  } finally {
    loading.value = false;
  }
}

async function save(): Promise<void> {
  if (!name.value.trim()) { errorMessage.value = t('videoWall.invalidName'); return; }
  if (!mappings.value.length) { errorMessage.value = t('videoWall.emptyRegion'); return; }
  saving.value = true;
  errorMessage.value = '';
  try {
    const response = await api.saveVideoWallLayout({ name: name.value.trim(), mappings: mappings.value });
    if (response.layout.draft) restoreDraft(response.layout.draft);
    toast.success(t('videoWall.saved'));
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('videoWall.saveFail');
  } finally {
    saving.value = false;
  }
}

async function apply(): Promise<void> {
  if (!isSaved.value || !matchesCapturedPreset.value) return;
  applying.value = true;
  errorMessage.value = '';
  try {
    await runtime.applySavedVideoWallLayout();
    toast.success(t('videoWall.applied'));
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('videoWall.applyFail');
  } finally {
    applying.value = false;
  }
}

onMounted(() => { void load(); });
</script>

<template>
  <n-card :title="t('videoWall.title')" class="wall-map">
    <p class="wall-map__description">{{ t('videoWall.description') }}</p>
    <n-form-item :label="t('videoWall.name')">
      <n-input v-model:value="name" :placeholder="t('videoWall.namePlaceholder')" />
    </n-form-item>
    <n-form-item :label="t('videoWall.layout')">
      <n-radio-group v-model:value="layoutKind">
        <n-radio value="fullscreen">{{ t('videoWall.fullscreen') }}</n-radio>
        <n-radio value="split">{{ t('videoWall.split') }}</n-radio>
      </n-radio-group>
    </n-form-item>

    <div class="wall-map__screen" :class="{ 'wall-map__screen--split': layoutKind === 'split' }"
      :aria-label="t('videoWall.title')">
      <div v-if="layoutKind === 'fullscreen'" class="wall-map__region">
        <span class="wall-map__region-label">{{ t('videoWall.fullscreen') }}</span>
        <strong>{{ inputLabel(fullInput, fullIp) }}</strong>
      </div>
      <template v-else>
        <div class="wall-map__region">
          <span class="wall-map__region-label">{{ t('videoWall.left') }}</span>
          <strong>{{ inputLabel(leftInput, leftIp) }}</strong>
        </div>
        <div class="wall-map__region">
          <span class="wall-map__region-label">{{ t('videoWall.right') }}</span>
          <strong>{{ inputLabel(rightInput, rightIp) }}</strong>
        </div>
      </template>
    </div>

    <div class="wall-map__inputs">
      <template v-if="layoutKind === 'fullscreen'">
        <n-form-item :label="t('videoWall.fullscreen')">
          <n-select v-model:value="fullInput" :options="fullOptions" />
        </n-form-item>
        <n-input v-if="fullInput === 'ip_stream'" v-model:value="fullIp" :placeholder="t('videoWall.ipAddress')" />
      </template>
      <template v-else>
        <div>
          <n-form-item :label="t('videoWall.left')"><n-select v-model:value="leftInput" :options="inputOptions" /></n-form-item>
          <n-input v-if="leftInput === 'ip_stream'" v-model:value="leftIp" :placeholder="t('videoWall.ipAddress')" />
        </div>
        <div>
          <n-form-item :label="t('videoWall.right')"><n-select v-model:value="rightInput" :options="inputOptions" /></n-form-item>
          <n-input v-if="rightInput === 'ip_stream'" v-model:value="rightIp" :placeholder="t('videoWall.ipAddress')" />
        </div>
      </template>
    </div>

    <n-alert :type="matchesCapturedPreset ? 'info' : 'warning'" class="wall-map__notice">
      {{ matchesCapturedPreset ? t('videoWall.presetReady') : t('videoWall.pendingFrames') }}
    </n-alert>
    <n-alert v-if="errorMessage" type="error" class="wall-map__notice">{{ errorMessage }}</n-alert>
    <div class="wall-map__actions">
      <n-button :loading="saving" :disabled="loading" @click="save">{{ t('videoWall.save') }}</n-button>
      <n-button type="primary" :loading="applying" :disabled="!isSaved || !matchesCapturedPreset || loading"
        @click="apply">{{ t('videoWall.apply') }}</n-button>
    </div>
  </n-card>
</template>

<style scoped>
.wall-map__description {
  margin: 0 0 var(--spacingVerticalM);
  color: var(--colorNeutralForeground2);
}
.wall-map__screen {
  display: grid;
  min-height: 8rem;
  margin-bottom: var(--spacingVerticalM);
  border: 1px solid var(--colorNeutralStroke1);
  border-radius: var(--borderRadiusMedium);
  background: var(--colorNeutralBackground2);
}
.wall-map__screen--split { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.wall-map__region {
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: var(--spacingVerticalXS);
  min-width: 0;
  padding: var(--spacingVerticalM) var(--spacingHorizontalM);
  text-align: center;
}
.wall-map__region + .wall-map__region { border-left: 1px solid var(--colorNeutralStroke1); }
.wall-map__region-label { color: var(--colorNeutralForeground3); font-size: var(--fontSizeBase200); }
.wall-map__region strong { overflow-wrap: anywhere; color: var(--colorBrandForeground1); }
.wall-map__inputs { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--spacingHorizontalM); }
.wall-map__inputs > :only-child { grid-column: 1 / -1; }
.wall-map__notice { margin-top: var(--spacingVerticalM); }
.wall-map__actions { display: flex; flex-wrap: wrap; gap: var(--spacingHorizontalS); margin-top: var(--spacingVerticalM); }
@media (max-width: 640px) {
  .wall-map__inputs { grid-template-columns: 1fr; }
  .wall-map__inputs > :only-child { grid-column: auto; }
  .wall-map__actions :deep(.n-button) { flex: 1 1 100%; }
}
</style>
