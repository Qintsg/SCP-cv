<!-- 预案墙面选择：新预设直接选布局，高级在独立弹层编辑并保存同一短码。 -->
<script setup lang="ts">
import { computed, ref } from 'vue';
import ARadio, { RadioGroup as ARadioGroup } from 'ant-design-vue/es/radio';
import AModal from 'ant-design-vue/es/modal';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import type { ScenarioDraft } from './scenarioModel';
import type { VideoWallMappingItem } from '@/services/apiTypes';
import WallLayoutComposer from '@/features/runtime/wall/WallLayoutComposer.vue';
import { decodeLayout, encodeLayout } from '@/features/runtime/wall/layoutCode';
const draft = defineModel<ScenarioDraft>({ required: true });
const open = ref(false), error = ref('');
const selection = ref<VideoWallMappingItem[]>([]);
const choice = computed({
  get: () => draft.value.advanced.wall_layout_code ? 'custom' : draft.value.bigScreenModeState === 'unset' ? 'legacy-keep' : draft.value.bigScreenMode,
  set: (value: string) => {
    if (value === 'custom') { showAdvanced(); return; }
    draft.value.advanced = { ...draft.value.advanced, wall_layout_code: null };
    draft.value.bigScreenModeState = value === 'legacy-keep' ? 'unset' : 'set';
    if (value === 'single' || value === 'double') draft.value.bigScreenMode = value;
  },
});
/** :returns: 独立工作副本，取消不会改预案。 */
function showAdvanced(): void {
  error.value = '';
  try { selection.value = draft.value.advanced.wall_layout_code ? decodeLayout(draft.value.advanced.wall_layout_code)
    : draft.value.bigScreenMode === 'single' ? [{ region: 'fullscreen', input: { kind: 'window_1' } }]
      : [{ region: 'left', input: { kind: 'window_1' } }, { region: 'right', input: { kind: 'window_2' } }]; }
  catch { selection.value = []; error.value = '原布局代码无效，请重新编排'; }
  open.value = true;
}
/** :returns: 仅保存意图及代码；不在预案编辑阶段发送墙面命令。 */
function save(): void {
  try {
    if (!selection.value.length) throw new Error('请至少配置一个显示块');
    const code = encodeLayout(selection.value);
    draft.value.advanced = { ...draft.value.advanced, wall_layout_code: code };
    draft.value.bigScreenModeState = 'unset'; open.value = false;
  } catch (failure) { error.value = failure instanceof Error ? failure.message : '布局无效'; }
}
</script>
<template><div class="scenario-wall-settings">
  <a-radio-group v-model:value="choice"><a-radio value="single">窗口 1 全屏</a-radio><a-radio value="double">窗口 1 左／窗口 2 右</a-radio><a-radio value="custom">高级</a-radio>
    <a-radio v-if="choice === 'legacy-keep'" value="legacy-keep">保留旧预案：不改变布局</a-radio>
  </a-radio-group>
  <div v-if="draft.advanced.wall_layout_code" class="scenario-wall-settings__code"><code>{{ draft.advanced.wall_layout_code }}</code><a-button size="small" @click="showAdvanced">编辑布局</a-button></div>
  <a-modal :open="open" title="预案大屏布局" :width="1000" ok-text="保存布局" cancel-text="取消" @ok="save" @cancel="open = false">
    <WallLayoutComposer v-model="selection" /><a-alert v-if="error" type="error" :message="error" />
  </a-modal>
</div></template>
<style scoped>.scenario-wall-settings .ant-radio-group{display:flex;gap:8px;flex-wrap:wrap}.scenario-wall-settings__code{display:flex;gap:8px;align-items:center;margin-top:12px}.scenario-wall-settings__code code{min-width:0;overflow-wrap:anywhere;font:12px Consolas,monospace;color:var(--console-muted)}</style>
