# Tasks: 输入与预览原型

**Input**: spec.md、plan.md、research.md、data-model.md、contracts/ui.md

## Phase 1: Setup

- [x] T001 在specs/019-input-preview-prototype/记录最新表格、原型范围、设计与16项质量门禁。

## Phase 2: Foundational

- [x] T002 在frontend/scripts/serve-input-prototype.mjs及prototype/index.html、main.ts建立回环独立入口，并在frontend/package.json增加demo:inputs，不接生产路由。

## Phase 3: US1 选源（P1）

独立验收：A/B/C可打开，其他输入可搜索／滚动，75不存在，65能找到D4-3。

- [x] T003 [US1] 在frontend/src/features/runtime/wall/prototype/catalog.ts及InputPicker.vue实现目录与三种选源形态。

## Phase 4: US3 预览连续（P1）

独立验收：多个video共用同MediaStream，CSS几何变化不重建，指标明确仅本地。

- [x] T004 [US3] 在frontend/src/features/runtime/wall/prototype/demoStreams.ts、PreviewTile.vue及DemoApp.vue实现同源样片、稳定块身份、比例和模式切换。

## Phase 5: US2 目录（P2）

独立验收：工作站与Camera分类、模拟可达和搜索，只更新所选区域。

- [x] T005 [US2] 在frontend/src/features/runtime/wall/prototype/DemoApp.vue和prototype.css实现媒体目录及选中目标联动。

## Phase 6: US4 摄像机、电视与预案（P1）

- [x] T006 [US4] 在prototype/TvAndScenario.vue、demoOutputs.ts、DemoApp.vue、PreviewTile.vue补摄像机预览、电视单路选源／预案内存去重及1px三态边框；CameraBOX只读结论写入camera-box-review.md。

## Phase 7: Polish

- [x] T007 类型／生产构建、只读浏览器媒体／几何／网络检查；在specs/019-input-preview-prototype/verification.md及prototype/NOTES.md记录原型限制和待用户确认，更新docs/CHANGELOG.md。

## Dependencies & Execution Order

T001→T002→T003→T004→T005→T006→T007；MVP为T003。目录和演示源纯模块可分别校核；当前不分派agent。原型不做正式测试套件，遵宪章保留实际渲染和媒体验收记录。

## 后续生产实现（非本轮交付）

用户确认形态后另立实现任务：真实在线发现、受保护工作站目录、正式输入／组播及窗口3/4预览解析、客户端FLV共享／首GOP增量、摄像机正式页预览和参考协议回归、电视专有帧及预案接线、服务端按输出持久保存上次成功下发（未知／部分失败不可去重、几何变化仍下发、仪表盘允许强制相同重发）、清晰度与身份围栏回归、实机测量；不能据本原型勾为完成。
