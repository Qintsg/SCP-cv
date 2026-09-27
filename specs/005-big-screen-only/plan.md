# Implementation Plan: 大屏专用控制与媒体整理

**Branch**: `main` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-big-screen-only/spec.md`

## Summary

将播放拓扑收缩为窗口 1、2 与两块大屏输出；保留电视电源、背景音频和原有两个墙面预设。新增原子布局意图的保存/预览合同，未抓包的手动映射必须在实体下发前拒绝。PPT/PPTX 上传保留原件、通过独立 STA Office 宿主转换为逐页图片，默认播放图片，原生放映需服务端持久的实验开关。媒体目录改为页面文件夹映射的实际层级，移动/重命名由受控文件操作与数据库更新共同完成，旧媒体不批量删除或迁移。

## Technical Context

**Language/Version**: C# / .NET 10；TypeScript / Vue 3；PowerShell 5.1（现场脚本）

**Primary Dependencies**: ASP.NET Core、EF Core/SQLite、WPF、Named Pipe、Office COM、Vue Router、Pinia、pnpm。不上 MongoDB；如后续证明必需，只能以 Docker 部署。

**Storage**: 现有 SQLite 业务库 + DataRoot/media 原件 + 受版本约束的逐页图片制品；保留旧日期/散列路径的历史记录。

**Testing**: xUnit 非 Physical、合同测试、前端脚本/类型检查/构建、本机浏览器冒烟；D4 实体测试按用户要求暂停。

**Target Platform**: D4 Windows x64 交互桌面及共享 Web/Electron/Capacitor 控制端；本机进行无物理副作用验证。

**Project Type**: 多进程 Windows 播放主机 + REST/SSE 控制客户端。

**Performance Goals**: 两播放器就绪不劣于原有启动预算；媒体移动不复制同卷大文件；文稿转换异步并报告进度/失败，不阻塞常规媒体操作。

**Constraints**: 未知墙面帧绝不猜测下发；两块大屏输出必须在 Hardware 配置中显式绑定并校验；不删除用户原件/数据库/日志；现场服务保持关闭。

**Scale/Scope**: 两个播放输出、50 个墙面节点、三个电源设备、用户命名媒体目录、单 Office STA 宿主。

## Constitution Check

*GATE: Phase 0 前及 Phase 1 后复核。*

- 宪章 v3.0.0 已将四播放器/四窗约束改为两块大屏播放器/两窗，符合本需求；其余运行安全与合同边界不变。
- 未知帧布局保存与实体下发分离，只有现有两个预设可调用已验证序列；失败不得修改活动布局，满足原则 I。
- 上传转换仍使用独立 PowerPointHost 的 STA/COM，不在 ControlHost 或 PlayerWorker 内创建 COM 对象，满足原则 IV。
- 文件与数据库操作采用 staging、路径校验、补偿与可诊断状态；不自动删除历史数据，满足原则 V。
- 前端改动需本机浏览器验证；D4 物理验证明确暂缓，不能把 mock 或构建当作实体通过，满足原则 III。

## Project Structure

### Documentation (this feature)

```text
specs/005-big-screen-only/
├── plan.md              # This file ($speckit-plan command output)
├── research.md          # Phase 0 output ($speckit-plan command)
├── data-model.md        # Phase 1 output ($speckit-plan command)
├── quickstart.md        # Phase 1 output ($speckit-plan command)
├── contracts/           # Phase 1 output ($speckit-plan command)
└── tasks.md             # Phase 2 output ($speckit-tasks command - NOT created by $speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
runtime-dotnet/
├── src/ScpCv.Domain/                 # 有效窗口、布局和媒体状态语义
├── src/ScpCv.Contracts/              # HTTP 与进程合同
├── src/ScpCv.Infrastructure/         # SQLite、媒体目录、场景和映射服务
├── src/ScpCv.ControlHost/            # REST/SSE、运行组就绪与设置
├── src/ScpCv.Supervisor/             # 两个播放器启动和显示器落位
├── src/ScpCv.PlayerWorker/           # 逐页图片播放
├── src/ScpCv.PowerPointHost/         # 上传转换与实验性放映
└── tests/                             # 单元、合同、集成与 Windows 测试
frontend/
├── src/features/                    # 大屏映射、媒体、预案与设置页面
├── src/layouts/                     # 两窗口导航
├── src/stores/                       # 会话与设置状态
├── src/services/                     # REST/SSE 类型
└── scripts/                           # 本地前端测试
docs/
├── openapi.yaml                      # 对外合同根文件
├── components/、paths/               # 分拆的 OpenAPI 合同
└── 使用文档.md、维护文档.md、CHANGELOG.md
```

**Structure Decision**: 沿用现有分层和共享客户端，不新增数据库服务或第二套播放器；数据/文件操作由 ControlHost 统一写入。手动墙面意图与已应用预设分开持久化，既能预览，也不把未掌握的协议假装成已执行。

## Complexity Tracking

无例外：宪章 v3.0.0 已按用户新的两窗范围修订。
