/** 多选操作逐项结果；失败保留选择，不自动重放，不跨主机继续写入。SCP-cv 2026-10-12。 */
import { computed, ref, watch } from 'vue';
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
  watch(() => store.currentFolderId, () => { if (!busy.value) { selected.value = []; results.value = []; } });
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
    const batch = [...entries.value], generation = clientConnection.captureGeneration(), user = auth.user?.id;
    const target = destination.value === 0 ? null : destination.value;
    const detail = batch.map(item => sourcePath(item, store.folders)).join('\n');
    const accepted = action === 'delete'
      ? await dialog.danger({ title: `删除 ${batch.length} 个媒体源？`, description: detail + '\n删除对应受管原件，正在使用或内置项会拒绝。', confirmLabel: '删除所选' })
      : await dialog.confirm({ title: `移动 ${batch.length} 个媒体源？`, description: detail + `\n目标：${destinations.value.find(item => item.value === destination.value)?.label}`, confirmLabel: '移动所选' });
    if (!accepted || generation !== clientConnection.captureGeneration() || user !== auth.user?.id) return;
    busy.value = true; results.value = [];
    try {
      for (const item of batch) {
        const path = sourcePath(item, store.folders);
        if (generation !== clientConnection.captureGeneration() || user !== auth.user?.id) {
          results.value.push({ id: item.id, path, success: false, detail: '主机或账号已更换，未执行' }); continue;
        }
        try {
          if (item.is_protected) throw new Error('内置源不可修改');
          if (action === 'delete') await store.deleteSource(item.id); else await store.moveSource(item.id, target);
          results.value.push({ id: item.id, path, success: true, detail: action === 'delete' ? '已删除' : '已移动' });
          selected.value = selected.value.filter(id => id !== item.id);
        } catch (error) {
          results.value.push({ id: item.id, path, success: false, detail: error instanceof Error ? error.message : '未完成，请核对' });
        }
      }
    } finally { busy.value = false; }
  }
  return { selected, busy, destination, results, allSelected, destinations, toggle, toggleAll, execute };
}
