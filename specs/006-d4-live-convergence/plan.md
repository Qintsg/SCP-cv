# Implementation Plan: D4 大屏实机收敛与优化

**Branch**: `main` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md)

**Input**: 在已完成的 005 两窗重构上，按用户新授权更新 D4 并收敛实体画面、Office、媒体目录、稳定性和已知缺口。

## Summary

先确认 D4 停机、提交与数据库/WAL 组合快照，再从 secondary 更新并编译。分阶段启动 Hardware ControlHost 和两个交互桌面播放器，给窗口 1/2 的媒体、墙面预设、PPT 默认页图/实验放映与目录移动建立实体证据。诊断并修复已记录的音量、直播源、资源增长和文件清理缺陷，执行两窗基准与 60 分钟混合门禁；每一类修复都补自动化、现场复测和文档。结束后恢复现场原先停机状态。

## Technical Context

**Language/Version**: C# / .NET 10；TypeScript / Vue 3；PowerShell 5.1（现场脚本）

**Primary Dependencies**: ASP.NET Core、EF Core/SQLite、WPF、LibVLCSharp、WebView2、Windows.Data.Pdf、Office COM、MediaMTX、Vue/Pinia、pnpm、Git

**Storage**: D4 `D:\SCP-cv\.validation\t129-workstation` 的 SQLite + WAL/SHM、`media/` 原件和 `cache/artifacts/` 页图；本机仓库 `.validation/` 保存临时证据

**Testing**: xUnit 非 Physical/Physical、OpenAPI/Spec Kit 校验、真实 Chromium 桌面/手机视口、D4 交互桌面截图、HTTP/IPC 结果、独立流解码、进程资源采样、60 分钟混合运行

**Target Platform**: D4 Windows 10 Pro 1909 的已批准兼容性例外；受支持产品基线仍为 Windows 10 2004+ x64；控制端 Web/Electron/Capacitor

**Project Type**: 多进程 Windows 播放主机与跨平台共享控制台

**Performance Goals**: 两窗 1000 普通写命令及 100 健康热切换记录 p95/最大/失败数；60 分钟混合播出无未报告黑屏、错屏、冻结或持续资源增长

**Constraints**: 不触碰小电视电源；未知墙面控制帧零下发；仅关闭归属可证明的 Office/窗口进程；数据操作前核对路径；测试完成恢复 D4 停机状态；实体画面不能只以 API 状态替代

**Scale/Scope**: 两个播放输出、两个已知墙面预设、50 墙节点、单 Office STA、多类媒体与中文层级目录

## Constitution Check

*Phase 0 前与设计后复核，当前无需要豁免的原则。*

- **I 运行安全**：每次物理动作前核对目标与进程所有权；失败不写“成功”状态；未掌握的墙面帧保持禁止。用户批准的 D4 文件/数据库范围不等于允许误杀用户 Office 或小电视电源。
- **II 规范可追溯**：006 独立承接 005 的未验收实体门禁；已知问题对应 003/T133、T137、T134/135、T115/116/129 和当前规范 FR/SC。
- **III 分层验证**：单元/合同测试不能替代 D4 画面、音频、Office 与长稳；前端变更须有真实浏览器状态/布局/控制台证据。
- **IV 边界清晰**：ControlHost 仍是数据库唯一写入者；Worker 经带身份与代次的管道协作；Office COM 只在 PowerPointHost STA。现场手工 SQL 仅限停机备份与诊断，不作为应用写入路径。
- **V 最小复杂度**：优先修既有服务与测试脚本；不引入 MongoDB、第二运行时或通用回滚框架；源码按 500 行约束拆分，忽略目录素材不提交。

## Project Structure

### Documentation (this feature)

```text
specs/006-d4-live-convergence/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
├── checklists/requirements.md
├── tasks.md
└── verification.md
```

### Source Code (repository root)

```text
runtime-dotnet/
├── src/ScpCv.Domain/                 # 两窗、媒体源与作业规则
├── src/ScpCv.Infrastructure/         # SQLite、文件、预案音量、墙面与流服务
├── src/ScpCv.ControlHost/            # REST/SSE、进程协作、转换作业
├── src/ScpCv.Supervisor/             # 仅 player1/2 的生命周期
├── src/ScpCv.PlayerWorker/           # WPF/VLC/WebView2/PDF/页图的实体画面
├── src/ScpCv.PowerPointHost/         # 独立 STA 转换与实验放映
├── scripts/                           # 两窗启动、停机、基准与现场诊断
└── tests/                             # 自动化、Physical 与故障注入
frontend/
├── src/features/sources/             # 媒体/目录/直播源
├── src/features/display/             # 两窗控制与真实状态
├── src/features/runtime/             # 固定墙面预设及手动草稿
├── src/stores/、src/services/         # 共享状态与合同
└── scripts/                           # 前端回归
docs/
├── openapi.yaml、paths/、components/  # REST 合同
├── 使用文档.md、维护文档.md、CHANGELOG.md
├── known-pitfalls.md
└── qa/                                 # 当前实机证据与历史审计
```

**Structure Decision**: 沿用现有四层运行时与共享前端；现场证据放在忽略目录，正式验证结论写入 006 与文档。直播源新增登记合同时仍由现有 MediaSource/ControlHost 路径承载，不引入旁路数据库。

## Execution Design

1. **安全基线**：本机/D4 提交与脏文件检查、D4 精确项目进程与端口、完整数据库快照；修订四窗旧测试脚本和现场手册。
2. **分阶段上机**：D4 拉取当前提交、locked restore/构建；先 ControlHost 再两个 Worker。在交互会话核对显示器设备名、画面、进程和停机协作。
3. **功能矩阵**：两窗逐源、墙面两个固定预设、未知布局拒绝、PPT 转换/默认页图/实验模式、媒体目录/哈希；独立采集 API 与实体画面证据。
4. **问题驱动修复**：先复现和定位预案音量、直播源创建与解码、视频内存、Office 归属、单源删除，再按一类问题一组回归的方式修复。本机和 D4 同场景双层验证。
5. **稳定性与交付**：两窗 1000/100 基准、60 分钟混合播出、桌面/手机及可用封装端 UI QA；复核日志/资源/黑屏，停止并恢复原状态，更新文档/验证和两处既有 Git 远端。

## Complexity Tracking

无宪章豁免。任何需要新增持久状态或外部依赖的修复，先在任务和合同中给出具体现场证据及边界。
