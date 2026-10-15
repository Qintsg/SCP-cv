<!-- 预设目录与批量操作；每项结果独立显示，失败不自动重试。 -->
<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { NAlert, NButton, NInput, NModal, NSelect } from 'naive-ui';
import DirectoryNavigator from '../directories/DirectoryNavigator.vue';
import { directoryPath } from '../directories/directoryPath';
import { scenarioDirectoryApi } from '@/services/scenarioDirectoryApi';
import { useScenarioStore } from '@/stores/scenarios';
import { useDialog } from '@/composables/useDialog';
import { clientConnection } from '@/platform/connection';
import { useAuthStore } from '@/stores/auth';

const folderId = defineModel<number | null>('folderId', { required: true });
const selected = defineModel<number[]>('selected', { required: true });
const search = defineModel<string>('search', { required: true });
const props = defineProps<{ visibleIds: number[] }>();
const emit = defineEmits<{ (event: 'update:busy', value: boolean): void }>();
const store = useScenarioStore(), dialog = useDialog(), auth = useAuthStore();
const busy = ref(false), error = ref(''), results = ref<string[]>([]), modal = ref(false);
const editing = ref<number | null>(null), name = ref(''), parent = ref<number | null>(null), destination = ref<number>(0);
const currentFolder = computed(() => store.folders.find(folder => folder.id === folderId.value));
const options = computed(() => [{ label: '/', value: 0 }, ...store.folders.map(folder => ({ label: directoryPath(folder.id, store.folders), value: folder.id }))]);
let alive = true, scopeRevision = 0;
onBeforeUnmount(() => { alive = false; });
watch(busy, value => emit('update:busy', value), { flush: 'sync' });
/** :returns: 筛选或目录改变时清理隐藏选择，已确认批次独立保留。 */
function pruneSelection(): void {
  const visible = new Set(props.visibleIds);
  const next = selected.value.filter(id => visible.has(id));
  if (next.length !== selected.value.length) selected.value = next;
}
watch([folderId, search], () => { scopeRevision++; pruneSelection(); }, { flush: 'sync' });
watch(() => props.visibleIds, pruneSelection, { deep: true, flush: 'sync' });

/** :param edit: 是否修改当前目录。:returns: 可改名称和父目录的小卡片。 */
function editFolder(edit: boolean): void {
  editing.value = edit ? folderId.value : null;
  name.value = edit ? currentFolder.value?.name ?? '' : '';
  parent.value = edit ? currentFolder.value?.parent_id ?? null : folderId.value;
  error.value = ''; modal.value = true;
}
/** :param action: 显式操作。:returns: 当前主机逐项结果；代次变化停止后续发送。 */
async function execute(action: 'save' | 'folder-delete' | 'move' | 'delete'): Promise<void> {
  if (busy.value) return;
  const generation = clientConnection.captureGeneration(), user = auth.user?.id;
  const current = () => alive && generation === clientConnection.captureGeneration() && user === auth.user?.id;
  pruneSelection();
  const batch = selected.value.map(id => ({ id, path: store.scenarios.find(item => item.id === id)?.path
    ?? `/${store.scenarios.find(item => item.id === id)?.name ?? id}` }));
  if ((action === 'delete' || action === 'move') && batch.length === 0) return;
  const revision = scopeRevision, target = destination.value || null;
  const targetLabel = options.value.find(item => item.value === destination.value)?.label ?? '/';
  const folder = folderId.value, folderParent = currentFolder.value?.parent_id ?? null;
  const folderPath = directoryPath(folder, store.folders), editId = editing.value, nextName = name.value, nextParent = parent.value;
  busy.value = true; error.value = ''; results.value = [];
  try {
    if (action === 'delete' || action === 'folder-delete' || action === 'move') {
      const paths = batch.map(item => item.path).join('\n');
      const accepted = action === 'move'
        ? await dialog.confirm({ title: `移动 ${batch.length} 个预设？`, description: `${paths}\n目标：${targetLabel}`, confirmLabel: '移动所选' })
        : await dialog.danger({ title: action === 'folder-delete' ? '删除空目录？' : `删除 ${batch.length} 个预设？`,
          description: action === 'folder-delete' ? `${folderPath}\n只删除空目录；非空目录会被拒绝。` : paths, confirmLabel: '删除' });
      if (!accepted || !current()) return;
      if (revision !== scopeRevision) { error.value = '目录或搜索条件已改变，本次操作未发送。'; return; }
    }
    if (action === 'save') {
      if (editId === null) await scenarioDirectoryApi.create(nextName, nextParent);
      else await scenarioDirectoryApi.update(editId, nextName, nextParent);
      if (current()) modal.value = false;
    } else if (action === 'folder-delete' && folder !== null) {
      await scenarioDirectoryApi.remove(folder);
      if (current() && folderId.value === folder) folderId.value = folderParent;
    } else {
      for (const { id, path } of batch) {
        if (!current()) break;
        try {
          if (action === 'move') await scenarioDirectoryApi.move(id, target);
          else await store.remove(id);
          if (current()) { results.value.push(`${path}：${action === 'move' ? '已移动' : '已删除'}`); selected.value = selected.value.filter(item => item !== id); }
        } catch (failure) { if (current()) results.value.push(`${path}：${failure instanceof Error ? failure.message : '失败'}`); }
      }
    }
    if (current()) await store.refresh();
  } catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : '操作失败'; }
  finally { if (alive) { busy.value = false; pruneSelection(); } }
}
</script>
<template>
  <div class="scenario-directories">
    <DirectoryNavigator v-model:folder-id="folderId" :folders="store.folders" compact />
    <div class="tools"><n-input v-model:value="search" clearable placeholder="在当前目录及子目录搜索预设" :input-props="{ 'aria-label': '搜索预设' }" />
      <n-button :disabled="busy" @click="editFolder(false)">新建目录</n-button>
      <n-button v-if="folderId !== null" :disabled="busy" @click="editFolder(true)">编辑目录</n-button>
      <n-button v-if="folderId !== null" :disabled="busy" @click="execute('folder-delete')">删除空目录</n-button></div>
    <div v-if="selected.length" class="tools"><span>已选 {{ selected.length }} 项</span>
      <n-select v-model:value="destination" :options="options" :disabled="busy" aria-label="预设移动目标" style="width:240px" />
      <n-button :disabled="busy" @click="execute('move')">移动到…</n-button><n-button type="error" secondary :disabled="busy" @click="execute('delete')">删除所选</n-button>
      <n-button quaternary :disabled="busy" @click="selected = []">取消选择</n-button></div>
    <p v-if="busy" role="status">正在处理已选预设；结果将按原路径逐项显示。</p>
    <n-alert v-if="error" type="error">{{ error }}</n-alert>
    <n-alert v-if="results.length" type="info"><p v-for="result in results" :key="result">{{ result }}</p></n-alert>
    <n-modal v-model:show="modal" preset="card" title="预设目录" style="width:min(440px,calc(100vw - 32px))">
      <n-input v-model:value="name" :disabled="busy" placeholder="目录名称" :input-props="{ 'aria-label': '目录名称' }" />
      <n-select :value="parent ?? 0" :options="options" :disabled="busy" aria-label="目录位置" @update:value="parent = $event || null" />
      <n-alert v-if="error" type="error">{{ error }}</n-alert>
      <n-button type="primary" :loading="busy" :disabled="busy || !name.trim()" @click="execute('save')">保存</n-button>
    </n-modal>
  </div>
</template>
<style scoped>
.scenario-directories{display:grid;gap:12px}.tools{display:flex;gap:8px;flex-wrap:wrap;align-items:center}.tools>.n-input{flex:1;min-width:200px}
</style>
