# Tasks: 输入路由与共享低延迟预览

**Input**: spec/plan/research/data-model/contracts/quickstart
**Validation**: 16项规范质量检查通过，默认测试无现场网络；改动按目录所有权并行，最终统一集成。

## Phase 1: Setup

- [x] T001 保存019确认点19356ee，完成specs/020-input-routing-preview/规范、三项只读研究与合同，读取已知坑路径。

## Phase 2: Foundational

- [x] T002 在runtime-dotnet/src/ScpCv.Infrastructure/Devices/SiteInputCatalog.cs提供31路受信定义及ResolveDescriptor；测试严格排除75、别名和尺寸。
- [x] T003 在frontend/src/services/inputCatalogApi.ts及runtime/inputs/InputSourcePicker.vue定义正式A输入合同，并与backend /api/inputs/对齐，禁止任意控制帧。

## Phase 3: US2 目录与输入（P1）

- [x] T004 [US2] 在Infrastructure/Devices/WorkstationCatalog.cs、InputDiscovery.cs与ControlHost/Endpoints/InputCatalogEndpoints.cs实现受保护目录、缓存／并发／超时发现及假传输回归；更新Program注册与输入绑定。
- [x] T005 [US2] 在WallLayoutComposer.vue、WallInputPicker.vue及sources目录接入A菜单／可达工作站、窗口3/4预览，不增加原生窗口。

## Phase 4: US1 共享与首帧（P1）

- [x] T006 [P] [US1] 在features/preview/previewPool.ts、previewCanvas.ts、ClientStreamPreview.vue实现应用scope单解码、多canvas、引用／宽限、三态1px与无多余提示，补认证/几何/同源回归。
- [x] T007 [P] [US1] 在ControlHost/StreamPreview及StreamPreviewEndpoints.cs增加HMAC共享身份、native描述符、65/66／目录别名和短GOP；保持完整GOP/预算，补协议与实际首帧对照。

## Phase 5: US3/US4 电视与预案（P1）

- [x] T008 [P] [US3] 在Infrastructure/DisplayRouting及独立Endpoints实现电视同连接满屏+单源、黄金帧／假TCP顺序与受限API；不套用50节点序列。
- [x] T009 [P] [US4] 在RuntimeState/迁移、RuntimeStateService.VideoWall.cs、ScenarioService/YAML实现每输出记账与门禁内预案去重；失败/未知/几何/手动force回归。
- [x] T010 [US3] 在dashboard/DashboardView.vue、电视源卡片及useDevicePowerActions接入右侧电源下方两电视、按钮内非数值发送进度，保留确认/归属/防重。
- [x] T011 [US4] 在scenarios编辑器/摘要及apiTypes.ts同步advanced.televisions，旧预案保持，DOM回归不漏保存字段。

## Phase 6: US5 Electron直拉（P1）

- [x] T012 [P] [US5] 在frontend/electron/preview、preload/channels/security及platform/previewTypes.ts实现受限Main描述符、app短租约流、认证/源/主机/顶层所有者保护；Web不调用IPC。
- [x] T013 [P] [US5] 在ScpCv.ClientPreviewHost及专用构建脚本实现固定媒体资产、copy/兼容模式与stdin心跳/独占Job清理；真实父/子死亡与不产数据测试，声明发行许可边界。

## Phase 7: US6 摄像机（P2）

- [x] T014 [US6] 在devices/DevicesView.vue接当前所选摄像机共享预览、固定监控禁转，补CameraBOX黄金帧和既有停止/覆盖保护回归。

## Phase 8: Integration / Polish

- [x] T015 删除或吸收prototype变体/入口、只保留正式A；更新package资源与启动命令，生产构建不留演示状态或伪成功。
- [x] T016 在docs/openapi.yaml及对应paths/schema同步输入/显示/native/预案合同，用Redocly校验；登记新副作用路径和维护/使用/许可资料。
- [x] T017 运行相关与完整非Physical.NET、frontend/类型/Web/App/Electron构建、规范检查，实际合成媒体/本机Electron/只读布局分层验收并记verification.md，不以未验项收口。

## Dependencies & Execution Order

T001→T002→T003/T004；T005依赖输入合同。T006/T007与T012/T013按共享preview契约并行，Native端点由T007提供。T008/T009共用TV agent目录避免冲突，T011后续转同TV agent完成，root集成T010；T014复用共享池。T015–T017收口。

## Parallel Examples

按plan明确的所有权划分：预览agent只改features/preview及ControlHost StreamPreview；Native agent只改electron/platform/helper；TV agent只改DisplayRouting/场景/迁移；root改输入目录/正式UI/Program/OpenAPI总入口。共享文件先协调，不能互相覆盖或提交他人未完成内容。

## Implementation Strategy

先受信定义与接口、测试，再分域接线；每域先相关验证，再完整合成。测试和清理仍区分假媒体、真回环、真客户端与未运行现场。不得将编译、HTTP成功或逻辑缓存标为实体画面确认。
