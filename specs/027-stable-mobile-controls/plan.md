# Implementation Plan: 稳定播放控制与手机工作台

**Branch**: `main` | **Date**: 2026-10-27 | **Spec**: [spec.md](spec.md)

## Summary

合并同次滑块拖动的重复收尾，将高频状态置于固定位置，统一文稿两按钮导航，增加手机控制策略与玻璃悬浮栏，并由服务端发布共享视频策略。

## Technical Context

**Language/Version**: TypeScript、Vue 3、C#/.NET 10。
**Primary Dependencies**: Ant Design Vue、Pinia、Capacitor、WebRTC、ASP.NET Core。
**Storage**: 既有运行数据与临时共享内存状态。
**Testing**: 实际Vue代码、文件提交、真实本地Hardware服务、浏览器WebRTC和只读页面检查。
**Target Platform**: 桌面网页、Windows客户端、Android与手机网页。
**Project Type**: 播放控制服务及共享控制台。
**Constraints**: 沿用认证、来源代次、媒体与设备合同；不使用computer use或增加自动显示恢复。
**Scope**: 滑块、PPT、频繁状态、预览、移动导航与共享视频策略。

## Constitution Check

保留状态真实性、来源隔离和资源关闭。新增共享字段同步OpenAPI；手机停用预览同时释放订阅；行为变更使用对应验证。复用现有组件及设计令牌。

## Project Structure

- `frontend/src/composables/`：滑块和手机策略。
- `frontend/src/features/display/`与`pptFocus/`：文稿导航和布局。
- `frontend/src/layouts/`与`styles/`：悬浮栏和安全区。
- `frontend/src/features/preview/`及预览调用页：手机停用预览。
- `frontend/src/features/screen-share/`：发送策略和共享状态。
- `runtime-dotnet/src/ScpCv.ControlHost/ScreenSharing/`：视频策略与快照。
- `docs/`与相关测试目录：合同、说明和验证。

## Design

滑块按拖动会话合并收尾；普通会话更新继续保留来源围栏。移除频繁插拔的页级pending提示，将状态加入固定高度的页头。两按钮组件与键盘共用动作选择，静态与动态文稿分别映射到现有合同。

手机策略统一窄视口及触摸设备短边判定；父容器去掉无预览时的高度，预览组件同时停止订阅。底栏使用半透明主题色、模糊和边缘高光，并增加内容底部安全留白。

共享默认视频上限16/24/48 Mbps、30fps，服务配置决定实际策略。前端只在分辨率档位变化时更新发送参数，质量诊断读取已接受参数；接收端继续直接解码。

## 部署与客户端制品

推送GitHub和GitLab；D4从GitLab快进。通过既有发布入口生成独立运行包，保留DataRoot、当前显示目标和音频端点；认证停止旧运行组、冷备数据库，替换任务引用和网页后启动，核对进程、媒体与显示配置。

Windows沿用Electron完整目录包，校验ASAR与原生预览资产后归档便携ZIP。Android使用Java21、SDK36和项目Gradle，按arm64-v8a生成APK并使用本机保留的签名密钥签名；校验签名、包内网页和ABI配置。APK为D4两个既有地址带入当前公开根证书，服务允许Capacitor的https://localhost来源；保持TLS和Cookie校验。两份制品放入本机桌面。
