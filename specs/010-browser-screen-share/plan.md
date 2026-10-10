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
