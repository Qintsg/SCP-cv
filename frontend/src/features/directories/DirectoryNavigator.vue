<!-- 目录优先导航：桌面Ant Tree，窄屏可搜索Select；目录选择不触发文件或播放操作。 -->
<script setup lang="ts">
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import { computed, h, ref, watch } from 'vue';
import ASelect from 'ant-design-vue/es/select';
import ATree from 'ant-design-vue/es/tree';
import type { TreeProps } from 'ant-design-vue';
import FIcon from '@/design-system/FIcon.vue';
import { nullableNumber } from '@/design-system/controlValue';
import { directoryPath, type DirectoryEntry } from './directoryPath';
const props = defineProps<{ folders: DirectoryEntry[]; compact?: boolean }>();
const folderId = defineModel<number | null>('folderId', { required: true });
const expanded = ref<(string | number)[]>(['root']);
const entries = computed(() => [...props.folders].sort((a, b) => a.name.localeCompare(b.name, 'zh-Hans-CN')));
const options = computed(() => [{ label: '/ · 根目录', value: 0 }, ...entries.value.map(folder => ({
  label: directoryPath(folder.id, props.folders) + (folder.is_protected ? ' · 内置' : ''), value: folder.id,
}))]);
const currentPath = computed(() => directoryPath(folderId.value, props.folders));
/** :param parent: 父目录。:param seen: 祖先集合。:returns: 有界目录树。 */
function children(parent: number | null, seen = new Set<number>()): NonNullable<TreeProps['treeData']> {
  return entries.value.filter(folder => folder.parent_id === parent && !seen.has(folder.id)).map(folder => ({
    key: folder.id, title: folder.name + (folder.is_protected ? ' · 内置' : ''),
    icon: () => h(FIcon, { name: 'folder_24_regular', size: 17 }),
    children: children(folder.id, new Set([...seen, folder.id])),
  }));
}
const tree = computed<TreeProps['treeData']>(() => [{ key: 'root', title: '/ 根目录',
  icon: () => h(FIcon, { name: 'folder_24_regular', size: 17 }), children: children(null) }]);
watch(() => props.folders.map(folder => folder.id).join(','), () => {
  expanded.value = [...new Set([...expanded.value, 'root', ...props.folders.map(folder => folder.id)])];
}, { immediate: true });
/** :param keys: Ant选中项。:returns: 仅改变浏览目录。 */
function select(keys: (string | number)[]): void {
  if (keys.length) folderId.value = keys[0] === 'root' ? null : Number(keys[0]);
}
</script>
<template>
  <nav class="directory-nav" :class="{ 'directory-nav--compact': compact }" aria-label="目录导航">
    <a-select v-select-aria-label="'当前目录'" v-if="compact" :value="folderId ?? 0" :options="options" aria-label="当前目录" show-search option-filter-prop="label"
      @update:value="folderId = nullableNumber($event) || null" />
    <template v-else>
      <p class="directory-nav__label">文件目录</p>
      <a-tree v-model:expandedKeys="expanded" :tree-data="tree" :selected-keys="[folderId ?? 'root']" show-icon block-node @select="select" />
      <p class="directory-nav__path" :title="currentPath">{{ currentPath }}</p>
    </template>
  </nav>
</template>
<style scoped>
.directory-nav{display:flex;flex-direction:column;gap:12px;padding:16px 8px;border:1px solid var(--colorNeutralStroke2);border-radius:8px;background:var(--colorNeutralBackground1);min-width:0;max-height:calc(100dvh - 180px);overflow:auto;position:sticky;top:0}
.directory-nav__label{color:var(--colorNeutralForeground3);font-size:12px;font-weight:600;margin:0 8px 4px}
.directory-nav__path{font:12px Consolas,monospace;overflow-wrap:anywhere;margin:8px;color:var(--colorNeutralForeground3);border-top:1px solid var(--colorNeutralStroke2);padding-top:12px}
.directory-nav--compact{max-height:none;padding:0;border:0;position:static}
.directory-nav :deep(.ant-tree-title){overflow-wrap:anywhere}
</style>
