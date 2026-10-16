<!-- 业务侧栏：真实Ant Drawer，统一补全弹层语义和关闭后的键盘焦点，不兼容旧库API。 -->
<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useAttrs, useId, watch } from 'vue';
import ADrawer from 'ant-design-vue/es/drawer';
import { restoreDialogFocus } from './dialogFocus';
defineOptions({ inheritAttrs: false });
const props = defineProps<{ open: boolean; title?: string }>();
const emit = defineEmits<{
  (event: 'update:open', value: boolean): void;
  (event: 'close', value: MouseEvent | KeyboardEvent): void;
  (event: 'afterOpenChange', value: boolean): void;
}>();
const attrs = useAttrs();
const marker = 'scp-control-panel-' + useId().replaceAll(':', '-');
const opener = ref<HTMLElement | null>(null);
let observer: MutationObserver | null = null;
let alive = true;

/** :returns: 为Ant实际传送到body的根容器设置语义，不依赖其私有子节点结构。 */
function syncSemantics(): void {
  const element = document.getElementsByClassName(marker)[0];
  if (!element) return;
  element.setAttribute('role', 'dialog');
  element.setAttribute('aria-modal', String(props.open));
  element.setAttribute('data-scp-dialog-open', String(props.open));
  if (props.open) element.removeAttribute('aria-hidden');
  element.setAttribute('aria-label', props.title || String(attrs['aria-label'] || '操作面板'));
  observer?.disconnect(); observer = null;
}

/** :param open: Ant实际过渡状态。:returns: 已关闭后只恢复仍属于当前文档的原入口。 */
function afterChange(open: boolean): void {
  syncSemantics();
  if (!open && !props.open && alive) {
    const element = document.getElementsByClassName(marker)[0];
    restoreDialogFocus(opener.value, element ?? null);
  }
  if (!open && !props.open) document.getElementsByClassName(marker)[0]?.setAttribute('aria-hidden', 'true');
  emit('afterOpenChange', open);
}
/** :param event: Ant关闭事件。:returns: 只向业务消费者转发，不执行其它控制动作。 */
function forwardClose(event: MouseEvent | KeyboardEvent): void { emit('close', event); }
watch(() => props.open, open => {
  observer?.disconnect(); observer = null;
  void nextTick(syncSemantics);
  if (open && typeof document !== 'undefined') {
    opener.value = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    observer = new MutationObserver(syncSemantics);
    observer.observe(document.body, { childList: true, subtree: true });
    void nextTick(syncSemantics);
  }
}, { immediate: true, flush: 'sync' });
watch(() => props.title, () => { if (props.open) void nextTick(syncSemantics); });
onBeforeUnmount(() => { alive = false; observer?.disconnect(); observer = null; });
</script>
<template>
  <a-drawer v-bind="attrs" :open="open" :title="title" :root-class-name="marker"
    @update:open="value => emit('update:open', value)" @close="forwardClose" @afterOpenChange="afterChange">
    <slot />
    <template v-if="$slots.footer" #footer><slot name="footer" /></template>
    <template v-if="$slots.extra" #extra><slot name="extra" /></template>
    <template v-if="$slots.title" #title><slot name="title" /></template>
  </a-drawer>
</template>
