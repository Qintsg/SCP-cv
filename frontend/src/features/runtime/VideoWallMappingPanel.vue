<!-- 大屏编排只需一次应用；保留认证/主机/页面围栏和修订预检，不把发送视为实体画面确认。 -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import FIcon from '@/design-system/FIcon.vue';
import { useToast } from '@/composables/useToast';
import { useRequestContext } from '@/composables/useRequestContext';
import { api, type VideoWallMappingItem } from '@/services/api';
import { clientConnection } from '@/platform/connection';
import { useRuntimeStore } from '@/stores/runtime';
import { useAuthStore } from '@/stores/auth';
import WallLayoutComposer from './wall/WallLayoutComposer.vue';
import { validateBlocks } from './wall/layoutModel';
const { t } = useI18n(), runtime = useRuntimeStore(), auth = useAuthStore(), toast = useToast();
const name = ref(t('videoWall.defaultName'));
const mappings = ref<VideoWallMappingItem[]>([{ region: 'left', input: { kind: 'window_1' } }, { region: 'right', input: { kind: 'window_2' } }]);
const loading = ref(false), applying = ref(false), ready = ref(false), error = ref(''), audioWarning = ref('');
const editorRevision = ref(0);
let alive = true, reload = 0;
const captureContext = useRequestContext(() => {
  const ticket = ++reload;
  editorRevision.value++;
  loading.value = false; applying.value = false; ready.value = false; error.value = ''; audioWarning.value = '';
  name.value = t('videoWall.defaultName'); mappings.value = [{ region: 'fullscreen', input: { kind: 'window_1' } }];
  queueMicrotask(() => { if (alive && ticket === reload && auth.isAuthenticated && !clientConnection.isSwitching) void load(); });
});
const busy = computed(() => loading.value || applying.value);
const validation = computed(() => {
  if (!mappings.value.length) return '请至少配置一个显示块';
  try { validateBlocks(mappings.value); }
  catch (failure) { return failure instanceof Error ? failure.message : '布局无效'; }
  return '';
});
watch(() => JSON.stringify(mappings.value), () => { if (!busy.value) error.value = ''; });
/** :returns: 只读恢复，不随页面挂载发送物理指令。 */
async function load(): Promise<void> {
  if (!alive || busy.value || !auth.isAuthenticated || clientConnection.isSwitching) return;
  const current = captureContext(); loading.value = true; error.value = ''; ready.value = false;
  try {
    const response = await api.getVideoWallLayout();
    if (!current()) return;
    if (response.layout.draft) { name.value = response.layout.draft.name; mappings.value = response.layout.draft.mappings; }
    ready.value = true;
  } catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : t('videoWall.loadFail'); }
  finally { if (current()) loading.value = false; }
}
/** :returns: 一次点击预检后仅应用本次修订；冲突、离页、失败不重试或补发。 */
async function apply(): Promise<void> {
  if (!alive || busy.value || !ready.value || validation.value || !auth.isAuthenticated || clientConnection.isSwitching) return;
  const current = captureContext();
  const submitted = { name: name.value.trim(), mappings: mappings.value.map(item => ({ ...item, input: { ...item.input }, ...(item.rect ? { rect: { ...item.rect } } : {}) })) };
  applying.value = true; error.value = ''; audioWarning.value = '';
  try {
    const checked = await api.saveVideoWallLayout(submitted);
    if (!current()) return;
    if (!checked.layout.draft?.can_apply) throw new Error(checked.layout.draft?.unavailable_reason || t('videoWall.pendingFrames'));
    await runtime.applySavedVideoWallLayout(checked.layout.draft_revision);
    if (!current()) return;
    const audio = runtime.venueAudioResult;
    audioWarning.value = audio && !['confirmed', 'simulated', 'unchanged'].includes(audio.status) ? audio.detail : '';
    toast.success(t('videoWall.applied'));
  } catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : t('videoWall.applyFail'); }
  finally { if (current()) applying.value = false; }
}
onMounted(() => { void load(); });
onBeforeUnmount(() => { alive = false; reload++; });
</script>
<template>
  <a-card class="wall-map" :loading="loading">
    <template #title><span class="wall-map__title"><FIcon name="desktop_24_regular" :size="20" />{{ t('videoWall.title') }}</span></template>
    <template #extra><span class="wall-map__input-hint">D4 · 4K · 63 / 64 / IP81</span></template>
    <WallLayoutComposer :key="editorRevision" v-model="mappings" :disabled="busy" />
    <p v-if="validation && ready" class="wall-map__validation" role="status">{{ validation }}</p>
    <a-alert v-if="error" type="error" show-icon :message="error" role="alert" />
    <a-alert v-if="audioWarning" type="warning" show-icon :message="audioWarning" />
    <div class="wall-map__actions">
      <a-button v-if="!ready && !loading" @click="load">{{ t('common.retry') }}</a-button>
      <a-button type="primary" :loading="applying" :disabled="busy || !ready || !!validation" @click="apply">{{ t('videoWall.apply') }}<FIcon name="arrow_right_24_regular" :size="16" /></a-button>
    </div>
  </a-card>
</template>
<style scoped src="./VideoWallMappingPanel.css" />
