<script setup lang="ts">
/**
 * 文件夹创建与删除对话框，复用页面的目录状态与操作回调。
 * 输入属性传给原生控件，保持文件夹错误的可访问语义。
 */
import { useI18n } from 'vue-i18n';
import { NAlert, NCheckbox, NInput, NModal } from 'naive-ui';
import type { MediaFolderItem } from '@/services/api';

const props = defineProps<{
  creating: boolean;
  deleting: boolean;
  deleteTarget: MediaFolderItem | null;
  createFolder: () => Promise<boolean>;
  deleteFolder: () => Promise<void>;
}>();

const emit = defineEmits<{ (event: 'delete-cancelled'): void }>();
const createOpen = defineModel<boolean>('createOpen', { required: true });
const name = defineModel<string>('name', { required: true });
const nameError = defineModel<boolean>('nameError', { required: true });
const deleteOpen = defineModel<boolean>('deleteOpen', { required: true });
const deleteContents = defineModel<boolean>('deleteContents', { required: true });
const { t } = useI18n();

/**
 * 等待当前创建完成，避免回车再次提交同一目录。
 * :returns: 创建结果；正在等待时返回 false，保持弹窗。
 */
async function submitCreate(): Promise<boolean> {
  if (props.creating) return false;
  return props.createFolder();
}

/**
 * 由目录操作在成功时关闭确认框，避免 void 失败回调触发组件库自动关闭。
 * :returns: false，保留父级对弹窗状态的控制。
 */
async function submitDelete(): Promise<boolean> {
  if (props.deleting) return false;
  await props.deleteFolder();
  return false;
}
</script>

<template>
  <n-modal v-model:show="deleteOpen" preset="dialog"
    :title="t('sources.deleteFolderTitle', { name: deleteTarget?.name ?? '' })"
    :positive-text="t('sources.deleteFolderOk')"
    :negative-text="t('common.cancel')"
    :positive-button-props="{ type: 'error', loading: deleting }"
    @positive-click="submitDelete"
    @negative-click="emit('delete-cancelled')">
    <p style="margin: 0 0 var(--spacingVerticalS); color: var(--colorNeutralForeground2);">
      {{ t('sources.deleteFolderDesc') }}
    </p>
    <n-checkbox v-model:checked="deleteContents">
      {{ t('sources.deleteFolderContentsCheckbox') }}
    </n-checkbox>
    <n-alert v-if="deleteContents" type="warning" :closable="false" style="margin-top: var(--spacingVerticalS);">
      {{ t('sources.deleteFolderContentsWarn') }}
    </n-alert>
  </n-modal>

  <n-modal v-model:show="createOpen" preset="dialog" :title="t('sources.newFolder')"
    :positive-text="t('sources.newFolderOk')" :negative-text="t('common.cancel')"
    :loading="creating" @positive-click="submitCreate">
    <n-input v-model:value="name" :placeholder="t('sources.newFolderPlaceholder')"
      :input-props="{ 'aria-label': t('sources.newFolderName'), 'aria-invalid': nameError }"
      :disabled="creating" :status="nameError ? 'error' : undefined"
      @update:value="nameError = false" @keyup.enter="submitCreate" />
    <div v-if="nameError" class="folder-name-error" role="alert">
      {{ t('sources.newFolderNameRequired') }}
    </div>
  </n-modal>
</template>

<style scoped>
/* 沿用媒体页的目录校验提示设计令牌。 */
.folder-name-error {
  margin-top: var(--spacing-xs);
  color: var(--colorStatusDangerForeground1);
  font-size: var(--fontSizeBase200);
}
</style>
