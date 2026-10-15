/** 多选操作逐项结果；失败保留选择，不自动重放，不跨主机继续写入。SCP-cv 2026-10-12。 */
import { computed, onScopeDispose, ref, watch } from 'vue';
import { useSourceStore } from '@/stores/sources';
import { useAuthStore } from '@/stores/auth';
import { useDialog } from '@/composables/useDialog';
import { clientConnection } from '@/platform/connection';
import { sourcePath } from '../directories/directoryPath';

/** :returns: 当前库选择及有确认、身份围栏的批量删移。 */
export function useSourceSelection() {
  const store = useSourceStore(), auth = useAuthStore(), dialog = useDialog();
  const selected = ref<number[]>([]), busy = ref(false), destination = ref(0);
  const results = ref<{ id: number; path: string; success: boolean; detail: string }[]>([]);
  const entries = computed(() => store.sources.filter(item => selected.value.includes(item.id)));
  const selectable = computed(() => store.filtered.filter(item => !item.is_protected));
  const allSelected = computed(() => selectable.value.length > 0 && selectable.value.every(item => selected.value.includes(item.id)));
  const destinations = computed(() => [{ label: '/ · 根目录', value: 0 }, ...store.folders.filter(folder => !folder.is_protected)
    .map(folder => ({ label: folder.relative_path || '/' + folder.name, value: folder.id }))]);
  let alive = true, scopeRevision = 0;
  onScopeDispose(() => { alive = false; });
  /** :returns: 只保留当前可见普通媒体；已经确认的批次另存，不受列表变化替换。 */
  function pruneSelection(): void {
    const visible = new Set(selectable.value.map(item => item.id));
    selected.value = selected.value.filter(id => visible.has(id));
  }
  watch(() => [store.currentFolderId, store.category, store.searchKeyword, store.searchSubfolders], () => {
    scopeRevision++; pruneSelection();
    if (!busy.value) results.value = [];
  }, { flush: 'sync' });
  watch(selectable, pruneSelection, { flush: 'sync' });
  /** :param id: 普通源。:param checked: 选择意图。:returns: 不选择锁定源。 */
  function toggle(id: number, checked: boolean): void {
    if (busy.value || store.findById(id)?.is_protected) return;
    selected.value = checked ? [...new Set([...selected.value, id])] : selected.value.filter(value => value !== id);
  }
  /** :param checked: 全选当前结果。:returns: 不影响其它目录或锁定项。 */
  function toggleAll(checked: boolean): void { for (const item of selectable.value) toggle(item.id, checked); }
  /** :param action: 已选择的操作。:returns: 每项独立结果，确认后才发送，不宣称总事务。 */
  async function execute(action: 'delete' | 'move'): Promise<void> {
    if (busy.value || entries.value.length === 0) return;
    pruneSelection();
    if (entries.value.length === 0) return;
    const batch = entries.value.map(item => ({ ...item, path: sourcePath(item, store.folders) }));
    const generation = clientConnection.captureGeneration(), user = auth.user?.id, revision = scopeRevision;
    const target = destination.value === 0 ? null : destination.value;
    const detail = batch.map(item => item.path).join('\n');
    const targetLabel = destinations.value.find(item => item.value === destination.value)?.label;
    const current = () => alive && generation === clientConnection.captureGeneration() && user === auth.user?.id;
    busy.value = true; results.value = [];
    try {
      const accepted = action === 'delete'
        ? await dialog.danger({ title: `删除 ${batch.length} 个媒体源？`, description: detail + '\n删除对应受管原件，正在使用或内置项会拒绝。', confirmLabel: '删除所选' })
        : await dialog.confirm({ title: `移动 ${batch.length} 个媒体源？`, description: detail + `\n目标：${targetLabel}`, confirmLabel: '移动所选' });
      if (!accepted || !current()) return;
      if (revision !== scopeRevision) {
        results.value = batch.map(item => ({ id: item.id, path: item.path, success: false, detail: '列表范围已改变，本次操作未发送' }));
        return;
      }
      for (const item of batch) {
        const path = item.path;
        if (!current()) break;
        try {
          if (item.is_protected) throw new Error('内置源不可修改');
          if (action === 'delete') await store.deleteSource(item.id); else await store.moveSource(item.id, target);
          if (!current()) break;
          results.value.push({ id: item.id, path, success: true, detail: action === 'delete' ? '已删除' : '已移动' });
          selected.value = selected.value.filter(id => id !== item.id);
        } catch (error) {
          if (current()) results.value.push({ id: item.id, path, success: false, detail: error instanceof Error ? error.message : '未完成，请核对' });
        }
      }
    } finally { busy.value = false; if (alive) pruneSelection(); }
  }
  return { selected, busy, destination, results, allSelected, destinations, toggle, toggleAll, execute };
}
