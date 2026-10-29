# Tasks: 窗口维护

**Input**: [spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[contracts/window-foreground.md](contracts/window-foreground.md)

## Phase 1: Contracts

- [x] T001 完成规范、只读接线研究与副作用路径核对。
- [x] T002 在Domain实体、Contracts、EF迁移及前端apiTypes中加入窗口观测。

## Phase 2: User Story 1

- [x] T003 [US1] 在PlayerWindow及PlayerRuntimeHost实现窗口排序、真实快照、空闲采样与隐藏保持。
- [x] T004 [US1] 在CommandRepository/DisplayCommandPolicy保留窗口意图，RuntimeStateService及HTTP端点添加批量控制。

## Phase 3: User Story 2

- [x] T005 [US2] 在RuntimeProjectionPublisher和RuntimeAuthorityRepository实现观测顺序、新所有者清理及独立发布。
- [x] T006 [US2] 在Web EmergencyMenu及组合函数接入实际状态、处理中状态、错误反馈与主机账号归属。

## Phase 4: Validation

- [x] T007 在runtime-dotnet/tests完成真实窗口、HTTP、队列与投影验证，并只读核对真实Web页面。
- [x] T008 更新docs/openapi.yaml、使用/维护文档及known-pitfalls路径清单，完成构建和规范校验，记录verification.md。

依赖：T001→T002→T003/T004/T005→T006→T007→T008。

## Phase 5: Deployment

- [x] T009 提交并推送两个仓库；按9f23702生成完整运行包及网页清单。
- [x] T010 在D4协作停机、冷备数据库、切换已核验制品及启动入口，恢复原来源并验证实际隐藏／显示。
- [x] T011 更新deployment.md及维护文档，核对双仓与D4源码、数据、显示配置及服务状态。
- [x] T012 优化runtime-dotnet/scripts/start-desktop.ps1及D4桌面快捷方式，核验配置和重复启动保持进程身份。
