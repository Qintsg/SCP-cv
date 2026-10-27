<script setup lang="ts">
/**
 * 移动端「更多」 Sheet：列出二级入口。
 * tabbar「更多」弹出底部 Sheet（DESIGN.md §2 Scale 适配下的移动端导航形态）。
 */
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import ControlDrawer from '@/design-system/ControlDrawer.vue';
import ASwitch from 'ant-design-vue/es/switch';

import EmergencyMenu from './EmergencyMenu.vue';
import FIcon from '@/design-system/FIcon.vue';
import BigScreenModeButtons from '@/features/runtime/BigScreenModeButtons.vue';
import { useDialog } from '@/composables/useDialog';
import { useToast } from '@/composables/useToast';
import { useRuntimeStore } from '@/stores/runtime';

const props = defineProps<{ open: boolean }>();
const emit = defineEmits<{ (event: 'update:open', value: boolean): void }>();

const { t } = useI18n();
const router = useRouter();
const runtime = useRuntimeStore();
const dialog = useDialog();
const toast = useToast();
const pendingMode = ref<'single' | 'double' | null>(null);

const currentScreenMode = computed(() => runtime.runtime?.big_screen_mode ?? 'single');

async function switchScreenMode(mode: 'single' | 'double'): Promise<void> {
  if (pendingMode.value !== null) return;
  pendingMode.value = mode;
  try {
    await runtime.setBigScreenMode(mode);
    toast.success(mode === 'double' ? t('more.switchedDouble') : t('more.switchedSingle'));
  } catch (error) {
    toast.error(t('more.switchFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    pendingMode.value = null;
  }
}

const muteToggle = computed({
  get: () => runtime.systemVolume.muted,
  set: async (next: boolean) => {
    try {
      await runtime.setSystemVolume(runtime.systemVolume.level, next);
    } catch (error) {
      toast.error(t('more.muteFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  },
});

const isOpen = computed({
  get: () => props.open,
  set: (value: boolean) => emit('update:open', value),
});

function close(): void {
  emit('update:open', false);
}

function navigate(path: string): void {
  close();
  void router.push(path);
}

async function onAboutHelp(): Promise<void> {
  close();
  await router.push('/settings');
}

void EmergencyMenu;
void dialog;
</script>

<template>
  <ControlDrawer role="dialog" aria-modal="true" :aria-label="t('more.title')" :title="t('more.title')" v-model:open="isOpen" :width="360" placement="bottom" :height="480">

      <a-card size="small" :title="t('more.screenMode')" class="more-sheet__card">
        <BigScreenModeButtons
          :current-mode="currentScreenMode"
          :pending-mode="pendingMode"
          block
          @select="switchScreenMode"
        />
      </a-card>

      <a-card size="small" :title="t('more.systemMute')" class="more-sheet__card">
        <a-switch v-model:checked="muteToggle">
          <template #checkedChildren>{{ t('more.enableSystemMute') }}</template>
          <template #unCheckedChildren>{{ t('more.enableSystemMute') }}</template>
        </a-switch>
      </a-card>

      <a-card size="small" :title="t('more.settings')" class="more-sheet__card">
        <a-button type="text" block @click="navigate('/background-audio')">
          <template #icon><FIcon name="music_note_2_24_regular" /></template>
          {{ t('more.openBackgroundAudio') }}
        </a-button>
        <a-button type="text" block @click="navigate('/settings')">
          <template #icon><FIcon name="settings_24_regular" /></template>
          {{ t('more.openSettings') }}
        </a-button>
        <a-button type="text" block @click="navigate('/devices')">
          <template #icon><FIcon name="camera_24_regular" /></template>
          {{ t('nav.devices') }}
        </a-button>
        <a-button type="text" block @click="onAboutHelp">
          <template #icon><FIcon name="info_24_regular" /></template>
          {{ t('more.aboutHelp') }}
        </a-button>
      </a-card>

  </ControlDrawer>
</template>

<style scoped>
.more-sheet__card {
  margin-bottom: var(--spacingVerticalM);
}
</style>
