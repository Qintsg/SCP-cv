<script setup lang="ts">
/**
 * 设置中心显示器 Tab。
 * 负责选择物理显示器并应用到指定播放窗口。
 */
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ARadio from 'ant-design-vue/es/radio';
import { RadioGroup as ARadioGroup } from 'ant-design-vue/es/radio';

import FIcon from '@/design-system/FIcon.vue';
import { useToast } from '@/composables/useToast';
import { useDisplayStore } from '@/stores/displays';
import type { DisplayTargetItem } from '@/services/api';
import { useSettingsRequestContext } from '../useSettingsRequestContext';

interface DisplaySelection {
  target: DisplayTargetItem | null;
  mode: 'single';
  label: string;
}

const { t } = useI18n();
const display = useDisplayStore();
const toast = useToast();

const targetWindowId = ref<number>(1);
const pending = ref(false);
const displaySelection = ref<DisplaySelection>({
  target: null,
  mode: 'single',
  label: '',
});
const captureContext = useSettingsRequestContext(() => {
  pending.value = false;
  displaySelection.value = { target: null, mode: 'single', label: '' };
});

const selectableTargets = computed(() => display.targets.filter((target) => {
  if (!target.is_playback_target) return false;
  const expectedRole = targetWindowId.value === 1 ? 'big_left' : 'big_right';
  return !target.playback_role || target.playback_role === expectedRole;
}));

watch(targetWindowId, () => {
  displaySelection.value = { target: null, mode: 'single', label: '' };
});

/** :param target: 当前可选的显示器。:returns: 只更新未提交的选择。 */
function pickDisplay(target: DisplayTargetItem): void {
  if (pending.value) return;
  displaySelection.value = {
    target,
    mode: 'single',
    label: target.name,
  };
}

/** :returns: 向捕获的窗口/显示器提交一次绑定，仅当前归属展示反馈。 */
async function applyDisplay(): Promise<void> {
  if (pending.value) return;
  const selection = displaySelection.value;
  if (!selection.label || !selectableTargets.value.some((target) => target.name === selection.label)) {
    toast.warning(t('settings.pickDisplayFirst'));
    return;
  }
  const windowId = targetWindowId.value;
  const targetLabel = selection.label;
  const current = captureContext();
  pending.value = true;
  try {
    await display.applyToWindow(windowId, selection.mode, targetLabel);
    if (current()) toast.success(t('settings.appliedOk', { id: windowId }));
  } catch (error) {
    if (current()) toast.error(t('settings.applyFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally { if (current()) pending.value = false; }
}
</script>

<template>
  <section class="settings-view__display">
    <a-card :title="t('settings.windowSelect')">
      <a-radio-group v-model:value="targetWindowId" :disabled="pending" :aria-label="t('settings.windowSelect')">
        <a-radio :value="1">{{ t('settings.window', { id: 1 }) }}</a-radio>
        <a-radio :value="2">{{ t('settings.window', { id: 2 }) }}</a-radio>
      </a-radio-group>
    </a-card>

    <a-card :title="t('settings.availableDisplays')">
      <div class="settings-view__display-grid">
        <button v-for="target in selectableTargets" :key="target.index" type="button"
          class="settings-view__display-tile"
          :disabled="pending"
          :aria-pressed="displaySelection.label === target.name"
          :class="{ 'settings-view__display-tile--selected': displaySelection.label === target.name && displaySelection.mode === 'single' }"
          @click="pickDisplay(target)">
          <p class="settings-view__display-name">{{ t('settings.displayName', { n: target.index + 1 }) }}</p>
          <p class="settings-view__display-meta">{{ target.width }} × {{ target.height }}</p>
          <p class="settings-view__display-meta">({{ target.x }}, {{ target.y }})</p>
          <FIcon v-if="target.is_primary" class="settings-view__display-primary" name="star_24_filled" />
        </button>
      </div>
    </a-card>

    <a-button type="primary" :disabled="pending || !displaySelection.label" :loading="pending" @click="applyDisplay">
      <template #icon><FIcon name="checkmark_24_regular" /></template>
      {{ t('settings.applyToWindow', { id: targetWindowId }) }}
    </a-button>
  </section>
</template>
