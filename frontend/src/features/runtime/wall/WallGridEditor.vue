<!-- 50格矩形编辑器：拖选/键盘框选、已占用块不可覆盖，修改只更新本地值。 -->
<script setup lang="ts">
import { computed, ref } from 'vue';
import AButton from 'ant-design-vue/es/button';
import APopover from 'ant-design-vue/es/popover';
import type { VideoWallMappingItem, VideoWallRect } from '@/services/apiTypes';
import ClientStreamPreview from '@/features/preview/ClientStreamPreview.vue';
import WallInputPicker from './WallInputPicker.vue';
import { inputLabel, overlaps, selectionRect, validateBlocks, type WallBlock } from './layoutModel';
const props = withDefaults(defineProps<{ disabled?: boolean }>(), { disabled: false });
const value = defineModel<WallBlock[]>({ required: true });
const grid = ref<HTMLElement | null>(null), anchor = ref<number | null>(null), end = ref(0), dragging = ref(false);
const pending = ref<VideoWallRect | null>(null), picker = ref(false), editing = ref<number | null>(null), message = ref('');
const current = computed(() => anchor.value === null ? pending.value : selectionRect(anchor.value, end.value));
const blocked = computed(() => !!current.value && value.value.some(b => overlaps(b.rect, current.value!)));
const selectionAction = computed(() => pending.value ? {
  ...(pending.value.column + pending.value.columns < 9 ? { left: `${(pending.value.column + pending.value.columns) * 10}%` } : { right: '8px' }),
  top: `${Math.min(pending.value.row * 20, 60)}%`,
} : {});
/** :param rect: 网格矩形。:returns: 不依赖屏幕分辨率的百分比位置。 */
function position(rect: VideoWallRect): Record<string, string> {
  return { left: `${rect.column * 10}%`, top: `${rect.row * 20}%`, width: `${rect.columns * 10}%`, height: `${rect.rows * 20}%` };
}
/** :param index: 单元格。:returns: 是否已由显示块占用。 */
function occupied(index: number): boolean { return value.value.some(b => overlaps(b.rect, selectionRect(index, index))); }
/** :param event: 指针位置。:returns: 网格内索引，越界返回null。 */
function cellAt(event: PointerEvent): number | null {
  const box = grid.value?.getBoundingClientRect();
  if (!box || !box.width || !box.height) return null;
  const column = Math.floor((event.clientX - box.left) / box.width * 10), row = Math.floor((event.clientY - box.top) / box.height * 5);
  return column >= 0 && column < 10 && row >= 0 && row < 5 ? row * 10 + column : null;
}
/** :param event: 从空单元开始的按下。:returns: 开始本地框选，不操作实体。 */
function begin(event: PointerEvent): void {
  if (props.disabled || event.button !== 0 || !(event.target instanceof Element) || event.target.closest('.wall-grid__block')) return;
  const index = cellAt(event); if (index === null || occupied(index)) return;
  pending.value = null; anchor.value = index; end.value = index; dragging.value = true; message.value = '';
  grid.value?.setPointerCapture?.(event.pointerId); event.preventDefault();
}
/** :param event: 本轮移动。:returns: 更新矩形预览，越界不扩大。 */
function move(event: PointerEvent): void { if (dragging.value) { const cell = cellAt(event); if (cell !== null) end.value = cell; } }
/** :returns: 完成本地框选；重叠则整次拒绝。 */
function finish(): void {
  if (anchor.value === null) return;
  pending.value = blocked.value ? null : current.value;
  message.value = blocked.value ? '不能覆盖已有显示块，请重新框选' : '';
  dragging.value = false; anchor.value = null;
}
/** :param index: 起点。:param event: 网格键盘。:returns: 空格选择、Shift方向扩选、Enter选源、Esc取消。 */
function keyboard(index: number, event: KeyboardEvent): void {
  if (props.disabled) return;
  if (event.key === 'Escape') { anchor.value = null; pending.value = null; return; }
  if (event.key === ' ' || event.key === 'Enter') {
    event.preventDefault();
    if (anchor.value === null) { anchor.value = index; end.value = index; }
    finish(); if (pending.value) { editing.value = null; picker.value = true; } return;
  }
  const delta = ({ ArrowLeft: -1, ArrowRight: 1, ArrowUp: -10, ArrowDown: 10 } as Record<string, number>)[event.key];
  if (delta === undefined) return;
  const next = index + delta;
  if (next < 0 || next > 49 || (Math.abs(delta) === 1 && Math.floor(next / 10) !== Math.floor(index / 10))) return;
  event.preventDefault();
  if (event.shiftKey) { anchor.value ??= index; end.value = next; }
  grid.value?.querySelector<HTMLButtonElement>(`[data-cell="${next}"]`)?.focus();
}
/** :param input: 新来源。:returns: 原子替换/添加并验证整个布局，不发送请求。 */
function choose(input: VideoWallMappingItem['input']): void {
  const blocks = [...value.value];
  if (editing.value !== null) {
    const previous = blocks[editing.value]; if (!previous) return;
    blocks[editing.value] = { ...previous, input };
  } else if (pending.value) blocks.push({ region: 'grid', rect: { ...pending.value }, input });
  else return;
  value.value = validateBlocks(blocks); pending.value = null; anchor.value = null; editing.value = null;
}
/** :param index: 明确块索引。:returns: 开启本地改源界面。 */
function edit(index: number): void { editing.value = index; picker.value = true; }
</script>
<template>
  <div class="wall-grid-editor">
    <div ref="grid" class="wall-grid" role="grid" aria-label="自定义大屏，10列5行。拖选矩形，或空格选择单元格，Shift方向键扩展，Enter选择来源。"
      @pointerdown="begin" @pointermove="move" @pointerup="finish" @pointercancel="anchor = null; dragging = false; pending = null">
      <button v-for="index in 50" :key="index" :data-cell="index - 1" type="button" role="gridcell" class="wall-grid__cell"
        :disabled="disabled || occupied(index - 1)" :aria-label="`第${Math.floor((index - 1) / 10) + 1}行第${(index - 1) % 10 + 1}列`"
        @keydown="keyboard(index - 1, $event)" />
      <div v-if="current" class="wall-grid__selection" :class="{ 'wall-grid__selection--invalid': blocked }" :style="position(current)" aria-hidden="true" />
      <div v-if="pending" class="wall-grid__selection-action" :style="selectionAction" @pointerdown.stop>
        <a-button :disabled="disabled" type="primary" size="small" @click="editing = null; picker = true">选择窗口</a-button>
      </div>
      <a-popover v-for="(block,index) in value" :key="`${block.rect.column}-${block.rect.row}`" trigger="click" placement="top">
        <template #content><div class="wall-grid__menu">
          <strong>{{ inputLabel(block.input) }}</strong><a-button size="small" :disabled="disabled" @click="edit(index)">切换来源</a-button>
          <a-button size="small" danger :disabled="disabled" @click="value = value.filter((_, i) => i !== index)">删除显示块</a-button>
        </div></template>
        <button type="button" class="wall-grid__block" :style="position(block.rect)" :disabled="disabled" :aria-label="`编辑${inputLabel(block.input)}显示块`">
          <ClientStreamPreview :input="block.input" :quality="block.rect.columns * block.rect.rows <= 4 ? 'low' : 'high'" :enabled="!disabled" :label="inputLabel(block.input)" />
          <span class="wall-grid__block-label">{{ inputLabel(block.input) }}<small v-if="block.input.kind === 'window_3' || block.input.kind === 'window_4'"> · 预留</small></span>
        </button>
      </a-popover>
    </div>
    <div v-if="message || pending" class="wall-grid__footer">
      <span class="wall-grid__hint" role="status">{{ message || (pending ? `已选 ${pending.columns}列 × ${pending.rows}行` : '') }}</span>
    </div>
    <WallInputPicker v-model:open="picker" :value="editing === null ? undefined : value[editing]?.input" @choose="choose" />
  </div>
</template>
<style scoped>
.wall-grid{aspect-ratio:32/9;display:grid;grid-template-columns:repeat(10,minmax(0,1fr));grid-template-rows:repeat(5,minmax(0,1fr));position:relative;border:1px solid var(--console-border);background:var(--console-subtle);touch-action:none;user-select:none}
.wall-grid__cell{padding:0;min-width:0;border-right:1px solid var(--console-border);border-bottom:1px solid var(--console-border);background:transparent}
.wall-grid__cell:disabled{cursor:default}.wall-grid__cell:focus-visible{outline:2px solid var(--console-primary);outline-offset:-2px}
.wall-grid__selection{position:absolute;background:var(--console-brand-tint);border:2px solid var(--console-primary);pointer-events:none;opacity:.8;z-index:1}
.wall-grid__selection--invalid{border-color:var(--colorStatusDangerForeground1);background:var(--colorStatusDangerBackground1)}
.wall-grid__selection-action{position:absolute;z-index:4;margin:6px;max-width:calc(100% - 12px)}
.wall-grid__block{position:absolute;z-index:2;padding:0;overflow:hidden;border:1px solid var(--console-primary);background:var(--console-surface);text-align:left}
.wall-grid__block:focus-visible{outline:3px solid var(--console-primary);outline-offset:2px;z-index:3}
.wall-grid__block-label{position:absolute;top:4px;left:5px;max-width:calc(100% - 10px);background:var(--console-surface);color:var(--console-text);padding:2px 5px;border-radius:var(--borderRadiusSmall);font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;pointer-events:none}
.wall-grid__footer,.wall-grid__menu{display:flex;gap:8px;align-items:center}.wall-grid__footer{min-height:38px;margin-top:8px;justify-content:space-between}.wall-grid__hint{font-size:12px;color:var(--console-muted)}
@media(max-width:767px){.wall-grid__block-label{font-size:10px}.wall-grid__menu{flex-direction:column;align-items:stretch}}
</style>
