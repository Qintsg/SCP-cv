/**
 * 文件描述：手机、Pad和触摸导航的共享布局策略。
 * 项目：SCP-cv；文件：useControlLayout.ts；作者：Qintsg；日期：2026-10-28。
 */
import { computed, onMounted, onUnmounted, ref } from 'vue';

export const MOBILE_CONTROL_QUERY = '(max-width: 767px), (pointer: coarse) and (max-height: 599px) and (max-width: 1023px)';
export const TABLET_CONTROL_QUERY = '(min-width: 600px) and (min-height: 600px) and (any-pointer: coarse)';

/** :returns: 手机精简模式、Pad完整模式及悬浮导航；旋转只更新几何条件。 */
export function useControlLayout() {
  const phoneMedia = typeof window === 'undefined' ? undefined : window.matchMedia(MOBILE_CONTROL_QUERY);
  const tabletMedia = typeof window === 'undefined' ? undefined : window.matchMedia(TABLET_CONTROL_QUERY);
  const tabletControls = ref(tabletMedia?.matches ?? false);
  const mobileControls = ref(Boolean(phoneMedia?.matches) && !tabletControls.value);
  const touchNavigation = computed(() => mobileControls.value || tabletControls.value);
  /** :returns: 依据当前窗口更新布局，保留页面和媒体来源身份。 */
  const update = (): void => {
    tabletControls.value = tabletMedia?.matches ?? false;
    mobileControls.value = Boolean(phoneMedia?.matches) && !tabletControls.value;
  };
  onMounted(() => {
    update(); phoneMedia?.addEventListener('change', update); tabletMedia?.addEventListener('change', update);
  });
  onUnmounted(() => {
    phoneMedia?.removeEventListener('change', update); tabletMedia?.removeEventListener('change', update);
  });
  return { mobileControls, tabletControls, touchNavigation };
}
