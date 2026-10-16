<script setup lang="ts">
/**
 * 预案管理：列表 + 预览 Drawer + 编辑 Drawer。
 *   - 默认形态仅 Sub-Toolbar + 列表；
 *   - 卡片整体可点开预览；
 *   - 新建 / 编辑 / 从当前状态生成 共用同一编辑表单。
 */
import { tagColor } from '@/design-system/tagColor';

import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACheckbox from 'ant-design-vue/es/checkbox';
import ACard from 'ant-design-vue/es/card';
import ASkeleton from 'ant-design-vue/es/skeleton';
import ATag from 'ant-design-vue/es/tag';

import FIcon from '@/design-system/FIcon.vue';
import FPageHeader from '@/design-system/FPageHeader.vue';
import FEmptyState from '@/design-system/FEmptyState.vue';
import ScenarioEditDrawer from './ScenarioEditDrawer.vue';
import ScenarioPreviewDrawer from './ScenarioPreviewDrawer.vue';
import { createEmptyDraft, hasRetiredActiveTargets, type ScenarioDraft } from './scenarioModel';
import { captureScenarioState } from './scenarioCapture';
import { useToast } from '@/composables/useToast';
import { useRuntimeStore } from '@/stores/runtime';
import { useScenarioStore } from '@/stores/scenarios';
import { useSessionStore } from '@/stores/sessions';
import { formatRelativeTime } from '@/design-system/utils';
import type { ScenarioItem } from '@/services/api';
import ScenarioDirectoryTools from './ScenarioDirectoryTools.vue';
import { inDirectory } from '../directories/directoryPath';

const { t } = useI18n();
const scenarioStore = useScenarioStore();
const sessionStore = useSessionStore();
const runtime = useRuntimeStore();
const toast = useToast();

const previewOpen = ref(false);
const editOpen = ref(false);
const isLoading = computed(() => scenarioStore.loading);
const batchBusy = ref(false);
const pendingActivateId = ref<number | null>(null);
const pendingPinId = ref<number | null>(null);

const previewScenarioId = ref<number | null>(null);
const editingScenario = ref<ScenarioItem | null>(null);
const prefillDraft = ref<ScenarioDraft | undefined>(undefined);

const folderId = ref<number | null>(null), search = ref(''), selected = ref<number[]>([]);
const sortedScenarios = computed(() => scenarioStore.sorted.filter(item =>
  inDirectory(item.folder_id ?? null, folderId.value, scenarioStore.folders, !!search.value.trim()) &&
  (!search.value.trim() || `${item.name} ${item.path ?? ''} ${item.description}`.toLocaleLowerCase().includes(search.value.trim().toLocaleLowerCase()))));
const visibleIds = computed(() => sortedScenarios.value.map(item => item.id));
// 列表与目录由应用启动同步统一加载；路由挂载不重复刷新或产生额外错误提示。
const previewScenario = computed<ScenarioItem | null>(() => {
  if (previewScenarioId.value === null) return null;
  return scenarioStore.scenarios.find((scenario) => scenario.id === previewScenarioId.value) ?? null;
});

async function refresh(): Promise<void> {
  try {
    await scenarioStore.refresh();
  } catch (error) {
    toast.error(t('scenarios.loadFail'), error instanceof Error ? error.message : t('common.retry'));
  }
}

/** :param scenario: 明确预览对象。:param event: 真实触发元素。:returns: 打开预览并让抽屉关闭时可恢复原按钮焦点。 */
function openPreview(scenario: ScenarioItem, event: MouseEvent): void {
  const trigger = event.currentTarget as HTMLElement;
  const button = trigger.tagName === 'BUTTON' ? trigger : trigger.querySelector<HTMLButtonElement>('.scenarios-view__preview-button');
  button?.focus({ preventScroll: true });
  previewScenarioId.value = scenario.id;
  previewOpen.value = true;
}

function openCreate(): void {
  editingScenario.value = null;
  prefillDraft.value = { ...createEmptyDraft(), folderId: folderId.value };
  editOpen.value = true;
}

function openEdit(scenario: ScenarioItem): void {
  editingScenario.value = scenario;
  prefillDraft.value = undefined;
  previewOpen.value = false;
  editOpen.value = true;
}

/** :returns: 临时共享先明确拒绝，不生成可被误解为已保存共享的草稿。 */
function captureFromCurrent(): void {
  const captured = captureScenarioState({ sessions: sessionStore.sessions, runtime: runtime.runtime,
    volume: runtime.systemVolume, folderId: folderId.value });
  if (!captured.draft) {
    toast.warning(t('scenarios.transientCaptureTitle'), t('scenarios.transientCaptureDetail', { id: captured.sharedWindowId }));
    return;
  }
  const omitted = captured.omittedWindowIds.map(id => t('scenarios.captureWindow', { id }));
  if (captured.omittedSystemVolume) omitted.push(t('scenarios.captureSystemVolume'));
  if (omitted.length) toast.warning(t('scenarios.partialCaptureTitle'),
    t('scenarios.partialCaptureDetail', { items: omitted.join('、') }));
  editingScenario.value = null;
  prefillDraft.value = captured.draft;
  editOpen.value = true;
}

function onAfterDelete(): void {
  previewScenarioId.value = null;
  void refresh();
}

function onSaved(scenario: ScenarioItem): void {
  toast.success(t('scenarios.saved'));
  editingScenario.value = null;
  prefillDraft.value = undefined;
  editOpen.value = false;
  previewScenarioId.value = scenario.id;
  previewOpen.value = true;
}

async function activateScenario(scenario: ScenarioItem): Promise<void> {
  if (hasRetiredActiveTargets(scenario)) {
    toast.warning(t('scenarios.preview.legacyTitle'), t('scenarios.preview.legacyDescription'));
    return;
  }
  pendingActivateId.value = scenario.id;
  try {
    await scenarioStore.activate(scenario.id);
    toast.success(t('scenarios.activatedOk'), t('scenarios.activatedDetail', { name: scenario.name }));
  } catch (error) {
    toast.error(t('scenarios.activateFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    pendingActivateId.value = null;
  }
}

async function togglePin(scenario: ScenarioItem): Promise<void> {
  const wasPinned = scenario.sort_order > 0;
  pendingPinId.value = scenario.id;
  try {
    const next = await scenarioStore.pin(scenario.id);
    toast.success(next.sort_order > 0 ? t('scenarios.pinnedOk') : t('scenarios.unpinnedOk'));
  } catch (error) {
    toast.error(wasPinned ? t('scenarios.unpinFail') : t('scenarios.pinFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    pendingPinId.value = null;
  }
}
</script>

<template>
  <div class="scenarios-view">
    <FPageHeader :title="t('scenarios.title')" :description="t('scenarios.caption')">
      <template #actions>
        <a-button :loading="isLoading" :aria-label="t('scenarios.refreshAria')" @click="refresh">
          <template #icon><FIcon name="arrow_clockwise_20_regular" /></template>
        </a-button>
        <a-button @click="captureFromCurrent">
          <template #icon><FIcon name="document_24_regular" /></template>
          {{ t('scenarios.captureFromState') }}
        </a-button>
        <a-button type="primary" @click="openCreate">
          <template #icon><FIcon name="add_24_regular" /></template>
          {{ t('scenarios.create') }}
        </a-button>
      </template>
    </FPageHeader>

    <ScenarioDirectoryTools v-model:folder-id="folderId" v-model:search="search" v-model:selected="selected"
      v-model:busy="batchBusy" :visible-ids="visibleIds">
    <a-alert v-if="scenarioStore.loadError" type="error" message="预设列表读取失败" role="alert"><template #description>
      <p>{{ scenarioStore.loadError }}<template v-if="scenarioStore.scenarios.length"> · 下方保留上次读取的内容。</template></p>
      <a-button :loading="isLoading" @click="refresh">重新读取</a-button>
    </template></a-alert>
    <section class="scenarios-view__grid" :aria-busy="isLoading || batchBusy">
      <template v-if="isLoading && sortedScenarios.length === 0">
        <a-card v-for="i in 4" :key="i" size="small">
          <a-skeleton text width="50%" />
          <a-skeleton text width="80%" />
          <a-skeleton text width="35%" />
        </a-card>
      </template>

      <template v-else-if="sortedScenarios.length === 0 && !scenarioStore.loadError">
        <FEmptyState :title="search.trim() ? '没有匹配的预设' : '当前目录没有预设'" :description="search.trim() ? '试试其他名称，或清除搜索条件。' : t('scenarios.emptyDesc')">
          <a-button v-if="search.trim()" @click="search = ''">清除搜索</a-button>
          <template v-else>
            <a-button type="primary" @click="openCreate">
              <template #icon><FIcon name="add_24_regular" /></template>
              {{ t('scenarios.create') }}
            </a-button>
            <a-button  @click="captureFromCurrent">
              <template #icon><FIcon name="document_24_regular" /></template>
              {{ t('scenarios.captureFromState') }}
            </a-button>
          </template>
        </FEmptyState>
      </template>

      <template v-else-if="sortedScenarios.length">
        <a-card
          v-for="scenario in sortedScenarios"
          :key="scenario.id"
          size="small"
          hoverable
          :class="{ 'scenarios-view__card--pinned': scenario.sort_order > 0 }"
          @click="openPreview(scenario, $event)"
        >
          <template #title>
            <div class="scenarios-view__card-header">
              <span v-if="scenario.sort_order > 0" class="scenarios-view__pinned">
                <FIcon name="pin_24_filled" /> {{ t('scenarios.pinned') }}
              </span>
              <span v-else class="scenarios-view__eyebrow">{{ t('scenarios.label') }}</span>
              <h3 class="scenarios-view__card-title"><button type="button" class="scenarios-view__preview-button"
                :aria-label="`查看预设 ${scenario.name}`" @click.stop="openPreview(scenario, $event)">{{ scenario.name }}</button></h3>
            </div>
          </template>
          <template #extra>
            <div class="scenarios-view__card-actions" @click.stop>
              <a-checkbox :checked="selected.includes(scenario.id)" :disabled="batchBusy" :aria-label="'选择'+scenario.name"
                @update:checked="selected = $event ? [...selected,scenario.id] : selected.filter(id=>id!==scenario.id)" />
              <a-button
                type="text"
                size="small"
                :aria-label="scenario.sort_order > 0 ? t('scenarios.unpinAria') : t('scenarios.pinAria')"
                :loading="pendingPinId === scenario.id"
                @click="togglePin(scenario)"
              >
                <template #icon>
                  <FIcon :name="scenario.sort_order > 0 ? 'pin_off_24_regular' : 'pin_24_regular'" />
                </template>
              </a-button>
              <a-button
                type="primary"
                size="small"
                :loading="pendingActivateId === scenario.id"
                @click="activateScenario(scenario)"
              >
                <template #icon><FIcon name="play_24_regular" /></template>
                {{ t('scenarios.activate') }}
              </a-button>
            </div>
          </template>

          <p class="scenarios-view__meta">
            <a-tag :color="tagColor(scenario.big_screen_mode_state === 'unset' ? 'default' : 'info')"  >
              {{ scenario.big_screen_mode_state === 'unset' ? t('scenarios.keepScreenMode') : scenario.big_screen_mode_label }}
            </a-tag>
            <a-tag :color="tagColor(scenario.volume_state === 'unset' ? 'default' : 'info')"  >
              {{ scenario.volume_state === 'unset' ? t('scenarios.keepVolume') : t('scenarios.volumeValue', { n: scenario.volume_level }) }}
            </a-tag>
          </p>
          <p v-if="scenario.description" class="scenarios-view__desc">{{ scenario.description }}</p>
          <p class="scenarios-view__desc">{{ scenario.path ?? '/'+scenario.name }}</p>
          <p class="scenarios-view__updated">{{ t('scenarios.updatedAt', { time: formatRelativeTime(scenario.updated_at) }) }}</p>
        </a-card>
      </template>
    </section>

    </ScenarioDirectoryTools>
    <ScenarioPreviewDrawer v-model:open="previewOpen" :scenario="previewScenario" @edit="openEdit"
      @after-delete="onAfterDelete" />
    <ScenarioEditDrawer v-model:open="editOpen" :scenario="editingScenario" :prefill-from-state="prefillDraft"
      @saved="onSaved" />
  </div>
</template>

<style scoped>
.scenarios-view {
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalL);
  max-width: 1280px;
}

.scenarios-view__toolbar {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--spacingHorizontalL);
  padding: var(--spacingVerticalM) 0;
  flex-wrap: wrap;
  border-bottom: 1px solid var(--colorNeutralStrokeDivider);
}

.scenarios-view__heading {
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalXS);
}

.scenarios-view__title {
  margin: 0;
  font-size: var(--fontSizeHero700);
  line-height: var(--lineHeightHero700);
  font-weight: 600;
}

.scenarios-view__caption {
  margin: 0;
  color: var(--colorNeutralForeground3);
  font-size: var(--fontSizeBase200);
}

.scenarios-view__actions {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalS);
  flex-wrap: wrap;
}

.scenarios-view__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: var(--spacingHorizontalL);
}

.scenarios-view__grid > .ant-empty {
  grid-column: 1 / -1;
  justify-self: center;
  align-self: center;
  padding: var(--spacingVerticalXXL) var(--spacingHorizontalL);
}

.scenarios-view__grid:has(.ant-empty) {
  min-height: 50vh;
}

.scenarios-view__card--pinned {
  border-left: 4px solid var(--colorBrandBackground);
}

.scenarios-view__card-header {
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalXS);
}

.scenarios-view__card-title {
  margin: 0;
  font-size: var(--fontSizeBase400);
  font-weight: 600;
}

.scenarios-view__preview-button {
  min-height: 44px;
  min-width: 44px;
  padding: 0;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: left;
  overflow-wrap: anywhere;
  cursor: pointer;
}

.scenarios-view__preview-button:hover { color: var(--colorBrandForeground1); text-decoration: underline; }

.scenarios-view__eyebrow {
  font-size: var(--fontSizeBase200);
  color: var(--colorNeutralForeground3);
}

.scenarios-view__pinned {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalXS);
  color: var(--colorBrandForeground1);
  font-size: var(--fontSizeBase200);
}

.scenarios-view__card-actions {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalXS);
}

.scenarios-view__meta {
  margin: var(--spacingVerticalS) 0 0;
  display: inline-flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--spacingHorizontalXS);
}

.scenarios-view__desc {
  margin: var(--spacingVerticalS) 0 0;
  color: var(--colorNeutralForeground2);
  font-size: var(--fontSizeBase200);
  line-height: var(--lineHeightBase300);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.scenarios-view__updated {
  margin: var(--spacingVerticalS) 0 0;
  color: var(--colorNeutralForeground3);
  font-size: var(--fontSizeBase200);
}

@media (max-width: 767px) {
  .scenarios-view__toolbar {
    flex-direction: column;
    align-items: stretch;
  }

  .scenarios-view__grid {
    grid-template-columns: 1fr;
  }
}
</style>
