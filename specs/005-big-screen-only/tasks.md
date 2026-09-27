# Tasks: 大屏专用控制与媒体整理

**Input**: [spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[contracts/big-screen-contract.md](contracts/big-screen-contract.md)

**Tests**: 用户允许本机测试，项目宪章要求行为变更有自动化或真实浏览器证据；禁止重启 D4 现场服务。

## Phase 1: Setup

**Goal**: 固定治理、范围和无现场副作用的验证边界。

- [X] T001 检查 `specs/005-big-screen-only/spec.md`、`plan.md` 与 `.specify/memory/constitution.md` 的两窗、电视电源、PPT、目录及抓包边界一致性。
- [X] T002 [P] 在 `specs/005-big-screen-only/verification.md` 记录本机现有 .NET/前端基线、D4 已停机状态和待执行的实体门禁。

## Phase 2: Foundational

**Goal**: 建立共享两窗语义、可配置的两块大屏输出及安全错误语义，供各故事复用。

- [X] T003 在 `runtime-dotnet/src/ScpCv.Domain/Model/DomainPrimitives.cs` 建立唯一有效窗口 1/2 的规则，并在 `runtime-dotnet/tests/ScpCv.Domain.Tests/` 添加边界测试。
- [X] T004 在 `runtime-dotnet/src/ScpCv.Contracts/Http/PlaybackDtos.cs` 与 `frontend/src/services/api.ts` 对齐两窗、布局、PPT 准备状态和文件位置的合同类型，保留电视电源类型。
- [X] T005 在 `runtime-dotnet/src/ScpCv.ControlHost/appsettings.json` 与 `runtime-dotnet/src/ScpCv.Infrastructure/Playback/BigScreenOutputOptions.cs` 定义显式大屏输出绑定及缺失时 fail-closed 规则，并补 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/BigScreenOutputOptionsTests.cs` 与 `runtime-dotnet/tests/ScpCv.Integration.Tests/HostHardwareIntegrationTests.cs` 验证。

## Phase 3: User Story 2 - 退役小电视播放能力 (P1)

**Goal**: 运行组、API 和页面只有窗口 1/2，同时保留电视电源。

**Independent Test**: 本机启动模拟组后只有两个播放器角色与会话；旧窗口调用均拒绝且不入队；电源设备列表仍有三设备。

- [X] T006 [P] [US2] 在 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/LegacyHttpContractTests.cs`、`PlaybackEndpointTests.cs` 与 `DeviceAndSystemEndpointTests.cs` 写两窗/旧窗口拒绝及电视电源保留回归。
- [X] T007 [P] [US2] 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/RuntimePipeBrokerTests.cs` 写仅 player-1/2 的就绪回归。
- [X] T008 [US2] 将 `runtime-dotnet/src/ScpCv.Supervisor/Runtime/RuntimeLauncher.cs` 与 `runtime-dotnet/src/ScpCv.PlayerWorker/App.xaml.cs` 收缩到两播放器、两块显式绑定的大屏输出。
- [X] T009 [US2] 将 `runtime-dotnet/src/ScpCv.ControlHost/Ipc/RuntimePipeBroker.cs`、`RuntimeMessageDispatcher.cs` 的角色与目标白名单收缩到两窗。
- [X] T010 [US2] 将 `runtime-dotnet/src/ScpCv.Infrastructure/Playback/RuntimeStateService.cs`、`Commands/CommandRepository.cs`、`Persistence/DatabaseInitializer.cs` 的新会话、快照、重置和命令限制为 1/2，保留旧数据库 3/4 行但不对外呈现或执行。
- [X] T011 [US2] 将 `frontend/src/features/display/displayTargets.ts`、`frontend/src/layouts/navItems.ts`、`frontend/src/router/index.ts`、`frontend/src/features/sources/SourcesView.vue` 的播放入口收缩为两窗；旧路由只重定向到大屏。
- [X] T012 [US2] 将 `frontend/src/stores/sessions.ts` 的服务端快照作为有效窗口权威集合，清除本地旧 3/4 会话；清理 `frontend/src/locales/zh-CN/` 中小电视播放文案但保留电视电源文案。
- [X] T013 [US2] 更新 `docs/components/parameters/WindowId.yaml`、`docs/components/schemas/ScenarioTarget.yaml` 和相关 OpenAPI 窗口范围，并运行两窗合同测试及 Redocly 校验。

## Phase 4: User Story 3 - 安全处理旧配置 (P1)

**Goal**: 旧四窗数据无损保留，不可执行的目标在任何墙面/媒体副作用前拒绝。

**Independent Test**: 旧预案含 3/4 有效目标时拒绝，墙面和 1/2 会话无变化；只含 1/2 时照常执行。

- [X] T014 [P] [US3] 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/HostHardwareIntegrationTests.cs` 增加旧 3/4 预案先拒绝、无副作用与 unset 兼容测试。
- [X] T015 [US3] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Scenarios/ScenarioService.cs` 统一新目标校验与旧预案激活前预检，禁止静默跳过退役目标。
- [X] T016 [US3] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Persistence/DatabaseInitializer.cs` 将历史未完成 3/4 命令显式终结为退役状态，不再唤醒不存在的 Worker。
- [X] T017 [US3] 在 `frontend/src/features/scenarios/scenarioModel.ts`、`ScenarioEditDrawer.vue`、`ScenarioPreviewDrawer.vue` 只编辑 1/2，并对含有效旧目标的预案显示只读警告和明确清除确认。

## Phase 5: User Story 1 - 控制两块大屏与手动映射 (P1)

**Goal**: 两固定预设保持可用，手动布局可保存预览，未知控制帧不可下发。

**Independent Test**: 两预设生成原有包序列；全屏与左右布局校验正确；笔记本/IP 手动布局在下发前拒绝、活动布局不变。

- [ ] T018 [P] [US1] 在 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallControllerTests.cs` 与 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/` 先写固定预设、手动布局保存/拒绝及零发包测试。
- [ ] T019 [US1] 在 `runtime-dotnet/src/ScpCv.Domain/Model/` 定义布局输入、区域、固定预设和冲突校验，新增 `runtime-dotnet/tests/ScpCv.Domain.Tests/` 测试。
- [ ] T020 [US1] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Persistence/` 为草稿布局和当前已应用预设增加持久记录及兼容迁移，不重写旧媒体数据。
- [ ] T021 [US1] 在 `runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/` 实现原子保存/预览/应用服务：仅两个已知预设调用现有包构造器，其余返回 `protocol_unavailable` 且零实体写入。
- [ ] T022 [US1] 在 `runtime-dotnet/src/ScpCv.ControlHost/Endpoints/` 接入布局 GET/保存/预览/应用与原 `/api/runtime/` 兼容预设投影，更新 `docs/paths/` 和 `docs/components/schemas/`。
- [ ] T023 [US1] 在 `frontend/src/features/` 增加统一大屏映射面板，替换 `BigScreenModeButtons.vue` 多入口的双状态交互，呈现手动布局预览、未就绪禁用、已应用预设与明确失败。
- [ ] T024 [US1] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Playback/RuntimeStateService.cs` 限制窗口 1/2 的显示器选择和停机后恢复只能落在显式绑定的两块大屏输出，增加 Hardware 装配测试。
- [ ] T025 [US1] 在 `frontend/scripts/` 增加本机映射与两窗导航测试，并按 `specs/005-big-screen-only/quickstart.md` 执行非实体验证。

## Phase 6: User Story 4 - PPT 默认逐页图片播放 (P1)

**Goal**: 原件保留，上传转页图；默认不用 Office 放映，原生模式由服务端实验开关控制。

**Independent Test**: 用本机 `C:\Users\qintsg\Desktop\Resources\AllinOne.pptx` 的副本上传，页图数量和顺序正确；默认打开/翻页不调用放映；实验开关在多个客户端状态一致。

- [ ] T026 [P] [US4] 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/` 写上传转换作业、失败重试、默认页图播放与实验开关合同测试，使用本机测试素材副本或生成小文稿。
- [ ] T027 [US4] 在 `runtime-dotnet/src/ScpCv.Contracts/` 与 `ScpCv.PowerPointHost/Interop/PowerPointComAdapter.cs` 增加独立 STA 的逐页 PNG 导出命令，不进入 SlideShow；补 Office 隔离/归属测试。
- [ ] T028 [US4] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/MediaPreparationService.cs`、`MediaSourceService.Ppt.cs` 实现版本化页图作业、清单、失败状态与重试；新上传和旧 PPT 源可触发准备但不删原件。
- [ ] T029 [US4] 在 `runtime-dotnet/src/ScpCv.PlayerWorker/` 实现页图显示、前后/指定页导航及会话 `slide_images` 状态；已有 PDF/PPT 控制仍按真实模式映射能力。
- [ ] T030 [US4] 在 `runtime-dotnet/src/ScpCv.ControlHost/Endpoints/` 与配置持久层加入默认关闭的实验性 PowerPoint 开关，只有开启后才允许原生放映，且仅影响后续打开。
- [ ] T031 [US4] 在 `frontend/src/features/settings/`、`frontend/src/features/sources/`、`frontend/src/features/display/` 展示转换状态、默认页图模式与实验性警告/开关，更新前端合同类型和测试。
- [ ] T032 [US4] 用本机素材副本和模拟 Office 适配器执行完整上传、默认播放、翻页、失败重试测试；D4 Office 实机留待用户恢复现场测试。

## Phase 7: User Story 5 - 媒体按用户文件夹存储与转移 (P1)

**Goal**: 页面文件夹与实体目录一致，原件和衍生文件安全移动，旧文件不自动改写。

**Independent Test**: 根级、中文子目录、同名、重命名与移动后实体路径和下载哈希一致，故障时不出现假成功。

- [ ] T033 [P] [US5] 在 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/` 写路径穿越、Windows 保留名、同名不覆盖、文件及目录移动补偿测试。
- [ ] T034 [US5] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/` 实现从 FolderId 层级到受管理路径的解析器、合法化与稳定冲突命名，禁止越出媒体根目录。
- [ ] T035 [US5] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/MediaSourceService.cs` 将新上传落盘从日期/散列目录改为页面文件夹路径，并在成功写库前完成 staging/摘要/物理落盘。
- [ ] T036 [US5] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/MediaSourceService.Support.cs` 实现媒体源移动/改名与文件路径、版本和衍生页图的原子或补偿更新；正在播放的文件移动须明确拒绝。
- [ ] T037 [US5] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/` 实现文件夹重命名、层级移动、删除前校验及子树文件路径更新，不自动批量迁移旧目录。
- [ ] T038 [US5] 在 `frontend/src/features/sources/` 与 `frontend/src/stores/sources.ts` 增加文件夹移动入口、真实路径反馈和冲突错误；修正根目录筛选及跨文件夹选源。
- [ ] T039 [US5] 在 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/MediaEndpointTests.cs` 与 `frontend/scripts/` 完成上传/移动/下载哈希合同回归。

## Final Phase: Polish & Cross-Cutting

- [ ] T040 更新 `docs/使用文档.md`、`docs/维护文档.md`、`docs/CHANGELOG.md`、`README.md` 与 `docs/known-pitfalls.md` 的当前两窗、电视电源、映射待抓包、PPT 和文件路径说明，保留历史 QA 审计。
- [ ] T041 运行 .NET 非 Physical 测试、前端 `pnpm test`/`typecheck`/构建、本机浏览器关键布局、Spec Kit 与 Redocly 校验；结果和未执行的 D4 实体门禁写入 `specs/005-big-screen-only/verification.md`。
- [ ] T042 检查 `git diff --check`、敏感文件与 `.validation` 排除，在 `specs/005-big-screen-only/verification.md` 记录任务完成和剩余依赖；按项目格式分小提交并推送既有远端，不重启 D4 服务。

## Dependencies & Execution Order

- Phase 1 → Phase 2 → US2 → US3 → US1 → US4 → US5 → Polish。US3 在墙面应用前完成，避免旧预案先下发再报错。
- US4 的页图制品和 US5 的实体目录可分开实现，但最终文件移动必须带着页图关联一起验证。
- T027 的 Office 转换与 T034 的路径解析位于不同项目/文件，可在各自前置测试完成后并行开发；前端 T023、T031、T038 也可在合同冻结后分别推进。
- MVP 为 US2+US3+US1 的两窗与安全映射；PPT 和媒体目录是同一需求的后续独立切片，不得以 MVP 代替全部交付。

## Parallel Example

- US2：可并行写 `RuntimePipeBrokerTests.cs` 的就绪测试和 `LegacyHttpContractTests.cs` 的 REST 合同测试；实现时先完成 Domain 规则再改 Supervisor/Broker。
- US4/US5：OfficeHost 页图导出与 MediaPathResolver 可在不同文件并行；最终共同验证媒体源移动后的页图路径。

## Implementation Strategy

1. 每项行为先让针对性回归失败，再实现、复测；一个独立可审查块一个小提交。
2. 先使旧窗口安全退役、保留电源，再提供固定预设与手动草稿；未知帧始终禁止下发。
3. 再交付 PPT 图片默认播放与实体媒体目录；本机测试可用测试资源副本，现场服务保持关闭。
4. 最后更新合同、用户文档与验证记录，明确 D4 实体门禁和后续抓包未完成。
