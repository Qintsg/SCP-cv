<script setup lang="ts">
/**
 * 预案编辑覆盖大卡片：新建 / 编辑 / 从当前状态生成 三种入口共用。
 *  - 桌面：480 px 右侧 Drawer（移动端自动 → 全屏 Sheet）；
 *  - 移动端可视为单页可滚动表单。
 */
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import { Textarea as ATextarea } from 'ant-design-vue/es/input';

import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ACheckbox from 'ant-design-vue/es/checkbox';
import ControlDrawer from '@/design-system/ControlDrawer.vue';
import { FormItem as AFormItem } from 'ant-design-vue/es/form';
import AInput from 'ant-design-vue/es/input';
import ARadio from 'ant-design-vue/es/radio';
import { RadioGroup as ARadioGroup } from 'ant-design-vue/es/radio';
import ASelect from 'ant-design-vue/es/select';
import ASlider from 'ant-design-vue/es/slider';
import ASwitch from 'ant-design-vue/es/switch';
import type { SelectProps } from 'ant-design-vue';

import { useRequestContext } from '@/composables/useRequestContext';
import { sliderAriaLabel as vSliderAriaLabel } from '@/design-system/sliderAriaLabel';
import { useScenarioStore } from '@/stores/scenarios';
import { useSourceStore } from '@/stores/sources';
import {
  createEmptyDraft,
  fromScenarioItem,
  toScenarioPayload,
  validateName,
  type ScenarioDraft,
  type ScenarioWindowDraft,
  type ScenarioWindowMode,
} from './scenarioModel';
import type { MediaSourceItem, ScenarioItem } from '@/services/api';
import ScenarioAdvancedSettings from './ScenarioAdvancedSettings.vue';
import ScenarioWallSettings from './ScenarioWallSettings.vue';
import ScenarioTelevisionFields from './ScenarioTelevisionFields.vue';
import { televisionError } from './scenarioTelevisions';
import AAutoComplete from 'ant-design-vue/es/auto-complete';
import { directoryPath, sourcePath } from '../directories/directoryPath';
import { nullableNumber } from '@/design-system/controlValue';

interface ScenarioEditDrawerProps {
  open: boolean;
  scenario: ScenarioItem | null;
  prefillFromState?: ScenarioDraft;
}

const props = withDefaults(defineProps<ScenarioEditDrawerProps>(), {
  prefillFromState: undefined,
});
const emit = defineEmits<{
  (event: 'update:open', value: boolean): void;
  (event: 'saved', scenario: ScenarioItem): void;
}>();

const { t } = useI18n();
const sourceStore = useSourceStore();
const scenarioStore = useScenarioStore();

const draft = ref<ScenarioDraft>(createEmptyDraft());
const errorMessage = ref('');
const saving = ref(false);
let editRevision = 0;
const captureSave = useRequestContext(() => { saving.value = false; errorMessage.value = ''; emit('update:open', false); });
const confirmRetiredTargetRemoval = ref(false);
const folderPathInput = ref('/');
const folderOptions = computed(() => [{ value: '/' }, ...scenarioStore.folders.map(folder => ({ value: directoryPath(folder.id,scenarioStore.folders) }))]
  .filter(option => option.value.toLocaleLowerCase().includes(folderPathInput.value.toLocaleLowerCase())));

const isOpen = computed({
  get: () => props.open,
  set: (value: boolean) => emit('update:open', value),
});

watch(
  () => [props.open, props.scenario, props.prefillFromState] as const,
  ([isOpenVal, scenario, prefill]) => {
    editRevision++; saving.value = false;
    if (!isOpenVal) return;
    if (scenario) {
      draft.value = fromScenarioItem(scenario);
    } else if (prefill) {
      draft.value = JSON.parse(JSON.stringify(prefill));
    } else {
      draft.value = { ...createEmptyDraft(), bigScreenModeState: 'set' };
    }
    errorMessage.value = '';
    folderPathInput.value = directoryPath(draft.value.folderId, scenarioStore.folders);
    confirmRetiredTargetRemoval.value = false;
  },
  { immediate: true },
);

const sourceOptionsByCategory = computed<NonNullable<SelectProps['options']>>(() => {
  const groups = new Map<string, MediaSourceItem[]>();
  for (const source of sourceStore.sources) {
    if (source.source_type === 'audio') continue;
    const cat = sourceStore.resolveCategory(source.source_type);
    const groupLabel = cat === 'ppt' ? t('sources.typeLabel.ppt')
      : cat === 'video' ? t('sources.typeLabel.video')
        : cat === 'image' ? t('sources.typeLabel.image')
          : cat === 'web' ? t('sources.typeLabel.web')
            : cat === 'stream' ? t('sources.typeLabel.stream') : t('sources.typeLabel.other');
    if (!groups.has(groupLabel)) groups.set(groupLabel, []);
    groups.get(groupLabel)!.push(source);
  }
  const options: NonNullable<SelectProps['options']> = [];
  for (const [groupLabel, list] of groups.entries()) {
    options.push({
      label: groupLabel,
      key: groupLabel,
      options: list.map((item) => ({
        label: sourcePath(item, sourceStore.folders),
        value: item.id,
        disabled: !item.is_available,
      })),
    });
  }
  return options;
});

const visibleWindows = computed<ScenarioWindowDraft[]>(() => draft.value.windows);

/** :param windowId: 逻辑播放窗口。:returns: 不按墙面左右/全屏改名或隐藏。 */
function windowLabel(windowId: number): string { return t('scenarios.edit.window', { id: windowId }); }

function isVolumeEditable(window: ScenarioWindowDraft): boolean {
  if (window.sourceState !== 'set' || !window.sourceId) return false;
  const source = sourceStore.sources.find((item) => item.id === window.sourceId);
  if (!source) return false;
  const cat = sourceStore.resolveCategory(source.source_type);
  return cat !== 'image' && cat !== 'web';
}

function showLoopToggle(window: ScenarioWindowDraft): boolean {
  if (window.sourceState !== 'set' || !window.sourceId) return false;
  const source = sourceStore.sources.find((item) => item.id === window.sourceId);
  if (!source) return false;
  const cat = sourceStore.resolveCategory(source.source_type);
  return cat === 'video';
}


async function save(): Promise<void> {
  if (saving.value || !props.open) return;
  const scope = captureSave(), revision = editRevision;
  const current = () => scope() && props.open && revision === editRevision;
  errorMessage.value = validateName(draft.value.name);
  if (errorMessage.value) return;
  errorMessage.value = televisionError(draft.value.advanced.televisions);
  if (errorMessage.value) return;
  const enteredPath = folderPathInput.value.trim().replace(/\/+$/, '') || '/';
  const folder = scenarioStore.folders.find(item => directoryPath(item.id,scenarioStore.folders) === enteredPath);
  if (enteredPath !== '/' && !folder) { errorMessage.value = '目录不存在，请选择已有目录或先在列表中新建目录'; return; }
  draft.value.folderId = folder?.id ?? null;
  if (draft.value.retiredTargets.length > 0 && !confirmRetiredTargetRemoval.value) {
    errorMessage.value = t('scenarios.edit.legacyConfirmRequired');
    return;
  }

  for (const window of draft.value.windows) {
    if (window.sourceState === 'set' && (window.sourceId === null || window.sourceId === undefined || window.sourceId < 1)) {
      errorMessage.value = t('scenarios.edit.requireSource', { label: windowLabel(window.windowId) });
      return;
    }
  }

  saving.value = true;
  try {
    const payload = toScenarioPayload(draft.value);
    const saved = draft.value.id
      ? await scenarioStore.update(draft.value.id, payload)
      : await scenarioStore.create(payload);
    if (!current()) return;
    emit('saved', saved);
    emit('update:open', false);
  } catch (error) {
    if (current()) errorMessage.value = error instanceof Error ? error.message : t('scenarios.edit.saveFail');
  } finally {
    if (current()) saving.value = false;
  }
}

function close(): void {
  emit('update:open', false);
}
</script>

<template>
  <ControlDrawer role="dialog" aria-modal="true" :aria-label="scenario ? t('scenarios.edit.titleEdit') : t('scenarios.edit.titleCreate')" :title="scenario ? t('scenarios.edit.titleEdit') : t('scenarios.edit.titleCreate')" v-model:open="isOpen" width="min(520px, 100vw)" placement="right">


      <a-alert v-if="draft.retiredTargets.length" type="warning" :message="t('scenarios.edit.legacyTitle')" class="scenario-edit__card"><template #description>
        {{ t('scenarios.edit.legacyDescription', { windows: draft.retiredTargets.map((target) => target.window_id).join('、') }) }}
        <a-checkbox v-model:checked="confirmRetiredTargetRemoval" class="scenario-edit__legacy-confirm">
          {{ t('scenarios.edit.legacyConfirm') }}
        </a-checkbox>
      </template></a-alert>

      <a-card size="small" :title="t('scenarios.edit.basic')" class="scenario-edit__card">
        <a-form-item :label="t('scenarios.edit.name')" required>
          <a-input v-model:value="draft.name" :placeholder="t('scenarios.edit.namePlaceholder')" :aria-label="t('scenarios.edit.name')" />
        </a-form-item>
        <a-form-item label="预案目录"><a-auto-complete v-model:value="folderPathInput" :options="folderOptions" style="width:100%"><a-input aria-label="预案目录路径" placeholder="/" /></a-auto-complete></a-form-item>
        <a-form-item :label="t('scenarios.edit.remark')">
            <a-textarea v-model:value="draft.description" :rows="2" :placeholder="t('scenarios.edit.remarkPlaceholder')" :aria-label="t('scenarios.edit.remark')" />
        </a-form-item>
        <a-form-item :label="t('scenarios.edit.bigScreenMode')">
          <ScenarioWallSettings v-if="isOpen" v-model="draft" />
        </a-form-item>
        <a-form-item :label="t('scenarios.edit.systemVolume')" :extra="t('scenarios.edit.systemVolumeHint')">
          <div class="scenario-edit__field-stack">
            <a-radio-group v-model:value="draft.volumeState">
              <a-radio value="unset">{{ t('scenarios.edit.keep') }}</a-radio>
              <a-radio value="set">{{ t('scenarios.edit.set') }}</a-radio>
            </a-radio-group>
            <a-slider v-if="draft.volumeState === 'set'" v-slider-aria-label="t('scenarios.edit.systemVolumeAria')"
              v-model:value="draft.volumeLevel" :min="0" :max="100"
              :aria-label="t('scenarios.edit.systemVolumeAria')" />
          </div>
        </a-form-item>
      </a-card>

      <a-card size="small" :title="t('scenarios.edit.windowsConfig')" class="scenario-edit__card">
        <div class="scenario-edit__windows">
          <a-card v-for="window in visibleWindows" :key="window.windowId" size="small" >
            <template #title>
              <div>
                <h4 class="scenario-edit__window-title">{{ t('scenarios.edit.window', { id: window.windowId }) }}</h4>
              </div>
            </template>
            <a-radio-group v-model:value="window.sourceState">
              <a-radio value="unset">{{ t('scenarios.edit.keep') }}</a-radio>
              <a-radio value="empty">{{ t('scenarios.edit.blackout') }}</a-radio>
              <a-radio value="set">{{ t('scenarios.edit.switch') }}</a-radio>
            </a-radio-group>
            <template v-if="window.sourceState === 'set'">
              <a-form-item :label="t('scenarios.edit.sourceSelect')" required>
                <a-select
                  v-select-aria-label="windowLabel(window.windowId) + t('scenarios.edit.sourceSelect')"
                  :aria-label="windowLabel(window.windowId) + t('scenarios.edit.sourceSelect')"
                  :value="window.sourceId ?? undefined"
                  @update:value="window.sourceId = nullableNumber($event)"
                  :options="sourceOptionsByCategory"
                  :placeholder="t('scenarios.edit.sourcePlaceholder')"
                  show-search option-filter-prop="label"
                />
              </a-form-item>
              <div class="scenario-edit__switches">
                <div class="scenario-edit__switch">
                  <span>{{ t('scenarios.edit.autoplay') }}</span>
                  <a-switch v-model:checked="window.autoplay" size="small" :aria-label="windowLabel(window.windowId) + t('scenarios.edit.autoplay')" />
                </div>
                <div v-if="showLoopToggle(window)" class="scenario-edit__switch">
                  <span>{{ t('scenarios.edit.resume') }}</span>
                  <a-switch v-model:checked="window.resume" size="small" :aria-label="windowLabel(window.windowId) + t('scenarios.edit.resume')" />
                </div>
              </div>
              <p v-if="window.sourceState === 'set' && !isVolumeEditable(window)" class="scenario-edit__hint">
                {{ t('scenarios.edit.noVolumeHint') }}
              </p>
            </template>
          </a-card>
        </div>
      </a-card>

      <ScenarioTelevisionFields v-model="draft.advanced" />
      <ScenarioAdvancedSettings v-model="draft.advanced" />
      <a-alert v-if="errorMessage" type="error" :message="t('scenarios.edit.cantSave')"><template #description>
        {{ errorMessage }}
      </template></a-alert>

      <template #footer>
        <div class="scenario-edit__actions">
          <a-button :disabled="saving" @click="close">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="saving" @click="save">{{ t('scenarios.edit.save') }}</a-button>
        </div>
      </template>

  </ControlDrawer>
</template>

<style scoped>
.scenario-edit__desc {
  margin: 0 0 var(--spacingVerticalM);
  color: var(--colorNeutralForeground2);
  font-size: var(--fontSizeBase200);
}

.scenario-edit__legacy-confirm {
  display: block;
  margin-top: var(--spacingVerticalS);
}

.scenario-edit__card {
  margin-bottom: var(--spacingVerticalM);
}

.scenario-edit__field-stack {
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalS);
  width: 100%;
}

.scenario-edit__windows {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--spacingVerticalM);
}

.scenario-edit__eyebrow {
  font-size: var(--fontSizeBase200);
  color: var(--colorNeutralForeground3);
}

.scenario-edit__window-title {
  margin: 0;
  font-size: var(--fontSizeBase300);
  font-weight: 600;
}

.scenario-edit__switches {
  display: flex;
  gap: var(--spacingHorizontalM);
  flex-wrap: wrap;
  margin-top: var(--spacingVerticalS);
}

.scenario-edit__switch {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalS);
}

.scenario-edit__hint {
  margin: var(--spacingVerticalXS) 0 0;
  color: var(--colorNeutralForeground3);
  font-size: var(--fontSizeBase200);
}

.scenario-edit__actions {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalS);
  justify-content: flex-end;
  width: 100%;
}

@media (min-width: 768px) {
  .scenario-edit__windows {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 640px) {
  .scenario-edit__actions :deep(.ant-btn) {
    min-height: 44px;
    min-width: 44px;
  }
}
</style>
