<script setup lang="ts">
/** 紧凑网页共享入口：逻辑目标、真实接收与停止确认分层，不自播预览。SCP-cv 2026-10-11。 */
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { NButton, NCheckbox } from 'naive-ui';
import FIcon from '@/design-system/FIcon.vue';
import { useDialogStore } from '@/composables/useDialog';
import { clientConnection } from '@/platform/connection';
import { useScreenShare } from './useScreenShare';
import { discoverHttpsPage } from './httpsUpgrade';
import ScreenShareAudioPanel from './ScreenShareAudioPanel.vue';

const props = defineProps<{ windowId: number; playerOnline: boolean }>();
const { t } = useI18n();
const audio = ref(false);
const dialog = useDialogStore();
const upgrading = ref(false), upgradeError = ref('');
let upgradeController: AbortController | undefined;
let ownDialog: typeof dialog.config;
const { view, availability, active, upgradeAvailable, isCurrent, start, stop, refreshStop, setAudio } = useScreenShare(() => props.windowId, () => props.playerOnline);
const reason = computed(() => upgradeError.value || view.value.errorCode || (!active.value ? availability.value : ''));
const status = computed(() => reason.value ? t(`screenShare.errors.${reason.value}`) : t(`screenShare.phases.${view.value.phase}`));
const canStop = computed(() => ['selecting', 'connecting', 'sharing'].includes(view.value.phase));
/** :returns: 结束本面板的确认/发现，不关闭其它业务弹窗。 */
function cancelUpgrade(): void {
  upgradeController?.abort();
  if (upgrading.value && ownDialog && dialog.config === ownDialog) dialog.cancel();
}
watch(upgradeAvailable, value => { if (!value) cancelUpgrade(); });
onBeforeUnmount(cancelUpgrade);
/** :param event: 原始用户点击。:returns: HTTPS原栈采集；HTTP只确认/发现/导航，绝不自动采集。 */
function startSharing(event: MouseEvent): void {
  if (upgrading.value || active.value) return;
  upgradeError.value = '';
  if (window.location.protocol !== 'http:') { void start(audio.value); return; }
  if (!upgradeAvailable.value) return;
  void upgrade(event.currentTarget as HTMLElement);
}
/** :param trigger: 焦点归还目标。:returns: 只在原主机/账号寿命仍有效且确认后导航。 */
async function upgrade(trigger: HTMLElement): Promise<void> {
  upgrading.value = true;
  upgradeController = new AbortController();
  const href = window.location.href;
  let timeout: ReturnType<typeof setTimeout> | undefined;
  try {
    if (clientConnection.profile && clientConnection.profile.origin !== window.location.origin) {
      throw new Error('share_https_same_origin_required');
    }
    const confirmation = dialog.confirm({ title: t('screenShare.upgrade.title'), description: t('screenShare.upgrade.description'),
      confirmLabel: t('screenShare.upgrade.confirm'), cancelLabel: t('screenShare.upgrade.cancel'), triggerElement: trigger });
    ownDialog = dialog.config;
    if (!await confirmation || !isCurrent() || !props.playerOnline || upgradeController.signal.aborted) return;
    timeout = setTimeout(() => upgradeController?.abort(), 5000);
    const next = await discoverHttpsPage(href, upgradeController.signal);
    if (isCurrent() && props.playerOnline && !upgradeController.signal.aborted && href === window.location.href) window.location.assign(next);
  } catch (error) {
    if (isCurrent() && props.playerOnline) upgradeError.value = error instanceof Error && error.message === 'share_https_same_origin_required'
      ? error.message : 'share_https_unavailable';
  } finally {
    if (timeout) clearTimeout(timeout);
    upgrading.value = false; upgradeController = undefined; ownDialog = null;
  }
}
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
    <details v-if="view.captureActive" class="screen-share__hint">
      <summary>共享质量 · 清晰度优先</summary>
      <p>采集 {{ view.quality?.captureWidth ?? '未知' }} × {{ view.quality?.captureHeight ?? '未知' }}；
        发送 {{ view.quality?.sentWidth ?? '未知' }} × {{ view.quality?.sentHeight ?? '未知' }}</p>
      <p>实际 {{ view.quality?.bitrateMbps?.toFixed(2) ?? '未知' }} Mbps · {{ view.quality?.fps ?? '未知' }} fps；
        建议上限 {{ view.quality?.limitMbps ?? '待协商' }} Mbps（非实际码率）</p>
      <p>实际接收 {{ view.snapshot?.receiver_quality?.width ?? '未知' }} × {{ view.snapshot?.receiver_quality?.height ?? '未知' }} ·
        {{ view.snapshot?.receiver_quality?.bitrate_mbps?.toFixed(2) ?? '未知' }} Mbps · {{ view.snapshot?.receiver_quality?.fps ?? '未知' }} fps</p>
      <p>限制：{{ ({ none: '无', bandwidth: '网络带宽', cpu: '编码性能', other: '其它', unknown: '未知' } as Record<string, string>)[view.quality?.limitation ?? 'unknown'] }}；
        参数：{{ view.quality?.parameters === 'configured' ? '已接受' : view.quality?.parameters === 'compatible' ? '兼容模式' : '浏览器默认/待协商' }}。接收清晰度仍需以大屏画面核对。</p>
    </details>
    <div class="screen-share__actions">
      <n-checkbox v-model:checked="audio" :disabled="active">{{ t('screenShare.includeAudio') }}</n-checkbox>
      <n-button type="primary" :loading="upgrading" :disabled="active || upgrading || (Boolean(availability) && !upgradeAvailable)" @click="startSharing">{{ t('screenShare.start') }}</n-button>
      <n-button v-if="canStop" type="error" secondary @click="stop">{{ t('screenShare.stop') }}</n-button>
      <n-button v-if="view.phase === 'stopping'" secondary @click="refreshStop">{{ t('screenShare.refreshStop') }}</n-button>
    </div>
    <ScreenShareAudioPanel v-if="view.captureActive" :snapshot="view.snapshot" :sharing="view.phase === 'sharing'"
      :local-has-audio="view.localHasAudio" :audio="view.audio" @set-audio="setAudio" />
  </section>
</template>

<style scoped src="./ScreenSharePanel.css"></style>
