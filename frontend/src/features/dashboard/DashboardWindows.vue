<!-- 首页双窗口入口，只读取已同步的会话；导航不会切换播放内容。 -->
<script setup lang="ts">
import { tagColor } from '@/design-system/tagColor';
import { RouterLink } from 'vue-router';
import ATag from 'ant-design-vue/es/tag';
import { useSessionStore } from '@/stores/sessions';
import FIcon from '@/design-system/FIcon.vue';
import { windowPresentation } from './windowPresentation';
const sessions = useSessionStore();
const targets = [{ id: 1, path: '/display/big-left' }, { id: 2, path: '/display/big-right' }];
</script>
<template>
  <section class="dashboard-windows" aria-label="两个播放窗口">
    <RouterLink v-for="target in targets" :key="target.id" :to="target.path" class="dashboard-window">
      <div class="dashboard-window__heading"><span class="dashboard-window__number" aria-hidden="true">{{ target.id }}</span><strong>窗口{{ target.id }}</strong>
        <a-tag :color="tagColor(windowPresentation(sessions.byWindowId(target.id)).tone)" >{{ windowPresentation(sessions.byWindowId(target.id)).label }}</a-tag></div>
      <p>{{ sessions.byWindowId(target.id)?.source_name || (sessions.byWindowId(target.id)?.source_type === 'screen_share' ? '屏幕共享' : '未选择媒体') }}</p>
      <span class="dashboard-window__action">进入控制 <FIcon name="open_24_regular" /></span>
    </RouterLink>
  </section>
</template>
<style scoped>
.dashboard-windows{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--spacingHorizontalL)}.dashboard-window{display:block;min-width:0;padding:var(--spacingVerticalL) var(--spacingHorizontalL);background:var(--colorNeutralBackground1);border:1px solid var(--colorNeutralStroke2);border-radius:var(--borderRadiusXLarge);color:var(--colorNeutralForeground1);text-decoration:none;transition:border-color var(--durationFast)}.dashboard-window:hover{border-color:var(--colorBrandStroke1);text-decoration:none}.dashboard-window__heading{display:flex;align-items:center;gap:var(--spacingHorizontalS);flex-wrap:wrap}.dashboard-window__number{display:grid;place-items:center;width:32px;height:32px;background:var(--colorBrandBackground2);color:var(--colorBrandForeground2);border-radius:var(--borderRadiusMedium);font-family:Consolas,monospace;font-weight:600}.dashboard-window p{margin:var(--spacingVerticalM) 0;color:var(--colorNeutralForeground2);overflow-wrap:anywhere}.dashboard-window__action{display:flex;align-items:center;gap:var(--spacingHorizontalS);color:var(--colorBrandForeground1);font-size:var(--fontSizeBase200)}
@media(max-width:599px){.dashboard-windows{grid-template-columns:1fr}}
</style>
