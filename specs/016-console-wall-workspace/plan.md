# Implementation Plan: 4K与目录化控制工作区

**Branch**: `main` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)

## Summary

复用真实Ant控件、既有REST/SSE与修订围栏。先建立网格/代码纯模型，再接编辑器、现有发送链、预案与列表。4K仅更改63/64源尺寸；原生输出跟随实际屏幕。用户本机可选FFmpeg桥提供无声JPEG预览，不改D4代理配置。

## Technical Context

**Language/Version**: TypeScript/Vue3、C#/.NET10、Node22+。
**Primary Dependencies**: 现有Ant Design Vue、Pinia、Vite、EF Core；本机预览可选FFmpeg。
**Storage**: SQLite仍为Host唯一写入，预案增加YAML文件表示；短码不建查找表。
**Testing**: Node test/HappyDOM、xUnit、只读无头Chromium截图（用户本轮明确允许）。
**Target Platform**: 网页/Electron共享控制台、Windows播放主机；3/4仅UI预留。
**Project Type**: 前后端现场控制应用。
**Performance Goals**: 50格本地交互即时；小预览低清低帧率，共享连接、限制并发并及时释放。
**Constraints**: 不部署/推送/连D4/实体控制，不用computer use；不禁用浏览器安全、不把凭据放URL日志。
**Scale/Scope**: 10×5墙、4前端窗口入口、2原生输出、1–50矩形、媒体/预案/音乐/设置。

## Constitution Check

设计前后均通过：未知裁切拒发；预留3/4不增加Worker；保存/应用保留修订、范围围栏和音量重发；本机桥明确可选能力和失败；文件安全/原子写与测试并行于UI实现；每个模块≤500行；不能将本机测试冒称现场验收。

## Project Structure

```text
frontend/src/features/runtime/wall/  # 纯模型、短码、编辑器、来源选择
frontend/src/features/preview/       # 用户本机预览组件与会话
frontend/src/features/{sources,scenarios,backgroundAudio,settings,dashboard}/
frontend/scripts/                    # 纯模型和真实组件测试
tools/client-preview/                # 可选Node/FFmpeg本机桥
runtime-dotnet/src/ScpCv.Domain/{Model,Rules}/
runtime-dotnet/src/ScpCv.Infrastructure/{VideoWall,Scenarios}/
runtime-dotnet/src/ScpCv.Contracts/Http/
runtime-dotnet/tests/
specs/016-console-wall-workspace/
```

**Structure Decision**: 扩展现有业务边界，不初始化新网站或替换现有应用。提供Logo原件，复用Fluent库图标与Ant令牌：蓝#1677ff、画布#f3f6fb、表面#fff、文本#182338、边框#e2e8f0、侧栏#111c32；正文Microsoft YaHei/Segoe UI、代码Consolas。特色集中在32:9实用网格，无营销装饰。

## Complexity Tracking

无宪章例外。本机桥是浏览器不能直连RTSP的必要可选适配，不作为D4运行依赖。
