<script setup lang="ts">
/**
 * 文件描述：桌面侧栏与手机、Pad悬浮导航外壳。
 * 项目：SCP-cv；文件：AppShell.vue；作者：Qintsg；日期：2026-10-28。
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { RouterView, useRoute } from 'vue-router';

import AppNavigation from './AppNavigation.vue';
import AppTopBar from './AppTopBar.vue';
import AppBrand from './AppBrand.vue';
import ALayout from 'ant-design-vue/es/layout';
import { LayoutSider as ALayoutSider } from 'ant-design-vue/es/layout';
import {
  DESKTOP_PRIMARY_NAV,
  resolveDisplayLabel,
} from './navItems';
import type { NavItemDef } from './types';
import { useWindowSizeClass } from '@/composables/useWindowSizeClass';
import { useRuntimeStore } from '@/stores/runtime';
import { useControlLayout } from '@/composables/useControlLayout';

const { t } = useI18n();
const route = useRoute();
const runtime = useRuntimeStore();
const { sizeClass, isLargeUp } = useWindowSizeClass();
const { mobileControls, tabletControls, touchNavigation } = useControlLayout();

const moreOpen = ref(false);
const scrolled = ref(false);
const mainRef = ref<HTMLElement | null>(null);

/** 导航形态：compact 底栏 / rail 轨道 / drawer 抽屉。 */
const navVariant = computed<'bottom' | 'rail' | 'drawer'>(() =>
  touchNavigation.value ? 'bottom' : isLargeUp.value ? 'drawer' : 'rail',
);

const primaryItems = computed<NavItemDef[]>(() =>
  DESKTOP_PRIMARY_NAV.map((item) => ({
    ...item,
    label: item.path.startsWith('/display/')
      ? resolveDisplayLabel(item.path, runtime.isDoubleScreen, runtime.runtime?.big_screen_mode === 'custom')
      : item.label,
  })),
);

function isActive(path: string): boolean {
  if (path === '/dashboard') return route.path === '/' || route.path === '/dashboard';
  if (path === '/display/big-left' && touchNavigation.value) {
    return route.path.startsWith('/display/');
  }
  return route.path === path;
}

function onMainScroll(): void {
  scrolled.value = (mainRef.value?.scrollTop ?? 0) > 4;
}

/** :param path: 新页面路径。:returns: 导航后定位主内容；不重置同页查询与表单。 */
watch(() => route.path, async () => {
  await nextTick();
  const main = mainRef.value;
  if (!main) return;
  main.scrollTop = 0;
  main.scrollLeft = 0;
  scrolled.value = false;
  main.focus({ preventScroll: true });
});

onMounted(() => {
  mainRef.value?.addEventListener('scroll', onMainScroll, { passive: true });
});

onBeforeUnmount(() => {
  mainRef.value?.removeEventListener('scroll', onMainScroll);
});
</script>

<template>
  <a class="skip-link" href="#main-content">{{ t('app.skipToMain') }}</a>

  <a-layout
    class="app-shell console-shell"
    :data-size="sizeClass"
    :data-nav="navVariant"
    :data-mobile-controls="mobileControls"
    :data-tablet-controls="tabletControls"
    :data-touch-navigation="touchNavigation"
  >
    <a-layout-sider v-if="!touchNavigation" :width="228" :collapsed-width="76" :collapsed="!isLargeUp" class="console-sider" theme="dark">
      <AppBrand :collapsed="!isLargeUp" />
      <AppNavigation
        v-model:more-open="moreOpen"
        :compact="false"
        :nav-variant="navVariant"
        :primary-items="primaryItems"
        :is-active="isActive"
      />
      <div v-if="isLargeUp" class="console-sider__footer">上海第二工业大学</div>
    </a-layout-sider>
    <a-layout class="console-shell__workspace">
      <AppTopBar :scrolled="scrolled" />
      <main
        id="main-content"
        ref="mainRef"
        class="app-shell__content"
        tabindex="-1"
      >
        <RouterView />
      </main>
      <AppNavigation
      v-if="touchNavigation"
      v-model:more-open="moreOpen"
      compact
      :nav-variant="navVariant"
      :primary-items="primaryItems"
      :is-active="isActive"
      />
    </a-layout>
  </a-layout>
</template>

<style src="./AppShell.css"></style>
