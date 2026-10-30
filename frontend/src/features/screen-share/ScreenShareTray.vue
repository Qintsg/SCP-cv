<script setup lang="ts">
/** 文件描述：离开窗口控制页后保留紧凑共享停止入口。项目：SCP-cv；文件：ScreenShareTray.vue；作者：Qintsg；日期：2026-10-30。 */
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import AButton from 'ant-design-vue/es/button';
import { useScreenShareStore } from './screenShareStore';
import { useControlLayout } from '@/composables/useControlLayout';
const shares = useScreenShareStore(), route = useRoute();
const { mobileControls, touchNavigation } = useControlLayout();
const visible = computed(() => shares.activeOwners.filter(owner => mobileControls.value || route.path !== `/display/${owner.windowId === 1 ? 'big-left' : 'big-right'}`));
</script>
<template>
  <aside v-if="visible.length" class="share-tray" :class="{ 'share-tray--touch': touchNavigation }" aria-label="正在共享屏幕">
    <div v-for="owner in visible" :key="owner.windowId" class="share-tray__item">
      <RouterLink :to="`/display/${owner.windowId === 1 ? 'big-left' : 'big-right'}`">窗口 {{ owner.windowId }} · 屏幕共享</RouterLink>
      <a-button size="small" danger @click="owner.view.value.phase === 'stopping' ? owner.refreshStop() : owner.stop()">{{ owner.view.value.phase === 'stopping' ? '确认停止' : '停止共享' }}</a-button>
    </div>
  </aside>
</template>
<style scoped>
.share-tray{position:fixed;right:20px;bottom:20px;z-index:1050;display:grid;gap:8px;max-width:calc(100vw - 32px)}
.share-tray__item{display:flex;align-items:center;gap:16px;padding:10px 14px;background:var(--console-surface);border:1px solid var(--console-border);border-radius:12px;box-shadow:var(--shadow8);font-size:13px}
.share-tray--touch{bottom:calc(106px + env(safe-area-inset-bottom))}.share-tray--touch .share-tray__item{gap:8px}.share-tray--touch :deep(button){min-height:44px}
</style>
