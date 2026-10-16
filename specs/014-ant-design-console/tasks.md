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

## Phase 7: Convergence

2026-10-17复核当前代码及完整FR/SC、故事验收、方案与宪章；新增两个HIGH partial，不以既有555绿或浏览器矩阵替代未覆盖的重命名/旧反馈行为。既有任务不改写。

- [x] T016 将媒体目录`window.prompt`重命名改为真实Ant表单，冻结当前目录目标，支持名称校验、处理中防重、失败保留及键盘提交/取消；菜单触发弹层时正确移交焦点，关闭后回到可见触发器。先补真实DOM及浏览器回归，涉及`frontend/src/features/sources/useSourceFolders.ts`、`FolderDialogs.vue`、`SourcesView.vue`、`design-system/ActionMenu.vue`及目录/菜单测试，追溯FR-006/009/010、US3/AC1/3（partial，HIGH）。
- [x] T017 为媒体库单源背景音频及目录操作的异步反馈补齐页面/主机/账号归属，冻结名称与目标；旧响应不得在新页面提示成功/失败或启动后续刷新，正常操作与失败反馈保留。先以延迟HTTP覆盖离页/换主机/换账号、正常提交及防重，涉及`frontend/src/features/sources/useSourceActions.ts`、`useSourceFolders.ts`、`SourcesView.vue`及相关真实DOM回归，追溯FR-004/009、US3/AC3–4、Constitution I（partial，HIGH）。

本阶段完整前端601/601、Web/App类型与构建、56组页面/10组流程，以及390px/真实200%重命名键盘流程通过；41项新增归属回归覆盖失效/正常/原错误及恢复读取，详见[目录与归属收口](verification/20261017-directory-scope.md)。

## Phase 8: Convergence

2026-10-17进一步审计认证传输边界，新增一个CRITICAL contradicts：旧会话的401响应晚于新登录到达时，`api.requestJson`只复核主机代次，仍执行main的全局401回调并清除新用户。实际模块/内存HTTP红回归输出`unauthorizedCallbacks=1,currentUser=null`；当前401正对照通过。既有601不覆盖此时序，不提前推送或部署。

- [x] T018 CRITICAL 为JSON/XHR请求补齐认证寿命围栏并接入真实登录/退出/本地失效/初始化流程；旧401/成功响应不得清除或覆盖已成立的新登录、继续旧认证链路，当前401必须仍正常退出。先将本机真实模块红回归持久化，再覆盖迟到401/成功、当前401、XHR、同ID重新登录、初始化和认证操作交错；保留原CSRF/主机围栏/错误语义，不全局忽略401、不盲目重放或发现场指令。涉及`frontend/src/services/api.ts`、`stores/auth.ts`、`main.ts`及新增认证HTTP回归，追溯Constitution I、FR-009/011、US3/AC3–4（contradicts，CRITICAL）。

认证纯服务与真实Auth Store接线完成；全量628/628、Web/App类型及构建、54SFC通过。真实生产SPA旧版先重现误退出、修复后5段流程通过，当前401与错误密码仍保留原行为；56组页面及10组原操作流程复验见[认证响应记录](verification/20261017-auth-scope.md)。未改变main的当前401处理，没有推送或部署。

## Phase 9: Convergence

2026-10-17接续检查账户流程：后端`AuthEndpoints.ChangePasswordAsync`成功后调用`csrf.IssueToken`，而前端`resolveCsrfToken`优先使用原内存令牌，`Auth.changePassword`未同步或失效该缓存，后续写请求会携带旧令牌。属于FR-009账户功能的HIGH partial，不因密码提交成功即认为后续操作可用。

- [x] T019 HIGH 补齐修改密码成功后的CSRF同步及真实结果反馈：保留按目标协议/主机隔离、跨源客户端缓存和T018寿命围栏；先用实际API/Auth与受控Cookie轮换重现后续写入使用旧令牌，再覆盖正常同步、失败/离页/换主机/换账号和同步重试。密码已修改而令牌同步失败时必须明确区分，不自动重放密码或其它业务写入、不误报密码失败；真实Ant账户表单及生产浏览器验证敏感字段清空、忙态、防重和后续操作。涉及`frontend/src/stores/auth.ts`、`services/api.ts`、`features/settings/tabs/AccountSettingsTab.vue`、`locales/zh-CN/settings.ts`与相关测试/文档，追溯FR-009/011/012、US3/AC3–4、plan认证/CSRF边界（partial，HIGH）。

本机完整644/644、Web/App类型及构建、54SFC和规范校验通过；3组真实改密/同步失败/键盘重试恢复、原5段认证/10组操作/56组页面通过。另修复该恢复流程暴露的Ant旧通知离场删除新提示，真实DOM先红后绿；详细证据见[改密同步记录](verification/20261017-password-csrf.md)。未推送或部署。
