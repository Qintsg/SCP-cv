<script setup lang="ts">
/** 实验性 PowerPoint 原生放映必须由管理员在设置中显式启用。 */
import { onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { NAlert, NCard, NSwitch, NTag } from 'naive-ui';

import { api, type PowerPointSettingsItem } from '@/services/api';
import { useDialog } from '@/composables/useDialog';
import { useToast } from '@/composables/useToast';

const { t } = useI18n();
const dialog = useDialog();
const toast = useToast();
const settings = ref<PowerPointSettingsItem | null>(null);
const loading = ref(false);
const pending = ref(false);

async function refresh(): Promise<void> {
  loading.value = true;
  try {
    settings.value = (await api.getPowerPointSettings()).settings;
  } catch (error) {
    toast.error(t('settings.pptExperimentalLoadFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    loading.value = false;
  }
}

async function change(enabled: boolean): Promise<void> {
  if (!settings.value || pending.value || (enabled && !settings.value.available)) return;
  if (enabled) {
    const confirmed = await dialog.danger({
      title: t('settings.pptExperimentalConfirmTitle'),
      description: t('settings.pptExperimentalConfirmDesc'),
      confirmLabel: t('settings.pptExperimentalConfirm'),
    });
    if (!confirmed) return;
  }
  pending.value = true;
  try {
    settings.value = (await api.setPowerPointSettings(enabled)).settings;
    toast.success(enabled ? t('settings.pptExperimentalOn') : t('settings.pptExperimentalOff'));
  } catch (error) {
    toast.error(t('settings.pptExperimentalFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    pending.value = false;
  }
}

onMounted(() => { void refresh(); });
</script>

<template>
  <n-card :title="t('settings.pptExperimentalTitle')">
    <n-alert type="warning">{{ t('settings.pptExperimentalHint') }}</n-alert>
    <div class="ppt-experimental__row">
      <span>{{ t('settings.pptExperimentalToggle') }}</span>
      <n-switch :value="settings?.experimental_enabled ?? false"
        :disabled="loading || pending || !settings?.available"
        :loading="pending" @update:value="change" />
    </div>
    <n-tag :type="settings?.available ? 'warning' : 'default'" size="small" round>
      {{ settings?.available ? t('settings.pptExperimentalAvailable') : t('settings.pptExperimentalUnavailable') }}
    </n-tag>
    <p v-if="settings?.detail" class="ppt-experimental__detail">{{ settings.detail }}</p>
  </n-card>
</template>

<style scoped>
.ppt-experimental__row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--spacingHorizontalM);
  margin: var(--spacingVerticalM) 0;
}
.ppt-experimental__detail { margin: var(--spacingVerticalS) 0 0; color: var(--colorNeutralForeground2); }
</style>
