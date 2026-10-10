# Tasks: 网页屏幕共享

**Input**: 本目录spec/plan/research/data-model/contracts/quickstart。

**Prerequisites**: 质量清单通过，普通源/共享身份边界已核；用户明确要求测试。

## Phase 1: Setup

- [X] T001 校验`specs/010-browser-screen-share/`规范与16项质量清单，检查忽略规则、现有源身份及物理路径，不改D4。

## Phase 2: Foundational

- [X] T002 在`.validation/qa-screen-share-010/`建立真实浏览器采集→WebRTC→独立原生WebView2技术实验，核运动后帧/停止；记录`verification.md`，不能用canvas模拟替代原生采集。
- [X] T003 在`runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScreenShareRegistryTests.cs`先写窗口锁、所有权、cap、SDP上限、过期/旧身份回归；实现`ScpCv.ControlHost/ScreenSharing/ScreenShareRegistry.cs`及分文件模型。

## Phase 3: User Story 1（P1）

**Goal**: 网页原生分享正确目标，不需手工流源。

**Independent Test**: 分别两正式Worker的运动画面与另一窗保持，不把首连接当播出。

- [ ] T004 [US1] 在`runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScreenShareEndpointTests.cs`写认证/CSRF/离线/争抢/取消/offer回归，在Integration补动态OPEN事务与lease围栏测试。
- [ ] T005 [US1] 实现`RuntimeStateService.ScreenShare.cs`独立动态OPEN与身份；`RuntimeMessageDispatcher`仅在已认证匹配Worker的lease注入内存receiver授权，不持久cap。
- [ ] T006 [US1] 实现`ScreenShareService.cs`、`ScreenShareEndpoints.cs`及Program装配，接收页cap/schema/来源/阶段保护；Hardware与Simulation均测真实装配，后者不冒称出画。
- [ ] T007 [US1] 在`ScpCv.Windows.Tests/ScreenSharePlaybackTests.cs`写后帧/首帧失败/跨资源回调回归；将超500行`PlayerRuntimeHost.cs`先拆分Web资源方法，新增`PlayerRuntimeHost.ScreenShare.cs`专用资源与实际采样，不使用普通web缓存。
- [ ] T008 [US1] 在`frontend/scripts/screen-sharing.test.mjs`写原生许可先于API、拒绝零OPEN/释放轨道/两目标回归，实现`rtcPeer.ts`、`services/screenSharing.ts`、`useScreenShare.ts`、`ScreenSharePanel.vue`，接入`DisplayControlView.vue`。
- [ ] T009 [US1] 检查并修改`playbackObservation.ts`、`playbackCapabilities.ts`、`playbackOutput.ts`、`PlaybackControl.vue`、`DisplayControlView.vue`及`stores/sessions.ts`对共享null source_id的显示、关闭、能力与刷新；HTTP/SSE合同有明确共享身份，不造永久媒体源。

## Phase 4: User Story 2（P1）

**Goal**: 显式停止与异常回收不泄漏、不误关后来内容。

**Independent Test**: 十轮及迟到消息交错、两窗口隔离与实际资源释放。

- [ ] T010 [US2] 在`ScreenShareLifecycleTests.cs`、Integration及`frontend/scripts/screen-sharing.test.mjs`先补重复停止、停止失败待确认、旧代次结束不关新源、页面/主机/登录寿命和并发换源。
- [ ] T011 [US2] 实现`ScreenShareCleanupService.cs`、Registry终态与原子条件CLOSE，接入普通换源/系统停机；receiver和前端结束先停采集，不自动重试副作用，`ScenarioService`不保存/恢复共享并提示。
- [ ] T012 [US2] 在`.validation/qa-screen-share-010/`执行真实两peer与原生十轮共享/停止/换源/旧回调隔离，明确核track/peer/native资源释放，不只核状态。

## Phase 5: User Story 3（P2）

**Goal**: 实际接收状态清晰、内容音频能力诚实、HTTPS可正常使用。

**Independent Test**: 有/无音轨、实际帧、断线、桌面/390px、键盘及安全访问。

- [ ] T013 [US3] 在前端及Windows测试补首连接不当playing、无音轨与音量/静音桥；实现真实receiver帧/音轨状态与专用控件，不向普通VLC发送伪音频控制。
- [ ] T014 [US3] 为`frontend/vite.config.ts`或既有托管入口接入显式HTTPS证书配置与校验，在`docs/screen-sharing.md`记录正常LAN信任证书步骤及不支持浏览器，禁止以关闭安全策略替代。
- [ ] T015 [US3] 在`.validation/qa-screen-share-010/`以真实浏览器验证权限取消/拒绝、失败清理、390/桌面/浅深主题/键盘、实际状态和控制台错误；正常capture与mock用例分别计数。

## Phase 6: Polish & Cross-Cutting

- [ ] T016 在`.validation/qa-screen-share-010/`验证本机→D4跨机ICE及真实两Worker后帧、两会话5分钟、十轮停止/换源与非目标保持；不发墙面/电源；若直连失败先解决媒体路由，禁止把本机结果当D4完成。
- [ ] T017 更新`docs/openapi.yaml`及相关schema/path、使用/维护、`docs/CHANGELOG.md`、`docs/known-pitfalls.md`物理路径及新坑；更新本目录`verification.md`需求映射和原始证据。
- [ ] T018 执行相关与全非Physical.NET、全前端、typecheck/build:web、Redocly、Spec Kit、diff/文件长度；证据按实际执行计数，逐项完成审计，不因部分绿灯标完成。

## Dependencies & Execution Order

T001→T002→T003→US1（T004..009）→US2（T010..012）→US3（T013..015）→现场/文档/完整验证（T016..018）。安全和真实技术门禁优先，未通过不得推进生产接入；所有共享实现串行，避免共享文件竞争。

## Parallel Example

只读网络/协议研究可与独立规范核查并行；本轮按plan委派一名只读研究者，主代理读skill、形成决策和实施。功能代码不委派写同文件。

## Implementation Strategy

先证实原生采集与实际接收，再完整覆盖用户三个故事；MVP不代替最终目标。失败证据保留，修复回归先红后绿；D4部署/实体测试只在代码和数据安全检查后进行，未完成项保持未勾选。
