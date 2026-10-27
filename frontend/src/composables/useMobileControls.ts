/**
 * 文件描述：统一手机竖横屏的控制界面与预览策略。
 * 项目：SCP-cv；文件：useMobileControls.ts；作者：Qintsg；日期：2026-10-27。
 */
import { onMounted, onUnmounted, ref } from 'vue';

export const MOBILE_CONTROL_QUERY = '(max-width: 767px), (pointer: coarse) and (max-height: 599px) and (max-width: 1023px)';

/** :returns: 手机控制模式；旋转时保持触摸设备的轻量界面。 */
export function useMobileControls() {
  const media = typeof window === 'undefined' ? undefined : window.matchMedia(MOBILE_CONTROL_QUERY);
  const mobileControls = ref(media?.matches ?? false);
  const update = () => { mobileControls.value = media?.matches ?? false; };
  onMounted(() => { update(); media?.addEventListener('change', update); });
  onUnmounted(() => { media?.removeEventListener('change', update); });
  return mobileControls;
}
