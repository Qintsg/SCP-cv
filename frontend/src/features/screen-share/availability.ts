/** 网页共享支持条件；窄屏布局不代替实际浏览器采集能力。SCP-cv 2026-10-11。 */
export interface ScreenShareAvailability {
  secure: boolean;
  web: boolean;
  capture: boolean;
  peer: boolean;
  authenticated: boolean;
  online: boolean;
}

/** :param state: 当前页面及目标能力。:returns: 空字符串表示可共享，其余为固定提示键。 */
export function screenShareAvailability(state: ScreenShareAvailability): string {
  if (!state.secure) return 'share_https_required';
  if (!state.web || !state.capture || !state.peer) return 'share_browser_unsupported';
  if (!state.authenticated) return 'share_login_required';
  if (!state.online) return 'share_player_offline';
  return '';
}
