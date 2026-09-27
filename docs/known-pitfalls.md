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
| 拼接屏画面映射（视频墙） | `PUT /api/video-wall/layout/` 保存草稿；`POST /api/video-wall/layout/apply/` 或 `/presets/{preset}/apply/`；预案兼容入口 | `VideoWallLayoutService`/`ScenarioService` → `IVideoWallController` → 50 节点 `192.168.5.101~150:4830`；仅两个既有预设可下发，未知手动映射在下发前拒绝 | `SimulationVideoWallController`；未知布局同样拒绝 | `VideoWallLayoutServiceTests`、`VideoWallGoldenPacketsTests`、`HostHardwareIntegrationTests`；新映射帧待现场抓包，不能把本机预览或回环称为实体画面证据 |
| 系统音量 / 系统静音 | `PATCH /api/volume/` | `RuntimeStateService.cs:159` → `ISystemAudioController.Apply` → `WindowsCoreAudioController` | `SimulationSystemAudioController` | `GetSystemVolumeAsync` 返回 `system_synced`；**预案激活只落库不推硬件**，见坑 2 |
| 显示器拓扑与落位 | `GET /api/displays/`、`POST /api/displays/select/` | `IDisplayTopologyProvider` → `WindowsDisplayTopologyProvider`；`POST /api/system/restart/` 后由 `ReapplyDisplayTargetsAsync`（`RuntimeStateService.cs:104`）按已保存目标恢复落位 | `SimulationDisplayTopologyProvider` | `ScpCv.Windows.Tests`、`specs/003` verification 的 D4 记录 |
| 播放器窗口内容（仅大屏 1/2） | `POST /api/playback/{windowId}/open\|close\|control\|show-ids\|reset-all/`、`PATCH .../volume/`、`.../mute/`、`.../loop/` | 持久命令队列 → Named Pipe → `PlayerWorker`（WPF + VLC + WebView2 + 页图）；明确显示器绑定缺失时拒绝落到控制桌面 | `QueuedCommandWakeNotifier`（仅排队，不启动 worker） | `ScpCv.Integration.Tests`、`ScpCv.Windows.Tests`；3/4 请求拒绝、旧命令不重放 |
| PPT 上传转换 / 实验性 PowerPoint 放映 | `POST /api/sources/upload/` 登记作业；`POST /api/sources/{id}/prepare/` 重试；`PATCH /api/settings/powerpoint/` 显式开启后才允许原生放映 | `PptConversionHostedService` → `PowerPointHost` STA 导出 PNG；播放器默认 `slide_images`，实验开关只影响后续打开 | Simulation 不启动 Office，作业保持待处理 | `PowerPointSlideExportTests`、`PptConversionHostedServiceTests`、`PowerPointSettingsEndpointTests`；D4 实机转换/放映仍待复测 |
| 背景音频（独立列表） | `POST\|PATCH\|DELETE /api/background-audio/*` | `BackgroundAudioService` → 命令队列（`CommandTargetKind.Audio`）→ Named Pipe → `AudioWorker` | `QueuedCommandWakeNotifier`（仅排队） | `docs/实机测试结论.md` 背景音乐段 |
| 设备电源 | `POST /api/devices/{type}/power/{action}/`、`.../toggle/` | `DeviceService` → `IDeviceCommandTransport` → `TcpDeviceCommandTransport`：拼接屏 `192.168.5.10:8889`、电视左 `.161`、电视右 `.162`（只写不读，不保存状态） | `SimulationDeviceCommandTransport` | 2026-09-27 已在用户授权后发送拼接屏 ON→OFF→ON 与电视各两次 toggle；API 只确认写出，物理电源状态未独立读回，见 `docs/qa/003-full-regression-20260927.md`；`appsettings.json` §Devices |
| MediaMTX 推拉流 / 直播 | `POST /api/sources/local\|web/`、`GET /sources/{id}/`（在线探测） | MediaMTX 进程（流端口 `8890`/`9997`/`8554`）、`stream-probe` HttpClient | 无进程，探测失败即离线 | `docs/qa/003-runtime-lifecycle.md` |
| 进程组生命周期 | `POST /api/system/shutdown/`、`/system/restart/` | `RuntimeSupervisorControl` → Named Pipe → Supervisor → PlayerWorker×2 / AudioWorker / PowerPointHost / MediaMTX | `QueuedCommandWakeNotifier` 路径，不登记真实进程 | `docs/qa/003-workstation-runbook.md` |

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
  - 对每个硬件接口（`IVideoWallController`、`ISystemAudioController`、`IDisplayTopologyProvider`、`IDeviceCommandTransport`）列出实现与调用点；需要历史对照时查看清理前提交 `e822be9` 中的旧 Python 调用者。
- **现状**：已修复（`runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallController.cs`），规范见 `specs/004-video-wall-control/`。

### 坑 2 - 硬件副作用只接了单条入口（**未修复**，待确认）

- **症状（2026-09-27 D4 实机复现）**：Hardware 模式下激活带音量的预案（`volume_state=set, level=20`）返回成功，运行态音量变为 20、物理系统音量仍为 100；直接 `PATCH /api/volume/` 能实际改变 Core Audio。
- **根因**：`ISystemAudioController.Apply` 全仓库只有 `RuntimeStateService.SetSystemVolumeAsync`（`RuntimeStateService.cs:159`，入口 `PATCH /api/volume/`）会调用；`ScenarioService.ActivateAsync` 只把 `runtime.VolumeLevel` 写进库。清理前提交 `e822be9` 中的旧 Python `activate_scenario` 会调用物理音量设置。
- **检出方式**：对每条硬件接口反查全部调用点，再对照旧实现的调用者数量——「旧实现有 N 个入口、新实现只有 1 个」就是漏接线。
- **现状**：代码与实机证据均明确，**尚未修复**，由 003/T137 承接；详情见 `docs/qa/003-full-regression-20260927.md`。

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
- **仍挂起**（本条不覆盖）：004 的 FR-014（等待在写事务之外）仍为「部分」——真实网段的时延与丢包没有实测；FR-005 拆成两半：2 秒超时 2026-09-21 已用回环实测（坑 9），**TCP_NODELAY 仍是代码复核**（接收端应用层读不出差别，要抓包）。FR-017 传输层的取消重抛同日补上实环实测。SC-005/SC-007 与 002/003 的实机项仍等现场条件。FR-012/FR-013 已于同日补上自动化断言（服务层与控制器层各一条 + 并发交错一条）。**帧内容的现场抓包复核**登记为 issue #2；2026-09-21 补上了「.NET 与旧实现逐字节等价」的黄金样本比对（坑 8），但那不覆盖「旧值对真实墙面是否正确」——`verification.md` 末节已写明承接方与「补完改哪一行」。

### 坑 8 - 包级测试只断言序列里的第一个样本（2026-09-21 补上逐包比对）

- **症状**：视频墙的包级测试全绿，但换节点、换模式没人验——单屏模式只断言了**第一个**映射包的字节，**双屏模式 50 个映射包一个字节都没有断言**（当时只有一条数 phase 的用例）。把右半墙的组播地址与窗口号错写成左半墙的值，旧断言的 4 个用例**全部通过**。
- **根因**：`sequence.First(item => item.Phase == "mapping…")` 这类写法只证明「序列里有这么一种包」，不证明「每个节点都拿到自己的那一份」；而逐节点差异（裁切区、组播地址、窗口号、校验和）恰好是 1:1 迁移最容易走样的地方。
- **检出方式**：
  - 包级测试里出现 `First(...)`／`Any(...)` 而不是对全序列的断言时，先对一次数：`Build()` 返回多少项、断言覆盖多少项，差值就是盲区。
  - 给序列构造器补一条「全序列比对**独立来源**黄金样本」的用例。样本必须来自旧实现的实际运行结果，不能从新实现转抄，否则是自证。
- **现状**：已补（`VideoWallGoldenPacketsTests` + `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/Fixtures/video-wall-packets.json`，两种模式各 200 个包）。生成器与旧 Python 侧守卫在 T118 退役时删除，来源与生成证据保留在清理前提交 `e822be9` 和 `specs/004-video-wall-control/verification.md` 第 4 轮；当前 fixture 作为不可变迁移快照使用。

### 坑 9 - 本机回环测试的绿色容易被当成现场证据（2026-09-21 登记边界）

- **症状**：把 `192.168.5.101~150` 挂成本机别名、跑起假节点之后，视频墙的「超时、重试、中止、取消、逐包字节」全都有了实测数字，很容易顺手写成「视频墙已实测」——但假节点收到的是**自己发的包**，墙面上一帧都没动过。
- **根因**：回环能证明的只有「实现发出的字节 = 实现打算发的字节」，以及传输层在真实 socket 上的时序；它对**字节内容对不对**、**设备收到后怎么显示**零覆盖。issue #2（帧内容待抓包复核）与 SC-007（物理画面）恰好都在这条线之外，却被同一批数字「照亮」。
- **检出方式**：
  - 任何「本机自收自发」的测试，先在记录里回答三个问题：对端是真实设备吗？证据跨过机器边界了吗？断言里有没有独立于被测量的来源？
  - 边界要跟测试写在同一个提交里（本轮写在 `specs/004-video-wall-control/verification.md` §「回环假节点能测什么、不能测什么」），别只留在聊天记录里。
- **现状**：回环用例已补（`VideoWallLoopbackTests` + `runtime-dotnet/scripts/videowall-loopback.ps1`，5 个用例带 `Physical` trait，缺别名自动跳过）。能测的：连接超时（2 秒 × 5 次 = 12.5 s 实测）、重试退避、失败即中止（其余 49 个节点只收到清屏包）、取消原样抛出（409 ms、零日志）、逐包字节。**测不到的**：帧内容是否对墙正确（issue #2）、TCP_NODELAY、真实网段的时延与丢包、写超时（连上后写 12/45 字节必然成功，2 秒预算实际只兜住 `ConnectAsync`）。

### 坑 10 - 启动脚本的 HTTP 超时短于 Worker 就绪预算（2026-09-24 修复）

- **症状**：D4 ControlHost 的 `/health/ready` 为 200，但 `run-headless.ps1 -StartWorkers` 约 20 秒后报告失败，运行组持久状态变成 `Faulted`，`StopReason=runtime_restart_cancelled`。只看 HTTP 健康检查会误以为全部 Worker 已启动。
- **根因**：脚本为所有 HTTP 请求固定使用 15 秒超时，`POST /api/system/restart/` 因真实 Worker 冷启动超出该时间而被客户端取消；服务端按取消语义停止整组。另有 Supervisor 管道空闲超时风险，需由已认证心跳保持连接。
- **规避**：restart 请求使用 `ReadyTimeoutSeconds`，并以 `runtime_group_control.State=Armed`、七个受管进程的 PID/session 和脚本“全部 Worker 已就绪”结果共同判定启动成功；不要用 `/health/ready` 代替运行组就绪。相关回归：`HeadlessScriptSecurityTests.WorkerRestartUsesReadinessTimeout` 与 Supervisor 心跳集成测试。
- **边界**：该轮 D4 启动验证只完成运行组就绪与 HTTP 可达；同日后续多源冒烟另见 `docs/qa/003-windows-runtime.md`，仍未做 60 分钟长稳测试。

### 坑 11 - WPF 资源在脱离视觉树或错误线程上初始化（2026-09-24 修复）

- **症状**：网页源 `OPEN` 永久处于 `Processing`/`loading`；PPT `OPEN` 报“调用线程必须为 STA”。D4 阶段探针确认 WebView2 环境创建已完成，卡在 `EnsureCoreWebView2Async`，尚未导航。
- **根因**：新建 WebView2 控件未挂入已显示的播放器窗口就开始初始化；PowerPoint 子操作的 `ConfigureAwait(false)` 让后续 WPF `Grid` 创建离开 Dispatcher。
- **规避/现状**：WebView2 先作为待切入画面加入视觉树，成功后原位升为当前画面，失败时移除；环境、控件与导航均设明确超时。PlayerRuntimeHost 的 WPF 路径保留 UI 同步上下文。`PlayerWindowSurfaceTests` 覆盖预备/切入，D4 真实网页与 PPT 均已打开并截屏；网页初始化约 2 秒完成。

### 坑 12 - 受控停机后旧执行租约阻塞新命令（2026-09-24 修复）

- **症状**：网页命令卡住后，即使整组停机重启，新 `OPEN` 仍停在 `Pending`；旧命令一直是 `Processing`，`ClaimAsync` 因目标已有执行中命令拒绝领取。
- **根因**：原先 `CompleteStopAsync` 只写运行组 `Stopped`，没有在 Supervisor 已确认整组退出后终结旧租约；重复调用时还会提前返回。
- **规避/现状**：在已确认停机时将遗留 `Processing` 标为 `Superseded/runtime_group_stopped` 并清除 claim；重复停机也执行清理。`RuntimeAuthorityRepositoryTests` 两条红/绿回归通过，D4 旧命令 108/127 实测转为终态；不直接修改 SQLite。

### 坑 13 - 原生播放的异步状态与前后端动作词汇不同步（2026-09-24 修复）

- **症状**：音频已播放且命令 `Completed`，页面仍显示 `loading`；PPT“上一页”返回 `invalid_navigation`；短视频结束后 API 继续显示 `playing`。
- **根因**：LibVLC 的 `Play()` 在真正进入 `Playing` 前返回，首个音频快照因此是 `loading`；前端/API 文档发送 `prev`，服务层只识别 `previous`。视频自然结束没有后续状态上报路径。
- **规避/现状**：音频命令等待实际目标状态并使用 `SetPause(true)` 而非切换式 `Pause()`；REST 边界兼容 `prev`；视频自然结束使用 source generation fencing 主动上报 `stopped`，循环则重播。自动测试及 D4 声卡回录/实体画面复测见 `docs/qa/003-recovery-next-20260924.md`。不能仅凭 HTTP 已接受断言物理画面或声音正确。

### 坑 14 - 旧 Supervisor 清理会删除新运行组状态文件（2026-09-24 修复）

- **症状**：D4 `/api/system/restart/` 返回新组已就绪，七个子进程仍在，但 `runtime-processes.json` 消失；后续 stop/restart 无法依据状态文件认领这些进程。
- **根因**：重启启动了第二个 Supervisor；新组写入同一路径后，旧 Supervisor 处理旧子进程退出时无条件 `File.Delete`，删除了新组的文件。状态文件原先也通过 `File.Create` 原位写入，读者可能看到半文件。
- **检出方式**：测试先写旧组、再写新组，模拟旧组清理；断言只允许完整 PID/启动时间/会话匹配的组删除，且新组内容保持完整。现场须同时核对状态文件、数据库 `Armed` 与进程树，不能只看 HTTP 成功。
- **现状**：状态文件改为同目录原子发布，跨进程互斥保护读写删，删除前核对完整组身份。D4 原缺失文件经七个子进程的 PID/启动时间/会话及父进程逐一核验后短暂恢复，并通过受控 shutdown 停止旧组；新版本 API restart 实测旧组 0 残留、新组 7/7 且状态文件仍存在。停机另暴露项目自有 PowerPointHost 未退出（坑 15）。

### 坑 15 - PowerPointHost 被误当作用户 Office 保护，停机后残留（2026-09-24 部分修复）

- **症状**：D4 运行组停机返回成功、数据库 `Stopped`，但项目自有 `ScpCv.PowerPointHost.exe` 仍存活；状态文件已删，后续启动可能与旧 Host 争用唯一槽。
- **根因**：`ShutdownCoordinator` 将登记角色 `office` 整体排除强制退出；该角色实际是项目自有 Host，不是可能含用户文稿的 `POWERPNT.EXE`。
- **检出方式**：停机后按已登记 PID、启动时间、可执行路径及父进程核对残留；测试用无窗口子进程模拟 Host 超时，并断言只结束 Host 本身。
- **现状**：协作等待超时后定向结束项目 Host，不使用进程树强杀用户 Office。D4 旧 Host 已按精确身份核验后单独清理，新版本正常停机/重启实测项目 Host 无残留；自有 Presentation/Office COM 实例的持续归属问题仍由 T133 跟进。

### 坑 16 - 媒体命令完成不等于实体画面持续有效（2026-09-24 修复视频路径）

- **症状**：循环已开启的视频停在末帧、同源重复 OPEN 后实体屏幕黑屏；两种情形下 API 仍显示 `playing`。
- **根因**：视频自然结束未进入 Worker→ControlHost 状态报告；`SET_LOOP` 在播放开始后才对 VLC Media 添加选项，不保证原生循环生效；重复 OPEN 在旧 VideoView 尚显示时重新创建 VLC 播放器。
- **检出方式**：短视频叠加帧时钟，分别在自然结束前后、同源重复 OPEN 后抓取实体屏幕，核对帧计时/像素变化与 API；增加迟到 source generation、旧播放器结束回调不能改写新源的自动测试。
- **现状**：OPEN 携带循环意图，结束回调按资源身份与代次重播或上报 stopped；同源 ID/revision/URI 复用当前播放器。D4 8 秒片循环后相隔 13 秒的画面不同，同源再开仍出画，不循环结束 API 为 stopped。长期私有内存/GPU 斜率仍由 T116/T129 验证。

### 坑 17 - 动态 PPT 已出画而首次页码仍是 0（2026-09-27 修复）

- **症状**：D4 3 号窗实体已显示 9 页文稿的第 1 页，API 为 `playing/powerpoint`，却报告 `current_slide=0`；首次 NEXT 后才报告第 2 页。
- **根因**：Office OPEN 结果只返回页数，没有返回实际当前页；PlayerWorker 取请求默认 `target_slide=0` 作回退，把请求意图当成实际页码。
- **检出方式**：同时核对 OPEN 完成结果、API `current_slide`、实体首屏以及第一次 NEXT 的结果；只断言总页数或 `playing` 会漏掉错误。
- **现状**：COM 在放映窗口就绪后读取实际 `CurrentShowPosition`，经 Office IPC 上报；Worker 无观测值时按 1-based 安全兜底。4 条投影回归与 D4 实机验证通过：OPEN 1/9、NEXT 2/9，CLOSE 后目标屏回黑。

### 坑 18 - Office 进程由项目启动不等于退出时仍归项目独占（2026-09-27 待收敛）

- **症状**：运行组 shutdown 返回成功、七个自有进程全部退出，但 PowerPoint 自动化进程仍在；若仅凭它在项目 OPEN 时出现便强杀，可能中断后来加入的用户文稿。
- **根因**：Office COM 可能让项目文稿和桌面用户文稿共享进程；启动时的 PID/时间只证明历史归属，不证明当前 `Presentations` 集合仍全部自有。SSH 所在 session 0 的 `MainWindowHandle=0` 也不能证明交互桌面没有窗口。
- **检出方式**：停机后在交互会话只读枚举每份 Presentation 的路径、保存状态及放映窗口，和项目已登记源逐项比对；对用户文稿保留进程，不按名称或进程树杀 Office。
- **现状**：D4 本轮确有两份桌面用户文稿加入同一 Office 实例，项目 shutdown 正确保留它；用户明确授权后才定向关闭该实例。T133 仍需在没有用户文稿的清洁条件下验证自有 Office 退出，并补自动化归属/超时回归。

### 坑 19 - 两窗运行组不能只缩减启动数量（2026-09-27 修复）

- **症状**：只停用 PlayerWorker 3/4 时，旧命令、旧预案或前端残留入口仍可写入窗口 3/4，出现运行组“就绪”但目标永远不执行的假成功。
- **根因**：播放器数量、数据库种子、命令队列、预案、REST 参数、前端菜单和 Ready 角色门禁原本分别维护窗口集合。
- **检出方式**：从旧 3/4 请求、待执行命令、历史预案激活和进程组角色四条路径分别断言；物理显示器绑定缺失也必须拒绝而不是落到主桌面。
- **现状**：统一两窗边界；旧命令保留审计但不重放，旧预案目标在物理动作前拒绝，显示器缺失 fail closed。D4 两块实体输出仍待服务恢复后复测。

### 坑 20 - 墙面草稿不能冒充已知实体帧（2026-09-27 防护）

- **症状**：前端可以选笔记本或自定义 IP 流，但协议抓包尚无，若把保存草稿当作应用成功会制造假画面状态，甚至向 50 节点发送猜测帧。
- **根因**：预览模型与物理协议实现的能力边界不同。
- **检出方式**：未知映射应用应返回 `protocol_unavailable`，测试传输假节点收到 0 包；两个固定预设仍按既有黄金包验证。
- **现状**：草稿仅持久化/预览，只有既有“窗口 1 全屏”和“窗口 1 左、窗口 2 右”可执行；新增输入组合待抓包后单独实现与实机验证。

### 坑 21 - 文件已移动时数据库提交异常不等于事务未提交（2026-09-27 修复）

- **症状**：上传、目录移动或 PPT 页图发布先完成物理落盘，SQLite 提交阶段返回异常；如果立即把文件搬回，数据库可能已提交新路径，形成“成功记录指向不存在文件”。
- **根因**：文件系统与 SQLite 不共享一个原子事务；`CommitAsync` 的异常可能是提交结果不明。
- **检出方式**：故障注入在物理移动之后抛异常，核对旧路径/数据库均恢复；对提交结果不明的路径先用新连接查询目标记录或制品清单，再决定补偿，查询也失败时保留文件供人工核查。
- **现状**：上传、源/目录移动、文件夹删除与页图发布均先查询持久化结果再补偿；页图制品独立于媒体目录，移动原件不重写缓存清单。D4 长稳和断电故障仍未做。

### 坑 22 - 两窗常驻播出导致 PPT 转换永远排队（2026-09-28 修复）

- **症状**：D4 的多页 PPT 上传返回 201，原件位于新媒体目录，但页图准备超过观察窗口仍为 `queued`；PowerPointHost 与两个播放器均已就绪。
- **根因**：`MediaPreparationService.ClaimNextAsync` 把“任一窗口为 playing”作为全部准备作业的领取禁令。现场两块大屏几乎常驻，导致 PPT 上传转换饥饿；问题不在 PowerPoint COM 或上传 HTTP。
- **检出方式**：在窗口 1 播放图片时上传 PPT，断言 PPT 页图作业仍能被领取；D4 同时核对原件、作业状态和 OfficeHost 输出，不能只看 201 或 Worker 在线。
- **现状**：仅 PPT 页图作业不再要求普通窗口空闲，原生 Office 放映冲突仍由独占槽位拒绝并等待；本机红→绿回归通过。D4 新二进制的并行转换与实体播放待复测，见 `specs/006-d4-live-convergence/verification.md`。

### 坑 23 - 页图实际出画但会话模式为空（2026-09-28 修复）

- **症状**：D4 9 页 PPT 的原件和 PNG 完整，默认页图在窗口 1 显示并从 1/9 翻到 2/9，且 PowerPoint 放映进程为 0；会话却返回 `playback_mode=""`。
- **根因**：Worker 正确上报 `slide_images`，ControlHost 投影只特殊处理 `powerpoint`，其余用枚举名解析；`SlideImages` 枚举无法由带下划线的 `slide_images` 直接解析，退回旧 `None`。
- **检出方式**：把带代次的 Worker `state_report` 经过真实投影再读取对外会话；只断言打开请求中的 `presentation_mode` 或页码会漏掉。
- **现状**：投影显式映射 `slide_images`，新增公共会话回归先红后绿；D4 更新该修复后的会话读回仍待复测。

### 坑 24 - 视频有变化帧但进度只随控制命令更新（2026-09-28 修复）

- **症状**：D4 机械臂视频连续两次截图是不同帧，会话长时间为 `playing/position_ms=0/duration_ms=0`；发出一次循环设置后立即变为 `42903/158322 ms`，再无命令又停在该位置。
- **根因**：Worker 的 VLC 时间读取只出现在命令完成快照和自然结束事件；空闲循环只发传输心跳，未上报媒体进度。
- **检出方式**：播放期间先不发控制命令，间隔读取会话进度并核对实际变化帧；再发一条无视觉影响的控制命令观察进度跳变。仅靠 `player_online=true` 会漏掉。
- **现状**：在既有单连接、相关 ID 路由的管道空闲循环加入可选状态采样，播放器只在 VLC 资源存在时从 UI 线程取得带 source generation 的样本；真实 Named Pipe 集成测试先红后绿。D4 连续进度与长稳内存仍待新二进制复测。

## 3. 相关沉淀点（不在这里重复）

- `specs/003-dotnet-runtime-refactor/baseline.md` §易错语义：迁移前必须保住的**旧 Python 语义**（冻结在基线提交）。
- `specs/003-dotnet-runtime-refactor/verification.md` §尚未验证：未完成的硬件验证登记处。本文件不替代它。
- `docs/实机测试结论.md`、`docs/本地QA测试结论.md`、`docs/qa/003-*.md`：按日期/主题的「表现 → 修复 → 实机结论」记录，本文件的条目应从那里提炼。
- `docs/维护文档.md` §8 故障定位：运维向排查（现象驱动）。
- `docs/design/08-migration-guide.md`：迁移前的预防性注意事项。
