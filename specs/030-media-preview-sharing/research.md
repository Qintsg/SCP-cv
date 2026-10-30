# 研究结论

## 网页刷新

Decision：正式WebView调用Page.reload(ignoreCache=true)，禁用当前控件网络缓存并绕过Service Worker。

Rationale：旧按钮发送PLAY，非VLC分支仅改变状态。当前控件内导航保留Cookie、profile、窗口及声音绑定。

Alternatives considered：不清全局Cookie，不重建profile；普通直播重连继续使用原PLAY。

依据：PlaybackControl、RuntimeStateService、PlayerRuntimeHost.Web；官方WebView2 DevTools与CDP Page/Network合同。研究已完成。

## 持续共享

Decision：应用按窗口持有Publisher，控制面板订阅；退出应用、账号或主机变化停止。暂时disconnected保留原peer恢复机会。

Rationale：原useScreenShare在onBeforeUnmount直接dispose；rtcPeer将disconnected立即视为失败。

Alternatives considered：不缓存全部路由，不自动重采集。

## 全类型预览

Decision：详情面板复用来源ID、媒体文件接口、文稿页图及按需直播池。

Rationale：现有名称没有预览动作；PPT/PDF已有转换制品；直播池合并相同流。

Alternatives considered：不另建浏览器PPT解析器或转码服务。
