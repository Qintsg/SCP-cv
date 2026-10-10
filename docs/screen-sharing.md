# 网页屏幕共享

## 当前实现状态

010正在开发，尚未部署到D4，也还不能从正式控制台发起共享。已完成内存会话、发布者/接收者HTTP信令和已认证Worker租约授权；正式Player资源、网页采集入口、异常回收、内容音频、可信HTTPS及两窗实机验收仍在后续任务中。接口受理和本机技术原型不能作为完整功能交付。

目标是从桌面Chrome/Edge的原生屏幕/应用/标签页选择器直接共享到窗口1或2，不用额外推流程序。不请求麦克风，不保存屏幕媒体，不新增媒体库源，不改变墙面布局或电源。

## 会话与信令

取得原生许可后才预约，取消许可不得向播放主机提交OPEN。每窗一个共享，两个目标独立；预约/协商20秒硬上限，实际接收后发布者心跳期限30秒，不能自动续采集或重播。

- `POST /api/screen-shares/`仅预约，要求登录和CSRF；`window_id`只接受数值1/2，`has_audio`来自实际采集内容音轨。
- `POST .../{id}/offer/`只接受一次offer，再在SQLite事务中重核原Worker实例/owner/group并排动态OPEN；202不是已经出画。
- `GET .../{id}/`只返回本人状态和answer；`POST .../{id}/heartbeat/`不增加视频帧；`DELETE .../{id}/`先撤销媒体授权，再按share_id/source_generation条件停止。
- 已OPEN停止返回`stopping`，不把CLOSE入队或“不再是当前意图”当作资源已退出；实际确认后才能释放目标预约。未OPEN取消直接`closed`，不排CLOSE。

SDP解码后UTF-8字节不超过64KiB，HTTP请求体另有有限上限（含JSON转义开销）。所有共享响应no-store；错误只返回稳定说明/代码，不输出SDP、候选地址或私有授权。完整[OpenAPI](openapi.yaml)已登记端点；开发期间不要用这些入口替代尚未完成的控制台流程。

## 私有接收边界

接收cap是内存256位随机值，绑定分享、窗口、源代次及预约时的原Worker实例/owner/group；发布者Cookie不能领取或报告接收证据。新Worker即使已通过普通管道认证，也不能重放旧共享。

已认证且匹配原身份的OPEN lease才注入私有`receiver_uri`，数据库命令只留`source_type=screen_share/source_id=0/share_id`。cap仅放URI fragment，不放HTTP查询串。WPF首次导航通过[官方NavigateWithWebResourceRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatewithwebresourcerequest)设置Authorization Bearer；接收脚本读取fragment后立即从历史地址移除，后续fetch使用同一header且不携带用户Cookie。

`/api/screen-share/receiver/{id}/`及其offer/status/answer/frames/end操作同时核loopback、cap、身份、来源及阶段；代理的loopback地址不是授权。HTML/JS嵌入发布包，脚本和样式用精确CSP哈希，无外链或用户脚本拼接。预备帧和协商不能授予`receiving`，只有激活后实际视频帧才上报。

接收脚本停止会关闭peer、停止远端轨道、清视频srcObject、取消帧回调/HTTP/轮询及ICE等待。页面的closed消息不等于原生WebView已Dispose，正式Player和清理服务仍必须提供独立释放证据。

## 配置和验证边界

接收地址从ControlHost的`urls`首个监听地址派生，0.0.0.0/IPv6通配绑定转换为本机地址；沿用正式启动脚本的默认`https://localhost:18443`。可通过`ScreenSharing__ReceiverBaseUrl`显式选择**已监听**的loopback HTTP(S)纯地址；拒绝远程主机、用户信息、路径、query或fragment，且不自动新增监听或绕过证书验证。非通配LAN绑定需另行提供本机监听与明确接收地址。

浏览器采集必须是安全上下文；localhost只支持本机开发，普通局域网HTTP不满足要求。正常LAN可信证书部署步骤尚待T014实现，不能使用关闭安全策略/不安全来源flag作为正式方法。

当前自动化覆盖登录/CSRF、离线、争抢、SDP大小、窗口/代次/原Worker围栏、失败撤销、接收来源及停止待确认；模拟接收帧、脚本VM与本机独立WebView原型不算D4两正式Worker出画。真实双窗5分钟、十轮停止/切换、选择器取消和内容音频仍须执行，见[010任务](../specs/010-browser-screen-share/tasks.md)与[验证记录](../specs/010-browser-screen-share/verification.md)。
