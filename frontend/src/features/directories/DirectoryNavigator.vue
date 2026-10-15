<script setup lang="ts">
/** 目录优先导航，媒体库/屏控/预设复用，选中状态属于调用页面。SCP-cv 2026-10-12。 */
import { computed } from 'vue';
import { NSelect } from 'naive-ui';
import FIcon from '@/design-system/FIcon.vue';
import { directoryPath, type DirectoryEntry } from './directoryPath';
const props = defineProps<{ folders: DirectoryEntry[]; compact?: boolean }>();
const folderId = defineModel<number | null>('folderId', { required: true });
const entries = computed(() => [...props.folders].sort((a, b) => directoryPath(a.id, props.folders).localeCompare(directoryPath(b.id, props.folders), 'zh-Hans-CN')));
const options = computed(() => [{ label: '/ · 根目录', value: 0 }, ...entries.value.map(folder => ({
  label: directoryPath(folder.id, props.folders) + (folder.is_protected ? ' · 内置' : ''), value: folder.id,
}))]);
const currentPath = computed(() => directoryPath(folderId.value, props.folders));
</script>
<template>
  <nav class="directory-nav" :class="{ 'directory-nav--compact': compact }" aria-label="目录导航">
    <template v-if="compact">
      <n-select :value="folderId ?? 0" :options="options" aria-label="当前目录" filterable
        @update:value="folderId = $event === 0 ? null : $event" />
    </template>
    <template v-else>
      <p class="directory-nav__label">目录</p>
      <button type="button" :aria-current="folderId === null ? 'page' : undefined" @click="folderId = null">
        <FIcon name="folder_24_regular" :size="18" /><span>/ 根目录</span>
      </button>
      <button v-for="folder in entries" :key="folder.id" type="button" :title="directoryPath(folder.id, folders)"
        :aria-current="folderId === folder.id ? 'page' : undefined" @click="folderId = folder.id">
        <FIcon name="folder_24_regular" :size="18" /><span>{{ directoryPath(folder.id, folders) }}</span>
        <small v-if="folder.is_protected">内置</small>
      </button>
    </template>
    <p v-if="!compact" class="directory-nav__path" :title="currentPath">{{ currentPath }}</p>
  </nav>
</template>
<style scoped>
.directory-nav{display:flex;flex-direction:column;gap:4px;padding:var(--spacingVerticalS);border:1px solid var(--colorNeutralStroke2);border-radius:var(--borderRadiusLarge);background:var(--colorNeutralBackground1);min-width:0;max-height:60vh;overflow:auto}
.directory-nav__label{color:var(--colorNeutralForeground3);font-size:var(--fontSizeBase200);margin:4px 8px}
.directory-nav button{display:flex;align-items:center;gap:8px;text-align:left;border:0;border-radius:var(--borderRadiusMedium);padding:10px;background:transparent;color:var(--colorNeutralForeground2);font:inherit;cursor:pointer;min-width:0}
.directory-nav button:hover{background:var(--colorNeutralBackground1Hover)}
.directory-nav button[aria-current=page]{background:var(--colorBrandBackground2);color:var(--colorBrandForeground1);font-weight:600}
.directory-nav button span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1}
.directory-nav small{font-size:11px;color:var(--colorNeutralForeground3)}
.directory-nav__path{font-family:monospace;overflow-wrap:anywhere;margin:6px 8px;color:var(--colorNeutralForeground3);font-size:12px}
.directory-nav--compact{max-height:none;padding:8px}
</style>
