<script setup lang="ts">
/** 紧凑网页共享入口：逻辑目标、真实接收与停止确认分层，不自播预览。SCP-cv 2026-10-11。 */
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AButton from 'ant-design-vue/es/button';
import ACheckbox from 'ant-design-vue/es/checkbox';
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
    <div class="screen-share__status" role="status" aria-live="polite" aria-atomic="true">
      <span class="screen-share__dot" aria-hidden="true" />
      <span class="screen-share__state">{{ status }}</span>
    </div>
    <div class="screen-share__actions">
      <a-checkbox v-model:checked="audio" :disabled="active">{{ t('screenShare.includeAudio') }}</a-checkbox>
      <a-button type="primary" :loading="upgrading" :disabled="active || upgrading || (Boolean(availability) && !upgradeAvailable)" @click="startSharing">{{ t('screenShare.start') }}</a-button>
      <a-button v-if="canStop" danger  @click="stop">{{ t('screenShare.stop') }}</a-button>
      <a-button v-if="view.phase === 'stopping'"  @click="refreshStop">{{ t('screenShare.refreshStop') }}</a-button>
    </div>
    <ScreenShareAudioPanel v-if="view.captureActive" :snapshot="view.snapshot" :sharing="view.phase === 'sharing'"
      :local-has-audio="view.localHasAudio" :audio="view.audio" @set-audio="setAudio" />
    <details v-if="view.captureActive" class="screen-share__hint"><summary>连接详情</summary>
      <p>{{ view.quality?.sentWidth ?? '—' }} × {{ view.quality?.sentHeight ?? '—' }} · {{ view.quality?.fps ?? '—' }} fps · {{ view.quality?.bitrateMbps?.toFixed(2) ?? '—' }} Mbps</p>
      <p>编码上限 {{ view.quality?.limitMbps ?? '浏览器默认' }} Mbps · 接收 {{ view.snapshot?.receiver_quality?.width ?? '—' }} × {{ view.snapshot?.receiver_quality?.height ?? '—' }}</p>
    </details>
  </section>
</template>

<style scoped src="./ScreenSharePanel.css"></style>
