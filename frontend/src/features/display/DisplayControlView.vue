<script setup lang="ts">
/**
 * 显示控制视图：单窗口源切换 + 播放控制。
 * 路由参数 :target 决定逻辑窗口；移动端使用原生单选组替代左侧导航，墙面布局不隐藏目标。
 */
import { tabsKeyboard as vTabsKeyboard } from '@/design-system/tabsKeyboard';

import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import ACard from 'ant-design-vue/es/card';
import AEmpty from 'ant-design-vue/es/empty';
import AAlert from 'ant-design-vue/es/alert';
import ATabs from 'ant-design-vue/es/tabs';
import { TabPane as ATabPane } from 'ant-design-vue/es/tabs';
import ASegmented from 'ant-design-vue/es/segmented';

import FIcon from '@/design-system/FIcon.vue';
import FPageHeader from '@/design-system/FPageHeader.vue';
import PlaybackControl from './PlaybackControl.vue';
import SourcePicker from './SourcePicker.vue';
import ScreenSharePanel from '../screen-share/ScreenSharePanel.vue';
import { DISPLAY_TARGETS, resolveDisplayTarget } from './displayTargets';
import { useBreakpoint } from '@/composables/useBreakpoint';
import { useSessionStore } from '@/stores/sessions';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const sessionStore = useSessionStore();
const { isMobile } = useBreakpoint();

const targetParam = computed(() => String(route.params.target ?? 'big-left'));
const targetMeta = computed(() => resolveDisplayTarget(targetParam.value));

const currentSession = computed(() => {
  if (!targetMeta.value) return undefined;
  return sessionStore.byWindowId(targetMeta.value.windowId);
});

const availableTargets = DISPLAY_TARGETS;
const targetOptions = computed(() => availableTargets.map(target => ({ label: target.title, value: target.param })));
const targetTitle = computed(() => availableTargets.find((target) => target.param === targetParam.value)?.title
  ?? t('display.windowUnknown'));

type TabId = 'source' | 'control';
const mobileTab = ref<TabId>('control');

const commandObservation = computed(() => (
  targetMeta.value ? sessionStore.commandObservation(targetMeta.value.windowId) : null
));

/** :param value: 明确逻辑目标路由。:returns: 仅切换控制视图，不下发墙面或播放命令。 */
function changeTarget(value: string): void {
  void router.push(`/display/${value}`);
}

/** :returns: 窄屏受理普通切源后显示控制区，实际播出仍等待Worker报告。 */
function handleSourceOpened(): void {
  if (isMobile.value) mobileTab.value = 'control';
}

</script>

<template>
  <div class="display-view">
    <a-segmented class="display-view__targets" :value="targetParam" :options="targetOptions" :aria-label="t('display.windowSelectAria')" @change="value => changeTarget(String(value))" />

    <FPageHeader :title="targetTitle + t('display.titleSuffix')" />

    <a-alert
      v-if="commandObservation && !commandObservation.playerOnline"
      type="warning"
      :message="t('playback.playerOfflineTitle')"

    />
    <a-alert
      v-else-if="commandObservation?.accepted"
      type="info"
      :message="t('playback.commandAcceptedTitle')"

    />

    <ScreenSharePanel v-if="targetMeta && currentSession" :key="targetMeta.windowId" :window-id="targetMeta.windowId" :player-online="currentSession.player_online" />

    <template v-if="!currentSession">
      <a-card>
        <a-empty :description="t('display.loadingTitle')">
          <template #image>
            <FIcon name="info_24_regular" />
          </template>
        </a-empty>
      </a-card>
    </template>

    <template v-else-if="isMobile">
      <a-tabs v-tabs-keyboard v-model:activeKey="mobileTab" type="line" role="tablist" :aria-label="t('display.viewSwitchAria')">
        <a-tab-pane key="source" :tab="t('display.tabSource')">
          <SourcePicker :window-id="currentSession.window_id" @opened="handleSourceOpened" />
        </a-tab-pane>
        <a-tab-pane key="control" :tab="t('display.tabControl')">
          <PlaybackControl :session="currentSession" />
        </a-tab-pane>
      </a-tabs>
    </template>

    <template v-else>
      <div class="display-view__columns">
        <a-card class="display-view__playback">
          <PlaybackControl :session="currentSession" />
        </a-card>
        <SourcePicker :window-id="currentSession.window_id" @opened="handleSourceOpened" />
      </div>
    </template>
  </div>
</template>

<style scoped>
.display-view {
  display: flex;
  flex-direction: column;
  gap: var(--spacingVerticalL);
  max-width: 1440px;
}

.display-view__targets {
  display: flex;
  gap: var(--spacingHorizontalM);
  margin: 0;
  padding: 0;
  border: 0;
}

.display-view__targets label {
  display: inline-flex;
  align-items: center;
  gap: var(--spacingHorizontalS);
  min-height: var(--touch-target-min);
  cursor: pointer;
}

.display-view__targets input {
  accent-color: var(--colorBrandBackground);
}

.display-view__hero {
  position: relative;
  padding: var(--spacingVerticalXXL) var(--spacingHorizontalXXXL);
  background: linear-gradient(135deg, var(--colorBrandBackground2) 0%, var(--colorNeutralBackground1) 100%);
  border-radius: var(--borderRadiusXLarge);
  border: 1px solid var(--colorNeutralStroke2);
  box-shadow: var(--shadow4);
  overflow: hidden;
}

.display-view__hero::after {
  content: '';
  position: absolute;
  right: -80px;
  top: -80px;
  width: 220px;
  height: 220px;
  border-radius: var(--borderRadiusCircular);
  background: radial-gradient(circle at center,
      color-mix(in srgb, var(--colorBrandBackground) 22%, transparent),
      transparent 70%);
  pointer-events: none;
}

.display-view__hero > * {
  position: relative;
  z-index: 1;
}

.display-view__eyebrow {
  margin: 0;
  font-size: var(--fontSizeBase200);
  font-weight: 600;
  color: var(--colorBrandForeground1);
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.display-view__title {
  margin: var(--spacingVerticalXS) 0;
  font-size: var(--fontSizeHero700);
  line-height: var(--lineHeightHero700);
  font-weight: 600;
}

.display-view__caption {
  margin: 0;
  color: var(--colorNeutralForeground2);
}

.display-view__columns {
  display: grid;
  grid-template-columns: minmax(320px, 5fr) minmax(360px, 7fr);
  gap: var(--spacingHorizontalL);
  align-items: start;
}

.display-view__playback {
  min-width: 0;
}

@media (max-width: 1023px) {
  .display-view__columns {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 767px) {
  .display-view :deep(.ant-tabs-tab[role='tab']) {
    min-height: var(--touch-target-min);
    align-items: center;
  }

  .display-view :deep(.ant-tabs-tab[role='tab']:focus-visible) {
    outline: var(--strokeWidthThick) solid var(--colorBrandStroke1);
    outline-offset: 2px;
  }

  .display-view__hero {
    padding: var(--spacingVerticalL) var(--spacingHorizontalL) var(--spacingVerticalXL);
  }

  .display-view__title {
    font-size: var(--fontSizeBase600);
    line-height: var(--lineHeightBase600);
  }
}
</style>
