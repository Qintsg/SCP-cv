# Tasks: PowerShell 7与D4横向显示排列

## Phase 1: Setup

- [x] T001 在specs/024-pwsh-display-layout/spec.md及checklists/requirements.md明确安装、五屏与不启动CV边界。
- [x] T002 在specs/024-pwsh-display-layout/plan.md、research.md、data-model.md和quickstart.md完成设计与验证方式。

## Phase 2: Foundational

- [x] T003 在.validation/pwsh-display-024/保存D4任务、桌面入口、配置及进程基线，并核对官方制品。

## Phase 3: User Story 1 - 项目PowerShell 7入口

- [x] T004 [US1] 在runtime-dotnet/scripts/start-deployed.ps1和run-headless.ps1明确7.2要求及Detach入口，实际验证解析和旧版拒绝。
- [x] T005 [US1] 在D4安装核验过的PowerShell7，实际检查签名、版本、CIM、ScheduledTasks及小文本序列化，记录specs/024-pwsh-display-layout/verification.md。
- [x] T006 [US1] 更新D4当前桌面CMD、release任务、startup.json摘要与维护解释器，在不启动任务时通过CheckOnly，记录verification.md。

## Phase 4: User Story 2 - 五屏横向排列

- [x] T007 [US2] 在.validation/pwsh-display-024/准备有身份校验及资源约束的单次脚本，实际读取原交互会话五屏。
- [x] T008 [US2] 在D4提交1至5连续横向位置并读回桌面及信号，查询63–66实际4K，记录verification.md。
- [x] T009 [US2] 核对显示维护期间CV未启动、临时任务和进程已清理、原配置与现存进程保持，记录verification.md。
- [x] T011 [US1] 按2026-10-23授权，在显示维护完成后通过新版入口启动CV，核验唯一进程组、两窗在线及网页访问，并复查显示位置，记录verification.md。

## Final Phase: Documentation

- [x] T010 在docs/维护文档.md、docs/input-resolution-021.md和specs/024-pwsh-display-layout/verification.md记录实际状态及边界，执行规范、格式校验。

## Dependencies & Execution Order

T001→T002→T003；T004与发行研究可并行，T005→T006→T007→T008→T009→T011→T010。远端写入串行执行；显示维护期间不运行CV，后续只执行已授权的项目启动测试。

## Parallel Opportunities

发行研究与脚本入口审查可以并行；源文件维护不与现场脚本或规范重叠。两个场景分别实际验证，完成全部范围后交付。
