<script setup lang="ts">
/** 共享声音操作区：实际属性与设置草稿分离，未知时不能猜成已开启。SCP-cv 2026-10-11。 */
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AButton from 'ant-design-vue/es/button';
import type { ScreenShareSnapshot } from '@/services/screenSharing';
import type { ScreenShareAudioView } from './ScreenShareAudioController';

const props = defineProps<{ snapshot: ScreenShareSnapshot | null; sharing: boolean; localHasAudio: boolean; audio: ScreenShareAudioView }>();
const emit = defineEmits<{ setAudio: [volume: number, muted: boolean] }>();
const { t } = useI18n();
const draft = ref<number | string>(100);
let edited = false;
const unavailable = computed(() => !props.sharing ? 'waiting' : !props.localHasAudio ? 'noLocalTrack' :
  props.snapshot?.receiver_has_audio !== true ? 'noReceiverTrack' : '');
const disabled = computed(() => Boolean(unavailable.value) || props.audio.phase === 'pending');
const validDraft = computed(() => typeof draft.value === 'number' && Number.isInteger(draft.value) && draft.value >= 0 && draft.value <= 100);
const actualKnown = computed(() => !unavailable.value && typeof props.snapshot?.actual_volume === 'number' &&
  Number.isInteger(props.snapshot.actual_volume) && props.snapshot.actual_volume >= 0 && props.snapshot.actual_volume <= 100 && typeof props.snapshot.actual_muted === 'boolean');
const actual = computed(() => actualKnown.value ? t('screenShare.audio.actual', { volume: props.snapshot!.actual_volume,
  muted: t(props.snapshot!.actual_muted ? 'screenShare.audio.muted' : 'screenShare.audio.unmuted') }) : t('screenShare.audio.unknown'));
const toggleLabel = computed(() => !actualKnown.value ? t('screenShare.audio.muteUnknown') :
  t(props.snapshot!.actual_muted ? 'screenShare.audio.unmute' : 'screenShare.audio.mute'));
const feedback = computed(() => props.audio.errorCode ? t(`screenShare.audio.errors.${props.audio.errorCode}`) :
  props.audio.phase === 'idle' ? '' : t(`screenShare.audio.phases.${props.audio.phase}`));

watch(() => props.snapshot?.id, () => { edited = false; draft.value = actualKnown.value ? props.snapshot!.actual_volume! : 100; }, { immediate: true });
watch(() => props.snapshot?.actual_volume, () => { if (!edited && actualKnown.value) draft.value = props.snapshot!.actual_volume!; });
/** :returns: 用户开始编辑后，周期回读只更新实际值，不覆盖尚未应用的设置。 */
function markEdited(): void { edited = true; }
/** :returns: 仅发一次明确设置，未知静音按安全静音请求，不乐观修改实际值。 */
function applyVolume(): void {
  if (!disabled.value && validDraft.value) emit('setAudio', Number(draft.value), actualKnown.value ? props.snapshot!.actual_muted! : true);
}
/** :returns: 切换已知静音；未知时只允许先静音，忽略尚未应用的音量草稿。 */
function toggleMute(): void {
  if (!disabled.value) emit('setAudio', actualKnown.value ? props.snapshot!.actual_volume! : 100, actualKnown.value ? !props.snapshot!.actual_muted : true);
}
</script>

<template>
  <div class="share-audio" :aria-label="t('screenShare.audio.title')">
    <div class="share-audio__heading">
      <h4>{{ t('screenShare.audio.title') }}</h4>
      <span class="share-audio__actual" role="status" aria-live="polite" aria-atomic="true">{{ actual }}</span>
    </div>
    <p class="share-audio__hint" :class="{ 'share-audio__empty': !unavailable }">{{ unavailable ? t(`screenShare.audio.${unavailable}`) : ' ' }}</p>
    <form class="share-audio__actions" @submit.prevent="applyVolume">
      <label class="share-audio__field">
        <span>{{ t('screenShare.audio.volume') }}</span>
        <input v-model.number="draft" type="number" min="0" max="100" step="1" :disabled="disabled"
          :aria-label="t('screenShare.audio.volumeLabel')" @input="markEdited" />
      </label>
      <a-button html-type="submit"  :disabled="disabled || !validDraft">{{ t('screenShare.audio.apply') }}</a-button>
      <a-button html-type="button"  :disabled="disabled" @click="toggleMute">{{ toggleLabel }}</a-button>
    </form>
    <p class="share-audio__feedback" :class="{ 'share-audio__empty': !feedback }" role="status" aria-live="polite" aria-atomic="true">{{ feedback || ' ' }}</p>
  </div>
</template>

<style scoped src="./ScreenShareAudioPanel.css"></style>
