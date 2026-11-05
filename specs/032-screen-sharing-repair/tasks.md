# Tasks: 屏幕共享连接与折叠入口

## Phase 1: 准备

- [X] T001 检查 `.specify/memory/constitution.md`、既有共享及验证边界。
- [X] T002 完成 `specs/032-screen-sharing-repair/` 规范、方案、合同和需求检查。

## Phase 2: US1 共享入口

- [X] T003 [P] [US1] 在 `frontend/src/features/display/DisplayControlView.vue` 完善共享整栏、箭头、焦点及窄窗口入口。
- [X] T004 [US1] 在 `frontend/scripts/` 增加本地真实浏览器布局检查，验证折叠不改变共享所有者。

## Phase 3: US2 共享诊断与修复

- [X] T005 [P] [US2] 在 `.validation/share-032/` 通过 10.88.50.194 收集 D4 命令与连接证据。
- [X] T006 [US2] 根据实际复现修复相应共享模块，并在 `specs/032-screen-sharing-repair/research.md` 记录原因。
- [X] T007 [US2] 在 `runtime-dotnet/src/ScpCv.ControlHost/ScreenSharing/` 和 `frontend/src/features/screen-share/` 配置并传递标准 ICE 服务，保留空配置的局域网直连行为。
- [X] T008 [US2] 在 `tools/screen-share-stun/` 实现成熟 Pion 服务的启动入口，验证真实 UDP Binding 和端口释放。
- [X] T009 [US2] 使用真实媒体、原生连接及正式 API 验证共享及资源释放。

## Phase 4: 验证与文档

- [X] T010 运行前端类型、生产构建与规范验证，在 `specs/032-screen-sharing-repair/verification.md` 记录实际结果。
- [X] T011 更新 `docs/使用文档.md` 和 `docs/CHANGELOG.md` 的共享行为。
- [X] T012 在 `.validation/release-032/` 保存部署清单、状态及冷备，更新 D4 并记录正式接口持续共享与清理证据。

## Dependencies and Execution

T001 → T002；T003 → T004；T005 → T006 → T007 → T008 → T009；随后完成 T010、T011、T012。US1 界面与 US2 诊断可并行。交付包含两项故事及真实验证。
