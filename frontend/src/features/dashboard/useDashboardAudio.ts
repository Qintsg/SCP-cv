/** 首页系统音量/静音交互：同一端点互斥，完整归属失效时不续发或反馈旧请求。 */
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { clientConnection } from '@/platform/connection';
import { useAuthStore } from '@/stores/auth';
import { useRuntimeStore } from '@/stores/runtime';
import { useRequestContext } from '@/composables/useRequestContext';
import { useThrottledSlider } from '@/composables/useThrottledSlider';
import { useToast } from '@/composables/useToast';
import { restoreActionFocus } from '@/design-system/dialogFocus';
import { isWritePending } from '@/services/writeFlights';
import { RequestScopeChangedError } from '@/services/authenticationScope';

/** :returns: 已绑定当前页面/主机/会话的滑杆与静音状态；不改硬件确认语义。 */
export function useDashboardAudio() {
  const { t } = useI18n(), runtime = useRuntimeStore(), auth = useAuthStore(), toast = useToast();
  const volumeScope = ref(0), muteBusy = ref(false), sliderWriting = ref(false), recoveryBusy = ref(false);
  let muteIntent = 0, recoveryIntent = 0;
  const captureContext = useRequestContext(() => {
    volumeScope.value++; muteIntent++; muteBusy.value = false; sliderWriting.value = false; recoveryBusy.value = false;
    recoverVolumeIfBusy();
  });
  /** :returns: 新页面/认证等待原写终端后只读恢复，不重放旧写入；卸载或再次切换则不读。 */
  function recoverVolumeIfBusy(): void {
    const intent = ++recoveryIntent;
    queueMicrotask(async () => {
      const current = captureContext();
      if (!current() || intent !== recoveryIntent || !auth.isAuthenticated || !isWritePending('system-volume')) return;
      const owned = () => current() && intent === recoveryIntent;
      recoveryBusy.value = true;
      try { await runtime.refreshSystemVolume(owned); }
      catch (error) { if (owned() && !(error instanceof RequestScopeChangedError)) toast.error(t('dashboard.volumeFail'), error instanceof Error ? error.message : t('common.retry')); }
      finally { if (owned()) recoveryBusy.value = false; }
    });
  }
  onMounted(recoverVolumeIfBusy);
  const transportBusy = computed(() => isWritePending('system-volume'));
  const volumeBlocked = computed(() => muteBusy.value || recoveryBusy.value || (transportBusy.value && !sliderWriting.value));
  const volume = useThrottledSlider(() => runtime.systemVolume.level, {
    scope: () => volumeScope.value,
    isActive: () => auth.isAuthenticated && !clientConnection.isSwitching && !volumeBlocked.value,
    commit: async level => {
      const scope = volumeScope.value;
      sliderWriting.value = true;
      try { await runtime.setSystemVolume(level, runtime.systemVolume.muted); }
      finally { if (scope === volumeScope.value) sliderWriting.value = false; }
    },
    onError: error => toast.error(t('dashboard.volumeFail'), error instanceof Error ? error.message : t('common.retry')),
  });
  const muteToggle = computed({
    get: () => runtime.systemVolume.muted,
    /** :param next: 本次明确静音值。:returns: 一次操作；不排队，也不让旧finally解除新忙态。 */
    set: async (next: boolean) => {
      if (muteBusy.value || recoveryBusy.value || volume.busy.value || transportBusy.value || !auth.isAuthenticated || clientConnection.isSwitching) return;
      const current = captureContext(), operation = ++muteIntent, level = volume.value.value;
      const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      muteBusy.value = true;
      try { await runtime.setSystemVolume(level, next); }
      catch (error) { if (current()) toast.error(t('dashboard.muteFail'), error instanceof Error ? error.message : t('common.retry')); }
      finally {
        if (operation === muteIntent) {
          muteBusy.value = false;
          await restoreActionFocus(trigger, () => current() && operation === muteIntent);
        }
      }
    },
  });
  return { volume, muteToggle, muteBusy, volumeBlocked, transportBusy, recoveryBusy };
}
