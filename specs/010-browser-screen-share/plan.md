# Implementation Plan: 网页屏幕共享

**Branch**: `main` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

**Input**: 网页直接共享到窗口1/2，无额外推流软件，用户要求自行定方案并完成测试。

## Summary

浏览器getDisplayMedia与专用WebView2接收者直接WebRTC连接；ControlHost仅负责认证、有界信令和会话寿命，复用持久OPEN/CLOSE与Worker代次。与普通web源严格分开，不能导航完成即playing或进入warm网页缓存。先验证本机真实采集与WebView2后帧，再进入完整实现；真实局域网ICE不通时依据证据修正方案，不用模拟成功代替。

## Technical Context

**Language/Version**: TypeScript6/Vue3、C#/.NET10、Windows WPF；Python仅QA且使用uv。

**Primary Dependencies**: 原生WebRTC、现有WebView2、Pinia/Naive UI、ControlHost认证与Named Pipe；无需商业库、额外推流进程或MongoDB。

**Storage**: 分享/SDP/接收授权只在内存且有期限；OPEN/CLOSE保持既有命令持久审计，不记录屏幕媒体或cap，不造永久媒体库源。Worker通过已认证lease注入短期接收授权，重启全部失效。

**Testing**: xUnit安全/生命周期/装配，Node DOM/纯逻辑，Playwright真实采集/传输，原生WebView2与D4两窗后帧及停止隔离。

**Target Platform**: 同局域网桌面Chrome/Edge、可信HTTPS；D4既有两Worker。localhost只为开发/QA安全上下文，不是LAN HTTP证明。

**Project Type**: 网页控制台与Windows双输出运行时。

**Performance Goals**: 20秒首画面、1080p上限/30fps意图；实际参数按证据报告，两独立共享5分钟及十轮生命周期。

**Constraints**: 仅1/2、一窗口一会话；信令64KiB；ICE单侧12秒预算、端到端20秒截止；预约20秒、活跃发布者心跳30秒期限，从分配后算。无默认外部STUN/TURN、自动重采集/重播。SDP网络信息不写日志。

**Scale/Scope**: 最多两活动会话；账号所有权；256位随机receiver cap仅loopback且绑定share/window/generation及原Worker instance/owner/group，不公开DTO/日志/DB。普通用户不能发送接收证据。代理使RemoteIP可变为loopback，所以cap及来源验证不可省略。

## Constitution Check

- I：取消/超时停本地采集，条件CLOSE检查源代次；未确认不报关闭，不重放。
- II：010规范/研究/模型/合同/任务先于生产代码。
- III：模拟HTTP、真实浏览器、原生两窗分别验，实际视频帧才授权playing。
- IV：浏览器只走HTTP信令，媒体在WebRTC端点之间；不接触DB/管道，Worker拥有原生资源。
- V：复用认证/队列，仅加必要共享模块。文件不足500行，旧大文件优先拆分。

设计前后门禁通过，无宪章例外；网络及遮挡后帧仍须实测，不宣称未知结果。

## Project Structure

```text
specs/010-browser-screen-share/
  spec.md plan.md research.md data-model.md quickstart.md tasks.md verification.md
  checklists/requirements.md contracts/screen-sharing.md
frontend/src/features/screen-share/
  rtcPeer.ts useScreenShare.ts ScreenSharePanel.vue
frontend/src/services/screenSharing.ts
runtime-dotnet/src/ScpCv.ControlHost/ScreenSharing/
  ScreenShareRegistry.cs ScreenShareService.cs ScreenShareCleanupService.cs ReceiverPage.cs
runtime-dotnet/src/ScpCv.ControlHost/Endpoints/ScreenShareEndpoints.cs
runtime-dotnet/src/ScpCv.Infrastructure/Playback/RuntimeStateService.ScreenShare.cs
runtime-dotnet/src/ScpCv.PlayerWorker/Playback/PlayerRuntimeHost.ScreenShare.cs
runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScreenShare*Tests.cs
runtime-dotnet/tests/ScpCv.Windows.Tests/ScreenShare*Tests.cs
frontend/scripts/screen-sharing.test.mjs
docs/screen-sharing.md
.validation/qa-screen-share-010/（本轮独立忽略QA证据）
```

**Structure Decision**: 分享是会话而非上传文件；Registry、传输与原生资源独立可测试，端点薄层。

## Design Decisions

1. 点击同步调用采集；取得轨道后create预约但不OPEN，offer与ICE完成再请求OPEN。拒绝权限零播放副作用。每阶段遵守端到端20秒期限，不把两侧12秒相加放宽指标。
2. receiver页面只来自loopback ControlHost和高熵cap；禁止外链导航/新窗/采集权限，来源与消息schema严核。不能只凭loopback绕过授权，localhost代理不会代替cap。
3. 动态OPEN source_type=screen_share、source_id=0（actual null）、share_id/source_generation；事务内核目标身份/在线。持久args不含cap，由RuntimeMessageDispatcher对已认证且匹配Worker的lease注入接收URI。不造永久媒体源，普通控制保持拒绝不支持动作。
4. Worker首视频帧后完成OPEN；候选遮挡准备与激活后后帧分开观测。关闭/换源/进程失败停止peer、清video并Dispose，不进web缓存；旧WebMessage不更新新代次。
5. 前端正source_id依赖的标题、关闭、播出、能力、sessions刷新分支显式支持分享身份；共享不保存到媒体库/场景。主机/登录/页面寿命结束停本地轨道，不污染新页面。
6. 内容音轨按实际支持呈现；专用音量/静音桥实测后接线，不沿用VLC假成功。窗口级音量命令的现有generation0语义不当作既有bug；若定义share专属操作则须身份围栏。
7. HTTPS显式证书/密钥配置，不降低Cookie/CSRF、不用不安全浏览器flag。现场访问证书信任与开发localhost分别记录。
8. cleanup周期1秒，过期先撤销授权，再原子条件CLOSE；终止受理和实际确认分开。普通换源终止旧分享但不关闭新源，不自动恢复分享。
9. receiver_uri中的cap使用fragment而非HTTP查询串；WPF首次导航通过NavigateWithWebResourceRequest设置Authorization Bearer，静态接收脚本在内存读取fragment后立即从历史地址移除，后续fetch仍用该header。HTML无私有插值，以嵌入脚本/样式哈希限定CSP；发布者cookie不能授予接收权限。

## T008网页操作区设计（2026-10-11）

- 对象是现场操作员在显示控制页向当前逻辑窗口分享临时画面。复用既有字体及令牌映射：品牌蓝`#0f6cbd`、白底`#ffffff`、墨灰`#242424`、浅灰`#f5f5f5`、边线`#d1d1d1`及成功绿`#107c10`；业务CSS只引用令牌并继承深色主题。Segoe UI标题16px/600，操作与正文14px，帧数使用既有等宽字体后备，不下载字体。
- 采用紧凑横向工具区，窄屏自然换行；目标窗口标识是唯一签名元素，表示真实窗口1/2而非装饰编号。不加新hero、渐变或自播预览；避免现场屏幕共享的递归镜像与无意义卡片。
- 布局为“屏幕共享/目标窗口 → 状态/真实接收帧 → 内容音频选项、共享和停止”。主操作同步打开原生选择器；等待许可不创建会话，不把connecting绿色化。采集/连接/后帧/停止待确认有独立文字和可访问live区域。
- 自审后去除设计草案中的三步常驻进度条和预览缩略图：它们重复状态且扩大采集暴露面。直接以目标小标识和单一状态文案编码实际任务。
- `ScreenSharePublisher.ts`拥有单个授权尝试、轨道、peer和定时器；Vue composable只提供响应式呈现及页面/账号寿命。每个实例独立，两个标签页可共享两个目标；停止优先断开本地采集，服务端未确认时禁止宣称已关闭或重新争抢。API复用当前cookie/CSRF/主机代次封装，不重造认证。

## T009临时播放投影与逻辑窗口（2026-10-11）

- `ScreenSharePlaybackProjection`至多保留两个窗口的非秘密身份/实际帧/音轨；OPEN绑定原Worker与代次，报告经过持久归属检查和提交后才补内存观测。投影同时核持久会话的实际适配器、请求/观测代次；单调会话版本拒绝异步倒序回调。重启不恢复、不持久媒体URI或cap、不增加MediaSource。
- HTTP/SSE共享同一DTO的`share_id/actual_share_id/actual_frames/actual_audio`；普通源固定空身份、零帧及未知音轨。实际playing还须同身份/代次、在线及正帧；旧画面停机待确认不绿色化。
- 控制页使用现有紧凑样式和主题令牌，仅显示实际状态摘要，不增加共享缩略图；停止仅撤销原分享ID，禁止普通无条件CLOSE及自动重开。墙面single/double/custom都保留两逻辑窗口，不把窗口2当物理右半屏。窄屏使用可见原生单选组和44px标签；Naive页签接`tabProps`的键盘、面板关系和可见焦点，未改第三方源码。
- 内容音量/静音桥、场景保存拒绝与后台终态确认仍分别由T013/T011补齐；本阶段不把前端禁用或公开状态当实际资源清理证据。

## T010共享寿命与迟到失败（2026-10-11）

- `ClientConnectionCoordinator`的代次是非响应式身份围栏；共享面板通过显式同步订阅结束原寿命，通知发生在异步清会话之前。取消订阅幂等，页面结束/账号改变/卸载共用一次dispose，不增加全局媒体store。
- 本地清理按原资源所有权判断，不能因账号/主机已失效而跳过；向远端停止仍须当前上下文。查询成功及失败分支都核同尝试，旧catch不能覆盖已确认终态。停止失败不自动重复副作用，原面板结束后不能重用旧原生授权。
- 公开HTTP/SSE与IPC合同不因本阶段改变。后端真实SQLite并发/提交后失败回归仅核排队、授权撤销与新意图保持；下一步T011还须独立的原Worker/分享/代次实际释放或原进程退出证据，禁止以offline、换Worker、NoLongerDesired或CLOSE入队推断原生死亡。

## T011后台回收与原生证据（2026-10-11）

- `ScreenShareCleanupService`在Hardware/Simulation均显式装配，独立一秒周期，不依赖发布者GET。过期、原Worker/组失效或普通换源先撤销cap；同窗口Service门禁内以原分享/OPEN代次执行原子条件CLOSE，不重发物理CLOSE或关闭后来内容。
- Player仅在原生控制器Dispose及待显示Surface移除实际完成后记录`ScreenShareReleaseEvidenceDto`。原Worker的有界32条回执随当前状态重复报告，空闲/图片也采样，避免一次结果报告丢失后永久占用槽。原资源释放和当前源状态是两条不同证据：已提交且匹配原Worker的旧源报告可确认原分享释放，不能推进新源公共状态；错误Worker、身份/代次或畸形数组整批拒绝。
- `ScreenShareProcessTracker`至多观察两个原Player，先核数据库归属与OS验证登记的PID/start/session/role/instance，再持有原进程退出观察。离线、换组、新Worker、取消或取证失败都不是死亡证明。宿主停止取消并等待自有观察收尾；Simulation缺实际OS观察器不造证明。
- 已绑定但OPEN事务回滚且无审计时，在同Service门禁内可以释放无原生接收可能的预约。服务重启后不恢复cap/SDP，只核当前动态OPEN审计并条件清除孤立意图；不从历史pending、actual或null源恢复共享。
- 预案捕获在同一写事务内拒绝共享意图/实际，失败不覆盖已有预案。网页本地捕获草稿也核请求ID/实际ID/适配器并提示；手动固定源、保持和清空仍可用，set零/负/空ID不持久化。

## T012本机双接收资源门禁（2026-10-11）

- 两独立Chromium进程真实采集自有DOM运动标签页；正式Panel/Publisher/RTC与正式receiver保留，只有账号/HTTP信令为内存夹具，cap和SDP不落盘。
- 同一自有WPF应用内两个正式PlayerRuntimeHost持续复用十轮；双共享、交替停止、再共享、旧条件CLOSE/旧消息、图片替换与非目标保持分别核状态和像素。这个层级不代替两个正式Worker或D4。
- 释放分别核轨道/peer、原生Dispose回执及0控制器、旧Core不可使用、原浏览器OS对象退出。十轮总时长超过五分钟不等于双共享连续五分钟，SC-003仍留T016；实测见[专项记录](verification/t012-native-pair.md)。
