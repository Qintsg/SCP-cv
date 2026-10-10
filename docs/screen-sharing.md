# 网页屏幕共享

## 当前实现状态

010正在开发，尚未部署到D4，尚不能作为完整功能使用。已完成内存会话、发布者/接收者HTTP信令、已认证Worker租约授权、正式Player专用接收资源、网页原生采集面板和动态HTTP/SSE身份展示；异常回收确认、内容音频、可信HTTPS及两窗实机验收仍在后续任务中。阶段受理和本机Host门禁不能作为完整功能交付。

目标是从桌面Chrome/Edge的原生屏幕/应用/标签页选择器直接共享到窗口1或2，不用额外推流程序。不请求麦克风，不保存屏幕媒体，不新增媒体库源，不改变墙面布局或电源。

## 网页操作区（开发阶段）

显示控制页新增当前逻辑窗口的“屏幕共享”操作区。“选择共享内容”在点击时直接打开原生选择器，取得视频轨道后才预约和协商；取消许可不提交OPEN。内容音频选项默认关闭，是否有音轨以浏览器返回结果为准，不请求麦克风。当前接收端仍静音，音频控制留待T013接通验证。

面板分别显示选择中、连接中、实际接收、失败、停止待确认。只有本人状态为receiving、接收帧为正且已应用answer才显示“已收到画面”；心跳、offer受理或RTC连接不单独授予出画状态。目标标签代表窗口1/2，不代表墙面左/右布局。

停止优先结束本地所有采集轨道、peer和轮询，再向原身份发一次停止。返回stopping或请求失败时保持“等待播放器确认”，不自动重试或开始新共享；“查询停止状态”只发GET，迟到查询失败不能覆盖已经确认的关闭结果。选择器、预约或应答迟到不能复活采集。

离开页面、账号改变或目标离线会停止本地资源。切播放主机在代次改变时同步终止原页面的共享寿命，不等待网络注销；旧请求随后被拒绝也必须释放本地采集，不把旧停止发送到新主机。主机/账号切换或页面结束后，原面板不自动重采且入口保持禁用；重新打开显示控制页后才能在新身份下再次主动选择内容。后台异常回收及实际原生释放确认仍须T011实现。

本机面板验证使用正式UI和真实getDisplayMedia/RTC，但隔离HTTP信令且没有接收播放器，不把这些测试中的closed当作正式Player释放证据。真实许可选择器取消、LAN HTTPS与D4跨机另验。

## 显示控制与临时输出

窗口1/2是两个逻辑播放器，不是墙面左右区域。单屏、双屏或自定义墙面布局下都可以进入两个窗口；导航或选择目标不会下发墙面映射。窄屏页签可用方向键、Home/End、Enter/空格切换，选中状态与焦点明确。

共享没有媒体库ID。会话的`share_id`表示当前请求，`actual_share_id`表示匹配原Worker的最后实际共享资源，二者和代次相符、正视频帧、在线且playing才显示“已收到画面”。`actual_frames=0`不能当出画；离线时明确是最后报告，不显示采集缩略图、文件地址或伪进度。`actual_audio`只表示接收资源有无内容音轨，不代表声音已播出。

播放控制条的“停止共享”按原`share_id`停止后刷新，不发送无条件窗口关闭；意图已清而实际共享尚在时显示“共享停止待确认”并禁用重复关闭。重开共享必须重新选择内容，不能从历史记录自动恢复采集。暂停、定位、循环、普通音量和静音当前均禁用，音频桥另行验证。

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

正式Player每次共享使用独占WebView控制器和InPrivate profile，不进普通网页保温池。禁止外链导航、新窗和采集权限；原生消息同时核文档来源、分享/窗口/代次及有限schema。协商完成和预备帧只允许切入准备，实际切入后还须新的`activated=true`视频帧才能完成OPEN；首帧失败释放候选并保留仍可用的旧画面。所有OPEN阶段共用有限预算，过期租约在原生创建前拒绝。

Worker纯值观测包含`share_id/actual_frames/actual_audio`和null媒体ID，音轨字段只是实际存在轨道，不是可听见的声音证明。HTTP/SSE在持久报告通过原Worker实例/owner/源代次门禁并提交后，映射非秘密`actual_share_id/actual_frames/actual_audio`；独立内存投影有版本围栏，重启不从旧数据库还原授权。当前阶段默认静音且拒绝普通播放/暂停/定位/翻页/循环/音量控制，音频桥等待T013实测。

接收脚本停止会关闭peer、停止远端轨道、清视频srcObject、取消帧回调/HTTP/轮询及ICE等待；Player随后Dispose控制器，停止/换源/退出不保温，旧实例消息不能改变新实例。页面的closed消息不等于原生WebView已Dispose；后台会话槽释放和原生释放确认的接线仍须T011完成。

## 配置和验证边界

接收地址从ControlHost的`urls`首个监听地址派生，0.0.0.0/IPv6通配绑定转换为本机地址；沿用正式启动脚本的默认`https://localhost:18443`。可通过`ScreenSharing__ReceiverBaseUrl`显式选择**已监听**的loopback HTTP(S)纯地址；拒绝远程主机、用户信息、路径、query或fragment，且不自动新增监听或绕过证书验证。非通配LAN绑定需另行提供本机监听与明确接收地址。

浏览器采集必须是安全上下文；localhost只支持本机开发，普通局域网HTTP不满足要求。正常LAN可信证书部署步骤尚待T014实现，不能使用关闭安全策略/不安全来源flag作为正式方法。

当前自动化覆盖登录/CSRF、离线、争抢、SDP大小、窗口/代次/原Worker围栏、失败撤销、接收来源及停止待确认；模拟接收帧、脚本VM与本机独立WebView原型不算D4两正式Worker出画。真实双窗5分钟、十轮停止/切换、选择器取消和内容音频仍须执行，见[010任务](../specs/010-browser-screen-share/tasks.md)与[验证记录](../specs/010-browser-screen-share/verification.md)。
