# Tasks: Ant Design 现场控制台重构

**Input**: [spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[UI合同](contracts/ui.md)

## Phase 1: Setup

- [x] T001 在specs/014-ant-design-console/spec.md与checklists/requirements.md记录新需求、范围与质量门禁。
- [x] T002 在specs/014-ant-design-console/research.md、plan.md、data-model.md及contracts/ui.md记录官方参考与迁移设计。

## Phase 2: Foundation

- [x] T003 在frontend/package.json、src/App.vue及src/design-system/theme/迁移真实Ant依赖与全局主题，移除Naive专用运行依赖。
- [x] T004 在frontend/src/design-system/FToastHost.vue、FDialogHost.vue、FIcon.vue及ActionMenu.vue迁移反馈、图标与单次菜单派发，并更新相关测试。

## Phase 3: US1 一次应用

**独立验收**: 修改布局一次点击提交当前参数，12组合、失败、冲突、离页/换主机均有回归。

- [x] T005 [US1] 在frontend/scripts/video-wall-mapping.test.mjs覆盖一次应用和移除草稿/发送常驻展示的合同。
- [x] T006 [US1] 在frontend/src/features/runtime/VideoWallMappingPanel.vue与locales/zh-CN/videoWall.ts实现单按钮与完整身份/修订/防重保护。

## Phase 4: US2 工作区重排

**独立验收**: 桌面/手机主路由真实呈现Ant控件和不同于旧排布的任务层级。

- [x] T007 [US2] 在frontend/src/layouts/与styles/实现分组侧栏、轻顶栏、明暗与手机导航。
- [x] T008 [US2] 在frontend/src/features/dashboard/重排双输出/墙面主区与声音/电源辅助栏。
- [x] T009 [US2] 在frontend/src/features/sources/、directories/及scenarios/统一目录、内容及上下文操作的工作区布局。
- [x] T010 [US2] 在frontend/src/features/display/、devices/及settings/重排显控、摄像机音量与设置层级。

## Phase 5: US3 功能保持

**独立验收**: 表单、开关、菜单、滑块、弹层及全部现有关键业务回归通过，无重复/漏发。

- [x] T011 [US3] 在frontend/src/features/其余组件完成Ant事件/属性/Slot迁移，保持播放、PPT、共享、音频与登录功能。
- [x] T012 [US3] 在frontend/scripts/迁移真实DOM夹具和断言，覆盖菜单、弹层、通知、滑杆及输入可访问性。

## Phase 6: 验证与文档

- [x] T013 在frontend/scripts/与specs/014-ant-design-console/verification.md记录完整前端测试、类型和Web/App构建结果。
- [x] T014 在specs/014-ant-design-console/verification.md记录本机真实浏览器关键流程、响应式、明暗/焦点及控制台检查，无实体副作用。
- [x] T015 在docs/使用文档.md、docs/维护文档.md、docs/video-wall-mapping.md、docs/CHANGELOG.md、STYLE.md及相关旧规范更新当前行为，校验Spec Kit及差异。

## Dependencies and Parallel Opportunities

T001→T002→T003→T004；US1与外壳设计在基础完成后分片推进，US2各功能目录可独立验证但共享主题/反馈顺序修改。T011依赖基础，T012跟随相应页面完成；T013–T015收口。示例：US1测试与US2外壳在不同文件可并行，但同一文件不并写。

## Implementation Strategy

先完成实际Ant基础与US1（最小可用切片），再逐个工作区迁移/重排，每片保留真实交互验证。不能把控件改名或静态原型当整轮完成。部署与实体设备验证不在本轮任务范围。
