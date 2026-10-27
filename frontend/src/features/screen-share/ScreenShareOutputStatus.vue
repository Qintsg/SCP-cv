<script setup lang="ts">
/** 播放控制区的临时共享摘要；不保存画面、不用文件缩略图或轨道存在冒充音频播出。SCP-cv 2026-10-11。 */
import { useI18n } from 'vue-i18n';
defineProps<{ audio: boolean | null; online: boolean }>();
const { t } = useI18n();
</script>

<template>
  <div class="share-output" role="status">
    <span>{{ t(online ? 'screenShare.output.current' : 'screenShare.output.last') }}</span>
    <span>{{ t(audio === null ? 'screenShare.output.audioUnknown' : audio ? 'screenShare.audioPresent' : 'screenShare.noAudio') }}</span>
  </div>
</template>
<style scoped>
.share-output{display:flex;align-items:center;justify-content:space-between;gap:12px;height:44px;overflow:hidden;font-size:12px;color:var(--console-muted)}.share-output>span{min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
</style>
