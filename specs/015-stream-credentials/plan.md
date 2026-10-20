# Implementation Plan: 带凭据的直播源

**Branch**: `main` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)

## Summary

放开直播专用URL验证器的受支持认证协议，保留完整URI给现有队列/Player/VLC；自动名称使用既有日志脱敏规则。前端新增纯地址展示脱敏与密码型地址输入，普通播放摘要不直接显示原URI。不增加认证字段、数据库或播放器选项。

## Technical Context

**Language/Version**: C#/.NET10、TypeScript/Vue3。
**Primary Dependencies**: ASP.NET Core、SQLite/EF、LibVLC、Ant Design Vue，保持版本。
**Storage**: 原MediaSource.Uri，授权媒体DTO仍含原值；不迁移。
**Testing**: xUnit服务/认证HTTP/回环RTSP；Node真实Vue/Ant组件及纯逻辑；构建/只读浏览器。
**Target Platform**: Windows x64播放器、共享网页控制端。
**Project Type**: 现有分进程播控。
**Performance Goals**: 不新增网络探测或热路径请求。
**Constraints**: 不连示例设备，不使用Computer Use；默认500行以内；不记录真实凭据。
**Scale/Scope**: 两窗，现有直播类型，两个用户故事。

## Constitution Check

I安全：仅协议允许名单放行，展示与播放原值分离；II规范：015独立追溯；III验证：红绿与实际认证分层；IV边界：前端仅REST、Host写库；V最小复杂度：无凭据库/新配置或第三方依赖。设计前后均PASS，无例外。

## Project Structure

- 后端：`MediaSourceService.Streams.cs`、`Diagnostics/LogRedaction.cs`。
- 前端：`features/sources/StreamAddressInput.vue`、`services/mediaAddress.ts`、新增/编辑抽屉和`PlaybackControl.vue`。
- 测试：`StreamSourceServiceTests.cs`、直播凭据HTTP测试、Windows回环RTSP夹具、前端`stream-credentials.test.mjs`。
- 文档：本目录、OpenAPI对应说明、使用/维护/CHANGELOG。

## Complexity Tracking

无额外复杂度。完整URI只在授权存储/编辑/播放合同中保留；默认隐藏不是加密，也不承诺任意查询字符串均无秘密。
