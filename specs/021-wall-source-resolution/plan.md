# Implementation Plan: 输入分辨率与墙面裁剪修复

**Branch**: `main`（本地功能021） | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

## Summary

区分桌面、组播输入、预览和墙节点尺寸。用户改设置后，D4四路桌面和63–66主输入已实测4K，保留4K裁剪。纠正其它设备被工程统一4K默认覆盖的问题；同一IP的别名解析冲突在首包前拒绝。不修改Windows、输出节点或正在播放的来源。

## Technical Context

- Language/Version: C#/.NET10；Node.js内置net及node:test。
- Dependencies: 复用现有EF/ASP.NET装配，无新增依赖。
- Storage: 无DB迁移；纯配置和脱敏协议证据。
- Testing: xUnit包级/装配/回环、Node解析/TCP回环；现场只读证据单独记录。
- Platform/Type: Windows D4、既有.NET服务及独立只读CLI。
- Performance: 不新增长驻扫描或全流拉取；布局生成仍纯CPU。
- Constraints: 查询仅固定31路4830，12字节611A、1500ms/65536字节预算，串行并关闭连接；不发映射/配置/关键帧。
- Scope: 31路输入、50墙节点和2电视，不新增播放器。

## Constitution Check

研究前及设计后均通过：I禁止试发物理帧；II规范/计划/任务可追溯；III先失败回归，现场只读与CPU/回环分开；IV不改进程/DB边界；V采用启动时配置，不引入共享options热更新竞态。无复杂度豁免。

## Project Structure

```text
specs/021-wall-source-resolution/{spec,plan,research,data-model,quickstart,tasks,verification}.md
specs/021-wall-source-resolution/contracts/resolution.md
runtime-dotnet/src/ScpCv.Infrastructure/Devices/SiteInputCatalog.cs
runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallSourceResolutionTests.cs
runtime-dotnet/tests/ScpCv.ControlHost.Tests/VideoWallRegistrationTests.cs
tools/input-resolution/{protocol,probe,protocol.test}.cjs
```

## Execution

先保留切4K前后回读，再失败回归、最小修复、完整验证。原Planner/Codec及完整帧指纹保留，尺寸在唯一目录及受信配置入口修复。84未知拒发，目录和预览仍可用。更换信号后重新只读核验并显式改VideoWall:Inputs、重启生效；本轮不引入在线热更新。不变更公开HTTP/SSE。本轮只本机实现/提交，不自动部署、下发实体布局。
