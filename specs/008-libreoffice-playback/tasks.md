# Tasks: LibreOffice完整接入

**Input**: spec/plan/research/data-model/contracts/quickstart；先保留549c57f保存点。

## Phase 1: Setup

- [X] T001 保存当前Git提交/标签与忽略实验代码归档，记录于 `research.md`，保留原件/数据库。
- [X] T002 在 `spec.md` 明确全部LibreOffice/退役PowerPoint及本机范围，requirements清单16/16。
- [X] T003 [P] 在 `.validation/libreoffice-integration-spike/` 证明.NET10旧CLI不兼容，并验证纯C#Net48官方绑定连接自有profile；失败日志保留。

## Phase 2: Foundational

- [ ] T004 在 `.validation/libreoffice-integration-spike/` 用纯C#正常Frame证明去菜单/导航、只读、动画与真实媒体pause/resume；若二进制接口不能满足则说明阻断，禁止用Python/源码fork偷偷替代。
- [X] T005 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/ShutdownPendingIntentTests.cs` 及独立回归先红，覆盖两窗旧Pending、音频、重复停机、迟到结果和最后观测。
- [X] T006 在 `runtime-dotnet/src/ScpCv.Infrastructure/Runtime/RuntimeAuthorityRepository.cs` 和partial修复确认停机后的Pending/Processing退役及投影，保留Actual/Observed，针对性19/19、加入排他后完整非Physical576/576；见 `verification.md`。
- [X] T007 在 `runtime-dotnet/src/ScpCv.ControlHost/Endpoints/SystemEndpoints.cs` 修复重复Stopped仍入队CLOSE及重启过早确认：先真实stop成功，再CompleteStop/BeginStart/start；失败不启动新组，合同4/4红→绿、相关15/15已通过。独立复核追加 `RuntimeSystemOperationGate.cs` 和 `SystemOperationConcurrencyTests.cs`，真实HTTP并发4/4红→绿，第二个启停请求在任何状态/外部操作前409拒绝，包含完成后再试，相关19/19、全576/576。原生stop超时后迟到操作仍需T008/T010单独身份围栏，不能据此宣称全部并发安全。
- [ ] T008 在 `runtime-dotnet/src/ScpCv.Presentation.Protocol/` 建立Net10/Net48可用纯值协议和身份/代次/完成指纹回归，不携带UNO/WPF对象。

## Phase 3: User Story 1

- [ ] T009 [US1] 在 `runtime-dotnet/src/ScpCv.LibreOfficeHost/` 以官方CLI实现只读自有profile/引擎/文稿/窗口，声明依赖配置与发现，不触碰用户实例。
- [ ] T010 [US1] 在 `runtime-dotnet/src/ScpCv.Supervisor/Runtime/`、ControlHost/Ipc与状态文件加入impress1/2及converter角色、immutable身份、协作退出和精确子进程证明。
- [ ] T011 [US1] 在 `runtime-dotnet/src/ScpCv.PlayerWorker/Presentations/` 实现fenced adapter与Frame承载，补打开/失败留旧画面/关闭/重开/晚结果回归，拆分近500行宿主。
- [ ] T012 [US1] 在 `PresentationPlaybackSelector.cs`、OPEN参数、Snapshot/Progress及投影接入LibreOffice默认，PDF/页图显式模式，明确真实confirmed与desired。
- [ ] T013 [US1] 在导航/媒体合同与adapter中实现动画步/跳页、统一媒体暂停恢复、mute/volume与真实位置；未验证动作明确unsupported而非假成功。

## Phase 4: User Story 2

- [ ] T014 [US2] 在 `ScpCv.ControlHost/Media/PptConversionHostedService.cs` 及Presentations/移除PowerPoint转换，LibreOffice独立converter导出有序PNG/匹配PDF与页数，不改原件。
- [ ] T015 [US2] 在 `ScpCv.Infrastructure/Media/` 解耦原件/native可用与静态准备，保留摘要/修订关联、uncertain手动确认、活跃文稿关闭证明前禁止移动/删除。
- [ ] T016 [US2] 在上传/prepare/移动tests补原件、预览失败、转换晚到、中文文件夹与关闭在途，原件SHA和锁保持/释放真实。

## Phase 5: User Story 3

- [ ] T017 [US3] 在本机 `.validation/` 完成用户LibreOffice共存、两窗同/异源10轮切换关闭重开、100控制和精确PID/文件锁，真实像素/媒体与状态分开。
- [ ] T018 [US3] 在Host/adapter/pipe tests及本机进程注入验证崩溃/断线/超时/取消/晚到/关闭失效，禁止全局kill和盲重放。
- [ ] T019 [US3] 在本机至少60分钟双窗混合/替换观察资源/画面/媒体/退出，已知严重缺陷先修再同条件复跑，D4仍待授权。

## Phase 6: User Story 4

- [ ] T020 [US4] 在前后端/settings与OpenAPI退役PowerPoint/reset-ppt旧COM入口，新增presentation依赖/能力/显式模式，并清除运行时PowerPointHost启动和调用。
- [ ] T021 [US4] 在 `frontend/src/features/display/`、sources/settings抽独立presentation controls/capabilities，动态下一步/翻页/媒体与静态fallback不同，桌面/手机真实回归。
- [ ] T022 [US4] 更新 `docs/openapi.yaml`及schemas/paths，Redocly校验；settings/source/session/prepare状态与实际实现一致。

## Final

- [ ] T023 更新 `README.md`、使用/维护/CHANGELOG/known-pitfalls及安装/运行脚本；治理约束同步作为独立constitution步骤，不改历史实测结论。
- [ ] T024 全.NET非Physical/相关Physical、pnpm测试/类型/Web构建、真实UI/Spec/Redocly，记录 `verification.md`，未执行原因明确。
- [ ] T025 收尾自有进程/窗口/profile监听，原件/数据库保留；按小块提交，仅用户另行要求时部署/推送。

## Dependencies & Parallel

保存点/规范→interop门禁与停机安全→协议/宿主→两窗播放/转换/控制台→稳定/完整验证。T005–T007不依赖尚未通过的媒体门禁，可独立修复必要安全前置；核心宿主/默认路由须T004通过或明确用户调整后推进。不同目录可并行；所有.NET构建由root协调，真实桌面操作错开。

## Implementation Strategy

先解决外部能力与生命周期，不删旧入口制造无法播出的中间版本。各门禁失败保持任务未完成，报告真实原因；不能把设置/DTO/禁用按钮当完整引擎接入完成。
