<script setup lang="ts">
/**
 * 文件描述：屏幕共享的原生折叠入口，展开状态只影响展示。
 * 项目：SCP-cv；文件：ScreenShareDisclosure.vue；作者：Qintsg；日期：2026-11-05。
 */
import FIcon from '@/design-system/FIcon.vue';
import ScreenSharePanel from './ScreenSharePanel.vue';

defineProps<{ windowId: number; playerOnline: boolean }>();
</script>

<template>
  <details class="share-disclosure">
    <summary class="share-disclosure__summary">
      <FIcon class="share-disclosure__icon" name="desktop_24_regular" :size="20" />
      <span class="share-disclosure__title">屏幕共享</span>
      <FIcon class="share-disclosure__chevron" name="chevron_right_24_regular" :size="18" />
    </summary>
    <div class="share-disclosure__content">
      <ScreenSharePanel :key="windowId" :window-id="windowId" :player-online="playerOnline" />
    </div>
  </details>
</template>

<style scoped>
.share-disclosure {
  min-width: 0;
  border: var(--strokeWidthThin) solid var(--colorNeutralStroke2);
  border-radius: var(--borderRadiusXLarge);
  background: var(--colorNeutralBackground1);
  color: var(--colorNeutralForeground1);
}

.share-disclosure__summary {
  display: flex;
  min-height: 56px;
  align-items: center;
  gap: var(--spacingHorizontalM);
  padding: var(--spacingVerticalM) var(--spacingHorizontalL);
  border-radius: inherit;
  cursor: pointer;
  list-style: none;
  user-select: none;
  transition: background-color var(--durationFaster) var(--curveEasyEase);
}

.share-disclosure__summary::-webkit-details-marker { display: none; }
.share-disclosure__summary:hover { background: var(--colorNeutralBackground1Hover); }
.share-disclosure__summary:focus-visible {
  outline: var(--strokeWidthThick) solid var(--colorBrandStroke1);
  outline-offset: 3px;
}
.share-disclosure__icon { color: var(--colorBrandForeground1); }
.share-disclosure__title { flex: 1; font-weight: var(--fontWeightSemibold); }
.share-disclosure__chevron {
  color: var(--colorNeutralForeground2);
  transition: transform var(--durationNormal) var(--curveEasyEase);
}
.share-disclosure[open] > .share-disclosure__summary {
  border-bottom: var(--strokeWidthThin) solid var(--colorNeutralStroke2);
  border-radius: var(--borderRadiusXLarge) var(--borderRadiusXLarge) 0 0;
  background: var(--colorNeutralBackground2);
}
.share-disclosure[open] > .share-disclosure__summary .share-disclosure__chevron { transform: rotate(90deg); }
.share-disclosure__content { padding: var(--spacingVerticalM) var(--spacingHorizontalM); }
.share-disclosure__content :deep(.screen-share) { border: 0; border-radius: 0; padding: 0; }

@media (max-width: 767px) {
  .share-disclosure__summary { padding-inline: var(--spacingHorizontalM); }
}
@media (prefers-reduced-motion: reduce) {
  .share-disclosure__summary, .share-disclosure__chevron { transition: none; }
}
</style>
