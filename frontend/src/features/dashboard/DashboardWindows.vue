<!-- 首页双窗口入口，只读取已同步的会话；导航不会切换播放内容。 -->
<script setup lang="ts">
import { tagColor } from '@/design-system/tagColor';
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import ATag from 'ant-design-vue/es/tag';
import { useSessionStore } from '@/stores/sessions';
import FIcon from '@/design-system/FIcon.vue';
import { useMobileControls } from '@/composables/useMobileControls';
const mobileControls = useMobileControls();
import { windowPresentation } from './windowPresentation';
import ClientStreamPreview from '@/features/preview/ClientStreamPreview.vue';
const sessions = useSessionStore();
const targets = [{ id: 1, path: '/display/big-left' }, { id: 2, path: '/display/big-right' }, { id: 3, path: '/display/window-3' }, { id: 4, path: '/display/window-4' }];
const visibleTargets = computed(() => mobileControls.value ? targets.filter(item => item.id <= 2) : targets);
</script>
<template>
  <section class="dashboard-windows" aria-label="播放窗口与预留输出">
    <RouterLink v-for="target in visibleTargets" :key="target.id" :to="target.path" class="dashboard-window">
      <div class="dashboard-window__heading"><span class="dashboard-window__number" aria-hidden="true">{{ target.id }}</span><strong>窗口{{ target.id }}</strong>
        <a-tag :color="target.id > 2 ? undefined : tagColor(windowPresentation(sessions.byWindowId(target.id)).tone)">{{ target.id > 2 ? '仅预览' : windowPresentation(sessions.byWindowId(target.id)).label }}</a-tag></div>
      <div v-if="!mobileControls" class="dashboard-window__preview"><ClientStreamPreview :input="{ kind: target.id === 1 ? 'window_1' : target.id === 2 ? 'window_2' : target.id === 3 ? 'window_3' : 'window_4' }" :label="`窗口${target.id}预览`" /></div>
      <div class="dashboard-window__footer"><p>{{ target.id > 2 ? `IP${target.id + 62}` : sessions.byWindowId(target.id)?.source_name || (sessions.byWindowId(target.id)?.source_type === 'screen_share' ? '屏幕共享' : '未选择媒体') }}</p>
        <span class="dashboard-window__action">{{ target.id > 2 ? '预留' : '进入控制' }} <FIcon name="open_24_regular" /></span></div>
    </RouterLink>
  </section>
</template>
<style scoped>
.dashboard-windows{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--spacingHorizontalL)}.dashboard-window{display:block;min-width:0;padding:var(--spacingVerticalL) var(--spacingHorizontalL);background:var(--colorNeutralBackground1);border:1px solid var(--colorNeutralStroke2);border-radius:var(--borderRadiusXLarge);color:var(--colorNeutralForeground1);text-decoration:none;transition:border-color var(--durationFast)}.dashboard-window:hover{border-color:var(--colorBrandStroke1);text-decoration:none}.dashboard-window__heading{display:flex;align-items:center;gap:var(--spacingHorizontalS);flex-wrap:wrap}.dashboard-window__number{display:grid;place-items:center;width:32px;height:32px;background:var(--colorBrandBackground2);color:var(--colorBrandForeground2);border-radius:var(--borderRadiusMedium);font-family:Consolas,monospace;font-weight:600}.dashboard-window p{margin:var(--spacingVerticalM) 0;color:var(--colorNeutralForeground2);overflow-wrap:anywhere}.dashboard-window__action{display:flex;align-items:center;gap:var(--spacingHorizontalS);color:var(--colorBrandForeground1);font-size:var(--fontSizeBase200)}
.dashboard-windows{grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}.dashboard-window{padding:12px}.dashboard-window__heading{gap:6px}.dashboard-window__number{width:24px;height:24px;font-size:12px}.dashboard-window__preview{aspect-ratio:16/9;margin-top:12px;border:1px solid var(--console-border);border-radius:var(--borderRadiusSmall);overflow:hidden}.dashboard-window p{font-size:12px;margin:10px 0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.dashboard-window__action{font-size:12px}
.dashboard-window__preview{margin-top:8px;border:0}.dashboard-window__footer{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-top:8px;min-width:0}.dashboard-window__footer p{margin:0;min-width:0}.dashboard-window__action{white-space:nowrap;flex-shrink:0;gap:4px}
@media(max-width:999px){.dashboard-windows{grid-template-columns:repeat(2,minmax(0,1fr))}}
</style>
