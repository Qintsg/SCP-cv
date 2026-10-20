<script setup lang="ts">
/** 目录优先的媒体文件管理：路径、类型筛选、多选结果与打开到小卡片。SCP-cv 2026-10-12。 */
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import { tagColor } from '@/design-system/tagColor';
import type { DropdownOption } from '@/design-system/dropdownActions';

import ActionMenu from '@/design-system/ActionMenu.vue';

import { computed, nextTick, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ACheckbox from 'ant-design-vue/es/checkbox';
import AInput from 'ant-design-vue/es/input';
import ASelect from 'ant-design-vue/es/select';
import ATag from 'ant-design-vue/es/tag';
import ATable from 'ant-design-vue/es/table';
import FIcon from '@/design-system/FIcon.vue';
import FPageHeader from '@/design-system/FPageHeader.vue';
import FEmptyState from '@/design-system/FEmptyState.vue';
import { runDropdownAction } from '@/design-system/dropdownActions';
import { useBreakpoint } from '@/composables/useBreakpoint';
import { useToast } from '@/composables/useToast';
import { useRequestContext } from '@/composables/useRequestContext';
import { useSourceStore, type SourceCategory } from '@/stores/sources';
import { useSessionStore } from '@/stores/sessions';
import type { MediaSourceItem } from '@/services/api';
import { formatBytes } from '@/design-system/utils';
import DirectoryNavigator from '../directories/DirectoryNavigator.vue';
import { directoryPath, sourcePath } from '../directories/directoryPath';
import { isConfirmedPlayback } from '../display/playbackObservation';
import AddSourceDrawer from './AddSourceDrawer.vue';
import EditSourceDrawer from './EditSourceDrawer.vue';
import FolderDialogs from './FolderDialogs.vue';
import SourceThumbnail from './SourceThumbnail.vue';
import SourceOpenCard from './SourceOpenCard.vue';
import { sourceAvailabilityLabel, sourceCategoryLabel, sourceCategoryTone } from './sourcePresentation';
import { useSourceFolders } from './useSourceFolders';
import { useSourceActions } from './useSourceActions';
import { useSourceSelection } from './useSourceSelection';

const { t } = useI18n(), sourceStore = useSourceStore(), sessions = useSessionStore(), toast = useToast();
const { isMobile } = useBreakpoint();
const isLoading = ref(false), drawerOpen = ref(false), editDrawerOpen = ref(false), openCard = ref(false);
const editingSource = ref<MediaSourceItem | null>(null), openingSource = ref<MediaSourceItem | null>(null);
const path = computed(() => directoryPath(sourceStore.currentFolderId, sourceStore.folders));
const protectedDirectory = computed(() => !!sourceStore.folders.find(folder => folder.id === sourceStore.currentFolderId)?.is_protected);
const categoryModel = computed({ get: () => sourceStore.category, set: (value: SourceCategory) => sourceStore.setCategory(value) });
const searchModel = computed({ get: () => sourceStore.searchKeyword, set: (value: string) => sourceStore.setSearchKeyword(value) });
const hasFilters = computed(() => !!searchModel.value.trim() || categoryModel.value !== 'all');
const loadError = ref('');
let refreshOwner: symbol | null = null;
const captureRefresh = useRequestContext(() => { refreshOwner = null; isLoading.value = false; loadError.value = ''; });
const searchInput = ref<{ focus: () => void } | null>(null);
const pathElement = ref<HTMLElement | null>(null);
/** :returns: 目录卡片移除后，把键盘定位留在新目录，而非退回文档根。 */
watch(() => sourceStore.currentFolderId, () => { void nextTick(() => pathElement.value?.focus({ preventScroll: true })); });
/** :returns: 只清除搜索和类型，不改变当前目录或媒体。 */
function clearFilters(): void {
  sourceStore.setCategory('all'); sourceStore.setSearchKeyword('');
  void nextTick(() => searchInput.value?.focus());
}
const categoryOptions = computed(() => ['all','ppt','video','audio','image','web','stream'].map(value => ({ value, label: t(`sources.cat.${value}Label`) })));
const columns = [{ key: 'selection', width: 48 }, { title: '名称', key: 'name' }, { title: '播放状态', key: 'status', width: 165 },
  { title: '类型', key: 'category', width: 150 }, { title: '大小', key: 'size', width: 100 }, { title: '操作', key: 'actions', width: 168 }];
const { selected, busy, destination, results, allSelected, destinations, toggle, toggleAll, execute } = useSourceSelection();
const mediaRows = computed(() => new Map(sourceStore.filtered.map(source => [source.id, source])));
/** :param id: Ant表格的宽类型记录键。:returns: 只接受当前已验证媒体索引中的记录，不强制断言任意对象。 */
function mediaRow(id: unknown): MediaSourceItem | undefined {
  return typeof id === 'number' ? mediaRows.value.get(id) : undefined;
}
/** :returns: 读取全量索引，呈现当前位置，不创建或移动任何文件。 */
async function refresh(): Promise<void> {
  const scope = captureRefresh(), owner = Symbol('source-view-refresh');
  refreshOwner = owner;
  const current = () => scope() && refreshOwner === owner;
  isLoading.value = true;
  loadError.value = '';
  try { await sourceStore.refresh(); }
  catch (error) { if (current()) { loadError.value = error instanceof Error ? error.message : t('common.retry'); toast.error(t('sources.loadFail'), loadError.value); } }
  finally { if (current()) isLoading.value = false; }
}
const { newFolderDialogOpen, newFolderName, folderNameError, creatingFolder,
  renameFolderOpen, renameFolderName, renameFolderTarget, renameFolderError, renamingFolder, executeFolderRename,
  deleteFolderTarget, deleteFolderContents, deletingFolder, deleteFolderDialogOpen,
  childFolders, navigateToFolder, openFolderDialog, createFolder,
  executeFolderDelete, buildFolderMenu, buildMoveToFolderOptions } = useSourceFolders(refresh, isLoading);
const visibleFolders = computed(() => childFolders.value.filter(folder => !searchModel.value.trim() ||
  `${folder.name} ${directoryPath(folder.id,sourceStore.folders)}`.toLocaleLowerCase().includes(searchModel.value.trim().toLocaleLowerCase())));
const tableRows = computed(() => [...visibleFolders.value.map(folder => ({ id: -folder.id })), ...sourceStore.filtered]);
/** :param id: 表格唯一键，负数为目录。:returns: 当前目录对象，不与正数媒体ID混淆。 */
function folderRow(id: unknown) { return typeof id === 'number' && id < 0 ? visibleFolders.value.find(folder => folder.id === -id) : undefined; }
/** :param source: 普通源。:returns: 打开编辑器，不编辑内置配置。 */
function startEdit(source: MediaSourceItem): void { if (!source.is_protected) { editingSource.value = source; editDrawerOpen.value = true; } }
/** :param source: 当前明确选择。:returns: 展示两目标卡片，不立即替换输出。 */
function showOpen(source: MediaSourceItem): void { openingSource.value = source; openCard.value = true; }
const buildRowMenu = useSourceActions(startEdit, showOpen, buildMoveToFolderOptions);
/** :param _key: 菜单键。:param option: 语义动作。:returns: 只派发一次，不重复绑定DOM点击。 */
function handleMenuSelect(_key: string | number, option: DropdownOption): void { runDropdownAction(option); }
/** :param id: 源ID。:returns: 只有匹配代次的实际playing才标播出，不宣称人工确认。 */
function activeWindowLabel(id: number): string {
  const windows = sessions.sessions.filter(session => session.source_id === id && isConfirmedPlayback(session)).map(session => session.window_id).join('、');
  return windows ? `窗口${windows}播放中` : '';
}
// 首次索引由应用启动同步负责；保留显式刷新，不在每次路由挂载重复发请求。
</script>

<template>
  <div class="sources-view">
    <FPageHeader title="媒体文件">
      <template #actions>
        <a-button :loading="isLoading" aria-label="刷新媒体文件" @click="refresh"><template #icon><FIcon name="arrow_clockwise_20_regular" /></template></a-button>
        <a-button type="primary" :disabled="protectedDirectory || busy" @click="drawerOpen = true">上传或添加</a-button>
      </template>
    </FPageHeader>
    <div class="sources-view__layout" :class="{ 'sources-view__layout--mobile': isMobile }">
      <DirectoryNavigator v-if="!isMobile" v-model:folder-id="sourceStore.currentFolderId" :folders="sourceStore.folders" />
      <section class="sources-view__main">
        <DirectoryNavigator v-if="isMobile" v-model:folder-id="sourceStore.currentFolderId" :folders="sourceStore.folders" compact />
        <div class="sources-view__folder-bar">
          <span ref="pathElement" class="sources-view__path" :title="path" tabindex="-1">{{ path }}</span>
          <a-button size="small" aria-label="新建文件夹" :disabled="protectedDirectory || busy" @click="openFolderDialog">新建目录</a-button>
        </div>
        <div class="sources-view__filters">
          <a-input ref="searchInput" v-model:value="searchModel" placeholder="搜索名称或路径" aria-label="搜索媒体名称或路径" allow-clear class="sources-view__search"><template #prefix><FIcon name="search_20_regular" /></template></a-input>
          <a-select v-select-aria-label="'媒体类型筛选'" v-model:value="categoryModel" :options="categoryOptions" aria-label="媒体类型筛选" style="width:150px" />
          <a-checkbox v-model:checked="sourceStore.searchSubfolders">搜索包含子目录</a-checkbox>
          <a-button v-if="hasFilters" type="text" size="small" @click="clearFilters">清除筛选</a-button>
          <span class="sources-view__caption" role="status">{{ tableRows.length }} 项</span>
          <span v-if="protectedDirectory" class="sources-view__caption">内置目录 · 配置只读</span>
        </div>
        <div v-if="selected.length" class="sources-view__batch" role="region" aria-label="批量操作">
          <span>已选 {{ selected.length }} 项</span>
          <a-select v-select-aria-label="'批量移动目标目录'" v-model:value="destination" :options="destinations" aria-label="批量移动目标目录" :disabled="busy" show-search option-filter-prop="label" style="min-width:160px;flex:1" />
          <a-button :disabled="busy" @click="execute('move')">移动到…</a-button>
          <a-button danger  :disabled="busy" @click="execute('delete')">删除所选</a-button>
          <a-button type="text" :disabled="busy" @click="selected = []">取消选择</a-button>
        </div>
        <p v-if="busy" class="sources-view__caption" role="status">正在处理已选媒体；结果将按原路径逐项显示。</p>
        <a-alert v-if="results.length" :type="results.some(item => !item.success) ? 'warning' : 'success'" message="逐项操作结果" :show-icon="false"><template #description>
          <p v-for="item in results" :key="item.id">{{ item.path }} · {{ item.detail }}</p>
        </template></a-alert>
        <a-card :body-style="{ padding: 0, overflowX: 'auto' }" :aria-busy="isLoading || busy">
          <FEmptyState v-if="loadError" title="媒体列表读取失败" :description="loadError"><a-button @click="refresh">重新读取</a-button></FEmptyState>
          <div v-else-if="isLoading && !sourceStore.filtered.length" class="sources-view__loading" role="status">正在读取媒体文件…</div>
          <FEmptyState v-else-if="!tableRows.length" :title="hasFilters ? '没有匹配的媒体' : '当前目录没有媒体文件'"
            :description="hasFilters ? '试试其他名称，或清除筛选条件。' : protectedDirectory ? '这里的内容由系统维护。' : '上传文件，或添加网页和直播流。'">
            <a-button v-if="hasFilters" @click="clearFilters">清除筛选</a-button>
            <a-button v-else-if="!protectedDirectory" type="primary" :disabled="busy" @click="drawerOpen = true">上传或添加</a-button>
          </FEmptyState>
          <a-table v-else-if="!isMobile" class="sources-view__table" :columns="columns" :data-source="tableRows" row-key="id" :pagination="false" :scroll="{ x: 800 }">
            <template #headerCell="{ column }"><a-checkbox v-if="column.key === 'selection'" :checked="allSelected" :disabled="busy || protectedDirectory" :aria-disabled="busy || protectedDirectory" aria-label="全选当前普通媒体" @update:checked="toggleAll" /><template v-else>{{ column.title }}</template></template>
            <template #bodyCell="{ column, record }">
              <template v-for="folder in [folderRow(record.id)]" :key="folder?.id"><template v-if="folder">
                <button v-if="column.key === 'name'" type="button" class="sources-view__folder-open" @click="navigateToFolder(folder.id)"><FIcon name="folder_24_regular" :size="28" /><span class="sources-view__name sources-view__folder-name">{{ folder.name }}</span><a-tag v-if="folder.is_protected">内置</a-tag></button>
                <span v-else-if="column.key === 'category'">文件夹</span><span v-else-if="column.key === 'size'">—</span>
                <ActionMenu v-else-if="column.key === 'actions' && !folder.is_protected" :options="buildFolderMenu(folder)" @select="handleMenuSelect"><a-button type="text" shape="circle" :aria-label="`目录 ${folder.name} 的操作`"><template #icon><FIcon name="more_horizontal_20_regular" /></template></a-button></ActionMenu>
              </template></template>
              <template v-for="source in [mediaRow(record.id)]" :key="source?.id"><template v-if="source">
              <a-checkbox v-if="column.key === 'selection'" :checked="selected.includes(source.id)" :disabled="busy || source.is_protected" :aria-disabled="busy || !!source.is_protected" :aria-label="`选择 ${source.name}`" @update:checked="toggle(source.id,$event)" />
              <div v-else-if="column.key === 'name'" class="sources-view__name-cell"><SourceThumbnail :source="source" size="comfortable" /><div>
                <p class="sources-view__name">{{ source.name }}</p>
              </div></div>
              <template v-else-if="column.key === 'status'"><a-tag v-if="activeWindowLabel(source.id)" color="success">{{ activeWindowLabel(source.id) }}</a-tag><span v-else>—</span></template>
              <template v-else-if="column.key === 'category'"><a-tag :color="tagColor(sourceCategoryTone(source))" >{{ sourceCategoryLabel(source) }}</a-tag><a-tag v-if="source.is_protected" >内置</a-tag>
                <p v-if="!source.is_available" class="sources-view__caption">{{ sourceAvailabilityLabel(source) }}</p></template>
              <template v-else-if="column.key === 'size'">{{ source.file_size ? formatBytes(source.file_size) : '—' }}</template>
              <div v-else-if="column.key === 'actions'" class="sources-view__row-actions">
                <a-button v-if="source.source_type !== 'audio'" size="small" :disabled="!source.is_available" @click="showOpen(source)">打开到…</a-button>
                <ActionMenu  placement="bottomRight" :options="buildRowMenu(source)" @select="handleMenuSelect"><a-button type="text" shape="circle" :aria-label="`媒体 ${source.name} 的操作`"><template #icon><FIcon name="more_horizontal_20_regular" /></template></a-button></ActionMenu>
              </div>
              </template></template>
            </template>
          </a-table>
          <div v-else class="sources-view__cards">
            <a-card v-for="folder in visibleFolders" :key="`f-${folder.id}`" size="small"><div class="sources-view__row-actions"><button type="button" class="sources-view__folder-open" @click="navigateToFolder(folder.id)"><FIcon name="folder_24_regular" :size="26" /><span>{{ folder.name }}</span><a-tag v-if="folder.is_protected">内置</a-tag></button>
              <ActionMenu v-if="!folder.is_protected" :options="buildFolderMenu(folder)" @select="handleMenuSelect"><a-button type="text" :aria-label="`目录 ${folder.name} 的操作`">更多</a-button></ActionMenu></div></a-card>
            <a-card v-for="source in sourceStore.filtered" :key="source.id" size="small">
              <template #title><div class="sources-view__card-title"><a-checkbox :checked="selected.includes(source.id)" :disabled="busy || source.is_protected" :aria-disabled="busy || !!source.is_protected" :aria-label="`选择 ${source.name}`" @update:checked="toggle(source.id,$event)" /><SourceThumbnail :source="source" /><span>{{ source.name }}</span></div></template>
              <a-tag v-if="activeWindowLabel(source.id)" color="success">{{ activeWindowLabel(source.id) }}</a-tag>
              <div class="sources-view__row-actions"><a-tag :color="tagColor(sourceCategoryTone(source))" >{{ sourceCategoryLabel(source) }}</a-tag>
                <a-button v-if="source.source_type !== 'audio'" size="small" :disabled="!source.is_available" @click="showOpen(source)">打开到…</a-button>
                <ActionMenu  :options="buildRowMenu(source)" @select="handleMenuSelect"><a-button type="text" :aria-label="`媒体 ${source.name} 的操作`">更多</a-button></ActionMenu>
              </div>
              <p v-if="!source.is_available" class="sources-view__caption">{{ sourceAvailabilityLabel(source) }}</p>
            </a-card>
          </div>
        </a-card>
      </section>
    </div>
    <FolderDialogs v-model:create-open="newFolderDialogOpen" v-model:name="newFolderName" v-model:name-error="folderNameError"
      v-model:rename-open="renameFolderOpen" v-model:rename-name="renameFolderName" :rename-target="renameFolderTarget"
      :rename-error="renameFolderError" :renaming="renamingFolder" :rename-folder="executeFolderRename"
      v-model:delete-open="deleteFolderDialogOpen" v-model:delete-contents="deleteFolderContents" :creating="creatingFolder" :deleting="deletingFolder"
      :delete-target="deleteFolderTarget" :create-folder="createFolder" :delete-folder="executeFolderDelete" @delete-cancelled="deleteFolderTarget = null" />
    <AddSourceDrawer v-model:open="drawerOpen" :folder-id="sourceStore.currentFolderId" @added="refresh" />
    <EditSourceDrawer v-model:open="editDrawerOpen" :source="editingSource" @updated="refresh" />
    <SourceOpenCard v-model:open="openCard" :source="openingSource" />
  </div>
</template>
<style scoped src="./SourcesView.css"></style>
