<script setup lang="ts">
/**
 * 文件描述：控制台根组件与应用共享生命周期。项目：SCP-cv；文件：App.vue；作者：Qintsg；日期：2026-10-30。
 * 应用根组件。
 *  - meta.focus = true：全屏专注内容（PPT 专注模式），替换整个外壳；
 *  - 其余路由：Ant Design现场工作台外壳；
 *  - 初始化主题（system/light/dark）、应用高度变量、全局 Toast / Dialog 宿主；
 *  - Ant ConfigProvider统一中文、明暗算法与全局反馈主题。
 */
import { computed, onBeforeUnmount, onMounted, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { RouterView, useRoute } from 'vue-router';
import AConfigProvider from 'ant-design-vue/es/config-provider';
import zhCN from 'ant-design-vue/es/locale/zh_CN';

import AppShell from '@/layouts/AppShell.vue';
import FDialogHost from '@/design-system/FDialogHost.vue';
import FToastHost from '@/design-system/FToastHost.vue';
import { consoleTheme } from '@/design-system/theme/ant-theme';
import { useReducedMotion } from '@/composables/useReducedMotion';
import { useTheme } from '@/composables/useTheme';
import { bindAppHeight } from '@/composables/useAppHeight';
import { bootstrapStores } from '@/stores';
import { useToast } from '@/composables/useToast';
import { useAuthStore } from '@/stores/auth';
import { useRuntimeStore } from '@/stores/runtime';
import { watch } from 'vue';
import { useScreenShareStore } from '@/features/screen-share/screenShareStore';
import ScreenShareTray from '@/features/screen-share/ScreenShareTray.vue';

const route = useRoute();
const { t } = useI18n();
const toast = useToast();
const auth = useAuthStore();
const runtime = useRuntimeStore();
const shares = useScreenShareStore();
window.addEventListener('pagehide', shares.disposeAll);

const { dark: isDarkResolved } = useTheme();
useReducedMotion();

const isFocusMode = computed(() => route.meta?.focus === true);

const antTheme = computed(() => consoleTheme(isDarkResolved.value));

let unbindHeight: (() => void) | null = null;
let bootstrapped = false;
let active = true;

/** :returns: 仅已认证实例启动读取；卸载后的旧初始化不向新应用反馈。 */
async function bootstrapWhenReady(): Promise<void> {
  if (bootstrapped || !auth.isAuthenticated) return;
  bootstrapped = true;
  try {
    await bootstrapStores(() => active);
  } catch (error) {
    if (active && auth.isAuthenticated) toast.error(t('bootstrap.initFail'), error instanceof Error ? error.message : t('bootstrap.initFailDetail'));
  }
}

onMounted(async () => {
  unbindHeight = bindAppHeight();
  await auth.ensureInitialized();
  await bootstrapWhenReady();
});

// 用户登录后才拉业务数据；退出登录时断开 SSE，避免后端 401 风暴。
watch(
  () => auth.isAuthenticated,
  (next, prev) => {
    if (next) {
      void bootstrapWhenReady();
    } else if (prev) {
      bootstrapped = false;
      runtime.disconnectEvents();
    }
  },
);

onBeforeUnmount(() => { active = false; window.removeEventListener('pagehide', shares.disposeAll); shares.disposeAll(); });
onUnmounted(() => {
  unbindHeight?.();
  runtime.disconnectEvents();
});
</script>

<template>
  <a-config-provider :theme="antTheme" :locale="zhCN">
    <RouterView v-if="isFocusMode" />
    <AppShell v-else />
    <FDialogHost />
    <ScreenShareTray />
    <FToastHost />
  </a-config-provider>
</template>
