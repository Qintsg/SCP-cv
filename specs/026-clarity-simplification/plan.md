# Implementation Plan: 控制台与实现简化

**Branch**: `main` | **Date**: 2026-10-27 | **Spec**: [spec.md](spec.md)

## Summary

依据用户操作和调用链检查前端、后端与面向用户的文档；简化重复提示，修复确实影响操作的校验与故障反馈，并保持现场控制和数据保护合同。

## Technical Context

**Language/Version**: C#/.NET 10、TypeScript、Vue 3、中文 Markdown。

**Primary Dependencies**: ASP.NET Core、Vue、Pinia、Vue Router、Ant Design Vue。

**Storage**: 既有 SQLite 与文件制品；保留现有数据和格式。

**Testing**: 实际本地服务、真实文件、前端构建、相关合同验证；界面通过获准的只读无头浏览器检查。

**Target Platform**: Windows 播放主机、网页及封装控制端。

**Project Type**: 播放控制服务及共享控制台。

**Constraints**: 无 computer use，不修改 D4 部署或发送实体指令；保留认证、数据与设备边界。

**Scale/Scope**: 受维护的前端、后端、用户文档；排除第三方代码、构建产物及历史取证文件。

## Constitution Check

- 安全与资源：维持设备动作及播放器生命周期的原有语义。
- 规范与验证：记录检查范围、修订及对应验证；不将只读采样视为实体测试。
- 合同：保留 REST、数据库和进程合同；行为变更核对调用者。
- 最小复杂度：采用现有组件和直接实现；不增加长期守护或迁移设施。

## Project Structure

`frontend/src/` 包含操作文案、认证与墙面编辑；`runtime-dotnet/src/` 包含服务及制品检查；`docs/` 包含面向用户的操作和维护说明；相应测试位于各项目测试目录。

## Design

- 逐项判断文本是否传达操作条件、事实或必要状态；重复信息在适当位置说明一次。
- 合法布局的规范化与服务端一致；无法恢复的布局保留并向用户说明错误。
- 认证探测故障和未登录分别呈现，保留明确的重试入口和请求归属。
- 检查页图的读取与完整制品复用之间的需求，保留文件路径、来源和所有页图的完整性合同。
- 已退役或无消费者的代码只有在确认其调用和合同后才清理。

## Constitution Recheck

设计保留需要的设备、认证、文件与并发保护。具体行为变更按相关测试、实际服务及页面进行验证。
