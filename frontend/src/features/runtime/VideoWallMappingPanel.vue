<script setup lang="ts">
/**
 * 紧凑大屏交互块：输入直接放在单屏或左右区域内，选择不自动下发。
 * 服务端能力、草稿版本与已发送意图分开，不以网络写出冒充画面确认。
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { NAlert, NButton, NCard, NFormItem, NInput, NRadioButton, NRadioGroup, NSelect } from 'naive-ui';

import { useToast } from '@/composables/useToast';
import FIcon from '@/design-system/FIcon.vue';
import { api, type VideoWallInputKind, type VideoWallLayoutItem, type VideoWallLayoutState, type VideoWallMappingItem } from '@/services/api';
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
const serverState = ref<VideoWallLayoutState | null>(null);
let alive = true;
const busy = computed(() => loading.value || saving.value || applying.value);

const inputOptions = computed(() => [
  { label: t('videoWall.window1'), value: 'window_1' },
  { label: t('videoWall.window2'), value: 'window_2' },
  { label: t('videoWall.ip81'), value: 'laptop' },
  { label: t('videoWall.none'), value: 'none' },
  { label: t('videoWall.ipStream'), value: 'ip_stream' },
]);
const fullOptions = computed(() => inputOptions.value.filter((option) => option.value !== 'none'));

/** :param kind: 逻辑输入。:param ipAddress: 自定义地址。:returns: 既有布局合同。 */
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
const canApply = computed(() => isSaved.value && serverState.value?.draft?.can_apply === true);
const capabilityMessage = computed(() => !isSaved.value ? t('videoWall.unsaved')
  : canApply.value ? t('videoWall.presetReady') : serverState.value?.draft?.unavailable_reason || t('videoWall.pendingFrames'));
const dispatchLabel = computed(() => {
  const status = serverState.value?.dispatch_status;
  return status === 'simulated' ? t('videoWall.statusSimulated')
    : status === 'sent_unconfirmed' ? t('videoWall.statusSent')
      : status === 'uncertain' ? t('videoWall.statusUncertain')
        : status === 'dispatching' ? t('videoWall.statusSending') : t('videoWall.statusNone');
});
const appliedLabel = computed(() => serverState.value?.applied?.mappings
  .map((mapping) => `${mapping.region === 'fullscreen' ? t('videoWall.fullscreen')
    : mapping.region === 'left' ? t('videoWall.left') : t('videoWall.right')}：${inputLabel(mapping.input.kind, mapping.input.ip_address || '')}`)
  .join(' / ') || t('videoWall.noApplied'));

/** :param kind: 逻辑输入。:param ipAddress: 自定义地址。:returns: 发送记录的完整输入名称。 */
function inputLabel(kind: InputChoice, ipAddress: string): string {
  const label = kind === 'none' ? t('videoWall.none')
    : kind === 'window_1' ? t('videoWall.window1')
      : kind === 'window_2' ? t('videoWall.window2')
        : kind === 'laptop' ? t('videoWall.laptop') : t('videoWall.ipStream');
  return kind === 'ip_stream' && ipAddress.trim() ? `${label} · ${ipAddress.trim()}` : label;
}

/** :param draft: 服务端草稿。:returns: 恢复当前布局；另一布局的未提交选择不清空。 */
function restoreDraft(draft: VideoWallLayoutItem): void {
  name.value = draft.name;
  const full = draft.mappings.find((mapping) => mapping.region === 'fullscreen');
  layoutKind.value = full ? 'fullscreen' : 'split';
  if (full) {
    fullInput.value = full.input.kind;
    fullIp.value = full.input.ip_address ?? '';
  }
  else {
    const left = draft.mappings.find((mapping) => mapping.region === 'left');
    const right = draft.mappings.find((mapping) => mapping.region === 'right');
    leftInput.value = left?.input.kind ?? 'none';
    rightInput.value = right?.input.kind ?? 'none';
    leftIp.value = left?.input.ip_address ?? '';
    rightIp.value = right?.input.ip_address ?? '';
  }
  savedSignature.value = currentSignature.value;
}

/** :returns: 读取草稿和发送状态，不改变实体墙面。 */
async function load(): Promise<void> {
  const generation = runtime.connectionGeneration;
  loading.value = true;
  errorMessage.value = '';
  try {
    const response = await api.getVideoWallLayout();
    if (!alive || generation !== runtime.connectionGeneration) return;
    serverState.value = response.layout;
    if (response.layout.draft) restoreDraft(response.layout.draft);
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('videoWall.loadFail');
  } finally {
    loading.value = false;
  }
}

/** :returns: 保存当前选择及版本，不自动应用。 */
async function save(): Promise<void> {
  if (busy.value) return;
  if (!name.value.trim()) { errorMessage.value = t('videoWall.invalidName'); return; }
  if (!mappings.value.length) { errorMessage.value = t('videoWall.emptyRegion'); return; }
  saving.value = true;
  const submittedSignature = currentSignature.value;
  const generation = runtime.connectionGeneration;
  errorMessage.value = '';
  try {
    const response = await api.saveVideoWallLayout({ name: name.value.trim(), mappings: mappings.value });
    if (!alive || generation !== runtime.connectionGeneration) return;
    serverState.value = response.layout;
    if (response.layout.draft && submittedSignature === currentSignature.value) restoreDraft(response.layout.draft);
    else savedSignature.value = submittedSignature;
    toast.success(t('videoWall.saved'));
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('videoWall.saveFail');
  } finally {
    saving.value = false;
  }
}

/** :returns: 显式应用已保存的版本，失败读取状态但不自动重发。 */
async function apply(): Promise<void> {
  if (busy.value || !canApply.value || !serverState.value) return;
  const generation = runtime.connectionGeneration;
  applying.value = true;
  errorMessage.value = '';
  try {
    const state = await runtime.applySavedVideoWallLayout(serverState.value.draft_revision);
    if (!alive || generation !== runtime.connectionGeneration) return;
    serverState.value = state;
    toast.success(t('videoWall.applied'));
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('videoWall.applyFail');
    try {
      const response = await api.getVideoWallLayout();
      if (alive && generation === runtime.connectionGeneration) {
        serverState.value = response.layout;
        // 另一客户端更新草稿时，不让旧表单借用新草稿的可应用能力。
        if (response.layout.draft && JSON.stringify({ name: response.layout.draft.name,
          mappings: response.layout.draft.mappings }) !== currentSignature.value) savedSignature.value = '';
      }
    } catch { /* 保留原发送错误，不猜测墙面已经恢复。 */ }
  } finally {
    applying.value = false;
  }
}

onMounted(() => { void load(); });
onBeforeUnmount(() => { alive = false; });
</script>

<template>
  <n-card class="wall-map">
    <template #header>
      <span class="wall-map__title">
        <span class="wall-map__title-icon"><FIcon name="desktop_24_regular" :size="20" /></span>
        {{ t('videoWall.title') }}
      </span>
    </template>
    <template #header-extra>
      <span class="wall-map__draft-state" :class="{ 'wall-map__draft-state--saved': isSaved && !loading }"
        role="status" data-wall-draft>
        <FIcon v-if="isSaved && !loading" name="checkmark_16_filled" :size="14" />
        <span v-else class="wall-map__state-dot" aria-hidden="true" />
        {{ loading ? t('videoWall.loading') : isSaved ? t('videoWall.draftSaved') : t('videoWall.draftEdited') }}
      </span>
    </template>
    <div class="wall-map__toolbar">
      <n-radio-group v-model:value="layoutKind" :disabled="busy" :aria-label="t('videoWall.layout')">
        <n-radio-button value="fullscreen">
          <span class="wall-map__layout-icon" aria-hidden="true"><i /></span>{{ t('videoWall.singleScreen') }}
        </n-radio-button>
        <n-radio-button value="split">
          <span class="wall-map__layout-icon" aria-hidden="true"><i /><i /></span>{{ t('videoWall.doubleScreen') }}
        </n-radio-button>
      </n-radio-group>
    </div>

    <div class="wall-map__screen" :class="{ 'wall-map__screen--split': layoutKind === 'split' }"
      :aria-label="t('videoWall.preview')">
      <div v-if="layoutKind === 'fullscreen'" class="wall-map__region">
        <span class="wall-map__region-label">{{ t('videoWall.fullscreen') }}</span>
        <n-select v-model:value="fullInput" :disabled="busy" :options="fullOptions" :virtual-scroll="false"
          :aria-label="t('videoWall.fullscreen') + t('videoWall.input')" />
        <n-input v-if="fullInput === 'ip_stream'" v-model:value="fullIp" :disabled="busy"
          :input-props="{ 'aria-label': t('videoWall.fullscreen') + t('videoWall.ipAddress') }" :placeholder="t('videoWall.ipAddress')" />
      </div>
      <template v-else>
        <div class="wall-map__region" :class="{ 'wall-map__region--empty': leftInput === 'none' }">
          <span class="wall-map__region-label">{{ t('videoWall.left') }}</span>
          <n-select v-model:value="leftInput" :disabled="busy" :options="inputOptions" :virtual-scroll="false"
            :aria-label="t('videoWall.left') + t('videoWall.input')" />
          <n-input v-if="leftInput === 'ip_stream'" v-model:value="leftIp" :disabled="busy"
            :input-props="{ 'aria-label': t('videoWall.left') + t('videoWall.ipAddress') }" :placeholder="t('videoWall.ipAddress')" />
        </div>
        <div class="wall-map__region" :class="{ 'wall-map__region--empty': rightInput === 'none' }">
          <span class="wall-map__region-label">{{ t('videoWall.right') }}</span>
          <n-select v-model:value="rightInput" :disabled="busy" :options="inputOptions" :virtual-scroll="false"
            :aria-label="t('videoWall.right') + t('videoWall.input')" />
          <n-input v-if="rightInput === 'ip_stream'" v-model:value="rightIp" :disabled="busy"
            :input-props="{ 'aria-label': t('videoWall.right') + t('videoWall.ipAddress') }" :placeholder="t('videoWall.ipAddress')" />
        </div>
      </template>
    </div>

    <p class="wall-map__capability" :class="{ 'wall-map__capability--unavailable': isSaved && !canApply }" aria-live="polite">
      {{ capabilityMessage }}
    </p>
    <n-alert v-if="errorMessage" type="error" class="wall-map__notice">{{ errorMessage }}</n-alert>
    <div class="wall-map__actions">
      <n-button :loading="saving" :disabled="busy" @click="save">{{ t('videoWall.save') }}</n-button>
      <n-button type="primary" :loading="applying" :disabled="!canApply || busy"
        @click="apply">
        {{ t('videoWall.apply') }}
        <FIcon class="wall-map__apply-icon" name="arrow_right_24_regular" :size="16" />
      </n-button>
    </div>
    <details class="wall-map__options">
      <summary><FIcon class="wall-map__chevron" name="chevron_right_20_regular" :size="16" />{{ t('videoWall.options') }}</summary>
      <n-form-item :label="t('videoWall.name')">
        <n-input v-model:value="name" :disabled="busy" :input-props="{ 'aria-label': t('videoWall.name') }" :placeholder="t('videoWall.namePlaceholder')" />
      </n-form-item>
      <p>{{ t('videoWall.ip81Hint') }}</p>
      <p>{{ t('videoWall.streamHint') }}</p>
    </details>
    <div v-if="serverState" class="wall-map__dispatch" :class="{
      'wall-map__dispatch--uncertain': serverState.dispatch_status === 'uncertain' || serverState.dispatch_status === 'sent_unconfirmed',
      'wall-map__dispatch--simulated': serverState.dispatch_status === 'simulated',
    }" data-wall-dispatch aria-live="polite">
      <span class="wall-map__dispatch-label"><span class="wall-map__state-dot" aria-hidden="true" />{{ dispatchLabel }}</span>
      <details v-if="serverState.applied || serverState.dispatch_detail">
        <summary><FIcon class="wall-map__chevron" name="chevron_right_20_regular" :size="14" />{{ t('videoWall.dispatchDetails') }}</summary>
        <p>{{ t('videoWall.lastApplied') }}：{{ appliedLabel }}</p>
        <p v-if="serverState.dispatch_detail">{{ serverState.dispatch_detail }}</p>
      </details>
    </div>
  </n-card>
</template>

<style scoped src="./VideoWallMappingPanel.css" />
