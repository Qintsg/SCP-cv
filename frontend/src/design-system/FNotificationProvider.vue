<script setup lang="ts">
/**
 * 全局通知布局宿主，保持业务通知 API 和现有 Naive UI 外观。
 * Compact 与底栏断点一致：通知移到头部下方，滚动区域为底部操作保留空间。
 */
import { computed, type CSSProperties } from 'vue';
import { NNotificationProvider } from 'naive-ui';
import { useWindowSizeClass } from '@/composables/useWindowSizeClass';

const { isCompact } = useWindowSizeClass();
const leftGap = 'max(var(--spacingHorizontalL), env(safe-area-inset-left, 0px))';
const rightGap = 'max(var(--spacingHorizontalL), env(safe-area-inset-right, 0px))';

// 3.5rem 来自 AppTopBar 的 min-h-14；底栏包含图标、单行标签、间距及上下留白，共 4.625rem。
const containerStyle = computed<CSSProperties | undefined>(() => isCompact.value ? {
  top: 'calc(3.5rem + env(safe-area-inset-top, 0px) + var(--spacingVerticalS))',
  left: leftGap,
  right: rightGap,
  width: 'auto',
  transform: 'none',
  maxHeight: 'calc(var(--app-height, 100dvh) - 3.5rem - 4.625rem - env(safe-area-inset-top, 0px) - env(safe-area-inset-bottom, 0px) - var(--spacingVerticalS) - var(--spacingVerticalS))',
  overflowY: 'auto',
  overflowX: 'hidden',
} : undefined);

const themeOverrides = computed(() => isCompact.value ? {
  width: `calc(100vw - ${leftGap} - ${rightGap})`,
} : undefined);
</script>

<template>
  <n-notification-provider
    :placement="isCompact ? 'top' : 'bottom-right'"
    :container-style="containerStyle"
    :theme-overrides="themeOverrides"
  >
    <slot />
  </n-notification-provider>
</template>
