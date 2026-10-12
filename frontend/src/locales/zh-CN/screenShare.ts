/** 屏幕共享用户文案；临时授权、实际画面与关闭确认明确分开。SCP-cv 2026-10-11。 */
export default {
  screenShare: {
    title: '屏幕共享', target: '目标 · 窗口 {id}', hint: '共享屏幕、应用窗口或标签页。临时实时画面，不写入媒体库。',
    start: '选择共享内容', stop: '停止共享', refreshStop: '查询停止状态', includeAudio: '包括内容音频（不含麦克风）',
    frames: '已接收 {count} 帧', noAudio: '仅共享画面：浏览器未提供内容音轨。',
    audioPresent: '检测到内容音轨。默认接收静音，可在共享声音区明确开启；不获取麦克风。',
    audio: {
      title: '共享声音', volume: '设置音量', volumeLabel: '音量设置（0–100）', apply: '应用音量', mute: '静音', unmute: '开启声音',
      muteUnknown: '先设为静音', muted: '静音', unmuted: '未静音', actual: '实际音量 {volume} · {muted}', unknown: '实际音量/静音尚未确认',
      waiting: '实际接收画面后才能调整声音。', noLocalTrack: '浏览器未提供内容音轨，声音控制不可用。', noReceiverTrack: '接收端尚无内容音轨，声音控制不可用。',
      phases: { pending: '等待播放器应用并回读实际属性…', confirmed: '播放器已确认本次音频设置。', unconfirmed: '未确认本次音频设置，不会自动重发。', failed: '本次音频设置未完成。' },
      errors: {
        share_audio_invalid: '音量必须是 0–100 的整数。', share_audio_identity_invalid: '音频响应身份不匹配，未确认本次设置。',
        share_audio_failed: '播放器未完成本次音频设置；视频共享仍继续。', share_audio_unconfirmed: '未确认本次音频设置；实际值以回读为准，不会自动重发。',
        share_audio_unavailable: '当前共享尚不具备声音控制条件。', share_player_offline: '播放器离线，未确认本次音频设置。', share_ended: '原共享已结束，不继续控制声音。',
      },
    },
    stopPending: '本地采集已停止，正在等待播放器实际释放；不会自动重新共享。',
    upgrade: {
      title: '屏幕共享需要 HTTPS',
      description: '普通播放控制可继续使用 HTTP，但浏览器屏幕共享需要 HTTPS 安全连接。确认后将跳转到当前页面的 HTTPS 入口，可能需要重新登录；请再次点击共享并选择内容。自签证书未被信任时，浏览器仍会显示证书警告，跳转不会自动建立信任或开始采集。',
      confirm: '前往 HTTPS', cancel: '继续使用 HTTP',
    },
    output: {
      current: '播放器实际共享状态', last: '最后报告的共享状态（播放器离线）', waiting: '等待共享画面',
      stopPending: '共享停止待确认', audioUnknown: '尚未确认接收端的内容音轨。',
      temporary: '临时实时画面，不存入媒体库或预案；不能暂停、定位、循环或自动重开。',
    },
    phases: {
      idle: '点击后在浏览器选择器中确认共享内容。', selecting: '请在浏览器选择器中确认；尚未切换播放器。',
      connecting: '正在连接播放器；尚未收到实际画面。', sharing: '已收到画面', stopping: '停止已受理，等待播放器确认。',
      closed: '共享已关闭。', failed: '共享未完成，请检查播放器或网络后重新选择。', cancelled: '已取消共享，本地采集已停止。',
    },
    errors: {
      share_https_required: '当前使用 HTTP；点击共享可确认前往 HTTPS，普通控制不受影响。',
      share_https_unavailable: 'HTTPS 入口未配置或暂时不可用，请联系管理员检查证书和端口；未开始共享。',
      share_https_same_origin_required: '当前连接了另一个主机入口。请直接打开该主机的网页控制台后再切换 HTTPS，避免改变共享目标。',
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
