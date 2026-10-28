/**
 * 文件描述：统一手机竖横屏的控制界面与预览策略。
 * 项目：SCP-cv；文件：useMobileControls.ts；作者：Qintsg；日期：2026-10-28。
 */
import { useControlLayout } from './useControlLayout';
export { MOBILE_CONTROL_QUERY } from './useControlLayout';

/** :returns: 手机控制模式；旋转时保持触摸设备的轻量界面。 */
export function useMobileControls() {
  return useControlLayout().mobileControls;
}
