# Tasks: 按需共享的免安装直播预览

**Input**: [spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[contracts/preview.md](contracts/preview.md)
**Tests**: 需求要求确定性回归、显式合成回环与只读浏览器；默认测试不触设备。

## Phase 1: Setup

- [x] T001 在specs/018-shared-flv-preview/建立规范与设计，16项质量检查通过，读取docs/known-pitfalls.md §1并完成只读研究。

## Phase 2: Foundational

- [x] T002 在runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/PreviewOptions.cs及PreviewSourceResolver.cs实现有界配置和受限输入解析；在StreamPreviewSourceTests.cs覆盖URL拒绝、凭据与通道语义。
- [x] T003 在runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/FlvReader.cs、FlvGopBuffer.cs实现完整tag／GOP与时间戳代次；在StreamPreviewFlvTests.cs覆盖损坏、加入、追赶、调档和缓存限额。

## Phase 3: US1 免安装播放（P1，MVP）

独立验收：合成源经过真实转换，在两种FLV出口可解码；浏览器无本机桥。

- [ ] T004 [US1] 在runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/MediaMtxPreviewClient.cs、PreviewProcess.cs实现私有按需路径与Windows独占自有转换进程，依赖失败明确返回。
- [ ] T005 [US1] 在runtime-dotnet/src/ScpCv.ControlHost/Endpoints/StreamPreviewEndpoints.cs及StreamPreviewRegistration.cs接入HTTP／WS，Program.cs和frontend/config/dualProtocol.ts明确WS接线。
- [ ] T006 [US1] 通过pnpm加入mpegts.js精确依赖，在frontend/src/features/preview/previewClient.ts、ClientStreamPreview.vue接入实际流与卡内状态；去除tools/client-preview旧生产双栈及ClientPreviewSettings.vue本机令牌入口，更新调用方。

## Phase 4: US2 按需共享（P1）

独立验收：65同流只一份生产者，无观看者后有限时间0上游。

- [ ] T007 [US2] 在runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/SharedPreviewFeed.cs、StreamPreviewRegistry.cs实现原子同源复用、单租约消费、续租及有限释放；StreamPreviewRegistryTests.cs覆盖并发／取消／新旧代次。
- [ ] T008 [US2] 在frontend/scripts/server-preview.test.mjs覆盖可见性、离页、认证／主机切换、销毁与失败清理，不模拟电脑输入。

## Phase 5: US3 资源让路与拥塞降档（P1）

独立验收：慢消费者独立、完整GOP追赶、限速有界、降档可继续解码。

- [ ] T009 [US3] 在runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/PreviewBandwidth.cs实现全局限速、档位预算、NIC尽力检测与迟滞；StreamPreviewBudgetTests.cs验证假时钟／慢消费者及正式播放优先准入。
- [ ] T010 [US3] 在.validation/preview018/执行真实合成RTMP、HTTP／WS、并发中途加入／降档解码与清理取证；更新specs/018-shared-flv-preview/verification.md，明确入站／系统QoS边界。

## Phase 6: US4 认证与所有权（P2）

独立验收：无认证／跨站／他人／旧登录／超限均不启动流，停机只清自有资源。

- [ ] T011 [US4] 在runtime-dotnet/tests/ScpCv.ControlHost.Tests/StreamPreviewEndpointTests.cs验证正式Cookie／CSRF、HTTP／WS Origin、租约范围、注销期限、依赖与容量失败；核对Hardware装配与Host退出。

## Phase 7: Polish

- [ ] T012 更新docs/openapi.yaml、docs/console-workspace-016.md、docs/使用文档.md、docs/维护文档.md、docs/CHANGELOG.md及docs/known-pitfalls/console-workspace.md，说明配置、按需／共享、预算与可选系统QoS。
- [ ] T013 执行相关／完整.NET非Physical、前端测试／类型／Web／App／Electron-main构建、Redocly及Spec Kit；在specs/018-shared-flv-preview/verification.md记录未验边界与结果，仅本地提交不推送。

## Dependencies & Execution Order

T001→T002／T003→T004→T007；端点T005依赖租约，前端T006依赖合同；T009与共享生产者集成后做T010；T011覆盖正式装配，T012／T013收口。MVP先单源双出口，再共享、拥塞和所有权。

## Parallel Examples

源解析与FLV纯解析的工具测试可以并行；文档与独立前端测试不冲突。研究任务已完成，不额外默认分派实现agent。

## Implementation Strategy

测试先验证纯协议和注册语义，再接实际进程／端点；所有测试必须区分假生产者、真实回环和未运行现场。不得把HTTP200、路径ready或进程存在当成已出画。
