<script setup lang="ts">
/** 播放控制区的临时共享摘要；不保存画面、不用文件缩略图或轨道存在冒充音频播出。SCP-cv 2026-10-11。 */
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
defineProps<{ frames: number; audio: boolean | null; online: boolean }>();
const { t } = useI18n();
</script>

<template>
  <a-alert type="info" :message="t(online ? 'screenShare.output.current' : 'screenShare.output.last')"  aria-live="polite"><template #description>
    <p>{{ t('screenShare.frames', { count: frames }) }}</p>
    <p>{{ t(audio === null ? 'screenShare.output.audioUnknown' : audio ? 'screenShare.audioPresent' : 'screenShare.noAudio') }}</p>
  </template></a-alert>
</template>
