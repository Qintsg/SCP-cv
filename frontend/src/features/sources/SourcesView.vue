<script setup lang="ts">
/** 目录优先的媒体文件管理：路径、类型筛选、多选结果与打开到小卡片。SCP-cv 2026-10-12。 */
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { NAlert, NButton, NCard, NCheckbox, NDropdown, NEmpty, NInput, NSelect, NTag, type DropdownOption } from 'naive-ui';
import FIcon from '@/design-system/FIcon.vue';
import { runDropdownAction } from '@/design-system/dropdownActions';
import { useBreakpoint } from '@/composables/useBreakpoint';
import { useToast } from '@/composables/useToast';
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
const categoryOptions = computed(() => ['all','ppt','video','audio','image','web','stream'].map(value => ({ value, label: t(`sources.cat.${value}Label`) })));
const { selected, busy, destination, results, allSelected, destinations, toggle, toggleAll, execute } = useSourceSelection();
/** :returns: 读取全量索引，呈现当前位置，不创建或移动任何文件。 */
async function refresh(): Promise<void> {
  isLoading.value = true;
  try { await sourceStore.refresh(); }
  catch (error) { toast.error(t('sources.loadFail'), error instanceof Error ? error.message : t('common.retry')); }
  finally { isLoading.value = false; }
}
const { newFolderDialogOpen, newFolderName, folderNameError, creatingFolder,
  deleteFolderTarget, deleteFolderContents, deletingFolder, deleteFolderDialogOpen,
  childFolders, navigateToFolder, openFolderDialog, createFolder,
  executeFolderDelete, buildFolderMenu, buildMoveToFolderOptions } = useSourceFolders(refresh, isLoading);
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
    <header class="sources-view__toolbar">
      <div class="sources-view__heading"><h2 class="sources-view__title">媒体文件</h2><p class="sources-view__caption">{{ sourceStore.filtered.length }} 个媒体源 · {{ path }}</p></div>
      <div class="sources-view__actions">
        <n-input v-model:value="searchModel" placeholder="搜索名称或路径" :input-props="{ 'aria-label': '搜索媒体名称或路径' }" clearable><template #prefix><FIcon name="search_20_regular" /></template></n-input>
        <n-button :loading="isLoading" aria-label="刷新媒体文件" @click="refresh"><template #icon><FIcon name="arrow_clockwise_20_regular" /></template></n-button>
        <n-button type="primary" :disabled="protectedDirectory || busy" @click="drawerOpen = true">上传 / 添加</n-button>
      </div>
    </header>
    <div class="sources-view__layout" :class="{ 'sources-view__layout--mobile': isMobile }">
      <DirectoryNavigator v-if="!isMobile" v-model:folder-id="sourceStore.currentFolderId" :folders="sourceStore.folders" />
      <section class="sources-view__main">
        <DirectoryNavigator v-if="isMobile" v-model:folder-id="sourceStore.currentFolderId" :folders="sourceStore.folders" compact />
        <div class="sources-view__folder-bar">
          <span class="sources-view__path">{{ path }}</span>
          <n-button size="small" aria-label="新建文件夹" :disabled="protectedDirectory || busy" @click="openFolderDialog">新建目录</n-button>
        </div>
        <div class="sources-view__filters">
          <n-select v-model:value="categoryModel" :options="categoryOptions" aria-label="媒体类型筛选" style="width:150px" />
          <n-checkbox v-model:checked="sourceStore.searchSubfolders">搜索包含子目录</n-checkbox>
          <span v-if="protectedDirectory" class="sources-view__caption">内置目录 · 配置只读</span>
        </div>
        <div v-if="childFolders.length" class="sources-view__folders">
          <div v-for="folder in childFolders" :key="folder.id" class="sources-view__folder-card">
            <button type="button" class="sources-view__folder-open" @click="navigateToFolder(folder.id)"><FIcon name="folder_24_regular" :size="26" /><span class="sources-view__folder-name">{{ folder.name }}</span></button>
            <n-tag v-if="folder.is_protected" size="small">内置</n-tag>
            <n-dropdown v-else trigger="click" :options="buildFolderMenu(folder)" @select="handleMenuSelect"><n-button quaternary circle size="tiny" :aria-label="`目录 ${folder.name} 的操作`"><template #icon><FIcon name="more_horizontal_20_regular" /></template></n-button></n-dropdown>
          </div>
        </div>
        <div v-if="selected.length" class="sources-view__batch" role="region" aria-label="批量操作">
          <span>已选 {{ selected.length }} 项</span>
          <n-select v-model:value="destination" :options="destinations" aria-label="批量移动目标目录" :disabled="busy" filterable style="min-width:160px;flex:1" />
          <n-button :disabled="busy" @click="execute('move')">移动到…</n-button>
          <n-button type="error" secondary :disabled="busy" @click="execute('delete')">删除所选</n-button>
          <n-button quaternary :disabled="busy" @click="selected = []">取消选择</n-button>
        </div>
        <n-alert v-if="results.length" :type="results.some(item => !item.success) ? 'warning' : 'success'" title="逐项操作结果" :show-icon="false">
          <p v-for="item in results" :key="item.id">{{ item.path }} · {{ item.detail }}</p>
        </n-alert>
        <n-card content-style="padding:0; overflow-x:auto" :aria-busy="isLoading || busy">
          <n-empty v-if="!sourceStore.filtered.length" :description="searchModel ? '未找到匹配媒体' : '当前目录没有媒体文件'" />
          <table v-else-if="!isMobile" class="sources-view__table">
            <thead><tr>
              <th scope="col"><n-checkbox :checked="allSelected" :disabled="busy || protectedDirectory" :aria-disabled="busy || protectedDirectory" aria-label="全选当前普通媒体" @update:checked="toggleAll" /></th>
              <th scope="col">名称与路径</th><th scope="col">类型</th><th scope="col">大小</th><th scope="col">操作</th>
            </tr></thead>
            <tbody><tr v-for="source in sourceStore.filtered" :key="source.id">
              <td><n-checkbox :checked="selected.includes(source.id)" :disabled="busy || source.is_protected" :aria-disabled="busy || !!source.is_protected" :aria-label="`选择 ${source.name}`" @update:checked="toggle(source.id,$event)" /></td>
              <td><div class="sources-view__name-cell"><SourceThumbnail :source="source" size="comfortable" /><div>
                <p class="sources-view__name">{{ source.name }}</p><p class="sources-view__uri">{{ sourcePath(source,sourceStore.folders) }}</p>
                <n-tag v-if="activeWindowLabel(source.id)" type="success" size="small">{{ activeWindowLabel(source.id) }}</n-tag>
              </div></div></td>
              <td><n-tag :type="sourceCategoryTone(source)" size="small">{{ sourceCategoryLabel(source) }}</n-tag><n-tag v-if="source.is_protected" size="small">内置</n-tag>
                <p v-if="!source.is_available" class="sources-view__caption">{{ sourceAvailabilityLabel(source) }}</p></td>
              <td>{{ source.file_size ? formatBytes(source.file_size) : '—' }}</td>
              <td><div class="sources-view__row-actions">
                <n-button v-if="source.source_type !== 'audio'" size="small" :disabled="!source.is_available" @click="showOpen(source)">打开到…</n-button>
                <n-dropdown trigger="click" placement="bottom-end" :options="buildRowMenu(source)" @select="handleMenuSelect"><n-button quaternary circle :aria-label="`媒体 ${source.name} 的操作`"><template #icon><FIcon name="more_horizontal_20_regular" /></template></n-button></n-dropdown>
              </div></td>
            </tr></tbody>
          </table>
          <div v-else class="sources-view__cards">
            <n-card v-for="source in sourceStore.filtered" :key="source.id" size="small">
              <template #header><div class="sources-view__card-title"><n-checkbox :checked="selected.includes(source.id)" :disabled="busy || source.is_protected" :aria-disabled="busy || !!source.is_protected" :aria-label="`选择 ${source.name}`" @update:checked="toggle(source.id,$event)" /><SourceThumbnail :source="source" /><span>{{ source.name }}</span></div></template>
              <p class="sources-view__uri">{{ sourcePath(source,sourceStore.folders) }}</p>
              <div class="sources-view__row-actions"><n-tag :type="sourceCategoryTone(source)" size="small">{{ sourceCategoryLabel(source) }}</n-tag>
                <n-button v-if="source.source_type !== 'audio'" size="small" :disabled="!source.is_available" @click="showOpen(source)">打开到…</n-button>
                <n-dropdown trigger="click" :options="buildRowMenu(source)" @select="handleMenuSelect"><n-button quaternary :aria-label="`媒体 ${source.name} 的操作`">更多</n-button></n-dropdown>
              </div>
              <p v-if="!source.is_available" class="sources-view__caption">{{ sourceAvailabilityLabel(source) }}</p>
            </n-card>
          </div>
        </n-card>
      </section>
    </div>
    <FolderDialogs v-model:create-open="newFolderDialogOpen" v-model:name="newFolderName" v-model:name-error="folderNameError"
      v-model:delete-open="deleteFolderDialogOpen" v-model:delete-contents="deleteFolderContents" :creating="creatingFolder" :deleting="deletingFolder"
      :delete-target="deleteFolderTarget" :create-folder="createFolder" :delete-folder="executeFolderDelete" @delete-cancelled="deleteFolderTarget = null" />
    <AddSourceDrawer v-model:open="drawerOpen" :folder-id="sourceStore.currentFolderId" @added="refresh" />
    <EditSourceDrawer v-model:open="editDrawerOpen" :source="editingSource" @updated="refresh" />
    <SourceOpenCard v-model:open="openCard" :source="openingSource" />
  </div>
</template>
<style scoped src="./SourcesView.css"></style>
