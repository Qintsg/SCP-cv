# Tasks: D4 大屏实机收敛与优化

**Input**: [spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[contracts/validation-contract.md](contracts/validation-contract.md)、[quickstart.md](quickstart.md)

**Tests**: 本功能明确要求自动化、浏览器和 D4 实体证据。每类缺陷先复现/写回归，再修复与同条件复测。现场素材、截图、数据库快照和凭据只放忽略目录。

## Phase 1: Setup

**Goal**: 固定当前状态与可恢复的 D4 基线。

- [X] T001 在 `specs/006-d4-live-convergence/verification.md` 记录本机与 D4 提交、工作区、D4 项目进程/端口、交互会话、显示器和电源可读基线；区分只读 SSH 枚举与实体拓扑。
- [X] T002 在 D4 `D:\SCP-cv\.validation\t129-workstation/` 停机条件下，对 `control.db`、`control.db-wal`、`control.db-shm` 及媒体目录清单做同组快照并记录摘要到 `verification.md`，先核对绝对路径，不清空用户数据。
- [X] T003 [P] 更新 `runtime-dotnet/scripts/benchmark-commands.ps1` 的默认和校验目标为窗口 1/2，补对应脚本测试；同步 `docs/qa/003-workstation-runbook.md` 的当前两窗/进程数/启动方式，旧四窗结果明确标记历史。

## Phase 2: Foundational

**Goal**: D4 与本机版本一致，交互桌面中的两窗运行组可被安全启停和观察。

- [X] T004 在 D4 `D:\SCP-cv` 从内网 GitLab 以快进方式取得当前 `main`，确认本机与 D4 HEAD 一致、工作区无意外改动；D4 执行 .NET locked restore/build 与 `pnpm` 前端构建，结果写入 `verification.md`。
- [X] T005 在 D4 使用 `runtime-dotnet/scripts/run-headless.ps1` 先启动无 Worker 的 Hardware ControlHost，核对 DataRoot 迁移、`/health/ready`、`/api/displays/` 的交互会话真实设备名和窗口 1/2 绑定；失败先停止并诊断，不直接开全部 Worker。
- [X] T006 在 D4 通过协作重启启动仅 player1/2 的运行组，核对 PID/启动时间/epoch、两个会话及控制桌面未被占用；增加 `runtime-dotnet/tests/ScpCv.Integration.Tests/` 的启动/停机回归（如发现缺口）。
- [X] T007 [P] 在 D4 `.validation/` 建立只记录本轮窗口 1/2 的画面、会话、命令、进程内存/句柄和日志的验证脚本或清单；证据格式遵守 `contracts/validation-contract.md`，不提交媒体或截图。

## Phase 3: User Story 1 - 两块大屏可靠播出 (P1)

**Goal**: 两窗多源与两个固定预设在实体画面、状态和进程上相符，直播源能从页面登记。

**Independent Test**: 在 D4 窗口 1/2 分别开/关图片、视频、网页、PDF、PPT 页图及直播；切换两个固定预设后恢复，未知布局只预览不下发。

- [X] T008 [P] [US1] 在 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/MediaEndpointTests.cs` 与 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/` 先写直播源登记、URL/协议校验、错误状态及修改回归；在 `frontend/scripts/` 补添加源类型合同测试。
- [X] T009 [US1] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/MediaSourceService.Streams.cs` 和 `runtime-dotnet/src/ScpCv.ControlHost/Endpoints/MediaEndpoints.cs` 实现受认证 RTSP/SRT/自定义流登记与更新，复用现有 `MediaSource`，明确区分“可尝试连接”与“已出画”。
- [X] T010 [US1] 在 `frontend/src/features/sources/AddSourceDrawer.vue`、`frontend/src/stores/sources.ts`、`frontend/src/services/api.ts` 增加直播源登记和失败反馈；更新 `docs/openapi.yaml` 及分拆合同，保留两块大屏为唯一播放目标。
- [ ] T011 [US1] 在 D4 交互桌面对窗口 1/2 逐类执行打开、导航/循环或进度、关闭，交叉核对截图、会话、命令完成及目标 PID；把每项结论写入 `verification.md`。
- [ ] T012 [US1] 在已获准的拼接屏电源条件下对 D4 执行两个既有预设的实体映射并恢复，保存左右/全屏画面证据；手动笔记本/IP 布局仅保存预览并核对零未知帧下发。
- [ ] T013 [US1] 在 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/LegacyHttpContractTests.cs` 与 D4 请求中复核窗口 3/4 全入口拒绝，确保本轮直播源或显示器修改不重新引入旧窗。

## Phase 4: User Story 2 - PPT 原件转换与页图播出 (P1)

**Goal**: D4 上传真实文稿可得到有序页图，默认不放映，实验模式只操作归属明确的 Office。

**Independent Test**: 多页 PPT 上传、页图、翻页与关闭在窗口 1/2 均有实体证据；打开/关闭实验开关时核对 Office PID 和用户文稿。

- [X] T014 [P] [US2] 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/PptConversionHostedServiceTests.cs` 与 `runtime-dotnet/tests/ScpCv.Windows.Tests/PowerPointOwnershipTests.cs` 补转换失败、停止、原件移动、用户 Office 并存和放映归属非 Physical 回归；真实 COM 证据由 T016/T017 承接。
- [X] T015 [US2] 在 D4 使用 `C:\Users\qintsg\Desktop\Resources` 的只读副本或等价测试文稿上传，核对原件 SHA-256、页数、PNG 顺序和图片端点；在窗口 1/2 默认模式验证第一页、前后/指定页和无 Office 放映窗口。
- [ ] T016 [US2] 在 D4 显式启停 `PowerPointSettingsService` 的实验模式，验证两个目标窗口的原生放映、关闭、失败状态与用户文稿保护；定位真实 Office/HWND/STA 缺陷并在 `ScpCv.PowerPointHost/Interop/`、`ScpCv.ControlHost/Ipc/` 修复。
- [ ] T017 [US2] 在 `runtime-dotnet/src/ScpCv.Supervisor/Runtime/ShutdownCoordinator.cs` 与 `ScpCv.PowerPointHost/Interop/PowerPointComAdapter.cs` 核对项目 Office 自有实例的协作退出、超时和共享用户文稿保护，并把 PID/启动时间与残留证据写入 `docs/qa/003-office-interop.md`。
- [ ] T018 [US2] 在 `frontend/src/features/sources/`、`frontend/src/features/settings/` 与真实浏览器验证转换状态、重试、默认页图与实验警告，不以 Simulation 的排队态代替 D4 转换成功。

## Phase 5: User Story 3 - 媒体与文件夹安全整理 (P1)

**Goal**: D4 页面目录与实体路径一致，移动/删除故障不造成无记录原件或假成功。

**Independent Test**: 根目录和两级中文目录的重名上传、源/目录移动、下载 SHA-256、页图关联及活跃文件拒绝全部通过。

- [X] T019 [P] [US3] 在 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/MediaStorageLayoutTests.cs` 和 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/MediaEndpointTests.cs` 写单源删除文件占用/拒绝访问的失败回归，先证实现有“删库成功、文件残留”问题。
- [X] T020 [US3] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/MediaSourceService.cs` 与相应 partial 文件中修复单源删除的文件/数据库一致性和可诊断清理语义，避免吞掉文件删除错误；补同条件复测。
- [X] T021 [US3] 在 D4 `D:\SCP-cv\.validation\t129-workstation\media/` 执行根目录、中文子目录、重复文件名、源移动与文件夹移动，逐步核对 D4 物理路径、下载摘要、旧路径及页面刷新。
- [X] T022 [US3] 在 D4 对已准备 PPT 先移动原件再验证页图、下载和再次播放；正在播放/转换时尝试移动，确认拒绝且原件与数据库仍一致。
- [ ] T023 [US3] 在 `frontend/src/features/sources/SourcesView.vue` 及本机/D4 浏览器对目录树、真实路径、冲突、等待与错误反馈做桌面和手机布局回归，修复发现的可用性问题。

## Phase 6: User Story 4 - 长稳与故障恢复 (P1)

**Goal**: 修复已知假成功和媒体稳定性缺口，用长时间真实画面及资源数据证明两窗可靠。

**Independent Test**: 预案音量、流解码、视频资源、Office 归属与 60 分钟混合场景均有可复现前后对照；两窗 1000/100 基准无旧窗请求。

- [X] T024 [P] [US4] 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/HostHardwareIntegrationTests.cs` 与 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/` 写预案音量真实控制器调用/失败不落库回归；只做无物理副作用的自动化故障注入。
- [ ] T025 [US4] 在 `runtime-dotnet/src/ScpCv.Infrastructure/Scenarios/ScenarioService.cs` 修复预案音量只落库的假成功，复用硬件控制边界并处理墙面、音量与状态的失败顺序；若获准，在 D4 记录并恢复系统音量后实体复测。
- [ ] T026 [P] [US4] 在 D4 `.validation/` 用受控 RTSP/SRT 源做独立解码与窗口 1/2 实体播放，对旧报告的损坏帧和 `INVALID SIZE` 告警定位到发布端、MediaMTX、传输或 VLC；在相关 `runtime-dotnet/src/ScpCv.Infrastructure/Streams/`、`ScpCv.PlayerWorker/` 或配置中修复并复测。
- [ ] T027 [P] [US4] 在 `runtime-dotnet/tests/ScpCv.Windows.Tests/` 为视频自然结束、循环、同源重开与释放增加回归；D4 重做短视频重复切源并采集私有内存、句柄、画面帧变化，定位旧增长。
- [X] T028 [US4] 在 `runtime-dotnet/src/ScpCv.PlayerWorker/Adapters/` 与 `Playback/PlayerRuntimeHost.cs` 修复 T027 证实的视频资源保留或状态假成功，并在 D4 同条件复测。
- [ ] T029 [US4] 用已改成两窗的 `runtime-dotnet/scripts/benchmark-commands.ps1` 在 D4 执行普通写命令 1000 样本和健康热切换 100 样本，记录 p95/最大/失败数及原始证据到 `verification.md`。
- [ ] T030 [US4] 在 D4 运行至少 60 分钟窗口 1/2 混合媒体与重复切源，按分钟记录画面、会话、命令、资源、Office/MediaMTX 与流错误；发现问题先诊断修复再同场景复跑。
- [ ] T031 [US4] 在 D4 测试播放器失联、源文件暂失、转换失败及协作重启/停机，核对旧命令不重放、两个播放器退出、状态文件与日志如实更新。

## Final Phase: Polish & Cross-Cutting

- [ ] T032 [P] 更新 `README.md`、`docs/使用文档.md`、`docs/维护文档.md`、`docs/known-pitfalls.md`、`docs/CHANGELOG.md` 与 `docs/qa/003-workstation-runbook.md`，区分当前两窗 D4 实机结论和历史四窗记录。
- [ ] T033 在本机运行 .NET 非 Physical/相关 Physical、前端 `pnpm test`/`typecheck`/Web 构建、桌面/手机真实浏览器、Spec Kit 与 Redocly；D4 运行可用的对应测试，逐项记录命令与未执行原因到 `verification.md`。
- [ ] T034 在 D4 协作停机并核对精确项目 PID/端口/Office、墙面预设、电源和系统音量恢复测试前状态；仅清理本轮已登记测试数据，原始媒体和完整快照保留。
- [ ] T035 对 `spec.md`、`plan.md`、`tasks.md` 与实际结果执行规范收敛/一致性复核，未满足项继续追加并实现；不得把未知墙面帧或缺失实机证据标为完成。
- [ ] T036 检查 `git diff --check`、文件头/行数、忽略数据与敏感信息；按独立可审查块提交并推送既有 `origin`、`gitlab`，记录提交与剩余外部依赖到 `verification.md`。
- [X] T037 在 `runtime-dotnet/scripts/runtime.ps1` 补独立入口的明确状态路径、两个显示器参数及缺参拒绝回归，或明确退役已被 `run-headless.ps1` 与认证 API 替代的启动入口；现有脚本不能继续宣称可直接启动当前两窗运行组。
- [X] T038 在 `frontend/src/features/sources/` 与 `frontend/src/layouts/EmergencyMenu.vue` 修复 Dropdown DOM props 与 onSelect 双重派发及禁用绕过；以真实 Naive UI DOM 加受控动作回调先红后绿验证鼠标/键盘各一次、禁用零次，再继续 D4 目录浏览器矩阵，不在测试中点击真实应急电源入口。
- [X] T039 依据 FR-004/FR-005/FR-008 在 `runtime-dotnet/src/ScpCv.Infrastructure/Media/MediaSourceService.Mapping.cs` 及独立公开接口回归修复旧 PPT 缺页图仍投影可用的问题，保持已准备页图/PDF/流及实验模式语义；D4 通过公开接口显式重试恢复旧有效原件并核对摘要，不删除原件或静默批量迁移。
- [X] T040 依据 SC-006 在 `frontend/src/App.vue` 及通知布局相关组件修复手机稳定帧通知遮挡底栏/编辑抽屉按钮；复用导航/安全区令牌并保持桌面布局，以真实 DOM 回归和 D4 浏览器几何/截图复测，不将图片加载或过渡态黑块误报为播放失败。
- [X] T041 依据 FR-007/FR-008 在 `runtime-dotnet/src/ScpCv.Contracts/Runtime/RuntimeWorkerSession.cs` 与公共 Named Pipe 回归消除调用方取消/服务端退出竞态，保持服务端协作 shutdown 正常完成、调用方取消契约明确，多轮及全套验证不可仅放宽断言。
- [X] T042 依据 FR-008/SC-006 在 `runtime-dotnet/tests/ScpCv.ControlHost.Tests/` 的测试工厂验证 SQLite pool 清理隔离，修复并行 Open/Dispose 偶发对象已释放；只清理自有池和精确临时目录，不串行化或跳过全套测试掩盖问题。

## Dependencies & Execution Order

- Setup → Foundational → US1/US2/US3 的独立功能矩阵 → US4 长稳/故障 → Final。D4 部署基线与两窗落位先于任何实体播出；需要拼接屏实体画面的 T012 等待明确授权。
- US2 的 Office 转换可在 US1 的图片/视频矩阵之后单独验收；US3 文件操作必须避开正在使用的源，并在 US2 有准备页图时验证关联。
- US4 的音量、流和视频修复可在各自回归先失败后独立推进；长稳 T030 应在这些已知高风险问题得到处理后执行。
- 旧 003/T115、T116、T129、T133、T137 的未闭环项由 T017、T024–T031 承接，但旧四屏门禁不能原样拿来当两窗结论。

## Parallel Opportunities

- T003 的两窗脚本/文档与 T002 的 D4 快照路径不同；T008 的直播合同测试和 T014 的 Office 故障测试在不同文件可并行。
- T019 的文件删除回归、T024 的预案音量回归、T027 的视频生命周期回归相互独立；实体 D4 操作仍按同一运行组顺序执行，避免互相抢画面。

## Implementation Strategy

1. 先取得可恢复 D4 基线并让窗口 1/2 实体出画，完成 US1 最小可用验收。
2. 逐一完成 PPT、实体媒体目录和已知缺陷的针对性回归；每个独立块小提交。
3. 最后执行两窗基准和 60 分钟混合门禁、真实浏览器 QA、停机恢复及文档/远端同步。未被证据证明的要求保持未完成。
