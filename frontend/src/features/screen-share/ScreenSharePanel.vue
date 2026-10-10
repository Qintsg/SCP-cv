<script setup lang="ts">
/** 紧凑网页共享入口：逻辑目标、真实接收与停止确认分层，不自播预览。SCP-cv 2026-10-11。 */
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { NButton, NCheckbox } from 'naive-ui';
import FIcon from '@/design-system/FIcon.vue';
import { useScreenShare } from './useScreenShare';

const props = defineProps<{ windowId: number; playerOnline: boolean }>();
const { t } = useI18n();
const audio = ref(false);
const { view, availability, active, start, stop, refreshStop } = useScreenShare(() => props.windowId, () => props.playerOnline);
const reason = computed(() => view.value.errorCode || (!active.value ? availability.value : ''));
const status = computed(() => reason.value ? t(`screenShare.errors.${reason.value}`) : t(`screenShare.phases.${view.value.phase}`));
const canStop = computed(() => ['selecting', 'connecting', 'sharing'].includes(view.value.phase));
/** :returns: 原点击栈直接打开选择器，不能在许可前等待HTTP。 */
function startSharing(): void { void start(audio.value); }
</script>

<template>
  <section class="screen-share" :class="{ 'screen-share--receiving': view.phase === 'sharing' }" :aria-label="t('screenShare.title')">
    <div class="screen-share__heading">
      <span class="screen-share__icon"><FIcon name="desktop_24_regular" :size="20" /></span>
      <h3>{{ t('screenShare.title') }}</h3>
      <span class="screen-share__target">{{ t('screenShare.target', { id: windowId }) }}</span>
    </div>
    <p class="screen-share__hint">{{ t('screenShare.hint') }}</p>
    <div class="screen-share__status" role="status" aria-live="polite" aria-atomic="true">
      <span class="screen-share__dot" aria-hidden="true" />
      <span>{{ status }}</span>
      <span v-if="view.phase === 'sharing'" class="screen-share__frames">{{ t('screenShare.frames', { count: view.snapshot?.actual_frames ?? 0 }) }}</span>
    </div>
    <p v-if="view.phase === 'stopping'" class="screen-share__hint">{{ t('screenShare.stopPending') }}</p>
    <p v-if="view.captureActive" class="screen-share__hint">{{ t(view.localHasAudio ? 'screenShare.audioPresent' : 'screenShare.noAudio') }}</p>
    <div class="screen-share__actions">
      <n-checkbox v-model:checked="audio" :disabled="active">{{ t('screenShare.includeAudio') }}</n-checkbox>
      <n-button type="primary" :disabled="active || Boolean(availability)" @click="startSharing">{{ t('screenShare.start') }}</n-button>
      <n-button v-if="canStop" type="error" secondary @click="stop">{{ t('screenShare.stop') }}</n-button>
      <n-button v-if="view.phase === 'stopping'" secondary @click="refreshStop">{{ t('screenShare.refreshStop') }}</n-button>
    </div>
  </section>
</template>

<style scoped src="./ScreenSharePanel.css"></style>
