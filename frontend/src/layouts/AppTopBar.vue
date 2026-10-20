<script setup lang="ts">
/**
 * 应用顶栏：品牌、运行态标签、主题切换与应急菜单。
 * 从 AppShell 拆出以降低外壳组件复杂度。
 */
import { tagColor } from '@/design-system/tagColor';

import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import ATag from 'ant-design-vue/es/tag';

import EmergencyMenu from './EmergencyMenu.vue';
import ThemeToggle from './ThemeToggle.vue';
import { useRuntimeStore } from '@/stores/runtime';
import { useSessionStore } from '@/stores/sessions';
import { useRoute } from 'vue-router';

interface AppTopBarProps {
  scrolled: boolean;
}

defineProps<AppTopBarProps>();

const { t } = useI18n();
const runtime = useRuntimeStore();
const sessions = useSessionStore();
const route = useRoute();
const pageTitle = computed(() => route.meta.titleKey ? t(String(route.meta.titleKey)) : typeof route.meta.title === 'string' ? route.meta.title : '现场控制台');

type StatusTone = 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error';

const sseType = computed<StatusTone>(() => {
  switch (runtime.sseStatus) {
    case 'connected':
      return 'success';
    case 'reconnecting':
      return 'warning';
    case 'closed':
      return 'error';
    default:
      return 'default';
  }
});

const sseLabel = computed(() => {
  switch (runtime.sseStatus) {
    case 'connected':
      return t('app.sse.connected');
    case 'connecting':
      return t('app.sse.connecting');
    case 'reconnecting':
      return t('app.sse.reconnecting');
    default:
      return t('app.sse.closed');
  }
});

const playerType = computed<StatusTone>(() => sessions.onlinePlayerCount === 2 ? 'success' : sessions.hasOnlinePlayer ? 'warning' : 'error');
const playerLabel = computed(() => (sessions.hasOnlinePlayer
  ? t('app.player.online', { count: sessions.onlinePlayerCount })
  : t('app.player.offline')));
</script>

<template>
  <header
    class="app-shell__bar sticky top-0 z-[var(--z-sticky)] flex min-h-14 items-center justify-between gap-1 bg-surface px-3 pt-[calc(0.5rem+env(safe-area-inset-top,0px))] pb-2 text-foreground transition-[background,box-shadow] sm:gap-4 sm:px-4"
    :class="{ 'app-shell__bar--scrolled': scrolled }"
    role="banner"
  >
    <div class="console-context"><span>28#108</span><span aria-hidden="true">/</span><strong>{{ pageTitle }}</strong></div>

    <div class="app-shell__bar-meta inline-flex min-w-0 flex-wrap items-center justify-end gap-1 sm:gap-2">
      <a-tag :color="tagColor(sseType)"   :title="sseLabel">
        <span class="hidden sm:inline">{{ sseLabel }}</span>
        <span class="sm:hidden">{{ runtime.sseStatus === 'connected' ? '已连接' : runtime.sseStatus === 'closed' ? '未连接' : '连接中' }}</span>
      </a-tag>
      <a-tag :color="tagColor(playerType)"   :title="playerLabel">
        <span class="hidden sm:inline">{{ playerLabel }}</span><span class="sm:hidden">{{ sessions.onlinePlayerCount }}/2 在线</span>
      </a-tag>
      <span
        v-if="runtime.systemVolume.muted"
        class="app-shell__mute inline-flex shrink-0 items-center whitespace-nowrap rounded-full bg-[var(--colorStatusWarningBackground1)] px-2 py-0.5 text-xs font-semibold text-warning"
      >
        <span class="app-shell__mute-full hidden sm:inline">{{ t('app.systemMuted') }}</span>
        <span class="app-shell__mute-compact sm:hidden">{{ t('app.systemMutedCompact') }}</span>
      </span>
      <ThemeToggle />
      <EmergencyMenu />
    </div>
  </header>
</template>
