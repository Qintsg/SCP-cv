/*
 * 媒体源页面的实体文件夹操作：浏览、创建、重命名、移动与删除。
 */
import { computed, h, ref, type Ref } from 'vue';
import { useI18n } from 'vue-i18n';
import type { ActionDropdownOption } from '@/design-system/dropdownActions';

import FIcon from '@/design-system/FIcon.vue';
import { useToast } from '@/composables/useToast';
import { useRequestContext } from '@/composables/useRequestContext';
import type { MediaFolderItem, MediaSourceItem } from '@/services/api';
import { useSourceStore } from '@/stores/sources';

/** 让媒体源视图只负责布局；所有目录动作共享同一刷新与错误反馈。 */
export function useSourceFolders(refresh: () => Promise<void>, isLoading: Ref<boolean>) {
  const { t } = useI18n();
  const toast = useToast();
  const store = useSourceStore();
  const newFolderDialogOpen = ref(false);
  const newFolderName = ref('');
  const folderNameError = ref(false);
  const creatingFolder = ref(false);
  const deleteFolderTarget = ref<MediaFolderItem | null>(null);
  const deleteFolderContents = ref(false);
  const deletingFolder = ref(false);
  const deleteFolderDialogOpen = ref(false);
  const renameFolderOpen = ref(false), renameValue = ref(''), renameFolderError = ref('');
  const renameFolderName = computed({ get: () => renameValue.value,
    set: (value: string) => { renameValue.value = value; renameFolderError.value = ''; } });
  const renameFolderTarget = ref<MediaFolderItem | null>(null), renamingFolder = ref(false);
  const captureContext = useRequestContext(() => {
    renameFolderOpen.value = false; renameFolderTarget.value = null;
    renameFolderName.value = ''; renameFolderError.value = ''; renamingFolder.value = false;
    newFolderDialogOpen.value = false; creatingFolder.value = false; newFolderName.value = ''; folderNameError.value = false;
    deleteFolderDialogOpen.value = false; deleteFolderTarget.value = null; deletingFolder.value = false;
    isLoading.value = false;
  });
  let renameCurrent = () => false;
  let deleteCurrent = () => false;
  let createParent: number | null = null;
  const breadcrumbs = computed(() => store.folderBreadcrumbs);
  const childFolders = computed(() => store.childFolders);

  /** :param folderId: 明确选择的目录。:returns: 当前归属的读取完成或保留真实错误。 */
  async function navigateToFolder(folderId: number | null): Promise<void> {
    const current = captureContext();
    isLoading.value = true;
    try {
      store.setCurrentFolder(folderId);
      await store.refresh();
    } catch (error) {
      if (current()) toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
    } finally {
      if (current()) isLoading.value = false;
    }
  }

  /** :returns: 冻结父目录并清空新建表单，等待中不重复打开。 */
  function openFolderDialog(): void {
    if (creatingFolder.value) return;
    createParent = store.currentFolderId;
    newFolderName.value = '';
    folderNameError.value = false;
    newFolderDialogOpen.value = true;
  }

  /** :returns: 仅原归属创建成功时允许关闭；错误保留供重试。 */
  async function createFolder(): Promise<boolean> {
    if (creatingFolder.value) return false;
    const current = captureContext();
    const name = newFolderName.value.trim();
    if (!name) { folderNameError.value = true; return false; }
    folderNameError.value = false;
    creatingFolder.value = true;
    try {
      await store.createFolder(name, createParent);
      if (!current()) return false;
      toast.success(t('sources.folderCreatedOk'));
      newFolderName.value = '';
      newFolderDialogOpen.value = false;
      return true;
    } catch (error) {
      if (current()) toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
      return false;
    } finally {
      if (current()) creatingFolder.value = false;
    }
  }

  /** :param folder: 菜单选中的目录。:returns: 冻结原目录并打开Ant表单，不发送请求。 */
  function renameFolder(folder: MediaFolderItem): void {
    if (folder.is_protected || renamingFolder.value) return;
    renameCurrent = captureContext();
    renameFolderTarget.value = { ...folder };
    renameFolderName.value = folder.name; renameFolderError.value = ''; renameFolderOpen.value = true;
  }

  /** :returns: 原归属下重命名成功才允许关闭；失败保留名称与目标。 */
  async function executeFolderRename(): Promise<boolean> {
    const folder = renameFolderTarget.value, name = renameFolderName.value.trim(), current = renameCurrent;
    if (!folder || folder.is_protected || renamingFolder.value || !current()) return false;
    if (!name) { renameFolderError.value = '请输入目录名称'; return false; }
    if (name === folder.name) return true;
    renamingFolder.value = true; renameFolderError.value = '';
    try {
      await store.renameFolder(folder.id, name, current);
      if (!current()) return false;
      toast.success(t('sources.folderRenamedOk'));
      return true;
    } catch (error) {
      if (current()) renameFolderError.value = error instanceof Error ? error.message : t('common.retry');
      return false;
    } finally { if (current()) renamingFolder.value = false; }
  }

  /** :param folder: 原目录。:returns: 冻结删除对象，不立即发送请求。 */
  function deleteFolderConfirm(folder: MediaFolderItem): void {
    if (folder.is_protected || deletingFolder.value) return;
    deleteCurrent = captureContext();
    deleteFolderTarget.value = { ...folder };
    deleteFolderContents.value = false;
    deleteFolderDialogOpen.value = true;
  }

  /** :returns: 按冻结对象和递归选项删除，旧归属不再续读或反馈。 */
  async function executeFolderDelete(): Promise<void> {
    const folder = deleteFolderTarget.value;
    const current = deleteCurrent, deleteContents = deleteFolderContents.value;
    if (folder === null || folder.is_protected || deletingFolder.value || !current()) return;
    deletingFolder.value = true;
    try {
      await store.deleteFolder(folder.id, deleteContents, current);
      if (!current()) return;
      toast.success(t('sources.folderDeletedOk'));
      deleteFolderDialogOpen.value = false;
      deleteFolderTarget.value = null;
    } catch (error) {
      if (current()) toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
    } finally {
      if (current()) deletingFolder.value = false;
    }
  }

  /** :param folder: 原目录。:param parentId: 明确目标。:returns: 当前归属下完成或反馈真实错误。 */
  async function moveFolder(folder: MediaFolderItem, parentId: number | null): Promise<void> {
    const current = captureContext();
    try {
      await store.moveFolder(folder.id, parentId, current);
      if (!current()) return;
      toast.success(parentId === null ? t('sources.folderMovedRootOk') : t('sources.folderMovedOk'));
    } catch (error) {
      if (current()) toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  }

  function descendantsOf(folderId: number): Set<number> {
    const result = new Set([folderId]);
    for (const folder of store.folders) {
      let cursor = folder.parent_id;
      const visited = new Set<number>();
      while (cursor !== null && !visited.has(cursor)) {
        visited.add(cursor);
        if (cursor === folderId) { result.add(folder.id); break; }
        cursor = store.folders.find((candidate) => candidate.id === cursor)?.parent_id ?? null;
      }
    }
    return result;
  }

  /**
   * 构造文件夹菜单，独立动作不注册到 DOM 点击事件。
   * :param folder: 当前文件夹。
   * :returns: 经过禁用门禁的语义菜单。
   */
  function buildFolderMenu(folder: MediaFolderItem): ActionDropdownOption[] {
    if (folder.is_protected) return [];
    const excluded = descendantsOf(folder.id);
    const destinations: ActionDropdownOption[] = [
      { label: t('sources.moveToRoot'), key: `folder-root-${folder.id}`,
        disabled: folder.parent_id === null, action: () => moveFolder(folder, null) },
      ...store.folders.filter((candidate) => !excluded.has(candidate.id) && !candidate.is_protected).map((candidate) => ({
        label: candidate.relative_path || candidate.name,
        key: `folder-${folder.id}-to-${candidate.id}`,
        disabled: folder.parent_id === candidate.id,
        action: () => moveFolder(folder, candidate.id),
      })),
    ];
    return [
      { label: t('sources.renameFolder'), key: 'rename-folder',
        icon: () => h(FIcon, { name: 'edit_24_regular', size: 18 }),
        action: () => renameFolder(folder) },
      { label: t('sources.moveFolder'), key: 'move-folder',
        icon: () => h(FIcon, { name: 'folder_24_regular', size: 18 }), children: destinations },
      { label: t('sources.deleteFolder'), key: 'delete-folder',
        icon: () => h(FIcon, { name: 'delete_24_regular', size: 18 }),
        action: () => deleteFolderConfirm(folder), props: { style: 'color: var(--colorStatusDangerForeground1);' } },
    ];
  }

  /**
   * 构造源的目标目录列表，不将动作暴露为 DOM props。
   * :param source: 当前媒体源。
   * :returns: 可选择或明确禁用的目标目录。
   */
  function buildMoveToFolderOptions(source: MediaSourceItem): ActionDropdownOption[] {
    return [
      { label: t('sources.moveToRoot'), key: 'move-root', disabled: source.folder_id === null,
        action: () => moveSourceToFolder(source, null) },
      ...store.folders.filter((folder) => folder.id !== source.folder_id && !folder.is_protected).map((folder) => ({
        label: folder.relative_path || folder.name, key: `move-${folder.id}`,
        action: () => moveSourceToFolder(source, folder.id),
      })),
    ];
  }

  /** :param source: 原媒体。:param folderId: 冻结目标。:returns: 当前页面才反馈并刷新。 */
  async function moveSourceToFolder(source: MediaSourceItem, folderId: number | null): Promise<void> {
    const current = captureContext(), folderName = store.folders.find(folder => folder.id === folderId)?.name;
    try {
      await store.moveSource(source.id, folderId);
      if (!current()) return;
      toast.success(folderId === null ? t('sources.movedRootOk') : t('sources.movedOk', { name: folderName ?? '' }));
      await refresh();
    } catch (error) {
      if (current()) toast.error(t('sources.moveFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  }

  return { newFolderDialogOpen, newFolderName, folderNameError, creatingFolder,
    renameFolderOpen, renameFolderName, renameFolderTarget, renameFolderError, renamingFolder, executeFolderRename,
    deleteFolderTarget, deleteFolderContents, deletingFolder, deleteFolderDialogOpen,
    breadcrumbs, childFolders, navigateToFolder, openFolderDialog, createFolder,
    executeFolderDelete, buildFolderMenu, buildMoveToFolderOptions };
}
