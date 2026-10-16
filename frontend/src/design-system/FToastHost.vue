<!-- Ant通知宿主：保持业务store、持久错误与重试动作，沿用当前ConfigProvider主题。 -->
<script setup lang="ts">
import { h, onBeforeUnmount, onMounted, watch } from 'vue';
import notification from 'ant-design-vue/es/notification';
import Button from 'ant-design-vue/es/button';
import { useToastStore, type ToastItem } from '@/composables/useToast';
import { useWindowSizeClass } from '@/composables/useWindowSizeClass';

const store = useToastStore();
const { isCompact } = useWindowSizeClass();
const [notices, ContextHolder] = notification.useNotification({ maxCount: 3 });
const active = new Set<number>();
let mounted = false;

/** :param item: 业务通知，duration单位毫秒。:returns: 一次创建真实Ant通知。 */
function open(item: ToastItem): void {
  if (active.has(item.id)) return;
  active.add(item.id);
  notices.open({
    key: item.id, type: item.level, message: item.message, description: item.description,
    duration: item.duration / 1000, placement: isCompact.value ? 'topRight' : 'bottomRight',
    class: 'console-toast',
    onClose: () => { active.delete(item.id); store.dismiss(item.id); },
    btn: item.action ? () => h(Button, { size: 'small', type: 'link', onClick: async () => {
      try { await item.action!.onTrigger(); } finally { store.dismiss(item.id); }
    } }, () => item.action!.label) : undefined,
  });
}

/** :returns: store与真实通知保持一致，手动关闭不留下隐藏通知。 */
function sync(): void {
  if (!mounted) return;
  for (const item of store.items) open(item);
  for (const key of active) {
    if (!store.items.some(item => item.id === key)) { active.delete(key); notices.destroy(key); }
  }
}
watch(() => store.items.map(item => item.id), sync);
onMounted(() => { mounted = true; sync(); });
onBeforeUnmount(() => { mounted = false; notices.destroy(); active.clear(); });
</script>
<template><ContextHolder /></template>
