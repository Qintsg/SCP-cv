# 已知坑与硬件副作用路径清单

**用途**：

- 新增或改动「会改变现场物理状态」的入口前，先对照 §1 的路径清单，确认触发入口、下游部件和验证位置都在。
- 修完一类坑（静默漏迁、假成功、测试误判）后，到 §2 追加条目：症状 → 根因 → 检出方式 → 现状。

相关沉淀点见 §3，本文不重复记录那些内容。

## 1. 硬件副作用路径清单

**判据**：这条路径会改变开发机不存在、只在现场存在的物理状态——墙面、声卡、显示器、设备电源、Office 放映、进程组。

**为什么要列这张表**：Simulation 模式用静默空实现顶替硬件（`runtime-dotnet/src/ScpCv.ControlHost/Program.cs:100-131`），接线断了和接线正常在开发机上表现完全一致。2026-09-19 的视频墙缺陷就是这样漏过去的：包构造有测试、常量齐全，但**没有任何生产调用方**。所以每条路径都必须有「Hardware 装配测试」或「包级/时序测试」兜底，只靠 Simulation 联调不算验证。

| 能力 | 触发入口（HTTP） | 下游部件（Hardware） | Simulation 替身 | 验证位置 |
|---|---|---|---|---|
| 拼接屏画面映射（视频墙） | `PATCH /api/runtime/`；`POST /api/scenarios/{id}/activate/` | `RuntimeStateService.cs:70`、`ScenarioService.cs:187` → `IVideoWallController` → 50 节点 `192.168.5.101~150:4830`（清屏/映射/提交/刷新） | `SimulationVideoWallController`（只校验模式，不发包） | `VideoWallControllerTests`、`VideoWallSequenceTests`、`HostHardwareIntegrationTests`、`VideoWallRegistrationTests`、`VideoWallLoggingTests`（成功/失败/重试/取消的日志）；**物理画面未实机验证**（见 `specs/004-video-wall-control/spec.md` SC-007）；排查入口 `docs/维护文档.md` §8.6 |
| 系统音量 / 系统静音 | `PATCH /api/volume/` | `RuntimeStateService.cs:159` → `ISystemAudioController.Apply` → `WindowsCoreAudioController` | `SimulationSystemAudioController` | `GetSystemVolumeAsync` 返回 `system_synced`；**预案激活只落库不推硬件**，见坑 2 |
| 显示器拓扑与落位 | `GET /api/displays/`、`POST /api/displays/select/` | `IDisplayTopologyProvider` → `WindowsDisplayTopologyProvider`；`POST /api/system/restart/` 后由 `ReapplyDisplayTargetsAsync`（`RuntimeStateService.cs:104`）按已保存目标恢复落位 | `SimulationDisplayTopologyProvider` | `ScpCv.Windows.Tests`、`specs/003` verification 的 D4 记录 |
| 播放器窗口内容（四窗 / 置顶 / 物理像素铺满） | `POST /api/playback/{windowId}/open\|close\|control\|show-ids\|reset-all/`、`PATCH .../volume/`、`.../mute/`、`.../loop/`；切换大屏模式后的固定静音策略也走这条链路（`RuntimeStateService.cs:91-94` 逐个 `SetMuteAsync`） | 持久命令队列 → Named Pipe → `PlayerWorker`（WPF + VLC + WebView2） | `QueuedCommandWakeNotifier`（仅排队，不启动 worker） | `ScpCv.Integration.Tests`（simulation 跨层）、`docs/qa/003-*.md` |
| PowerPoint 放映（唯一 STA/COM 槽） | `POST /api/playback/{windowId}/navigate/`、`.../ppt-media/`、`/playback/reset-ppt/`、`GET\|PUT /sources/{id}/ppt-resources/` | 命令队列 → `PowerPointHost` 进程（唯一 STA、PDF 回退） | 无（Hardware 才有该进程） | `docs/qa/003-office-interop.md`、`docs/实机测试结论.md` |
| 背景音频（独立列表） | `POST\|PATCH\|DELETE /api/background-audio/*` | `BackgroundAudioService` → 命令队列（`CommandTargetKind.Audio`）→ Named Pipe → `AudioWorker` | `QueuedCommandWakeNotifier`（仅排队） | `docs/实机测试结论.md` 背景音乐段 |
| 设备电源 | `POST /api/devices/{type}/power/{action}/`、`.../toggle/` | `DeviceService` → `IDeviceCommandTransport` → `TcpDeviceCommandTransport`：拼接屏 `192.168.5.10:8889`、电视左 `.161`、电视右 `.162`（只写不读，不保存状态） | `SimulationDeviceCommandTransport` | 未实际发送关机（`docs/实机测试结论.md` §3「外部设备电源」）；`appsettings.json` §Devices |
| MediaMTX 推拉流 / 直播 | `POST /api/sources/local\|web/`、`GET /sources/{id}/`（在线探测） | MediaMTX 进程（流端口 `8890`/`9997`/`8554`）、`stream-probe` HttpClient | 无进程，探测失败即离线 | `docs/qa/003-runtime-lifecycle.md` |
| 进程组生命周期 | `POST /api/system/shutdown/`、`/system/restart/` | `RuntimeSupervisorControl` → Named Pipe → Supervisor → PlayerWorker×4 / AudioWorker / PowerPointHost / MediaMTX | `QueuedCommandWakeNotifier` 路径，不登记真实进程 | `docs/qa/003-workstation-runbook.md` |

核对程度说明：表内 `file:line` 是 2026-09-19 逐个 grep 出来的代码位置。其中「拼接屏画面映射」「系统音量」「显示器拓扑」「设备电源」四行我读到了完整链路（入口 → 接口 → 实现 → 目标地址）；「播放器窗口内容」「PowerPoint」「背景音频」「MediaMTX」「进程组生命周期」五行的**下游部件**列来自 003 规范与既有 QA 记录，只核到了入口与 Hardware 注册，未逐行走完进程侧链路——首次改动这几条时请顺便复核本表。

**新增能力时的检查动作**：

**新增能力时的检查动作**：

1. 列出触发入口（哪些 HTTP 端点/激活路径会碰到物理状态）。
2. 确认下游部件在 Hardware 分支注册（`Program.cs:106-113` 一带），且**不是**靠构造函数默认值兜底——`TimeProvider` 等已注册的服务会掩盖默认值注入是否真的可用。
3. 给该路径补一条「Hardware 装配能解析出真实实现」的测试 + 一条包级/时序测试；Simulation 联调不算验证。
4. 明确失败语义：物理副作用失败时，界面/数据库/静音策略是否跟着回滚（通行做法见 `RuntimeStateService.SetRuntimeModeAsync`：**先下发、成功后才落库**，失败即整体不生效）。
5. 把结果补进本表和对应 `specs/` 条目。

## 2. 已记录的坑

### 坑 1 - 整层漏迁：模块有测试、有常量，零生产调用方（2026-09-19 修复）

- **症状**：切换单/双大屏模式时界面正常、接口返回成功，物理墙面完全不动作。
- **根因**：迁移时只迁了 `VideoWallSequenceBuilder`（控制包构造 + 常量 + 单测），下发层（协议阶段、并发上限、重试退避、失败即中止）没人承接；`specs/001~003` 全文没有「视频墙/拼接屏」条目，于是没有任何任务或验收点会暴露缺失。
- **检出方式**：
  - 对每个 `*Builder` / `*Sequence` / `*Packet` / `*Service` 类型反查生产调用方：`grep -rn "<类型名>" --include=*.cs runtime-dotnet/src | grep -v Tests`，为 0 就是嫌疑。
  - 对每个硬件接口（`IVideoWallController`、`ISystemAudioController`、`IDisplayTopologyProvider`、`IDeviceCommandTransport`）列出实现与调用点，逐条对照旧 `scp_cv/services/*.py` 的调用者。
- **现状**：已修复（`runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallController.cs`），规范见 `specs/004-video-wall-control/`。

### 坑 2 - 硬件副作用只接了单条入口（**未修复**，待确认）

- **症状（推断）**：Hardware 模式下激活带音量的预案（`volume_state=set`），运行态音量变了、物理系统音量没变。
- **根因**：`ISystemAudioController.Apply` 全仓库只有 `RuntimeStateService.SetSystemVolumeAsync`（`RuntimeStateService.cs:159`，入口 `PATCH /api/volume/`）会调用；`ScenarioService.ActivateAsync` 只把 `runtime.VolumeLevel` 写进库。旧 Python 的 `activate_scenario` 会调 `set_system_volume(level)`（`scp_cv/services/scenario.py:305`）。
- **检出方式**：对每条硬件接口反查全部调用点，再对照旧实现的调用者数量——「旧实现有 N 个入口、新实现只有 1 个」就是漏接线。
- **现状**：代码证据明确，**尚未实机验证，尚未修复**。修复前先按宪章 II 立规范条目（不并入 004）。

### 坑 3 - 两个「拼接屏」不是一回事

- `splice_screen`：**设备电源**，`192.168.5.10:8889`，只写不读（`appsettings.json` §Devices、`DeviceService`）。
- 视频墙：**画面映射下发**，50 个节点 `192.168.5.101~150:4830`（`VideoWallSequenceBuilder`）。
- **规避**：改「拼接屏电源」不要动 4830 那条链路，反之亦然；提需求时说明是电源还是画面。

### 坑 4 - `dotnet test --no-build` 跑的是旧二进制

- **症状**：改完源码跑测试，新测试用旧二进制执行，看起来像「刚才还好好的测试突然全挂」或「新测试没生效」。
- **规避**：改动 `runtime-dotnet/src` 后先 `dotnet build` 再 `dotnet test`，或不要加 `--no-build`。判定「新测试确实能发现该缺陷」时，必须重新构建后再跑。

### 坑 5 - 分析器闸门会让代码直接构建失败

- `runtime-dotnet/Directory.Build.props` 开了 `TreatWarningsAsErrors`、`AnalysisLevel=latest-recommended`、`EnforceCodeStyleInBuild`、`Nullable=enable`。常见触发：持有 `IDisposable` 字段的类必须自己实现 `IDisposable`（CA1001，如 `SemaphoreSlim`/`Lock` 字段）；只在调用点传 `List<T>` 的参数应声明为 `List<T>`（CA1859）；常量数组用集合表达式 `[...]` 而不是 `new[] { ... }`（CA1861）。
- **规避**：不要靠 `#pragma`/关闭规则过关；按提示调整类型或实现接口。测试文件同样受管。

### 坑 6 - 冷启动就绪测试的假失败（2026-09-19 修复）

- **症状**：`HardwareControlHostStartupTests` 偶发失败，报就绪超时，与代码改动无关。
- **根因**：就绪预算过短，且子进程 stdout/stderr 未及时排空导致管道缓冲堵塞。
- **现状**：已修复（延长就绪预算、立即排空输出、`process.HasExited` 快速失败）。默认测试不启动真实播放器/Office/MediaMTX/设备，需要物理副作用的测试打 `Physical` trait，常规命令用 `--filter "Category!=Physical"` 排除（见 `runtime-dotnet/tests/README.md`）。

### 坑 7 - 验证记录里的「部分 / 缺口」会原样留到现场（2026-09-19 修复 004 的 FR-018/FR-020）

- **症状**：功能上线、测试全绿，但规范里写着「仅代码复核」的条目一直没人补；下一次改动时也看不出哪些是欠账。
- **根因**：`verification.md` 把 FR-018（下发无日志）与 FR-020（使用文档未说明失败表现）明确记为**缺口**，却没有对应的 `tasks.md` 条目或 issue 承接——004 是走「spec → 直接实现 → 验证」的，缺口没有归属就没有闭环。同一类问题在 003 里表现为 4 条实机项长期挂起。
- **检出方式**：
  - `grep -n "仅代码复核\|缺口\|未满足\|未验证" specs/*/verification.md`，逐条确认是否已有承接方（tasks.md 条目 / issue）。
  - 每条「缺口」都要能回答：谁来补、补完改哪一行。答不出来的就不是验证记录，是待办清单。
- **现状**：004 的两处缺口已补——下发成功/失败/重试/取消的日志（`VideoWallLog.cs`，5 条新测试，见 `VideoWallLoggingTests`）与文档（`docs/使用文档.md` §4.5、`docs/维护文档.md` §8.6）。**日志的现场价值**：节点只写不读，日志是判断「墙面动没动」的唯一线索，只把失败写进 HTTP 错误体等于没写。
- **仍挂起**（本条不覆盖）：004 的 FR-005（2 秒超时与 TCP_NODELAY）与 FR-014（等待在写事务之外）仍为「仅代码复核」；FR-017 只补上了控制器侧断言，传输层的取消重抛仍是代码复核。SC-005/SC-007 与 002/003 的实机项仍等现场条件。FR-012/FR-013 已于同日补上自动化断言（服务层与控制器层各一条 + 并发交错一条）。

## 3. 相关沉淀点（不在这里重复）

- `specs/003-dotnet-runtime-refactor/baseline.md` §易错语义：迁移前必须保住的**旧 Python 语义**（冻结在基线提交）。
- `specs/003-dotnet-runtime-refactor/verification.md` §尚未验证：未完成的硬件验证登记处。本文件不替代它。
- `docs/实机测试结论.md`、`docs/本地QA测试结论.md`、`docs/qa/003-*.md`：按日期/主题的「表现 → 修复 → 实机结论」记录，本文件的条目应从那里提炼。
- `docs/维护文档.md` §8 故障定位：运维向排查（现象驱动）。
- `docs/design/08-migration-guide.md`：迁移前的预防性注意事项。
