# Implementation Plan: 屏幕共享连接与折叠入口

**Branch**: `main` | **Date**: 2026-11-05 | **Spec**: [spec.md](spec.md)

## Summary

保留应用级共享所有者和原生 details 语义，给 summary 增加独立箭头、整栏边框、背景和焦点；修复窄桌面窗口入口被尺寸条件隐藏的问题。通过 D4 真实命令记录、HTTP 状态和用户重试信息定位无法出画的实际环节，再对相关代码补充真实回归。

## Technical Context

- Vue 3、TypeScript、Pinia、Ant Design Vue、Vite；沿用当前依赖。
- ControlHost、WebView2 与 WebRTC 沿用现有身份及接收协议。
- 由 ScreenSharing 配置下发标准 RTCIceServer 列表；发布端从预约快照读取，接收端从私有 offer 读取，均由浏览器正常收集候选。使用成熟 Pion STUN-only 示例验证真实组网候选。
- 不新增数据库字段或外部服务；通过 SQLite 只读接口检查历史失败命令。
- 验证使用真实无头浏览器、真实媒体及实际 API，禁止输入事件模拟和 computer use。
- 目标平台为网页；窗口 1/2 共享，D4 管理连接固定为 10.88.50.194。

## Constitution Check

- 运行安全：远端先只读诊断，不变更显示模式、墙面或设备电源。
- 可追溯：本目录记录规范、任务和实际验证结果。
- 分层验证：样式须实际浏览器检查，连接缺陷须有相应真实复现。
- 身份边界：保留应用级采集所有者、账号、主机及源代次。
- 复杂度：复用现有 details、共享组件及测试入口。

## Project Structure

- `frontend/src/features/display/DisplayControlView.vue`：共享折叠入口。
- `frontend/src/features/screen-share/`：共享所有者、发布者及连接。
- `frontend/scripts/`：本地真实浏览器回归。
- `.validation/share-032/`：忽略的远端诊断和验证证据。
- `specs/032-screen-sharing-repair/verification.md`：实际结论与未验证范围。
