# Quickstart: 分享链路验收

## 前置

支持getDisplayMedia的桌面浏览器；localhost仅本机实验，现场须可信HTTPS。D4保持admin交互会话、两播放器DISPLAY2/3。只共享自有动态夹具，不采集用户隐私桌面，不发墙面/电源。

## 分层验收

1. `uv run --no-project .specify/scripts/python/validate_specs.py --specs-dir specs`。
2. xUnit ScreenShare子集核Owner/cap/SDP上限/争抢/过期/条件CLOSE、后台定时器、原Worker完整Dispose/原OS身份退出、重启孤立意图和预案捕获拒绝，记录实际计数/TRX；新Worker/离线/取消不能当释放证据。
3. `pnpm --pm-on-fail=ignore --prefix frontend exec node --test scripts/screen-sharing.test.mjs scripts/screen-sharing-output.test.mjs scripts/screen-sharing-navigation.test.mjs`、typecheck/build:web及全前端；CLI版本与packageManager不同时本命令不自动下载，仓库CERNET配置保持不变。
4. 独立本机数据目录，Playwright真实getDisplayMedia共享自有标签页、真实WebRTC及接收video运动像素；mock与canvas.captureStream不算原生采集。
5. 两专用原生WebView资源的激活后帧、关闭、十轮交错与旧回调隔离；本机一显示器不称D4两大屏。
6. 本机→D4两正式Worker逐一出画，记录candidate pair、运动原图、非目标保持；两独立会话5分钟、分别停止、断线/换源/取消；失败原件保留不放宽预算。

## 边界

这是计划，未执行不得勾选。009旧部署/媒体验证不算本功能证据；D4现有服务未自动更新。HTTPS、原生许可与内容音频分别验证。
