/** 文件描述：控制面板订阅应用共享状态。项目：SCP-cv；文件：useScreenShare.ts；作者：Qintsg；日期：2026-10-30。 */
import { computed } from 'vue';
import { useScreenShareStore } from './screenShareStore';

/** :param target: 当前控制窗口。:returns: 页面卸载后仍由应用持有的共享操作，在线状态由应用会话提供。 */
export function useScreenShare(target: () => number) {
  const store = useScreenShareStore(), owner = computed(() => store.forWindow(target()));
  return { view: computed(() => owner.value.view.value), availability: computed(() => owner.value.availability.value),
    active: computed(() => owner.value.active.value), upgradeAvailable: computed(() => owner.value.upgradeAvailable.value),
    isCurrent: () => owner.value.isCurrent(), start: (audio: boolean) => owner.value.start(audio), stop: () => owner.value.stop(),
    refreshStop: () => owner.value.refreshStop(), setAudio: (volume: number, muted: boolean) => owner.value.setAudio(volume, muted) };
}
