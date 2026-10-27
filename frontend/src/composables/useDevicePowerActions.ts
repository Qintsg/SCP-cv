/** 首页/设置共用电源交互：确认、单次意图与页面归属，不直接发送硬件帧。 */
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { clientConnection } from '@/platform/connection';
import { useAuthStore } from '@/stores/auth';
import { useDeviceStore } from '@/stores/devices';
import { restoreActionFocus } from '@/design-system/dialogFocus';
import { useDialog, type DialogConfig } from './useDialog';
import { useRequestContext } from './useRequestContext';
import { useToast } from './useToast';

interface Notice { title: string; detail?: string; error: string; fallback: string; warning?: boolean }
/** :param surface: 保留各页面原有文案。:returns: 具备防重/失效清理的公开交互。 */
export function useDevicePowerActions(surface: 'dashboard' | 'settings') {
  const { t } = useI18n(), device = useDeviceStore(), auth = useAuthStore();
  const toast = useToast(), dialog = useDialog();
  const jobs = ref<Record<string, { id: symbol; action: string; sending: boolean }>>({});
  const captureContext = useRequestContext(() => { jobs.value = {}; });
  /** :param type: 冻结设备。:returns: 发送忙态；确认期间不禁用入口，保留Modal焦点交接。 */
  const isPending = (type: string): boolean => Boolean(jobs.value[type]?.sending) || device.isPending(type);
  /** :param type: 设备。:param action: 原按钮动作。:returns: 仅原按钮显示加载指示。 */
  const isLoading = (type: string, action: string): boolean => Boolean(jobs.value[type]?.sending && jobs.value[type]?.action === action);

  /** :param type: 设备。:param action: 动作。:param send: 冻结的Store调用。:param notice: 冻结文案。:param confirmation: 可选危险确认。:returns: 不重放或向新环境反馈。 */
  async function execute(type: string, action: string, send: () => Promise<void>, notice: Notice, confirmation?: DialogConfig): Promise<void> {
    if (jobs.value[type] || device.isPending(type) || !auth.isAuthenticated || clientConnection.isSwitching) return;
    const scope = captureContext(), id = Symbol('device-action');
    const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    jobs.value[type] = { id, action, sending: !confirmation };
    const current = () => scope() && jobs.value[type]?.id === id;
    try {
      if (confirmation && !await dialog.danger(confirmation)) return;
      if (!current()) return;
      jobs.value[type]!.sending = true;
      await send();
      if (current()) {
        if (notice.warning) toast.warning(notice.title, notice.detail);
        else toast.success(notice.title, notice.detail);
      }
    } catch (error) {
      if (current()) toast.error(notice.error, error instanceof Error ? error.message : notice.fallback);
    } finally {
      if (jobs.value[type]?.id === id) {
        delete jobs.value[type];
        await restoreActionFocus(trigger, () => scope() && !jobs.value[type]);
      }
    }
  }
  /** :returns: 当前拼接屏一次开机，等待时不接受重复动作。 */
  function powerOnSplice(): Promise<void> {
    return execute('splice_screen', 'on', () => device.power('splice_screen', 'on'), {
      title: t(`${surface}.spliceOnOk`), error: t(`${surface}.spliceOnFail`), fallback: t('common.retry'),
    });
  }
  /** :returns: 明确确认后才发关机；取消/归属失效零派发。 */
  function powerOffSplice(): Promise<void> {
    return execute('splice_screen', 'off', () => device.power('splice_screen', 'off'), {
      title: t(`${surface}.spliceOffOk`), warning: true,
      error: t(`${surface}.spliceOffFail`), fallback: t(`${surface}.spliceOffFailDetail`),
    }, { title: t(`${surface}.spliceOffTitle`), description: t(`${surface}.spliceOffDesc`),
      confirmLabel: t(`${surface}.spliceOffConfirm`), cancelLabel: t('common.cancel') });
  }
  /** :param type: 左/右电视。:param label: 点击时名称。:returns: 一次切换，不读回或猜测电源状态。 */
  function toggleTv(type: 'tv_left' | 'tv_right', label: string): Promise<void> {
    return execute(type, 'toggle', () => device.toggle(type), {
      title: t(`${surface}.tvToggleOk`, { label }),
      error: t(`${surface}.tvToggleFail`, { label }), fallback: t('common.retry'),
    });
  }
  return { powerOnSplice, powerOffSplice, toggleTv, isPending, isLoading };
}
