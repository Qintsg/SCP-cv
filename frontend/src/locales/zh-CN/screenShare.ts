/** 屏幕共享用户文案；临时授权、实际画面与关闭确认明确分开。SCP-cv 2026-10-11。 */
export default {
  screenShare: {
    title: '屏幕共享', target: '目标 · 窗口 {id}', hint: '共享屏幕、应用窗口或标签页。临时实时画面，不写入媒体库。',
    start: '选择共享内容', stop: '停止共享', refreshStop: '查询停止状态', includeAudio: '包括内容音频（不含麦克风）',
    frames: '已接收 {count} 帧', noAudio: '仅共享画面：浏览器未提供内容音轨。',
    audioPresent: '检测到内容音轨；接收端音频控制仍在验证，当前静音。',
    stopPending: '本地采集已停止，正在等待播放器实际释放；不会自动重新共享。',
    phases: {
      idle: '点击后在浏览器选择器中确认共享内容。', selecting: '请在浏览器选择器中确认；尚未切换播放器。',
      connecting: '正在连接播放器；尚未收到实际画面。', sharing: '已收到画面', stopping: '停止已受理，等待播放器确认。',
      closed: '共享已关闭。', failed: '共享未完成，请检查播放器或网络后重新选择。', cancelled: '已取消共享，本地采集已停止。',
    },
    errors: {
      share_https_required: '请通过受信任的 HTTPS 访问控制台；局域网 HTTP 不支持屏幕共享。',
      share_browser_unsupported: '请在支持屏幕共享的桌面 Chrome 或 Edge 网页中操作，当前浏览器或封装端不支持。',
      share_login_required: '登录后才能共享。', share_player_offline: '目标播放器当前离线，恢复在线后再共享。',
      share_permission_denied: '已取消或拒绝共享许可，未切换播放器。', share_cancelled: '已取消共享。',
      share_capture_failed: '浏览器无法读取所选内容，请重新选择可共享的屏幕或窗口。', share_capture_empty: '浏览器未提供有效视频轨道，未切换播放器。',
      share_context_changed: '账号或播放主机已改变，本地采集已停止；旧共享将按服务端时限回收。',
      share_connection_failed: '共享连接失败，本地采集已停止；检查播放器和网络后重新选择。',
      share_first_frame_timeout: '播放器未在时限内收到画面，本地采集已停止。', share_peer_disconnected: '媒体连接已断开，本地采集已停止。',
      share_identity_invalid: '共享响应身份不匹配，本地采集已停止。', share_sdp_invalid: '共享协商信息无效，本地采集已停止。',
      share_ice_timeout: '媒体网络协商超时，本地采集已停止。', share_ended: '共享已结束，本地采集已停止。',
      share_window_busy: '此窗口已有共享或尚未确认释放，请先停止原共享，或选择另一个窗口。',
      share_sdp_too_large: '浏览器的媒体协商信息超过限制，本地采集已停止。',
      share_stop_unconfirmed: '本地采集已停止，但未确认播放器释放；可查询停止状态，不会自动重发停止。',
    },
  },
} as const;
