# Tasks: 本机免费文稿引擎评估

**Input**: spec.md、plan.md、research.md、contracts/probe.md。

## Phase 1: Setup

- [X] T001 在 `specs/007-ppt-engine-evaluation/` 记录隔离范围，前后hook不存在，保留006和现有工作区。
- [X] T002 在 `.validation/ppt-engine-eval/fixtures/` 盘点原件摘要/内容覆盖和同源参考图；不改原件。

## Phase 2: Foundational

- [X] T003 在 `contracts/probe.md` 明确动作/页码/不支持和证据边界；独立profile/回环端口。
- [X] T004 在 `plan.md` 记录本机依赖和许可证预算，验证 `.validation/` 已忽略。

## Phase 3: User Story 1

- [X] T005 [P] [US1] 在 `.validation/ppt-engine-eval/web/` 固定vanilla依赖，构建公开API诊断页与Chromium内容/媒体证据。
- [X] T006 [P] [US1] 在 `.validation/ppt-engine-eval/uno/` 验证LibreOffice/UNO实际内容、动画/媒体控制及.NET承载能力。
- [X] T007 [US1] 在 `.validation/ppt-engine-eval/webview/` 用真实WPF WebView2验证同页、动画、媒体和截图（首轮已跑，动画中间态不冒充完整保真）。
- [X] T008 [US1] 在 `verification.md` 汇总原件页图/真实截图的独立视觉审查，明确覆盖缺口。

## Phase 4: User Story 2

- [ ] T009 [P] [US2] 在 `.validation/ppt-engine-eval/webview/` 与 `web/` 记录有界重复导航/加载/资源/坏源恢复，以及两窗同/异源、A交替两文稿关闭重开、B实际状态不变，不放宽失败；WPF未运行不能由Chromium代验。
- [X] T010 [P] [US2] 在 `.validation/ppt-engine-eval/uno/` 记录双profile/宿主同/异源、10轮两文稿替换/关闭重开、另一窗页码/媒体、文件锁和退出；失败保留。
- [X] T011 [US2] 在 `verification.md` 对内容、控制、承载、隔离、恢复和部署给出稳定化难度，不按代码行数判断，WPF10轮待跑边界保留。

## Phase 5: User Story 3

- [ ] T012 [US3] 在 `verification.md` 核原件摘要、自有PID/端口清理、生产/006未改，指针恢复006。

## Final

- [ ] T013 核 `specs/007-ppt-engine-evaluation/` 规范格式/编码；交付本机事实及未验证风险，不推送或部署。

## Dependencies & Parallel

Setup→Foundational→两引擎独立内容→各自生命周期→对比与收尾。Web与UNO不同目录可并行，真实桌面窗口错开；同引擎原生资源不跨进程传递。

## Implementation Strategy

先有真实图像和状态，再做有界稳定性。失败可以完成评估任务，但不能把失败引擎标为通过。原型不纳入生产，报告保留复核入口与证据。
