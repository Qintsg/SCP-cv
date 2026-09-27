/*
 * 媒体源页面的实体文件夹操作：浏览、创建、重命名、移动与删除。
 */
import { computed, h, ref, type Ref } from 'vue';
import { useI18n } from 'vue-i18n';
import type { DropdownOption } from 'naive-ui';

import FIcon from '@/design-system/FIcon.vue';
import { useToast } from '@/composables/useToast';
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
  const breadcrumbs = computed(() => store.folderBreadcrumbs);
  const childFolders = computed(() => store.childFolders);

  async function navigateToFolder(folderId: number | null): Promise<void> {
    isLoading.value = true;
    try {
      store.setCurrentFolder(folderId);
      await store.refresh();
    } catch (error) {
      toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
    } finally {
      isLoading.value = false;
    }
  }

  function openFolderDialog(): void {
    newFolderName.value = '';
    folderNameError.value = false;
    newFolderDialogOpen.value = true;
  }

  async function createFolder(): Promise<boolean> {
    const name = newFolderName.value.trim();
    if (!name) { folderNameError.value = true; return false; }
    folderNameError.value = false;
    creatingFolder.value = true;
    try {
      await store.createFolder(name, store.currentFolderId);
      toast.success(t('sources.folderCreatedOk'));
      newFolderName.value = '';
      newFolderDialogOpen.value = false;
      return true;
    } catch (error) {
      toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
      return false;
    } finally {
      creatingFolder.value = false;
    }
  }

  async function renameFolder(folder: MediaFolderItem): Promise<void> {
    const newName = window.prompt(t('sources.renameFolder'), folder.name);
    if (newName === null || newName.trim() === folder.name) return;
    try {
      await store.renameFolder(folder.id, newName.trim());
      toast.success(t('sources.folderRenamedOk'));
    } catch (error) {
      toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  }

  function deleteFolderConfirm(folder: MediaFolderItem): void {
    deleteFolderTarget.value = folder;
    deleteFolderContents.value = false;
    deleteFolderDialogOpen.value = true;
  }

  async function executeFolderDelete(): Promise<void> {
    const folder = deleteFolderTarget.value;
    if (folder === null) return;
    deletingFolder.value = true;
    try {
      await store.deleteFolder(folder.id, deleteFolderContents.value);
      toast.success(t('sources.folderDeletedOk'));
      deleteFolderDialogOpen.value = false;
      deleteFolderTarget.value = null;
    } catch (error) {
      toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
    } finally {
      deletingFolder.value = false;
    }
  }

  async function moveFolder(folder: MediaFolderItem, parentId: number | null): Promise<void> {
    try {
      await store.moveFolder(folder.id, parentId);
      toast.success(parentId === null ? t('sources.folderMovedRootOk') : t('sources.folderMovedOk'));
    } catch (error) {
      toast.error(t('sources.folderFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  }

  function descendantsOf(folderId: number): Set<number> {
    const result = new Set([folderId]);
    for (const folder of store.folders) {
      let cursor = folder.parent_id;
      while (cursor !== null) {
        if (cursor === folderId) { result.add(folder.id); break; }
        cursor = store.folders.find((candidate) => candidate.id === cursor)?.parent_id ?? null;
      }
    }
    return result;
  }

  function buildFolderMenu(folder: MediaFolderItem): DropdownOption[] {
    const excluded = descendantsOf(folder.id);
    const destinations: DropdownOption[] = [
      { label: t('sources.moveToRoot'), key: `folder-root-${folder.id}`,
        disabled: folder.parent_id === null, props: { onClick: () => moveFolder(folder, null) } },
      ...store.folders.filter((candidate) => !excluded.has(candidate.id)).map((candidate) => ({
        label: candidate.relative_path || candidate.name,
        key: `folder-${folder.id}-to-${candidate.id}`,
        disabled: folder.parent_id === candidate.id,
        props: { onClick: () => moveFolder(folder, candidate.id) },
      })),
    ];
    return [
      { label: t('sources.renameFolder'), key: 'rename-folder',
        icon: () => h(FIcon, { name: 'edit_24_regular', size: 18 }),
        props: { onClick: () => renameFolder(folder) } },
      { label: t('sources.moveFolder'), key: 'move-folder',
        icon: () => h(FIcon, { name: 'folder_24_regular', size: 18 }), children: destinations },
      { label: t('sources.deleteFolder'), key: 'delete-folder',
        icon: () => h(FIcon, { name: 'delete_24_regular', size: 18 }),
        props: { onClick: () => deleteFolderConfirm(folder), style: 'color: var(--colorStatusDangerForeground1);' } },
    ];
  }

  function buildMoveToFolderOptions(source: MediaSourceItem): DropdownOption[] {
    return [
      { label: t('sources.moveToRoot'), key: 'move-root', disabled: source.folder_id === null,
        props: { onClick: () => moveSourceToFolder(source, null) } },
      ...store.folders.filter((folder) => folder.id !== source.folder_id).map((folder) => ({
        label: folder.relative_path || folder.name, key: `move-${folder.id}`,
        props: { onClick: () => moveSourceToFolder(source, folder.id) },
      })),
    ];
  }

  async function moveSourceToFolder(source: MediaSourceItem, folderId: number | null): Promise<void> {
    try {
      await store.moveSource(source.id, folderId);
      const folderName = store.folders.find((folder) => folder.id === folderId)?.name;
      toast.success(folderId === null ? t('sources.movedRootOk') : t('sources.movedOk', { name: folderName ?? '' }));
      await refresh();
    } catch (error) {
      toast.error(t('sources.moveFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  }

  return { newFolderDialogOpen, newFolderName, folderNameError, creatingFolder,
    deleteFolderTarget, deleteFolderContents, deletingFolder, deleteFolderDialogOpen,
    breadcrumbs, childFolders, navigateToFolder, openFolderDialog, createFolder,
    executeFolderDelete, buildFolderMenu, buildMoveToFolderOptions };
}
