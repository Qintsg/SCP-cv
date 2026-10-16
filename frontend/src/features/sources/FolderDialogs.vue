<script setup lang="ts">
/**
 * 文件夹创建、重命名与删除对话框，复用页面的目录状态与操作回调。
 * 输入属性传给原生控件，保持文件夹错误的可访问语义。
 */
import { useI18n } from 'vue-i18n';
import { ref, useId, watch } from 'vue';
import AAlert from 'ant-design-vue/es/alert';
import ACheckbox from 'ant-design-vue/es/checkbox';
import AInput from 'ant-design-vue/es/input';
import AModal from 'ant-design-vue/es/modal';
import { restoreDialogFocus } from '@/design-system/dialogFocus';
import type { MediaFolderItem } from '@/services/api';

const props = defineProps<{
  creating: boolean;
  deleting: boolean;
  deleteTarget: MediaFolderItem | null;
  createFolder: () => Promise<boolean>;
  deleteFolder: () => Promise<void>;
  renaming?: boolean;
  renameTarget?: MediaFolderItem | null;
  renameError?: string;
  renameFolder?: () => Promise<boolean>;
}>();

const emit = defineEmits<{ (event: 'delete-cancelled'): void }>();
const createOpen = defineModel<boolean>('createOpen', { required: true });
const name = defineModel<string>('name', { required: true });
const nameError = defineModel<boolean>('nameError', { required: true });
const deleteOpen = defineModel<boolean>('deleteOpen', { required: true });
const deleteContents = defineModel<boolean>('deleteContents', { required: true });
const renameOpen = defineModel<boolean>('renameOpen', { default: false });
const renameName = defineModel<string>('renameName', { default: '' });
const renameErrorId = useId();
const renameMarker = 'folder-rename-' + useId().replaceAll(':', '-');
let renameTrigger: HTMLElement | null = null;
const renameInput = ref<{ focus: (options: { cursor: 'all' }) => void } | null>(null);
const { t } = useI18n();
watch([renameOpen, renameInput], ([open, input]) => { if (open) input?.focus({ cursor: 'all' }); }, { flush: 'post' });
watch(renameOpen, open => {
  if (open) renameTrigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
}, { flush: 'sync' });

/** :returns: 自动聚焦输入框时Ant不会记录外部焦点，因此由业务显式安全返还。 */
function afterRenameClose(): void {
  if (!renameOpen.value && props.renameTarget) restoreDialogFocus(renameTrigger, document.getElementsByClassName(renameMarker)[0] ?? null);
}

/** :returns: 只在父级确认本次成功后关闭，不自动吞掉后端错误或取消等待中的请求。 */
async function submitRename(): Promise<boolean> {
  if (!renameOpen.value || props.renaming || !props.renameFolder) return false;
  const renamed = await props.renameFolder();
  if (renamed) renameOpen.value = false;
  return renamed;
}

/** :param event: 输入框自己的按下事件。:returns: 不接收打开菜单的keyup，不打断中文组合输入。 */
function renameFromKey(event: KeyboardEvent): void {
  if (event.isComposing || event.keyCode === 229 || event.repeat) return;
  event.preventDefault();
  void submitRename();
}

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
  <a-modal v-model:open="renameOpen" title="重命名目录" ok-text="重命名" :cancel-text="t('common.cancel')"
    :wrap-class-name="renameMarker" :focus-trigger-after-close="false" :after-close="afterRenameClose"
    :confirm-loading="renaming" :ok-button-props="{ disabled: renaming }" :cancel-button-props="{ disabled: renaming }"
    :mask-closable="!renaming" :keyboard="!renaming" :closable="!renaming" @ok="submitRename">
    <p class="folder-rename-path">原目录：{{ renameTarget?.relative_path || renameTarget?.name }}</p>
    <a-input ref="renameInput" v-model:value="renameName" aria-label="新目录名称" :disabled="renaming"
      :status="renameError ? 'error' : undefined" :aria-invalid="Boolean(renameError)"
      :aria-describedby="renameError ? renameErrorId : undefined" @pressEnter="renameFromKey" />
    <a-alert v-if="renameError" :id="renameErrorId" type="error" show-icon :message="renameError" style="margin-top:var(--spacingVerticalM)" />
  </a-modal>
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
    <a-checkbox v-model:checked="deleteContents" :disabled="deleting">
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
.folder-rename-path { margin:0 0 var(--spacingVerticalM); color:var(--colorNeutralForeground2); overflow-wrap:anywhere; }
</style>
