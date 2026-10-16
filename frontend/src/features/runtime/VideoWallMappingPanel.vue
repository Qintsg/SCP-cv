<!-- 大屏编排：选择只预览，一次应用内部完成预检与按修订发送，不暴露草稿流程。 -->
<script setup lang="ts">
import { selectAriaLabel as vSelectAriaLabel } from '@/design-system/selectAriaLabel';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import AInput from 'ant-design-vue/es/input';
import ASegmented from 'ant-design-vue/es/segmented';
import ASelect from 'ant-design-vue/es/select';
import FIcon from '@/design-system/FIcon.vue';
import { useToast } from '@/composables/useToast';
import { api, type VideoWallInputKind, type VideoWallLayoutItem, type VideoWallMappingItem } from '@/services/api';
import { useRuntimeStore } from '@/stores/runtime';
import { useAuthStore } from '@/stores/auth';

type InputChoice = VideoWallInputKind | 'none';
const { t } = useI18n();
const runtime = useRuntimeStore(), auth = useAuthStore(), toast = useToast();
const name = ref(t('videoWall.defaultName'));
const layout = ref<'fullscreen' | 'split'>('split');
const full = ref<InputChoice>('window_1'), left = ref<InputChoice>('window_1'), right = ref<InputChoice>('window_2');
const fullIp = ref(''), leftIp = ref(''), rightIp = ref('');
const loading = ref(false), applying = ref(false), ready = ref(false), error = ref('');
let alive = true;
const busy = computed(() => loading.value || applying.value);
const options = computed(() => [
  { label: t('videoWall.window1'), value: 'window_1' },
  { label: t('videoWall.window2'), value: 'window_2' },
  { label: t('videoWall.ip81'), value: 'laptop' },
  { label: t('videoWall.none'), value: 'none' },
  { label: t('videoWall.ipStream'), value: 'ip_stream' },
]);
const fullOptions = computed(() => options.value.filter(item => item.value !== 'none'));
const layouts = computed(() => [
  { label: t('videoWall.singleScreen'), value: 'fullscreen' },
  { label: t('videoWall.doubleScreen'), value: 'split' },
]);

/** :param region: 显示区域。:param kind: 输入。:param ip: 设备地址。:returns: 既有映射合同。 */
function mapping(region: VideoWallMappingItem['region'], kind: InputChoice, ip: string): VideoWallMappingItem {
  return { region, input: { kind: kind as VideoWallInputKind, ip_address: kind === 'ip_stream' ? ip.trim() : '' } };
}
const mappings = computed<VideoWallMappingItem[]>(() => layout.value === 'fullscreen'
  ? [mapping('fullscreen', full.value, fullIp.value)]
  : [left.value === 'none' ? null : mapping('left', left.value, leftIp.value),
    right.value === 'none' ? null : mapping('right', right.value, rightIp.value)]
    .filter((item): item is VideoWallMappingItem => item !== null));

/** :param ip: 用户填写的IPv4。:returns: 是否为有效设备地址。 */
function isIpv4(ip: string): boolean {
  return /^(\d{1,3}\.){3}\d{1,3}$/.test(ip) && ip.split('.').every(part => Number(part) <= 255);
}
const validation = computed(() => !name.value.trim() ? t('videoWall.invalidName')
  : !mappings.value.length ? t('videoWall.emptyRegion')
    : mappings.value.some(item => item.input.kind === 'ip_stream' && !isIpv4(item.input.ip_address || ''))
      ? t('videoWall.invalidIp') : '');
const audioWarning = computed(() => runtime.venueAudioResult && !['confirmed', 'simulated', 'unchanged'].includes(runtime.venueAudioResult.status)
  ? runtime.venueAudioResult.detail : '');
const selection = computed(() => JSON.stringify({ name: name.value, mappings: mappings.value }));
watch(selection, () => { if (!busy.value) error.value = ''; });

/** :returns: 锁定连接、账户及本组件寿命，旧响应不能延续到另一主机。 */
function currentScope(): () => boolean {
  const generation = runtime.connectionGeneration, userId = auth.user?.id, signedIn = auth.isAuthenticated;
  return () => alive && generation === runtime.connectionGeneration && userId === auth.user?.id && signedIn === auth.isAuthenticated;
}

/** :param value: 服务器保存值。:returns: 恢复当前布局，另一布局的本地选择保持。 */
function restore(value: VideoWallLayoutItem): void {
  name.value = value.name;
  const fullscreen = value.mappings.find(item => item.region === 'fullscreen');
  layout.value = fullscreen ? 'fullscreen' : 'split';
  if (fullscreen) { full.value = fullscreen.input.kind; fullIp.value = fullscreen.input.ip_address || ''; }
  else {
    const l = value.mappings.find(item => item.region === 'left'), r = value.mappings.find(item => item.region === 'right');
    left.value = l?.input.kind ?? 'none'; right.value = r?.input.kind ?? 'none';
    leftIp.value = l?.input.ip_address || ''; rightIp.value = r?.input.ip_address || '';
  }
}

/** :returns: 只读取布局；读取失败可重试，绝不随页面加载下发设备命令。 */
async function load(): Promise<void> {
  if (busy.value) return;
  const current = currentScope();
  loading.value = true; error.value = ''; ready.value = false;
  try {
    const response = await api.getVideoWallLayout();
    if (!current()) return;
    if (response.layout.draft) restore(response.layout.draft);
    ready.value = true;
  } catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : t('videoWall.loadFail'); }
  finally { if (alive) loading.value = false; }
}

/** :returns: 一次点击依次预检和应用本次修订；失败、冲突或离页不重试/补发。 */
async function apply(): Promise<void> {
  if (busy.value || !ready.value || validation.value) return;
  const current = currentScope();
  const submitted = { name: name.value.trim(), mappings: mappings.value.map(item => ({ region: item.region, input: { ...item.input } })) };
  applying.value = true; error.value = '';
  try {
    const checked = await api.saveVideoWallLayout(submitted);
    if (!current()) return;
    if (!checked.layout.draft?.can_apply) throw new Error(checked.layout.draft?.unavailable_reason || t('videoWall.pendingFrames'));
    // 使用本次预检返回的修订号；其它客户端插入修改由后端拒绝，不借用后来布局。
    await runtime.applySavedVideoWallLayout(checked.layout.draft_revision);
    if (current()) toast.success(t('videoWall.applied'));
  } catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : t('videoWall.applyFail'); }
  finally { if (alive) applying.value = false; }
}

onMounted(() => { void load(); });
onBeforeUnmount(() => { alive = false; });
</script>

<template>
  <a-card class="wall-map" :loading="loading">
    <template #title><span class="wall-map__title"><FIcon name="desktop_24_regular" :size="20" />{{ t('videoWall.title') }}</span></template>
    <template #extra><span class="wall-map__input-hint">D4 · 63 / 64 / IP81</span></template>
    <div class="wall-map__toolbar">
      <a-segmented v-model:value="layout" :options="layouts" :disabled="busy" :aria-label="t('videoWall.layout')" />
    </div>
    <div class="wall-map__screen" :class="{ 'wall-map__screen--split': layout === 'split' }" :aria-label="t('videoWall.preview')">
      <div v-if="layout === 'fullscreen'" class="wall-map__region">
        <span class="wall-map__region-label">{{ t('videoWall.fullscreen') }}</span>
        <a-select v-select-aria-label="t('videoWall.fullscreen') + t('videoWall.input')" v-model:value="full" :options="fullOptions" :disabled="busy" :virtual="false" :aria-label="t('videoWall.fullscreen') + t('videoWall.input')" />
        <a-input v-if="full === 'ip_stream'" v-model:value="fullIp" :disabled="busy" :placeholder="t('videoWall.ipAddress')" :aria-label="t('videoWall.fullscreen') + t('videoWall.ipAddress')" />
      </div>
      <template v-else>
        <div class="wall-map__region" :class="{ 'wall-map__region--empty': left === 'none' }">
          <span class="wall-map__region-label">{{ t('videoWall.left') }}</span>
          <a-select v-select-aria-label="t('videoWall.left') + t('videoWall.input')" v-model:value="left" :options="options" :disabled="busy" :virtual="false" :aria-label="t('videoWall.left') + t('videoWall.input')" />
          <a-input v-if="left === 'ip_stream'" v-model:value="leftIp" :disabled="busy" :placeholder="t('videoWall.ipAddress')" :aria-label="t('videoWall.left') + t('videoWall.ipAddress')" />
        </div>
        <div class="wall-map__region" :class="{ 'wall-map__region--empty': right === 'none' }">
          <span class="wall-map__region-label">{{ t('videoWall.right') }}</span>
          <a-select v-select-aria-label="t('videoWall.right') + t('videoWall.input')" v-model:value="right" :options="options" :disabled="busy" :virtual="false" :aria-label="t('videoWall.right') + t('videoWall.input')" />
          <a-input v-if="right === 'ip_stream'" v-model:value="rightIp" :disabled="busy" :placeholder="t('videoWall.ipAddress')" :aria-label="t('videoWall.right') + t('videoWall.ipAddress')" />
        </div>
      </template>
    </div>
    <p v-if="validation && ready" class="wall-map__validation" role="status">{{ validation }}</p>
    <a-alert v-if="error" type="error" show-icon :message="error" role="alert" />
    <a-alert v-if="audioWarning" type="warning" show-icon :message="audioWarning" />
    <div class="wall-map__actions">
      <a-button v-if="!ready && !loading" @click="load">{{ t('common.retry') }}</a-button>
      <a-button type="primary" :loading="applying" :disabled="busy || !ready || !!validation" @click="apply">
        {{ t('videoWall.apply') }}<FIcon name="arrow_right_24_regular" :size="16" />
      </a-button>
    </div>
    <details class="wall-map__options">
      <summary>{{ t('videoWall.options') }}</summary>
      <label>{{ t('videoWall.name') }}<a-input v-model:value="name" :disabled="busy" :aria-label="t('videoWall.name')" /></label>
      <p>{{ t('videoWall.ip81Hint') }}</p><p>{{ t('videoWall.streamHint') }}</p>
    </details>
  </a-card>
</template>
<style scoped src="./VideoWallMappingPanel.css" />
