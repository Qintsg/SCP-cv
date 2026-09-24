# 验收与验证记录

本文是 003 重构的证据索引。`通过` 只用于已经执行的自动化或人工检查；`待实机` 不等同于失败，也不得在交付说明中描述为已经通过。

## Q1–Q11 验收矩阵

| 范围 | 自动化证据 | 人工/实机证据 | 当前结论 |
| --- | --- | --- | --- |
| Q1 三端共享功能 | `frontend/scripts/platform-adapters.test.mjs`、`client-connection.test.mjs`、ControlHost 合同测试 | `docs/qa/003-client-matrix.md` | Web 通过；Electron 实包通过（含上传、原生保存落盘哈希一致、关闭后主机存活）；Android 实包完整通过 |
| Q2 会话与 SSE | `AuthEndpointTests`、`SseEndpointTests`、`verify-packaged-session.test.mjs`、`client-connection.test.mjs` | Electron 10 次页面重载及 Android 10 次 HOME/恢复记录见客户端矩阵 | 自动化、Electron 与 Android 实包均通过 |
| Q3 业务规则 | Domain/ControlHost 全套测试、`OpenApiCoverageTests` | 浏览器业务状态见 `docs/qa/003-browser-ui.md` | 自动化通过；浏览器复核待执行 |
| Q4 可靠命令 | `CommandFencingTests`、`CommandRecoveryTests`、`ReliabilityAcceptanceTests`、`SecurityBoundaryTests` | 无 | 自动化通过 |
| Q5 Office/PDF | `PresentationPolicyTests`、`OfficeOperationTests`、`MediaPreparationTests`、`RuntimePipeBrokerTests` | `docs/qa/003-office-interop.md` | Office IPC、去重、授权与软件边界通过；实际 Office/HWND 条件待验证 |
| Q6 网页预热 | `ResourceSwitchTests`、`WebViewPreheatTests` | `docs/qa/003-preheat-performance.md` | 状态机通过；真实 WebView2 性能待复核 |
| Q7 媒体/性能 | `VlcAdapterTests`、流发现实现与测试、`scripts/benchmark-commands.ps1` | `docs/qa/003-performance.md` | 能力边界通过；基准脚本就绪；1000/100 样本待工作站执行 |
| Q8 启停/音频 | `BackgroundAudioTests`、`RuntimeLifecycleTests`、`ReliabilityAcceptanceTests`、`RuntimeProjectionTests`、`HostHardwareIntegrationTests` | `docs/qa/003-windows-runtime.md` | 音频 generation fencing、Core Audio 接线与自动化通过；完整实机循环待执行 |
| Q9 开发数据/Git | `DatabaseInitializerTests`、`DevelopmentDataTests`、`DataBoundaryTests` | `runtime-dotnet/README.md` | 通过；新库与旧库边界明确 |
| Q10 Windows 运行 | Windows/Integration 测试工程、`HostHardwareIntegrationTests`、`HardwareControlHostStartupTests` | `docs/qa/003-windows-runtime.md` | 只读探针通过；Hardware 运行时 7 进程一次拉起并全部就绪；四屏 60 分钟待工作站 |
| Q11 原生壳/UI 安全 | `electron-security.test.mjs`、`capacitor-platform.test.mjs`、`security-boundary.test.mjs` | `docs/qa/003-electron.md`、`003-android.md`、`003-browser-ui.md` | Android 文件/外链/返回键实包通过；Electron 交互式文件与关闭场景待补 |

## FR-001–FR-030 映射

| 需求 | 主要证据 | 状态 |
| --- | --- | --- |
| FR-001–003 | 前端共享构建测试、HTTP 合同测试、`DatabaseInitializerTests` | 自动化与 Android 实包通过；Electron 文件场景待补 |
| FR-004–006 | `PlaybackRulesTests`、ControlHost 兼容测试、`BackgroundAudioTests` | 通过 |
| FR-007–010 | 命令仓储、围栏、恢复、投影和前端 actual-state 测试 | 通过 |
| FR-011–014 | `PresentationPolicyTests`、`OfficeOperationTests`、`MediaPreparationTests` | 软件通过；Office 实机待验证 |
| FR-015–017 | `ResourceSwitchTests`、`WebViewPreheatTests`、`VlcAdapterTests`、流发现实现 | 软件通过；长时间预热待验证 |
| FR-018–021 | `RuntimeLifecycleTests`、Supervisor/Worker 测试与 QA 模板 | 软件通过；Windows 运行待验证 |
| FR-022–023 | 独立 DataRoot 测试、开发脚本、Git 范围说明 | 通过 |
| FR-024 | 平台适配测试、主机/客户端进程边界 | Android 实包关闭后主机存活；Electron 关闭行为待验证 |
| FR-025 | `LogRedaction` 与 `SecurityBoundaryTests` | 通过 |
| FR-026–027 | 本文件、Spec Kit 产物、锁文件和验证命令 | 进行中 |
| FR-028–030 | 响应式/平台/安全边界测试 | 浏览器与 Android 实测通过；Electron 文件/关闭待验证 |

## SC-001–SC-010 映射

| 成功标准 | 证据 | 当前结论 |
| --- | --- | --- |
| SC-001 | Q1、三端矩阵 | Android 实包通过；Electron 文件/关闭待补 |
| SC-002 | `ReliabilityAcceptanceTests`、`CommandRecoveryTests` | 通过 |
| SC-003 | `CommandRecoveryTests`、`OfficeOperationTests` | 通过 |
| SC-004 | `PresentationPolicyTests`、Office QA | 软件通过，实机待验证 |
| SC-005 | `WebViewPreheatTests`、预热 QA | 软件通过，真实 WebView2 待验证 |
| SC-006 | 性能 QA | 待 1000/100 样本 |
| SC-007 | `RuntimeLifecycleTests`、Windows 运行 QA | 软件通过，完整次数待实机 |
| SC-008 | `DevelopmentDataTests`、`DataBoundaryTests` | 通过 |
| SC-009 | Windows 60 分钟 QA | 待实机 |
| SC-010 | 本矩阵、三端恢复和 Android QA | Android 实包完成；Electron 文件/关闭待补 |

## 自动化执行记录

执行日期：2026-09-10；环境：Windows x64，.NET SDK 10.0.400。

- `dotnet restore runtime-dotnet/ScpCv.sln --force-evaluate`：通过。
- `dotnet build runtime-dotnet/ScpCv.sln --no-restore`：通过，0 警告、0 错误。
- `dotnet test runtime-dotnet/ScpCv.sln --no-restore`：通过，Domain 38、Contracts 18、Windows 11、Integration 51、Infrastructure 19、ControlHost 54，共 191 项。
- `pnpm --dir frontend test`：通过，36/36。
- `pnpm --dir frontend typecheck`：通过。
- `pnpm --dir frontend build:web`、`build:app`、`build:electron-main`：通过；Vite 提示主入口压缩前约 1.07 MB，记录为后续代码分割优化项。
- Playwright + Chrome（Vite preview + simulation ControlHost，1440×900/768×1024/390×844）：通过；截图见 `docs/qa/003-browser-*.png`，console/pageerror 为 0。
- `pnpm --dir frontend run build:electron`：通过；electron-builder 26.15.3 下载 Electron 44.2.0 并生成 `frontend/release-electron/win-unpacked`。构建仅提示未设置应用图标和入口 chunk 体积较大，未修改安全配置绕过证书校验。
- Electron unpacked 包实测：`app://scp-cv` 对 HTTPS simulation ControlHost 的 csrf/login/me/SSE 已通过；`#/sources` 连续 10 次 reload 均保持登录并恢复控制链路，console/pageerror 为 0；上传（修复 Electron 安全 Cookie 导致的 CSRF 缺失）与原生“另存为”下载落盘 SHA-256 一致；关闭进程组后主机仍返回 200。
- Android AVD 实测（2026-09-11）：Medium_Tablet / Android 16 API 36 / WebView 134.0.6998.135 的 debug APK 已通过 HTTPS 登录、REST、SSE、10 次 HOME/恢复、原生文件选择/上传、受保护保存、外链转系统 Chrome及返回键完整序列；退出后 ControlHost 仍返回 HTTP 200。TLS 使用 debug 构建内的用户 CA trust anchor，未关闭证书校验或改用明文。
- Android 返回键回归：原生 AppPlugin 配置测试、显式浮层关闭测试、typecheck、`cap:sync`、Gradle `assembleDebug` 与真实 APK 三段 Back 序列通过。

## 尚未验证

- 普通命令 1000 样本、健康热切换 100 样本的 p95。
- 四屏、Office、VLC、MediaMTX、音频的 60 分钟混合运行。

工作站执行步骤见 `docs/qa/003-workstation-runbook.md`。

T115/T116/T129 仍未验证。2026-09-22 用户明确要求在本地软件测试完成后执行 T118，并接受实机门禁尚未完成可能带来的返工风险；旧 Django/Python 运行时因此提前清理，历史继续由 Git 保留，未删除旧数据库、媒体或日志。

## T118 本地门禁与旧栈清理（2026-09-22）

- `dotnet restore runtime-dotnet/ScpCv.sln --locked-mode`：通过。
- `dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore`：通过，0 警告、0 错误。
- 清空本机代理环境变量后执行 `dotnet test runtime-dotnet/ScpCv.sln -c Release --no-build --filter "Category!=Physical"`：219/219 通过。代理开启时两个自定义 Host Header 回环用例会收到本机代理返回的 502，关闭代理后通过，属于测试环境污染。
- `pnpm --prefix frontend test`：40/40 通过；`typecheck` 与 `build:web` 通过。Vite 仍提示主入口约 1.07 MB，代码分割是后续优化项。
- 删除前旧 Python 套件：426 通过、1 个 Qt 新子控件 mouse tracking 用例失败；该用例单独连续复跑 5/5 通过，记录为时序脆弱测试，不阻塞已被替代运行时清理。
- 已删除 Django/PySide 源码、pytest/uv 工程、旧配置与旧设计文档；没有删除未跟踪的 `db.sqlite3`、`media/`、`logs/` 或凭据。
- Node 依赖只使用 pnpm；按用户要求不再提交 pnpm 锁文件，仓库 `.npmrc` 使用 `https://mirrors.cernet.edu.cn/npm/`。

## Convergence 实施记录（2026-09-10）

- T120：显示、音频、场景激活及 PPT 控制写操作已接入 `CommandCoordinator`；大屏模式产生的窗口静音也持久入队。完成结果和后续状态上报均重新投影最早剩余 pending 命令，避免清空尚未执行的意图。`RuntimeIntentQueueTests` 与 `CommandPendingProjectionTests` 通过。
- T121：Supervisor 的 `start/stop/restart/status` 入口、状态文件、四 Player/Audio/Office/MediaMTX 编排及成员退出整组停止已实现；一次开发构建故障退出证据见 `docs/qa/003-runtime-lifecycle.md`。
- T122：physical ControlHost 托管并发 Named Pipe broker，验证 OS PID/start-time/session/role/instance，支持 Worker 注册、heartbeat、Claim/Renew/Result/State、Wake 和 Supervisor 子进程登记。`RuntimePipeBrokerTests` 3 项通过。
- Worker 通用管道客户端已改为单 reader loop，按 `correlation_id` 分发最多 64 个在途响应，并将 Wake 等主动消息置于独立流；`RuntimeWorkerSessionTests` 2 项通过。`RuntimeSupervisorControlTests` 覆盖 Supervisor 早退即时失败。
- T127 已完成软件闭环：`RuntimeSupervisorControl` 通过 broker readiness gate 等待 player-1..4、audio、office 的 `WorkerReady`，仅成功后 `ArmAsync`；缺失角色、错误 group epoch、Office 未 ready、连接断开、Supervisor 早退和启动超时均 fail closed。失败路径调用受控 stop、`FailStartAsync` 持久化 `faulted`，Supervisor 登记失败时清理本次已启动的自有子进程。`RuntimePipeBrokerTests` readiness 场景、`DeviceAndSystemEndpointTests` 8 项及 `RuntimeAuthorityRepositoryTests` 4 项通过。真实四屏/Office/VLC 启停仍保留在 T116/T129，未以 simulation 结果替代。
- T123：PlayerWorker 已接入启动参数、管道领取/续租/结果/状态循环及真实 WPF/WebView2/LibVLC/PDF/图片资源宿主；PDF 首页/总页数在 OPEN 后同步，WebView2 `ProcessFailed` 撤销健康资格，并通过 OfficeRequest 接入动态 PowerPoint 流程。
- T124：AudioWorker 已接入 LibVLC 命令执行和自然结束通知；`BackgroundAudioState` 使用 desired/observed generation fencing 拒绝旧状态，OPEN/自动切歌保留音量、静音与 loop，重复结束回调复用稳定 event ID。`RuntimeProjectionTests` 与音频执行器测试通过。
- T125：PlayerWorker → ControlHost broker → PowerPointHost → PlayerWorker 的 OfficeRequest/OfficeResult 闭环已实现；稳定 operation ID、参数指纹冲突拒绝、持久 operation、group/host/slot epoch 和 deadline 复验、单 STA/COM 串行执行、唯一动态槽、匹配摘要 PDF fallback、HWND PID/start-time/DPI/样式附着及自有 Office 安全关闭均已接线。超时 OPEN 持久化为 uncertain、拒绝迟到结果且不会释放未知副作用的动态槽。真实媒体、Office COM/HWND、混合 DPI 和硬件画面仍由 T116/T129 门禁验证。
- T126：Hardware ControlHost 使用 Per-Monitor-V2 上下文枚举真实 Windows 显示器，并通过 NAudio/Core Audio 读取和设置默认渲染端点；simulation 保持虚拟显示器与数据库音量。不存在交互桌面、显示器或默认端点时返回稳定 unavailable 诊断，硬件写失败不覆盖持久意图；PlayerWorker 按已验证设备名处理 `SELECT_DISPLAY`。本机只读探针识别 1 台 `2560×1600` 主显示器和可用 Core Audio 端点，不据此宣称四屏/实际播放通过。

## 运行时启动缺陷修复（2026-09-11）

首次在 `SafetyMode=Hardware` 上执行 `POST /api/system/restart/` 时，ControlHost 完成数据库初始化后不再监听 HTTP。按系统化调试在依赖图与管道握手中定位到三个真实缺陷，全部先加失败回归再修根因：

- **单例构造环**：`RuntimePipeBroker → AudioFinishedEventProcessor → BackgroundAudioService → CommandCoordinator → ICommandWakeNotifier → RuntimePipeBroker`。新增 `RuntimeCommandWakeNotifier` 延迟解析 broker 切断环；`HardwareControlHostStartupTests` 修复前 10 秒超时、修复后约 1 秒 `/health/ready` 200。
- **停止确认不幂等**：初始 `Stopped` 状态下 `CompleteStopAsync` 只接受 `Draining`，使首次启动 500。现同 `group_epoch` 的 `Stopped` 幂等返回；回归 `RuntimeLifecycleTests.CompleteStopIsIdempotentForTheCurrentStoppedEpoch`。
- **子进程先连接、Supervisor 后登记**：PowerPointHost 被 ControlHost 以“未登记身份”拒绝后退出并触发整组停止。新增启动门 `RuntimeStartGate`/`RuntimeStartGateHandle`，Supervisor 登记完成后才放行子进程连接；回归 `RuntimeStartGateTests`。

修复后真实实测：`POST /api/system/restart/` 返回 `{"success":true,"group_epoch":3,"detail":"Supervisor restart 的全部 Worker 已就绪。"}`，
4 个 PlayerWorker、AudioWorker、PowerPointHost、MediaMTX 全部在线。细节见 `docs/qa/003-runtime-lifecycle.md`。

同批次还修复了 Electron 上传的 CSRF 缺陷（multipart 只读 `document.cookie`，无法读取 Electron 安全 Cookie），
统一走 `resolveCsrfToken`；修复后真实包上传与原生保存下载均通过，下载文件 SHA-256 与源文件一致。

**这些只证明“真实运行时能被一次拉起”与“客户端文件链路可用”，不证明四屏画面、Office COM、VLC 解码或音频输出效果。**

## 已归档 D2 受限远程调试（2026-09-14）

- 在 `D:\dotnet` 安装与 `global.json` 一致的 .NET SDK 10.0.400，并配置 `admin` 用户级 `DOTNET_ROOT`/PATH；NuGet 缓存位于 `D:\nuget\packages`。
- 全新锁定还原首次发现无 RID 项目的 `packages.lock.json` 被先前 `publish -r win-x64` 写入 RID，导致 clean machine 的 solution restore 无法同时满足应用与测试项目。重新生成 5 个无 RID 项目锁文件后，D2 执行 `dotnet restore ScpCv.sln --locked-mode` 与 `dotnet build ScpCv.sln -c Debug --no-restore` 通过，0 警告、0 错误。
- `run-headless.ps1` 新增多 Origin、精确 `AllowedHosts`、通配监听的本机探测，以及口令文件/进程环境传递；真实进程命令行和生成的 `headless-launch.ps1` 均不含开发口令。对应静态安全测试与真实 ControlHost Host-header 集成测试通过。
- D2 仅以 `SafetyMode=Hardware` 启动 ControlHost，监听 `0.0.0.0:18443`；Host 白名单包含 localhost、127.0.0.1 和当时的 D2 固定地址。Windows 防火墙仅在 Private profile 对 LocalSubnet 开放 TCP 18443。
- 从开发机经当时的 D2 固定地址请求 `/health/ready` 返回 200，对应的凭据 CORS 预检返回 204，并精确回显 Origin 与 credentials。该地址已退役，不再是项目活动配置。验证时 PlayerWorker、AudioWorker、PowerPointHost、MediaMTX 进程数为 0，没有执行播放、显示选择、音量或设备写入。

该证据只收口 SDK、clean build 和受限 HTTP 控制面连通性。T115/T116/T129 所需真实 Worker、画面、Office/VLC/MediaMTX/音频及 60 分钟测试仍未执行，T118 继续阻塞。

## D4 兼容性例外部署（2026-09-17）

`D4`（`192.168.5.194`）为 Windows 10 Pro 1909 / build 18363.1556，低于项目 `net10.0-windows10.0.19041.0` 目标平台基线。
用户批准按兼容性例外继续部署；本记录只描述该机器上的实测结果，不修改受支持平台声明（见 `research.md` R05）。

安装与配置（`D:\dotnet` SDK 10.0.400、`D:\nodejs` Node v24.13.0 与 pnpm 11.22.0、`C:\Program Files\Git` Git 2.55.0.windows.5、
`D:\nuget\packages` NuGet 缓存、`D:\SCP-cv` 克隆自 `git.bbt.sspu.edu.cn` 的 `refactor/003-dotnet-runtime@ed26f2d` 并拉取 LFS
运行时资源）见 `docs/qa/003-workstation-runbook.md` 0.1 节。

实测证据：

- `dotnet restore ScpCv.sln --locked-mode`：exit 0，15 个项目。
- `dotnet build ScpCv.sln -c Debug --no-restore`：0 警告 0 错误（30.5 s）。
- `dotnet test ScpCv.sln -c Debug --no-build --filter "Category!=Physical"`：191 通过 / 0 失败。
- 前端 `pnpm test` 40 通过；`pnpm run build:web` 成功。
- Hardware ControlHost（无 Worker）`/health/ready` 200；本机 csrf/login/me/logout 均 200；`/api/displays/` 为
  4×1920×1080 横排 + 1×1920×1200。
- 开发机跨网段：`/health/ready` 200、凭据 CORS 预检 204、白名单外 Host 400、`http://192.168.5.194:5173/` 200。

边界与残余风险：

- `dotnet-install.ps1` 自身提示不校验 Windows 版本支持；1909 不在 .NET 10 支持矩阵内，兼容性例外存在未来补丁或运行时回归风险，
  升级到受支持版本后应重跑本节。
- D4 的 `以太网` 为 Public 配置文件，原有 `-Profile Private -RemoteAddress LocalSubnet` 规则不生效；已按源地址收窄新增规则，
  未把网络类别改成 Private。
- 本轮未启动任何 Worker，也未执行四屏/Office/VLC/MediaMTX/音频/60 分钟门禁；T115/T116/T129 仍未完成，T118 继续阻塞。
- D4 未安装 PowerPoint，`Office 16 Click-to-Run Extensibility Component` 的存在不能当作 PowerPoint 可用。

## D4 真实运行时启动与缺陷修复（2026-09-17）

在 D4 交互桌面（session 1）启动 `SafetyMode=Hardware` 的全部受管进程，并在真实屏幕上执行 `SELECT_DISPLAY`。过程中发现并修复一个此前从未暴露的缺陷：

- **现象**：`POST /api/system/restart/` 后 PlayerWorker 弹出“PlayerWorker 运行时故障：CommandResult 未被接受：error”模态框并停止响应；ControlHost 记录
  `System.InvalidOperationException: The requested operation requires an element of type 'Number', but the target element has type 'Null'.`
- **根因**：`PlayerRuntimeHost.Snapshot()` 在无源时把 `source_id` 序列化为 JSON `null`，而 `RuntimeProjectionPublisher.ReadInt64`（及同类 `ReadInt32`）直接调用
  `JsonElement.TryGetInt64`/`TryGetInt32`。这两个方法在 `ValueKind` 不是 `Number` 时**抛异常**而不是返回 false——已在 .NET 10 上单独验证：`Null` 与 `String` 均抛
  `InvalidOperationException`。异常使 ControlHost 以 `error` 帧回应 `command_result`，Worker 端据此抛出并停机。
- **修复**：读取数值前校验 `ValueKind == JsonValueKind.Number`，并把同样缺守卫的同类写入点一并收口（`RuntimeProjectionPublisher`、`ScenarioEndpoints`、
  `PlaybackEndpoints`、`BackgroundAudioEndpoints`、`MediaEndpoints`、`PresentationEndpoints`、`AudioCommandExecutor`、`RuntimePipeBroker.Office`、`RuntimeMessageDispatcher`）。
- **回归**：新增 `RuntimeProjectionTests.DisplayStateReportWithNullSourceIdIsAccepted`。移除守卫时该用例以完全相同的异常失败（红/绿已验证），恢复守卫后通过；
  本地 `dotnet test -c Debug --filter "Category!=Physical"` 共 192 项通过、0 失败。
- **实测**：修复后 `POST /api/system/restart/` 返回 `{"success":true,"group_epoch":5,"detail":"Supervisor restart 的全部 Worker 已就绪。"}`；
  4×PlayerWorker、AudioWorker、PowerPointHost、MediaMTX 全部在线，`control-host.err.log` 为空；对四个窗口执行 `POST /api/displays/select/` 全部 200，
  `target_display_label` 正确落库。

显示配置（按用户指定）：DISPLAY2/3/4/5 作为四路输出，集显 DISPLAY1 不参与播放。四块屏已切到 `3840×2160`，但 EDID 只提供 ≤30Hz 模式，刷新率因此由 60Hz 降到 30Hz；
切换分辨率会打乱虚拟桌面坐标，已用 `ChangeDisplaySettingsEx` 重排为 `0 / 3840 / 7680 / 11520`（y=0），控制屏移至 `15360,0`。

未执行：真实媒体播放、Office COM/HWND 附着、VLC/SRT/MediaMTX 播放、真实音频输出与 60 分钟混合测试；T115/T116/T129 状态不变。

## D4 更新后的启动验证（2026-09-24）

本机 `main` 经 secondary（GitLab）同步到 D4 `D:\SCP-cv` 的 `47874b7`。D4 Debug 构建及前端测试、类型检查和 Web 构建通过。
本机非 Physical .NET 测试 221/221 通过（清除该次测试进程的 HTTP 代理变量；带代理时两项回环断言误收 502）。
现场启动发现 `run-headless.ps1` 对 restart 请求固定 15 秒超时，导致 Worker 尚未就绪时客户端取消，运行组进入 `Faulted`；
修复为使用 `ReadyTimeoutSeconds`，并新增回归测试。同时补上 Supervisor 受认证心跳与 SQLite 测试清理重试。
修复后 D4 `SafetyMode=Hardware` 运行组到达 `Armed`（group epoch 29），全部七个受管进程在交互会话就绪，
ControlHost 与前端跨网段 HTTP 均为 200。详细命令、日志及验证边界见 `docs/qa/003-windows-runtime.md`。
本轮未执行 T115/T116/T129 的真实媒体和 60 分钟门禁，状态不变；已提前完成的 T118 清理由 2026-09-22 用户指令授权。

## D4 多源冒烟与剩余缺口（2026-09-24）

在 `2528f63` 上完成浏览器上传及 Hardware 打开：图片、视频、PDF、网页、低音量音频和 PPTX；
另经 `/api/sources/local/` 验证播放主机本地路径。交互桌面截图确认图片、视频帧、PDF、网页及真实 PPT
在指定输出屏出现；PPTX 9 页的 `next`/`prev`、关闭均实测。对应 003/FR-003、FR-011、FR-019
获得本轮局部现场证据，详细步骤、过程缺陷、清理记录见 `docs/qa/003-windows-runtime.md`。

WebView2 脱离视觉树卡死、PowerPoint 的 STA 上下文丢失、音频首个快照长期加载、受控停机旧租约堵塞、
`prev`/`previous` 导航合同不一致均已修复；新增自动回归，非 Physical .NET 测试 226/226 通过。
MediaMTX 真实 RTSP 短时推拉流通过，但应用缺直播 URL 创建入口，003/FR-017 只能记为**部分**；
T134/T135 承接录入、端到端打开与 10 分钟预热（SC-005）。
短视频自然结束后 API 状态仍为 `playing`，由 T136 补原生结束事件的状态投影。
Office 自动化实例在停机后可能残留，由 T133 明确归属和退出条件。
已知的预案音量物理应用缺口由 T137 承接，不随本次源冒烟标记为通过。
真实声卡输出、四屏同时混播、4K/混合 DPI 与 60 分钟稳定性仍未执行；T115/T116/T129 状态不变。

## D4 共享前端探索测试（2026-09-24）

在 D4 Hardware ControlHost 上以真实浏览器检查桌面/平板/手机视口、主导航及全部页面、登录/退出、
媒体源/文件夹、背景音乐、预案、设置、SSE 重连与 REST 请求；`pnpm test` 40/40、类型检查及 Web 构建通过。
安全范围内的临时测试数据已清理，现场设备副作用入口未执行。发现高 1、中 6、低 5 共 12 项可复现前端/状态问题，
包括文件夹编辑入口失效、跨页文件夹筛选隐藏音频、暂停状态不更新与可访问性缺陷。
完整复现与验证边界见 `docs/qa/003-frontend-exploratory-20260924.md`；
这些发现未修复，FR-001/FR-028–030 与 SC-001/SC-010 的三端等价验收不能因本轮网页测试宣称全部通过。

## Spec Kit 一致性分析（T119）

2026-09-08 对 `spec.md`、`plan.md`、`tasks.md`、项目宪章和实现路径进行只读交叉检查：

- Spec Kit 校验：通过（`validate_specs.py --specs-dir specs`）。
- `git diff --check`：通过；仅报告现有 CRLF/LF 转换提示，无空白错误。
- FR-001–FR-030：均有计划和任务映射；SC-001–SC-010：均有自动或人工证据条目。
- 任务依赖原要求 T118 等待 T107–T116；2026-09-22 用户明确接受实机门禁未完成的返工风险并要求提前清理，偏差和本地软件门禁已记录在上文。
- 发现并保留的未完成项：T115/T116/T129 为硬件、媒体与性能门禁，已标为待工作站执行（见 `docs/qa/003-workstation-runbook.md`），不冒充通过；T050/T113/T128 已于 2026-09-11 完成并记录证据。
- 无宪章 MUST 冲突、无未映射核心需求、无新增迁移/回滚工程。
