<!-- 共享本地编排器：单/双屏/50格与短码，不访问数据库和设备。 -->
<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import AButton from 'ant-design-vue/es/button';
import AInput from 'ant-design-vue/es/input';
import ASegmented from 'ant-design-vue/es/segmented';
import ASelect from 'ant-design-vue/es/select';
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import type { VideoWallMappingItem } from '@/services/apiTypes';
import ClientStreamPreview from '@/features/preview/ClientStreamPreview.vue';
import WallGridEditor from './WallGridEditor.vue';
import { decodeLayout, encodeLayout } from './layoutCode';
import { INPUT_KINDS, inputLabel, validateBlocks, validIp, type WallBlock } from './layoutModel';
const props = defineProps<{ disabled?: boolean }>();
const value = defineModel<VideoWallMappingItem[]>({ required: true });
const mode = ref<'fullscreen' | 'split' | 'custom'>('split'), codeInput = ref(''), codeError = ref('');
const custom = ref<WallBlock[]>([]);
const modeOptions = [{ label: '单屏', value: 'fullscreen' }, { label: '双屏', value: 'split' }, { label: '自定义', value: 'custom' }];
const options = [{ label: '窗口 1', value: 'window_1' }, { label: '窗口 2', value: 'window_2' },
  { label: 'IP81', value: 'laptop' }, { label: '窗口 3 · 预留', value: 'window_3' }, { label: '窗口 4 · 预留', value: 'window_4' }, { label: '自定义 IP', value: 'ip_stream' }];
const choices = ref<VideoWallMappingItem[]>([]);
const remembered = new Map<string, VideoWallMappingItem[]>();
let hasCustom = false;
/** :param region: 目标区域。:returns: 与可见区域一致的无障碍名称。 */
function regionLabel(region: string): string { return region === 'fullscreen' ? '全屏' : region === 'left' ? '大屏左' : '大屏右'; }
const valid = computed(() => value.value.every(m => m.input.kind !== 'ip_stream' || validIp(m.input.ip_address || '')));
const code = computed(() => { try { return encodeLayout(value.value); } catch { return ''; } });
let publishing = false;
/** :returns: 从外部布局恢复本地编辑态，初始化不产生写入。 */
function restore(): void {
  const mappings = value.value;
  mode.value = mappings.some(m => m.region === 'grid') || !mappings.length || mappings.length === 1 && mappings[0]?.region !== 'fullscreen'
    ? 'custom' : mappings.some(m => m.region === 'fullscreen') ? 'fullscreen' : 'split';
  choices.value = mappings.filter(m => m.region !== 'grid').map(m => ({ ...m, input: { ...m.input } }));
  try { custom.value = validateBlocks(mappings); } catch { custom.value = []; }
  if (mode.value === 'custom') hasCustom = true;
  else remembered.set(mode.value, choices.value.map(m => ({ ...m, input: { ...m.input } })));
  codeInput.value = code.value;
}
watch(() => value.value, () => { if (!publishing) restore(); else codeInput.value = code.value; }, { deep: true, immediate: true });
/** :param mappings: 本轮完整选择。:returns: 仅发本地更新。 */
function publish(mappings: VideoWallMappingItem[]): void {
  publishing = true; value.value = mappings;
  queueMicrotask(() => { publishing = false; codeInput.value = code.value; }); codeError.value = '';
}
/** :param next: 明确布局模式。:returns: 把快捷模式转换为相同映射合同。 */
function switchMode(next: string | number): void {
  if (props.disabled || !['fullscreen', 'split', 'custom'].includes(String(next))) return;
  if (mode.value !== 'custom') remembered.set(mode.value, choices.value.map(m => ({ ...m, input: { ...m.input } })));
  mode.value = next as typeof mode.value;
  if (mode.value === 'custom') { if (!hasCustom && valid.value) custom.value = validateBlocks(value.value); hasCustom = true; publish(custom.value); return; }
  choices.value = remembered.get(mode.value)?.map(m => ({ ...m, input: { ...m.input } })) ?? (mode.value === 'fullscreen'
    ? [{ region: 'fullscreen', input: { kind: 'window_1', ip_address: '' } }]
    : [{ region: 'left', input: { kind: 'window_1', ip_address: '' } }, { region: 'right', input: { kind: 'window_2', ip_address: '' } }]);
  publish(choices.value);
}
/** :returns: 整体解析后替换，仅本地预览，不自动应用实体。 */
function readCode(): void {
  try { const decoded = decodeLayout(codeInput.value.trim()); mode.value = 'custom'; custom.value = decoded; publish(decoded); }
  catch (failure) { codeError.value = failure instanceof Error ? failure.message : '布局代码无效'; }
}
/** :param index: 快捷区域。:param kind: 输入类型。:returns: 更换本地来源并清除遗留IP。 */
function selectInput(index: number, value: unknown): void {
  const kind = INPUT_KINDS.find(item => item === value); if (!kind) return;
  const selected = choices.value[index]; if (!selected) return;
  selected.input = { kind, ip_address: '' }; publish(choices.value.map(m => ({ ...m, input: { ...m.input } })));
}
</script>
<template>
  <div class="wall-composer" :class="{ 'wall-composer--quick': mode !== 'custom' }">
    <div class="wall-composer__toolbar">
      <a-segmented :value="mode" :options="modeOptions" :disabled="disabled" aria-label="大屏显示模式" @change="switchMode" />
      <div v-if="mode === 'custom'" class="wall-composer__code">
        <a-input v-model:value="codeInput" aria-label="自定义布局代码" placeholder="输入布局代码" :disabled="disabled" @pressEnter="readCode" />
        <a-button :disabled="disabled || !codeInput.trim()" @click="readCode">读取代码</a-button>
      </div>
    </div>
    <p v-if="codeError" class="wall-composer__error" role="alert">{{ codeError }}</p>
    <WallGridEditor v-if="mode === 'custom'" :model-value="custom" :disabled="disabled" @update:model-value="custom = $event; publish($event)" />
    <template v-else>
      <div class="wall-composer__choices" :class="{ 'wall-composer__choices--split': mode === 'split' }">
        <label v-for="(choice,index) in choices" :key="choice.region"><span>{{ regionLabel(choice.region) }}</span>
          <a-select v-select-aria-label="`${regionLabel(choice.region)}输入来源`" :value="choice.input.kind" :options="options" :disabled="disabled" :aria-label="`${regionLabel(choice.region)}输入来源`" @update:value="selectInput(index,$event)" />
          <a-input v-if="choice.input.kind === 'ip_stream'" v-model:value="choice.input.ip_address" :disabled="disabled" aria-label="输入设备IPv4" placeholder="192.168.5.…" @update:value="publish(choices.map(m => ({ ...m, input: { ...m.input } })))" />
        </label>
      </div>
      <div class="wall-composer__screen" :class="{ 'wall-composer__screen--split': mode === 'split' }" aria-label="32比9大屏预览">
        <ClientStreamPreview v-for="choice in choices" :key="choice.region" :input="choice.input" quality="high" :enabled="!disabled" :label="inputLabel(choice.input)" />
      </div>
    </template>
  </div>
</template>
<style scoped>
.wall-composer__toolbar{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:16px}
.wall-composer__code{display:flex;align-items:center;gap:8px;min-width:0;max-width:100%;width:350px}.wall-composer__code .ant-input{min-width:0;flex:1;font-family:Consolas,monospace}.wall-composer__code .ant-btn{flex-shrink:0}
.wall-composer__choices{display:grid;gap:12px;margin-bottom:12px}.wall-composer__choices--split{grid-template-columns:repeat(2,minmax(0,1fr))}
.wall-composer__choices label{min-width:0;display:grid;grid-template-columns:auto minmax(0,1fr);align-items:center;gap:8px;font-size:12px;color:var(--console-muted)}.wall-composer__choices label>.ant-input{grid-column:2}
.wall-composer__screen{display:grid;aspect-ratio:32/9;border:1px solid var(--console-border);border-radius:var(--borderRadiusMedium);overflow:hidden;grid-template-columns:minmax(0,1fr)}.wall-composer__screen--split{grid-template-columns:repeat(2,minmax(0,1fr))}.wall-composer__screen>:nth-child(2){border-left:1px solid var(--console-border)}
.wall-composer__error{font-size:12px;color:var(--colorStatusDangerForeground1)}
.wall-composer--quick{display:grid;grid-template-columns:auto minmax(0,1fr);grid-template-areas:'mode choices' 'screen screen';gap:12px 20px;align-items:center}.wall-composer--quick .wall-composer__toolbar{grid-area:mode;margin:0}.wall-composer--quick .wall-composer__choices{grid-area:choices;margin:0}.wall-composer--quick .wall-composer__screen{grid-area:screen}.wall-composer--quick .wall-composer__error{grid-column:1/-1}
@media(max-width:767px){.wall-composer__code{width:100%}.wall-composer__choices label{grid-template-columns:minmax(0,1fr)}.wall-composer__choices label>.ant-input{grid-column:1}}
@media(max-width:767px){.wall-composer--quick{display:block}.wall-composer--quick .wall-composer__toolbar{margin-bottom:12px}.wall-composer--quick .wall-composer__choices{margin-bottom:12px}}
</style>
