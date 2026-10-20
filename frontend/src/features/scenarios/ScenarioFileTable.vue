<!-- 预案与目录同表；显式查看/编辑/调用，名称不挤入卡片头部。 -->
<script setup lang="ts">
import { computed } from 'vue';
import ATable from 'ant-design-vue/es/table';
import AButton from 'ant-design-vue/es/button';
import ACheckbox from 'ant-design-vue/es/checkbox';
import ATag from 'ant-design-vue/es/tag';
import ACard from 'ant-design-vue/es/card';
import FIcon from '@/design-system/FIcon.vue';
import type { ScenarioItem, MediaFolderItem } from '@/services/api';
import { formatRelativeTime } from '@/design-system/utils';
const props = defineProps<{ scenarios: ScenarioItem[]; folders: MediaFolderItem[]; busy: boolean; pendingActivate: number | null; pendingPin: number | null }>();
const selected = defineModel<number[]>('selected', { required: true });
const emit = defineEmits<{ navigate: [id: number]; preview: [item: ScenarioItem, event: MouseEvent]; edit: [item: ScenarioItem]; activate: [item: ScenarioItem]; pin: [item: ScenarioItem] }>();
const rows = computed(() => [...props.folders.map(folder => ({ key: `folder-${folder.id}`, folder, scenario: null })),
  ...props.scenarios.map(scenario => ({ key: `scenario-${scenario.id}`, folder: null, scenario }))]);
const columns = [{ key: 'selection', width: 48 }, { key: 'name', title: '名称' }, { key: 'type', title: '类型', width: 90 },
  { key: 'updated', title: '更新于', width: 130 }, { key: 'actions', title: '操作', width: 240 }];
const allSelected = computed(() => props.scenarios.length > 0 && props.scenarios.every(item => selected.value.includes(item.id)));
/** :param id: 明确预案。:param checked: 当前勾选。:returns: 更新本地选择，不执行批量操作。 */
function toggle(id: number, checked: boolean): void { selected.value = checked ? [...new Set([...selected.value, id])] : selected.value.filter(value => value !== id); }
/** :param checked: 当前可见集合。:returns: 全选只含预案，不含目录。 */
function toggleAll(checked: boolean): void { selected.value = checked ? props.scenarios.map(item => item.id) : []; }
</script>
<template>
  <a-card :body-style="{ padding: 0, overflowX: 'auto' }" class="scenario-files">
    <a-table :columns="columns" :data-source="rows" row-key="key" :pagination="false" :scroll="{ x: 750 }" :aria-busy="busy">
      <template #headerCell="{ column }"><a-checkbox v-if="column.key === 'selection'" :checked="allSelected" :disabled="busy || !scenarios.length" aria-label="全选当前预案" @update:checked="toggleAll" /><template v-else>{{ column.title }}</template></template>
      <template #bodyCell="{ column, record }">
        <template v-if="record.folder">
          <button v-if="column.key === 'name'" type="button" class="scenario-files__name" @click="emit('navigate', record.folder.id)"><FIcon name="folder_24_regular" :size="28" /><strong>{{ record.folder.name }}</strong></button>
          <template v-else-if="column.key === 'type'">文件夹</template><template v-else-if="column.key === 'updated'">—</template>
        </template>
        <template v-else-if="record.scenario">
          <a-checkbox v-if="column.key === 'selection'" :checked="selected.includes(record.scenario.id)" :disabled="busy" :aria-label="`选择${record.scenario.name}`" @update:checked="toggle(record.scenario.id,$event)" />
          <button v-else-if="column.key === 'name'" type="button" class="scenario-files__name scenarios-view__preview-button" :aria-label="`查看预设 ${record.scenario.name}`" @click="emit('preview',record.scenario,$event)"><FIcon name="document_24_regular" :size="26" /><strong>{{ record.scenario.name }}</strong><FIcon v-if="record.scenario.sort_order > 0" name="pin_24_filled" /></button>
          <a-tag v-else-if="column.key === 'type'" color="blue">预案</a-tag>
          <span v-else-if="column.key === 'updated'" class="scenario-files__updated">{{ formatRelativeTime(record.scenario.updated_at) }}</span>
          <div v-else-if="column.key === 'actions'" class="scenario-files__actions">
            <a-button type="primary" size="small" :disabled="busy" :loading="pendingActivate === record.scenario.id" @click="emit('activate',record.scenario)"><template #icon><FIcon name="play_24_regular" /></template>调用</a-button>
            <a-button size="small" :disabled="busy" @click="emit('edit',record.scenario)">编辑</a-button>
            <a-button type="text" size="small" :disabled="busy" :loading="pendingPin === record.scenario.id" :aria-label="record.scenario.sort_order > 0 ? '取消置顶' : '置顶预案'" @click="emit('pin',record.scenario)"><template #icon><FIcon :name="record.scenario.sort_order > 0 ? 'pin_off_24_regular' : 'pin_24_regular'" /></template></a-button>
          </div>
        </template>
      </template>
    </a-table>
  </a-card>
</template>
<style scoped>.scenario-files{min-width:0}.scenario-files__name{display:inline-flex;align-items:center;gap:12px;text-align:left;padding:4px 0;color:var(--console-text)}.scenario-files__name:hover{color:var(--console-primary)}.scenario-files__actions{display:flex;align-items:center;gap:8px;flex-wrap:wrap}.scenario-files__updated{font-size:12px;color:var(--console-muted)}@media(max-width:767px){.scenario-files__name{min-height:44px}}</style>
