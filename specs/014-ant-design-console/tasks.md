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

## Phase 10: Convergence

2026-10-17复核FR-009/011、US3/AC3–4、账户/页面归属设计及宪章I/III，发现退出反馈HIGH partial：`AccountSettingsTab.logout`在await之后无条件发Toast/跳登录；API/Auth虽会拒绝旧响应，组件仍把归属失效当新页面的退出失败。当前退出网络失败还会清本地身份却停在旧受保护页面，不能把本地退出与服务端撤销混为一谈。原644不含账户页退出交错回归。

- [x] T020 HIGH 为账户页退出补齐单次意图与页面/主机/认证完成归属；正常退出自身清用户不误判旧请求，离页/换账号/换主机的旧结果不串反馈或导航。区分本地身份已清与服务端退出确认：真实网络失败保留原说明，不假报服务端已退出、不自动重放。先以真实Auth/API/Ant表单和延迟HTTP覆盖正常、当前失败、当前401、迟到成功/失败、同ID重登、在途清理及防重，再用生产浏览器复验；涉及`frontend/src/features/settings/tabs/AccountSettingsTab.vue`、`stores/auth.ts`、账户文案及相关测试/文档，追溯FR-009/011/012、US3/AC3–4、plan认证寿命（partial，HIGH）。

20项新增真实模块/组件回归、全量664/664、Web/App类型及构建、54SFC及规范校验通过；7组生产SPA退出先红后绿，原5段认证/3组改密/10组操作/56组页面复验通过，详见[退出完成记录](verification/20261017-logout-scope.md)。仅本地，未推送或部署。

## Phase 11: Convergence

2026-10-17重点复核FR-009/011、US3/AC3–4、认证/主机寿命设计及宪章I/III，发现两个缺口。CRITICAL contradicts：真实ClientConnectionCoordinator内存实验先挂起旧切换、完成新切换、再放旧请求，最终地址回退older.invalid，旧操作清状态/保存并返回新代次2；runtime还在等待旧logout之后才递增真实代次。HIGH partial：LoginView.submit无同步防重及页面/主机意图围栏，await后直接提示/读取可变redirect导航，旧finally无条件解锁。既有664绿不覆盖这些时序。

- [x] T021 CRITICAL 修复主机切换完整链路的意图/代次与认证归属，切换开始即使旧资源失效；旧logout/清会话/持久化完成不得清新会话、覆盖新主机或借用新代次，离页不继续旧探测/导航。先持久化coordinator反序完成红回归，再覆盖runtime真实API/Auth、连接页、并发/重入、当前失败/部分完成、Electron清会话与保存延迟、无效地址零副作用和当前成功；保存失败如实投影已经发生的状态，不盲重放或执行设备命令。涉及`frontend/src/platform/connection.ts`、`stores/runtime.ts`、`features/settings/ServerConnectionView.vue`及连接测试/文档，追溯Constitution I、FR-009/011/012、US3/AC3–4、plan主机寿命（contradicts，CRITICAL）。
- [x] T022 HIGH 为登录页补齐同步防重、冻结凭据/redirect、页面/主机/认证意图与完成归属；离页或旧意图不得在CSRF后继续提交密码、串成功/错误/导航或解锁新提交，当前错误密码仍有原说明。先补实际Auth/API/Ant及延迟HTTP红回归，覆盖同渲染轮重复提交、CSRF/密码响应在途离页与换主机/账号、正常/失败/重试及敏感字段清理，再验证生产SPA。涉及`frontend/src/features/auth/LoginView.vue`、`stores/auth.ts`及认证回归/文档，追溯FR-009/011/012、US3/AC3–4、plan认证寿命（partial，HIGH）。

T021已由51项新增回归使664→715；双构建/主进程编译、54SFC、6组真实连接与原5段认证/3组改密/7组退出/10组操作/56组页面通过。真实后退须先取消旧探测，再由初始化守卫进入新表单，不以URL变化当完成。见[切换验收](verification/20261017-server-switch.md)；T022仍待实现，未推送/部署。

后续T022已完成46项新增回归，全量761/761、双构建/主进程编译、54SFC、8组精确URL+真实媒体控件的生产SPA登录和全部原流程/56组页面通过，见[登录验收](verification/20261017-login-scope.md)。22/22为本机定义任务完成，不是原生或012实体欠账完成；未推送或部署。

## Phase 12: Convergence

2026-10-17按原生边界继续本机补验，发现FR-009/011、US3/AC3与客户端初始化方案的HIGH partial：当前44.2.0实际Electron以独立空资料目录运行生产app://构建，未配置主机却探测由页面协议/主机猜出的`http://scp-cv:8000/api/auth/csrf/`和`status/`，产生6条实际renderer CSP错误。不是网页模拟或将旧打包程序当作当前版本；不放宽CSP/沙箱掩盖错误。此前22/22仅覆盖已定义本机任务，不证明这个未验分支。

- [x] T023 HIGH 修复封装端无主机配置时的自动认证探测和伪地址回退：保持连接页可操作、身份为未知，不向app://派生的假主机发请求，不以编译期默认地址替代用户明确选择；保留网页同源、正常已配主机、受控开发地址与HTTPS策略。先持久化真实app构建模式零派发红回归，再以当前Electron运行时、独立userData/session目录实测生产协议加载、沙箱/受限桥、配置保存读取及Cookie清理；用正常验证的回环HTTPS端点补认证/退出/切换，逐层记录真实系统与测试端点边界。涉及`frontend/src/App.vue`、`services/api.ts`、`stores/auth.ts`、`stores/runtime.ts`及原生/构建测试、安装与验收文档；不碰原用户资料/证书信任、不伪造退出、不发D4/设备命令，追溯FR-009/011/012、US3/AC3–4、Constitution I/III（partial，HIGH）。

14项新增回归使761→775，双构建/主进程编译、54SFC、10组网页操作及56组页面通过。当前Electron44.2.0真实app://冷启动、HTTPS登录/退出、A→B切换、配置IPC保存读取/重载与原生Cookie清理通过；独立资料与正常TLS，无D4/业务写入，详见[原生冷启动验收](verification/20261017-native-cold-start.md)。

## Phase 13: Convergence

2026-10-17继续核对首页侧效应入口，发现CRITICAL contradicts与HIGH partial各一项：Dashboard系统音量使用useThrottledSlider却未提供scope/isActive，旧在途结束后的缓冲仍调用当前Runtime，可能转投新主机/账号；设备Store在旧API被寿命围栏拒绝后仍在catch/finally写新主机的错误/时间，Dashboard音量/电源反馈亦未复核页面与会话。已有775项未覆盖真实首页这些时序，不能据纯Slider支持scope认为调用方已经接线。

- [x] T024 CRITICAL 为首页系统音量滑杆接入真实主机/账号/页面归属及可操作边界，变化时丢弃未发送缓冲，不把旧输入发往新目标；旧错误不提示，正常同域连续输入仍提交最终值。先持久化真实Dashboard/Ant/API延迟HTTP红回归，覆盖主机实际代次与投影、换账号/同ID重登、离页/卸载、切换中、恢复后新输入和正常心跳，再做生产浏览器验证。涉及`frontend/src/features/dashboard/DashboardView.vue`及可选同目录交互模块、`composables/useThrottledSlider.ts`调用接线与`frontend/scripts/`；不撤销已受理写入、不改声卡/设备协议或发现场命令。追溯Constitution I、FR-002/009/011、US3/AC2–4、plan请求归属（contradicts，CRITICAL）。
- [ ] T025 HIGH 为首页电源/静音动作及设备Store结果补齐页面/主机/认证/单次操作归属：旧成功/错误/时间不污染新环境，同设备当前较新操作不被较旧完成反盖；正常成功/当前失败保持原说明，危险确认前后冻结目标，处理中防重且旧finally不解锁新动作。先用真实API/Auth/DeviceStore/Ant补迟到成功/失败、当前401、并发/恢复及重复点击红回归，再做生产浏览器与文档。涉及`frontend/src/stores/devices.ts`、`features/dashboard/`与相关测试，兼顾设置页共用Store但不改物理电源语义；追溯FR-004/009/011/012、US3/AC3–4、Constitution I/III、plan异步归属（partial，HIGH）。

T024已完成20项新增真实首页回归（初轮7绿13红→全绿），全量795/795、双构建、54SFC、8/5/3/7/6组账户/连接流程、10组操作及56组页面通过；实际.NET ControlHost Simulation+Electron正常TLS验证受控会话恢复丢旧缓冲，并完成真实改密/新密码登录/退出及Cookie清理。见[首页音量与实际后端验收](verification/20261017-dashboard-volume.md)。T025仍待实现，未推送/部署。
