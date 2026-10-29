# Implementation Plan: 窗口隐藏与显示

**Branch**: `main` | **Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

## Summary

Web维护菜单提交绝对窗口置顶意图，经现有队列由PlayerWorker执行；读取真实原生置顶状态，通过有序观测与SSE更新菜单。

## Technical Context

**Language/Version**: C#/.NET10、WPF、TypeScript/Vue3。
**Primary Dependencies**: 现有EF Core/SQLite、Named Pipe、Ant Design Vue及Win32窗口API。
**Storage**: PlaybackSession增加nullable WindowForeground与WindowObservationSequence；新所有者清空观测。
**Testing**: 真实SQLite/HTTP与Windows窗口，Web只读渲染，规范及OpenAPI校验。
**Target Platform**: Web控制台与Windows x64播放主机。
**Performance Goals**: 复用每秒实际状态采样；不增加预览拉流或播放资源。
**Constraints**: 不打包客户端，不修改显示模式；部署更新D4网页与主机运行时，保留当前数据及播放配置。
**Scale/Scope**: 两个输出窗口、一个动态菜单项。

## Constitution Check

- 窗口控制仅在PlayerWorker自己的UI线程执行；Web只访问HTTP。
- 真实状态和入队回执分开，旧所有者与序列不得倒退新状态。
- 本机真实窗口和HTTP验证覆盖副作用；没有使用模拟成功替代原生动作。
- 规范先于实现；新增逻辑分文件，复用既有队列及错误处理。

## Project Structure

- PlayerWorker：PlayerWindow窗口顺序、RuntimeHost维护命令与快照。
- Infrastructure：实体及迁移、队列保留维护命令、RuntimeStateService批量入口。
- ControlHost：HTTP端点、RuntimeProjectionPublisher窗口观测。
- frontend：API类型、维护操作组合函数、EmergencyMenu动态选项。
- docs：OpenAPI、使用及维护说明、变更记录与副作用清单。

## Design Review

窗口快照携带独立递增序列，在instance/owner校验后、媒体代次校验前更新。媒体投影与窗口变化分别发布，离线时对外返回未知。隐藏不销毁视觉树，位置调整读取当前维护模式。

## Deployment and Startup

按完整清单核验新运行包与网页，协作停止旧组、冷备数据库后切换独立发布目录及任务，恢复原来源。桌面快捷方式调用start-desktop.ps1，使用正常管理员提权及进程互斥，后台调用现有start-deployed.ps1完成就绪检查；控制进程输出明确使用UTF-8。成功自动结束，失败提供原因及诊断日志。
