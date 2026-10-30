# 本机验证

日期：2026-10-30。本机验证记录；030实现纳入031的源码提交、双仓推送和客户端打包，见[031记录](../031-navigation-glass-client/verification.md)。D4服务未部署，未连接D4。

## 网页刷新

`WebRefreshRealTests.RefreshReloadsActualCachedPageScriptAndData`通过。使用正式PlayerRuntimeHost、实际WebView2和回环HTTP服务器，页面、JS和数据均设置24小时缓存。版本1打开后修改服务器内容，再执行REFRESH，三个版本字段均读取到2，请求数至少新增3次。原控件、HWND、地址及Cookie保持；错误来源代次拒绝，HTTP500导航明确失败。

`MediaSharingRealTests.RefreshUsesRealQueueAndRejectsOtherSources`通过。真实Kestrel、登录、CSRF及SQLite命令记录确认REFRESH；空窗和图片来源400。会话pending_command反映队首，具体提交动作按submitted_command_id读取命令。

证据：`.validation/media-030/web-refresh-release.trx`、`media-sharing-release.trx`。

## 持续共享

`MediaSharingRealTests.ActualVideoCaptureSurvivesViewChangesAndReleases`通过。实际Kestrel、认证Named Pipe、PlayerRuntimeHost及专用WebView接收；浏览器使用FFmpeg生成的真实视频文件，通过原生captureStream采集并交给正式ScreenSharePublisher。控制观察组件卸载／重建10次，按窗口共享所有者保持同一实例。

最终一次实际共享身份保持，接收后帧从2增至889，工作线程时钟运行35次，持续时间超过原30秒发布租约；停止后状态closed，原采集轨道全部ended，原生接收关闭。浏览器没有页面异常。证据：`.validation/media-030/share-result.json`、`share-child.json`及`media-sharing-release.trx`。

补充无头标签切换验收的后帧从2增至891，状态closed、无页面异常，证据为`media-sharing-background.trx`。新建另一标签并切换前后，Chromium仍将原文档报告为visible，因此没有把该次切换计为真实后台调度验证。

这些测试验证真实媒体收发和组件资源归属，使用文件采集；未重新操作屏幕选择器，也未模拟鼠标、键盘或触摸。真实后台标签的系统节流、网络物理中断后的恢复仍需交互环境验收；本轮没有将这些边界计为通过。

## 全类型预览

`frontend/scripts/verify-media-preview-real.mjs`最终27组通过：390、800、1440宽度，分别查看图片、视频、音频、网页、PPT、PDF、RTSP、RTMP及SRT。使用实际ControlHost、LibreOffice转换、MediaMTX、FFmpeg及Chromium。没有替换网络、原生对象或文件内容解析器。

- 图片及PPT真实页图可解码；AllinOne转换为9页，读取第二页。
- PDF.js读取实际PDF，呈现第二页；按钮状态及页面宽度检查通过。
- 视频、音频读取真实媒体时长和元数据；视频的真实播放与采集由上面的共享收发独立验证。
- RTSP、RTMP、SRT分别收到真实解码后帧；每页的预览池为一条流和一个解码器。
- 所有页面无水平溢出、无页面异常；预览未发送窗口播放、墙面、电源或声音命令。
- 关闭观看后服务端lease_count和upstream_count均为0。

最终报告及截图：`.validation/media-030/run-1793367282435/`。只读截图已检查手机文稿、Pad PDF及桌面直播。HTTP(S)、UDP和RTP完成登记来源的协议接入，本轮未单独发送这些协议的媒体；受原站策略限制的外部网页和浏览器不支持的编码照实反馈。

## 构建与合同

- .NET锁定还原通过；最终Release完整solution构建0警告、0错误。
- 新增真实验证3项通过，未跳过。前端类型检查与Web生产构建通过。
- Spec Kit全目录校验通过；Redocly校验0错误，4项现有非2xx操作警告（WebSocket101及退役410）。
- `git diff --check`通过。

运行环境使用仓库`.validation/dotnet-sdk-026`内10.0.400 SDK。NuGet网络操作仅清空当前进程代理后正常验证，仓库来源及安全检查保持。测试使用独立数据、profile和进程，结束后无本轮服务端口监听；未修改系统显示模式。

## 重现入口

配置`SCP_PLAYWRIGHT_MODULE`、`SCP_BROWSER`实际路径。共享集成测试调用`frontend/scripts/verify-media-sharing-real.mjs`，通过PATH或`SCP_FFMPEG`选择FFmpeg。

预览脚本另需要`SCP_MEDIA_PPTX`、`SCP_MEDIA_PDF`和本机LibreOffice；当前脚本使用`.validation/dotnet-sdk-026`、项目MediaMTX及本机FFmpeg路径。只在本机回环端口19231–19236运行。

```powershell
dotnet test runtime-dotnet/tests/ScpCv.Windows.Tests/ScpCv.Windows.Tests.csproj -c Release --filter FullyQualifiedName~WebRefreshRealTests
dotnet test runtime-dotnet/tests/ScpCv.Integration.Tests/ScpCv.Integration.Tests.csproj -c Release --filter FullyQualifiedName~MediaSharingRealTests
node frontend/scripts/verify-media-preview-real.mjs
```
