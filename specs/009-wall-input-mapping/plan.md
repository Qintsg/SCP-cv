# Implementation Plan: 大屏输入组合与协议接入

**Branch**: `main` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)
**Input**: 三种输入的左右/全屏组合和已恢复协议。

## Summary

保留旧预设黄金包，新增受限均匀墙通用计划，独立协议编码/配置/回读解析。ControlHost注册真实配置，布局服务保存草稿，RuntimeStateService持久化已发送意图，前端使用服务端能力及结果。

## Technical Context

**Language/Version**: C#/.NET 10、TypeScript/Vue3。
**Primary Dependencies**: 现有EF Core、SQLite、ASP.NET Core、Naive UI；加密使用BCL，无新增运行依赖。
**Storage**: runtime_state增加已发送布局JSON、草稿版本及发送状态，只增加列。
**Testing**: xUnit黄金包/原代码向量、内存传输替身、回环TCP、HTTP、Node DOM和真实浏览器。
**Target Platform**: Windows x64主机，现有网页/薄封装控制端。
**Project Type**: 本机控制服务与共享前端。
**Performance Goals**: 50节点阶段内最多25并发，既有200ms清屏等待。
**Constraints**: 主动实现文件≤500行，既有自动生成EF快照由模型工具结构限制保留；传输成功非画面确认；密钥只能本地配置；仅原均匀10×5墙。
**Scale/Scope**: 12完整组合、6单侧组合，两个播放器，50墙面节点。

## Constitution Check

- I：清屏前预检；部分发送/取消标不确定，不自动重放，模拟显式标记。
- II：009记录替代005手动布局门禁的授权和验收条件，008草稿保留。
- III：包、下发、装配、持久化、HTTP及浏览器分别验证；现场未执行项不勾选。
- IV：前端只用REST；密钥不进入响应，ControlHost唯一写数据库。
- V：复用控制器和队列，独立codec/profile/plan/parser，不重造原软件管理和KVM。

研究前及设计后通过，无豁免。

## Project Structure

- specs/009-wall-input-mapping/：规范、设计、合同、任务、验证。
- runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/：VideoWallProtocolOptions、PacketCodec、LayoutPlanner、ReplyDecoder及既有控制器/服务。
- runtime-dotnet/src/ScpCv.Infrastructure/Playback/RuntimeStateService.VideoWall.cs：应用与状态独立partial。
- runtime-dotnet/src/ScpCv.Infrastructure/Persistence/Migrations/：只增列。
- runtime-dotnet/tests/ScpCv.{Infrastructure,Integration,ControlHost}.Tests/：协议及服务测试。
- frontend/src/features/runtime/VideoWallMappingPanel.vue、frontend/scripts/video-wall*.test.mjs：页面/DOM验证。
- docs/components/schemas/VideoWall*.yaml：公共合同。

**Structure Decision**: 领域复用布局，基础设施承担设备协议与配置；回读仅收集真实接收帧，不启用未证实查询；结果保持sent_unconfirmed。
