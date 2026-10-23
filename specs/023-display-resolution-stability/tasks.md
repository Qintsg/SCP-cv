# Tasks: D4四路4K模式稳定性

## 当前调查

- [x] T001 在specs/023-display-resolution-stability/spec.md明确用户范围及验收条件。
- [x] T002 [US1] 在specs/023-display-resolution-stability/verification.md记录四路系统/信号/611A实际模式与控制桌面保持。
- [ ] T003 [US2] 在specs/023-display-resolution-stability/research.md取得回退附近的显卡、Windows和相关远程软件证据，确认具体触发条件。
- [ ] T004 [US2] 根据确证原因完成所需修正，更新specs/023-display-resolution-stability/plan.md并复验原触发动作，记录实际结果。
- [x] T005 在docs/维护文档.md、docs/input-resolution-021.md和specs/023-display-resolution-stability/verification.md更新现场状态及验证边界。
- [x] T006 [US2] 按已授权范围临时停止并恢复D4的UU远程，在specs/023-display-resolution-stability/verification.md记录进程身份、停止/启动前后四路实际尺寸、ETW事件与业务保持；未复现时保留T003/T004未完成。
- [x] T007 [US2] 在specs/023-display-resolution-stability/verification.md记录2026-10-23的活动/CCD/GDI布局、有限ETW采集完整性及当前配置校验事件，核对位置覆盖与原进程身份。
- [x] T008 [US2] 在specs/023-display-resolution-stability/verification.md记录单次相同配置提交的调用者、模式事件和后续位置，确认采集能识别真实提交；不将未复现记为原因已排除。
- [x] T009 [US2] 在specs/023-display-resolution-stability/verification.md记录当前静音空闲窗口2的正常视频和LibreOffice打开/关闭对照，联合ETW与显示读回核对GPU和模式变化；恢复本次空闲状态。
- [x] T010 [US2] 在specs/023-display-resolution-stability/verification.md记录实时只读记录器的10秒完整退出验证，启动一次限时一小时的单次记录；保存任务/进程/资源上限和取证位置，不创建自动显示恢复。
- [ ] T011 [US2] 在specs/023-display-resolution-stability/research.md及verification.md读取限时记录实际触发、调用进程、完成文件与前后布局；核对原句柄及任务/事件会话清理，将结果用于T003归因。

## Dependencies & Execution Order

T001→T002→T006，T003取得归因后执行T004；T005随实际调查更新状态。实体修改必须先有归因和对应授权，不以常驻重设替代原因处理。
