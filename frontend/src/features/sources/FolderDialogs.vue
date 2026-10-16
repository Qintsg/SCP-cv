<script setup lang="ts">
/**
 * 文件夹创建与删除对话框，复用页面的目录状态与操作回调。
 * 输入属性传给原生控件，保持文件夹错误的可访问语义。
 */
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import ACheckbox from 'ant-design-vue/es/checkbox';
import AInput from 'ant-design-vue/es/input';
import AModal from 'ant-design-vue/es/modal';
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
  const created = await props.createFolder();
  if (created) createOpen.value = false;
  return created;
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

/** :param value: 原生输入值。:returns: 更新名称并清除旧校验，不重复绑定Ant更新事件。 */
function updateName(value: string): void { name.value = value; nameError.value = false; }
</script>

<template>
  <a-modal v-model:open="deleteOpen"
    :title="t('sources.deleteFolderTitle', { name: deleteTarget?.name ?? '' })"
    :ok-text="t('sources.deleteFolderOk')"
    :cancel-text="t('common.cancel')"
    :ok-button-props="{ danger: true, loading: deleting, disabled: deleting }"
    @ok="submitDelete"
    @cancel="emit('delete-cancelled')">
    <p style="margin: 0 0 var(--spacingVerticalS); color: var(--colorNeutralForeground2);">
      {{ t('sources.deleteFolderDesc') }}
    </p>
    <a-checkbox v-model:checked="deleteContents">
      {{ t('sources.deleteFolderContentsCheckbox') }}
    </a-checkbox>
    <a-alert v-if="deleteContents" type="warning" :closable="false" style="margin-top: var(--spacingVerticalS);"><template #description>
      {{ t('sources.deleteFolderContentsWarn') }}
    </template></a-alert>
  </a-modal>

  <a-modal v-model:open="createOpen"  :title="t('sources.newFolder')"
    :ok-text="t('sources.newFolderOk')" :cancel-text="t('common.cancel')"
    :confirm-loading="creating" :ok-button-props="{ disabled: creating }" @ok="submitCreate">
    <a-input :value="name" :placeholder="t('sources.newFolderPlaceholder')"
      v-bind="{ 'aria-label': t('sources.newFolderName'), 'aria-invalid': nameError }"
      :disabled="creating" :status="nameError ? 'error' : undefined"
      @update:value="updateName" @keyup.enter="submitCreate" />
    <div v-if="nameError" class="folder-name-error" role="alert">
      {{ t('sources.newFolderNameRequired') }}
    </div>
  </a-modal>
</template>

<style scoped>
/* 沿用媒体页的目录校验提示设计令牌。 */
.folder-name-error {
  margin-top: var(--spacing-xs);
  color: var(--colorStatusDangerForeground1);
  font-size: var(--fontSizeBase200);
}
</style>
