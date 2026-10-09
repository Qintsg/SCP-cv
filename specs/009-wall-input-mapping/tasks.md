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

## Dependencies & Execution Order

T001→T002→US1→US2→US3→文档/验证。协议先预检再清屏；测试先于实现。T005的独立向量可以与T008持久测试并行，但共享实现仍串行。

## Parallel Example

US1组合向量与US2纯codec向量分属不同测试文件可同时编写；US3 DOM与HTTP测试可并行执行。本轮只有研究代理只读并行，主代理实施。

## Implementation Strategy

先通过组合计划MVP，再接真实控制器/接收，最后持久与页面；只在验证完成后勾选。现场抓包/出画未执行不纳入已完成软件测试。
