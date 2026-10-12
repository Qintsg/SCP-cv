# Implementation Plan: 目录式现场控制台、清晰共享与摄像机音量

**Branch**: `main` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: `specs/012-directory-camera-console/spec.md`

## Summary

沿用.NET控制主机、SQLite与双播放器，先修复1080P裁剪和共享编码，再将媒体/预设统一为目录优先的操作台。实体控制独立成摄像机与现场音频服务，只有有证据的绑定才能下发。网页入口切到80/443，保留内部18444/18443及原证书。

## Technical Context

**Language/Version**: C#/.NET10、TypeScript/Vue3。

**Primary Dependencies**: ASP.NET Core、EF Core SQLite、VLC、WebView2、Vue Router、Pinia、naive-ui、Vite、浏览器WebRTC；pnpm管理前端依赖。

**Storage**: 既有SQLite；受管media实体目录与原件；内置摄像机固定配置；现场音频与预设高级配置由ControlHost持久化。不引入MongoDB。

**Testing**: xUnit、Node单元/组件测试、真实浏览器布局和交互、D4只读及有界实体验证；OpenAPI3.0/Redocly、Spec Kit检查。

**Target Platform**: Windows x64交互桌面D4，现代Chromium网页控制端。

**Project Type**: 分进程播放器与共享网页控制台。

**Performance Goals**: 两路1080P保留捕获尺寸；清晰文字优先30fps共享，发送码率按实际分辨率建议并以统计验证；摄像机运动有停止超时；目录操作不占用播放器线程。

**Constraints**: 两块大屏；不启动PowerPoint；不覆盖存量媒体/数据库；不猜测设备通道；无读回协议只报告发送，不假称执行；文件模块默认500行以内。

**Scale/Scope**: 50墙面节点、两个播放器、六路摄像机、三种现场音量、两个声音来源；六个用户故事。

## Constitution Check

*Phase 0与Phase 1共用门禁，设计后复核。*

| 原则 | 检查与结论 |
| --- | --- |
| I 运行安全 | PASS：路径/内置项服务端保护；运动超时停止；音频绑定未知则禁用；部分失败明确报告，不自动重放。 |
| II 规范驱动 | PASS：先spec，再研究/方案/数据合同/任务；现有012指针不提交。 |
| III 分层验证 | PASS：每故事独立测试；浏览器和实体结果分开；不能完成的实体项目不勾完成。 |
| IV 合同边界 | PASS：只有ControlHost写DB；前端仅REST；设备控制独立服务，不让网页直接UDP。 |
| V 最小复杂度 | PASS：复用MediaFolder、SQLite、双窗及设计令牌；仅预设增加必要目录/高级字段。 |

## Project Structure

```text
specs/012-directory-camera-console/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/api.md
└── tasks.md
runtime-dotnet/src/
├── ScpCv.Contracts/Http/
├── ScpCv.Infrastructure/{Media,Scenarios,VideoWall,Devices}/
└── ScpCv.ControlHost/{Endpoints,Configuration}/
frontend/src/
├── features/{sources,display,screen-share,scenarios,devices}/
├── services/
└── stores/
runtime-dotnet/tests/
frontend/scripts/
docs/
```

**Structure Decision**: 保持现有边界；目录共用轻量路径/工具栏，打开到窗口以小卡片呈现。媒体库侧栏显示目录而非类型，类型改紧凑筛选。预设高级项折叠，摄像机与音量用可用状态明确的设备卡片；沿用现有字体、色彩和间距，不引入新视觉库。

## Complexity Tracking

无宪章例外。摄像机实际RTSP/PTZ与音频通道仍在只读研究中；以研究结论补齐绑定，对未确认项不提供虚假可用入口。

## 软件输出接线补充（2026-10-12）

实机低增益窄带测试确认D4输入63对应端点`{0.0.0.00000000}.{eb7e6029-ae6b-4d15-b79a-268521bd9abd}`，64对应`{0.0.0.00000000}.{d5ff475c-6792-4d13-bc2b-2e601d097be4}`。不按同名HDMI的顺序或系统默认值选择。

先为两个PlayerWorker的LibVLC资源及AudioWorker实现显式端点绑定。可信启动环境`SCP_CV_AUDIO_OUTPUT_DEVICE_ID`提供Windows Render端点ID；未设置保留既有行为，已设置但不存在/不可用则拒绝播放，不回退系统默认设备。创建/重播前预检并设定绑定；健康同源切换和暂停恢复仅核验、不重置正在使用的输出模块。播放观测检查实际输出；不调用OS主音量/系统默认设备写入。新增模块链接给两个使用LibVLC的工程，纯策略置于Contracts，避免重复实现。

这一步仅覆盖LibVLC。WebView2共享/网页和LibreOffice必须单独验证并接线，在它们闭环前不把整个SCP-cv声音来源称为已完成，也不启用未核实的实体选源/话筒/云台。旧播放器列表的237监控地址是不可达历史候选，不能作为默认固定监控。

规划表与用户最新更正确认固定监控输入76，默认RTSP配置同步至76；音频处理器5.9接中控5.10 COM4，话筒与分布式输入接线见[绑定证据](verification/device-bindings.md)。表格只证明接线，不能据此选择Dante协议或把摄像机编码输入当云台地址。
