# Tasks: 大屏输入组合与协议接入

**Input**: spec.md、plan.md、research.md、data-model.md、contracts/video-wall.md。
**Tests**: 本机包级、HTTP、持久化、DOM及真实浏览器；不自动操作现场设备。

## Phase 1: Setup

- [X] T001 在 `specs/009-wall-input-mapping/` 完成规范/设计/合同及requirements质量门禁，保留008工作区。

## Phase 2: Foundational

- [X] T002 在 `runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallProtocolOptions.cs` 建立输入55/56/81及协议配置预检，`ScpCv.ControlHost/Program.cs` 注册。

## Phase 3: User Story 1 - 组合映射 (P1)

**Goal**: 12完整组合及6单侧布局产生正确包，旧400包保持。
**Independent Test**: 校验每片输入、裁剪、A/B/C、独立窗口以及未映射区域清除。

- [X] T003 [US1] 在 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallLayoutPlannerTests.cs` 写组合和预检回归。
- [X] T004 [US1] 在 `runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallPacketCodec.cs` 与 `VideoWallLayoutPlanner.cs` 实现受限几何与四种映射编码，保留旧预设。

## Phase 4: User Story 2 - 协议与可靠发送 (P1)

**Goal**: 配置分支、加密和接收有真实生产路径，失败不假成功。
**Independent Test**: 原代码向量、同节点顺序、取消/失败及回环TCP分帧验证。

- [X] T005 [US2] 在 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallProtocolTests.cs` 写四种格式、DES、回读半包/粘包及顺序测试。
- [X] T006 [US2] 在 `runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallController.cs` 实现布局下发与同节点串行，并在 `VideoWallReplyDecoder.cs` 实现有界诊断解析及传输接线。
- [X] T007 [US2] 在 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/VideoWallRegistrationTests.cs` 验证Hardware配置/真实控制器装配，零实体发包。

## Phase 5: User Story 3 - 页面与持久投影 (P1)

**Goal**: 草稿/已发送布局分开，服务端能力、静音及版本正确。
**Independent Test**: HTTP覆盖12组合，重建读取、过期请求和失败不确定状态；DOM及浏览器呈现模拟结果。

- [X] T008 [US3] 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/VideoWallLayoutApplicationTests.cs`、`VideoWallLayoutRecoveryTests.cs` 和 `ScpCv.ControlHost.Tests/VideoWallLayoutEndpointTests.cs` 写应用/版本/持久/取消/排他回归，保留既有LayoutServiceTests。
- [X] T009 [US3] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Playback/RuntimeStateService.VideoWall.cs`、Domain实体与Persistence迁移中实现已发送布局状态及布局静音，旧预案重置投影。
- [X] T010 [US3] 在 `runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallLayoutService.cs`、Contracts及ControlHost端点接入服务端能力和expected_revision。
- [X] T011 [US3] 在 `frontend/src/features/runtime/VideoWallMappingPanel.vue`、runtime store及API类型接入可应用组合、IP81标签、草稿版本及结果，补 `frontend/scripts/video-wall-mapping.test.mjs`。
- [X] T012 [US3] 在 `.validation/qa-wall-009/` 使用真实浏览器验证桌面/窄屏及失败/修改后禁用，不连接现场。

## Final Phase: Polish

- [X] T013 更新 `docs/components/schemas/VideoWall*.yaml`、运行态合同、README、使用/维护文档、CHANGELOG和known-pitfalls。
- [X] T014 执行相关.NET/前端/Spec Kit/Redocly验证，在 `specs/009-wall-input-mapping/verification.md` 记录结果和现场剩余边界，检查diff与文件长度。

## 补充阶段：紧凑屏幕交互块（US4）

- [X] T015 [US4] 在 `frontend/scripts/video-wall-mapping.test.mjs` 补单/双屏区域内选择、布局往返保留、未点应用零POST与真实保存版本回归；先运行证明现有界面不满足新合同。
- [X] T016 [US4] 在 `frontend/src/features/runtime/VideoWallMappingPanel.vue` 和 `frontend/src/locales/zh-CN/videoWall.ts` 实现紧凑单/双屏交互块和折叠布局名称，保留单侧/IP草稿与发送保护。
- [X] T017 [US4] 在 `frontend/src/features/dashboard/DashboardView.vue`、`frontend/src/features/settings/tabs/RuntimeSettingsTab.vue` 接入同一交互块，移除被替代的重复预设卡。
- [X] T018 [US4] 在 `.validation/qa-wall-009-compact/` 用本机独立Simulation执行真实浏览器12组合、布局往返、首页/设置、窄屏/键盘与错误门禁；不访问D4墙面。
- [X] T019 [US4] 更新 `docs/使用文档.md`、`docs/video-wall-mapping.md`、`docs/CHANGELOG.md` 和本功能verification；执行前端全套、类型/Web构建及Spec Kit校验，说明未部署边界。
- [X] T020 [US1] 在 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallLayoutPlannerTests.cs` 和 `ScpCv.ControlHost.Tests/VideoWallRegistrationTests.cs` 补D4默认63/64、12组合及旧两预设按当前绑定生成包回归；旧55/56黄金保持显式历史配置，先红后绿。
- [X] T021 [US1] 更新 `runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallProtocolOptions.cs`、`ScpCv.ControlHost/appsettings.json`、现行绑定文档及known-pitfalls，执行相关后端回归；不改历史抓包/fixture、不触发D4控制。

补充阶段顺序：T015→T016→T017→T018→T019；绑定修正T020→T021在T018之前完成，互不编译同一项目；本轮不重跑或重新勾选既有实体未验证项目。

## Dependencies & Execution Order

### 美化补充（FR-012）

- [X] T022 [US4] 在`frontend/scripts/video-wall-mapping.test.mjs`先补草稿徽标不冒充出画和留空区域可重新选源的DOM回归，记录红绿。
- [X] T023 [US4] 美化`frontend/src/features/runtime/VideoWallMappingPanel.vue`，将样式拆入同目录`VideoWallMappingPanel.css`，在`frontend/src/locales/zh-CN/videoWall.ts`精简预检辅助文字；仅视觉变化，保留现有业务边界。360px发现顶栏溢出后，仅在`AppTopBar.vue`调整换行并补壳层回归断言。
- [X] T024 [US4] 在`.validation/qa-wall-009-polish/`本机真实浏览器核对12组合、草稿/未知输入、键盘、360/390浅深主题及减少动效，截图自审；不访问D4。
- [X] T025 [US4] 执行前端全套、类型/Web构建、Spec Kit和diff检查，更新使用指南、CHANGELOG和verification，明确未部署且后端未重跑。

美化补充顺序：T022→T023→T024→T025；原实现和绑定结果保留，未改变的后端/实体测试不重复计数。

### D4部署补充（用户2026-10-09另行授权）

- [X] T026 核对D4源码/运行任务、既有数据和桌面会话；完成本机独立提交并推送内网secondary对应远端，D4在`D:/SCP-cv`仅快进拉取。
- [X] T027 从已提交源码生成完整Release包及Web制品，上传核SHA256；先协作停止旧运行组及前端，备份数据库/原Web制品，保留旧发布目录，不修改媒体/私有口令。
- [X] T028 沿用既有桌面会话/两显示器/DataRoot启动新包及前端，只读核对版本、63/64配置、两播放器/运行组、健康与新界面；不下发墙面、电源或重放媒体。
- [X] T029 在`docs/deployments/`及verification记录制品/提交/备份/运行核验与实体未验边界，完成文档提交推送和远端快进，确认工作区与服务最终状态。

部署顺序：T026→T027→T028→T029；以已有测试作为功能证据，发布/远端核验是本轮独立证据。失败保留旧包和数据，不自动回滚数据库或重放物理动作。

T001→T002→US1→US2→US3→文档/验证。协议先预检再清屏；测试先于实现。T005的独立向量可以与T008持久测试并行，但共享实现仍串行。

## Parallel Example

US1组合向量与US2纯codec向量分属不同测试文件可同时编写；US3 DOM与HTTP测试可并行执行。本轮只有研究代理只读并行，主代理实施。

## Implementation Strategy

先通过组合计划MVP，再接真实控制器/接收，最后持久与页面；只在验证完成后勾选。现场抓包/出画未执行不纳入已完成软件测试。
