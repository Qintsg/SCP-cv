# 验证记录

**日期**：2026-09-19（第 1~3 轮）、2026-09-21（第 4、5 轮）
**平台**：Windows 11 开发机（**不在视频墙网段**；未以 `-SafetyMode Hardware` 连接现场墙面）
**对应需求**：`spec.md` FR-001 ~ FR-020、SC-001 ~ SC-007
**变更集**：
- 第 1 轮——视频墙下发层补齐（`VideoWallController.cs` 新增，`VideoWallSequenceBuilder`、`RuntimeStateService`、`ScenarioService`、`Program.cs` 修改）+ 3 个测试文件。
- 第 2 轮（同日，补 FR-018/FR-020 缺口）——`VideoWallLog.cs` 新增，`VideoWallController.cs` 注入日志；`docs/使用文档.md` §4.5、`docs/维护文档.md` §8.6、`docs/CHANGELOG.md`、`docs/known-pitfalls.md` 同步；新增 `VideoWallLoggingTests.cs` 与测试替身 `RecordingLogger.cs`。
- 第 3 轮（同日，补 FR-012/FR-013 缺口）——只新增测试，**未改动任何产品代码**：`VideoWallControllerTests.cs` 加 2 个用例，`HostHardwareIntegrationTests.cs` 加 1 个用例。两轮探针过后源码与探针前逐字节一致。
- 第 4 轮（2026-09-21，补「帧内容待抓包复核」里**本机可做**的那一半）——**未改动任何产品代码**：新增 `tools/generate_video_wall_golden.py`（从旧 Python 实现的实际运行结果导出黄金样本）、`tests/ScpCv.Infrastructure.Tests/Fixtures/video-wall-packets.json`（生成产物，400 个控制包）、`VideoWallGoldenPacketsTests.cs`（逐包校对两种模式）、`tests/test_video_wall_golden_fixture.py`（守样本与旧实现的一致性）、`ScpCv.Infrastructure.Tests.csproj` 复制样本到输出目录；`docs/known-pitfalls.md`、`docs/CHANGELOG.md` 同步。issue #2 的抓包复核本身**仍未完成**（见末节）。
- 第 5 轮（2026-09-21，把「仅代码复核」的传输层行为换成回环实测）——**未改动任何产品代码**：新增 `runtime-dotnet/scripts/videowall-loopback.ps1`（在指定网卡上挂/摘 `192.168.5.101~150/32` 别名，`store=active`，重启即消失）、`VideoWallLoopbackTests.cs`（5 个用例在真实 TCP 上跑下发）、假节点 `FakeVideoWallNodes.cs`、别名探针与条件跳过 `VideoWallLoopbackAliases.cs`；`runtime-dotnet/tests/README.md`、`docs/known-pitfalls.md`、`docs/CHANGELOG.md` 同步。实测：全节点 200 包 **224~240 ms**；单节点掉线或拒连 **12.4~12.6 s** 后中止（5 次 2 秒超时 + 0.2/0.4/0.8/1.0 秒退避）；400 ms 取消在 **409 ms** 原样抛出且零日志。**仍未验证**：真实网段的时延与丢包、TCP_NODELAY、issue #2 的帧内容（回环收到的就是自己发的包，回声不构成证据）。

## 自动化执行记录

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| .NET 完整测试（第 1 次） | `dotnet test runtime-dotnet/ScpCv.sln` | **207/208**，失败 `SseEventStreamTests.IdleSubscriptionEmitsHeartbeat`（判定见末节） |
| .NET 完整测试（第 2 次） | 同上 | **208/208 通过** |
| .NET 完整测试（第 3 次，与 8 次 SSE 类重复同时运行，人为加负荷） | 同上 | **208/208 通过** |
| SSE 类重复（负荷中） | `dotnet test runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScpCv.ControlHost.Tests.csproj --no-build --filter "FullyQualifiedName~SseEventStreamTests"` ×8 | 8/8 通过 |
| 集成层硬件入口 | `dotnet test runtime-dotnet/tests/ScpCv.Integration.Tests/ScpCv.Integration.Tests.csproj --filter "FullyQualifiedName~HostHardwareIntegrationTests"` | 7/7 通过 |
| Spec Kit 校验 | `python .specify/scripts/python/validate_specs.py --specs-dir specs` | 通过（exit 0，无未完成占位符） |
| .NET 完整测试（第 2 轮，补日志后） | `dotnet test runtime-dotnet/ScpCv.sln` | **213/213 通过** |
| .NET 完整测试（第 3 轮，补 FR-012/FR-013 后） | `dotnet test runtime-dotnet/ScpCv.sln` | **216/216 通过** |
| .NET 完整测试（第 4 轮，补黄金样本比对后） | `dotnet test runtime-dotnet/ScpCv.sln` | **219/219 通过** |
| 黄金样本与旧实现一致性（第 4 轮） | `python -m pytest tests/test_video_wall_golden_fixture.py -q` | **3 passed**（python 环境见下注） |
| 回环下发·配置一（第 5 轮，50 个别名全就绪） | `videowall-loopback.ps1 -Apply` 后 `dotnet test .../ScpCv.Infrastructure.Tests --filter "Requires=VideoWallLoopback"` | **4 通过 / 1 跳过**（跳过的是需要「某节点没有别名」的那条） |
| 回环下发·配置二（第 5 轮，故意缺 `192.168.5.150`） | `videowall-loopback.ps1 -Apply -Except 192.168.5.150` 后同上 | **1 通过 / 4 跳过** |
| 归还别名后复跑（第 5 轮） | `videowall-loopback.ps1 -Clear`（`-Status` 为 0/50）后同上 | **5 跳过、0 失败**：本机已恢复原状，用例也不假装跑过 |
| .NET 完整测试（第 5 轮，常规命令） | `dotnet test runtime-dotnet/ScpCv.sln -c Debug --no-build --filter "Category!=Physical"` | **219/219 通过**（5 个新用例都带 `Physical` trait，被该命令排除，分项见下） |

注：第 4 轮的 Python 用例**不是**在 `uv` 项目环境里跑的——本机没有 `uv`，`uv run pytest tests/ -v` 仍未执行（同末节）。为取得证据，用仓库外的临时 venv（`%TEMP%\scp-cv-golden-venv`，装 `django==6.0.3`、`djangorestframework==3.17.1`、`screeninfo`、`psutil`、`requests`、`pywin32`、`pytest`、`pytest-django`）执行了该文件与生成脚本；临时环境不属于仓库，已可随时删除。**其余 Python 测试与 Ruff 仍为未执行**。

第 1 轮第 2 次分项：Domain 38 / Windows 11 / Contracts 18 / Infrastructure 28 / Integration 57 / ControlHost 56 = **208**（补下发层前为 197，本轮新增 11 个测试）。

第 2 轮分项：Domain 38 / Windows 11 / Contracts 18 / Infrastructure 33 / Integration 57 / ControlHost 56 = **213**（新增 5 个日志测试，未改动其它分层）。

第 3 轮分项：Domain 38 / Windows 11 / Contracts 18 / Infrastructure 35 / Integration 58 / ControlHost 56 = **216**（新增 2 个控制器测试 + 1 个集成测试，未改动产品代码）。

第 4 轮分项：Domain 38 / Windows 11 / Contracts 18 / Infrastructure 38 / Integration 58 / ControlHost 56 = **219**（Infrastructure 新增 2 个模式用例 + 1 个样本覆盖用例，未改动产品代码）。

第 5 轮分项：常规命令的分项与第 4 轮**逐一相同**（= **219**）——第 5 轮的 5 个用例全带 `Physical` trait，不在这个数里，别把 219 当成「含回环实测」。单独跑 `Requires=VideoWallLoopback` 时按机器现状：配置一 5 中 4 通过 1 跳过、配置二 5 中 1 通过 4 跳过、未做准备或已归还别名 5 全跳过。

### 回归测试确实能发现该缺陷（探针观察，已撤销）

2026-09-19 把两处入口的下发调用临时替换为空操作（`RuntimeStateService.SetRuntimeModeAsync`、`ScenarioService.ActivateAsync`）后，立即重跑集成类：

```text
失败!  - 失败: 4，通过: 3，已跳过: 0，总计: 7 - ScpCv.Integration.Tests.dll (net10.0)
```

失败的正是本次新增的 4 个：`RuntimeModeSwitchDispatchesVideoWallBeforePersisting`、`VideoWallFailureKeepsPersistedRuntimeModeAndMutePolicy`、`ScenarioActivationDispatchesVideoWallForItsBigScreenMode`、`ScenarioActivationFailureLeavesRuntimeModeUnchanged`；原有 3 个集成测试不受影响。即：这两个入口的下发一旦被摘掉，测试会失败，不会静默通过。

探针已撤销。撤销后两个文件与探针前逐字节一致（md5 `5888a760fc3ae2293988818321155b46` / `0b50e6e1920f16af5dce10117d3ff694`），源码内无探针残留；该字节状态即通过上表第 2、3 次完整测试的状态，故未再次重跑。

### 新增测试（11 个）

| 文件 | 数量 | 覆盖 |
| --- | --- | --- |
| `tests/ScpCv.Infrastructure.Tests/VideoWallControllerTests.cs` | 5 | 阶段顺序与清屏→映射 200 ms 间隔、阶段内并发上限 25、单节点重试 5 次与 0.2/0.4/0.8/1.0 s 退避、任一阶段失败即中止（后续阶段 0 包）、默认参数与旧版常量逐项一致、Simulation 空实现仍校验模式 |
| `tests/ScpCv.Integration.Tests/HostHardwareIntegrationTests.cs` | 4 | `PATCH /api/runtime/` 先下发后落库、下发失败不改运行态与静音策略、预案激活按其模式下发、预案下发失败整体不生效 |
| `tests/ScpCv.ControlHost.Tests/VideoWallRegistrationTests.cs` | 2 | Simulation 解析非硬件控制器、Hardware 解析 TCP 控制器 + 传输 + 下发参数 |
| `tests/ScpCv.Infrastructure.Tests/VideoWallSequenceTests.cs`（补下发层前已存在） | 0（沿用） | 包字节、节点集合、45/46 互换补丁、分块与组播地址 |

第 2 轮新增（5 个，`tests/ScpCv.Infrastructure.Tests/VideoWallLoggingTests.cs`，日志替身 `RecordingLogger.cs`）：

| 用例 | 断言 |
| --- | --- |
| `SuccessfulDispatchLogsModeAndPacketCount` | 成功恰好 1 条 Information，含模式与 `控制包 200 个`；无 Error |
| `FailedDispatchLogsThePhaseNodeCountAndReason` | 失败恰好 1 条 Error，含模式、`中止于阶段 clear`、`失败节点 50 个`、首个失败节点 `192.168.5.101:4830`；且**不出现**「下发完成」 |
| `RetryThatSucceedsIsLoggedWithTheAttemptCount` | 每个包失败 2 次后成功 → 恰好 200 条重试记录，均为 `第 3/5 次尝试` |
| `CancelledDispatchIsLoggedAsNeitherFailureNorSuccess` | 取消传播为 `OperationCanceledException`（不是 `VideoWallException`），且**零条**日志 |
| `SimulationLogsTheSkipInsteadOfASuccessfulDispatch` | Simulation 恰好 1 条 **Information**，写明 `本应下发 200 个控制包`；不出现「下发完成」 |

四条日志的级别都 ≥ `Information`：`appsettings.json` 的默认级别就是 `Information`（`Microsoft.AspNetCore` 为 `Warning`，不影响本模块的 category），若把 Simulation 那条写成 `Debug`，文档让维护者去找的日志在现场根本不会输出——首版即踩了这个坑，已改并加了级别断言。

第 3 轮新增（3 个）：

| 用例 | 位置 | 断言 |
| --- | --- | --- |
| `RepeatingTheCurrentModeStillDispatchesTheWholeSequence` | `HostHardwareIntegrationTests` | 运行态初值即 `single`，连续两次 `SetRuntimeModeAsync("single")` → 控制器收到 `["single", "single"]` 两次下发；服务层不得因模式未变而跳过 |
| `DispatchingTheSameModeTwiceSendsTheWholeSequenceTwice` | `VideoWallControllerTests` | 同模式下发两次 → 400 个包、200 个 (节点, 阶段) 键各尝试 2 次；控制器不得去重或短路 |
| `ConcurrentDispatchesAreSerializedInsteadOfInterleaving` | `VideoWallControllerTests` | 把首个「映射」发送卡住后再发起第二次下发：此刻时间线折叠后必须恰好是 `clear → wait → mapping`，第二次一个包都不能发；释放后整段折叠为两次完整的四阶段序列 |

FR-012 分两层测是因为服务层与控制器层**任一**短路都会破坏「现场补救路径」：集成用例证明 `SetRuntimeModeAsync` 没有相同模式早退，控制器用例证明 `DispatchAsync` 没有去重。

配套的文档缺口（属 FR-020）：这条补救路径此前只存在于代码里——前端 `BigScreenModeButtons.vue` 的 `selectMode` 同样刻意不因模式相同而短路，当前模式的按钮在切换完成后可再次点击——但两份运维文档都没告诉现场怎么用。已补 `docs/使用文档.md` §4.5 与 `docs/维护文档.md` §8.6。

### FR-012 / FR-013 的测试确实能发现缺陷（探针观察，已撤销）

2026-09-19 分两批注入缺陷：

1. **控制器层**——给 `TcpVideoWallController` 加 `_lastMode` 去重（重复模式直接 return），并把 `_gate` 从 `(1, 1)` 放宽到 `(int.MaxValue, int.MaxValue)`（等效于无串行化）：

```text
失败!  - 失败: 2，通过: 33，已跳过: 0，总计: 35 - ScpCv.Infrastructure.Tests.dll (net10.0)
```

失败的正是 `DispatchingTheSameModeTwice…` 与 `ConcurrentDispatchesAreSerialized…`，其余 33 个用例不受影响。串行化用例的失败信息即交错特征：

```text
Expected: [···, "wait:1", "send:mapping"]
Actual:   [···, "wait:1", "send:mapping", "send:clear", "wait:1", "send:mapping"]
```

2. **服务层**——在 `SetRuntimeModeAsync` 下发之前插入「模式与当前一致就直接返回」：

```text
失败!  - 失败: 1，通过: 57，已跳过: 0，总计: 58 - ScpCv.Integration.Tests.dll (net10.0)
```

失败的正是 `RepeatingTheCurrentModeStillDispatchesTheWholeSequence`。

探针已全部撤销。`VideoWallController.cs`（md5 `a0a61b4426c29df864901fc73c2beb2f`）与 `RuntimeStateService.cs`（md5 `5888a760fc3ae2293988818321155b46`）均与探针前逐字节一致，`git diff -- runtime-dotnet/src/` 为空，即第 3 轮**没有改动任何产品代码**。

### 日志测试确实能发现该缺陷（探针观察，已撤销）

2026-09-19 把两处构造函数的日志注入改为恒定的 `NullLogger.Instance`（模拟「日志全部不可达」）后重跑基础层：

```text
失败!  - 失败: 4，通过: 29，已跳过: 0，总计: 33 - ScpCv.Infrastructure.Tests.dll (net10.0)
```

失败的正是新增 5 个中的 4 个（`SuccessfulDispatch…`、`FailedDispatch…`、`RetryThatSucceeds…`、`SimulationLogsTheSkip…`）；第 5 个 `CancelledDispatchIsLoggedAs…` 断言的是「不记日志」，不受该探针影响，故仍通过——这是该用例的预期行为，不是漏检。

探针已撤销，撤销后文件与探针前逐字节一致（md5 `a0a61b4426c29df864901fc73c2beb2f`），源码内无探针残留；该字节状态即上表第 2 轮完整测试的状态。

### 第 4 轮新增（3 个）

| 用例 | 位置 | 断言 |
| --- | --- | --- |
| `WholeSequenceMatchesLegacyGoldenPackets(single)` | `VideoWallGoldenPacketsTests` | 200 个包与旧实现黄金样本逐项一致（阶段、`IP:端口`、包字节） |
| `WholeSequenceMatchesLegacyGoldenPackets(double)` | 同上 | 同上；覆盖右半墙 25 个 `mapping_ws22_right_half` 的组播地址、窗口号与裁切区 |
| `GoldenFixtureOnlyCoversTheTwoRuntimeModes` | 同上 | 样本的模式集合恰为 `single`/`double`，与运行态取值一一对应 |

另有 Python 侧 3 个用例（`tests/test_video_wall_golden_fixture.py`）守在样本这一头：样本可读、覆盖两种模式、逐包等于 `build_sequence` 的当前结果——样本一旦与旧实现脱节，.NET 侧的比对就退化成「拿旧结果对新代码」，本文件不允许多出这条静默路径。

**为什么加这一轮**：issue #2「视频墙单双屏切换帧内容待抓包复核」的抓包复核本机做不了（见末节），但它顺带暴露了一个本机可做的缺口——`VideoWallSequenceTests` 只断言单屏模式**第一个**映射包的字节，**双屏模式 50 个映射包一个字节都没有断言**（`SplitModeUsesWs21OnLeftHalfAndWs22OnRightHalf` 只数 phase）。黄金样本取自旧 Python 实现的**运行结果**，不是从本实现或规范表格转抄，因此能发现迁移中任何逐节点走样。

### 逐包比对确实能发现迁移走样（探针观察，已撤销）

2026-09-21 分两批注入「旧断言抓不到、新断言必须抓到」的走样：

1. **.NET 侧**——把右半墙映射包的组播地址与窗口号改成左半墙的值（`Ws22MulticastAddress, 2` → `Ws21MulticastAddress, 1`）：

```text
失败!  - 失败:     1，通过:     6，已跳过:     0，总计:     7 - ScpCv.Infrastructure.Tests.dll (net10.0)
```

失败的正是 `WholeSequenceMatchesLegacyGoldenPackets(bigScreenMode: "double")`，报「第 75 个控制包与旧实现不一致」，两个包并排给出（`…0002e0010138…` 对 `…0001e0010137…`）。**同一次运行里 `VideoWallSequenceTests` 的 4 个用例全部通过**——盲区即在此。

2. **Python 侧**——把样本里 double 模式第 75 项改成同样的错值，再跑一致性用例：

```text
1 failed, 2 passed
```

失败于 `test_golden_fixture_matches_legacy_build_sequence`，pytest 直接给出「At index 75 diff」。随后重跑生成脚本，样本 md5 回到 `c7a23b1f1ff0855456f699aa6dfdf78d`（与探针前一致），`--check` 通过。

探针已撤销：`VideoWallSequenceBuilder.cs` md5 `45a175c93a3473e8e4b01f8cb3dfc5dc` 与探针前一致，`git diff -- runtime-dotnet/src/` 为空，即本轮**未改动任何产品代码**。

### 第 5 轮新增（5 个，`VideoWallLoopbackTests`）

跑法（改网卡地址要管理员权限；别名一律 `store=active`，重启自动消失，不写持久配置）：

```powershell
runtime-dotnet/scripts/videowall-loopback.ps1 -Apply                      # 配置一：50 个节点地址全就绪
dotnet test runtime-dotnet/ScpCv.sln -c Debug --filter "Requires=VideoWallLoopback"
runtime-dotnet/scripts/videowall-loopback.ps1 -Apply -Except 192.168.5.150 # 配置二：造一个「连不上」的节点
dotnet test runtime-dotnet/ScpCv.sln -c Debug --filter "Requires=VideoWallLoopback"
runtime-dotnet/scripts/videowall-loopback.ps1 -Clear                       # 归还全部 50 个地址
```

假节点就在这些别名上监听真实端口 `4830`，所以这里跑的是**真的** `TcpVideoWallTransport` + 控制器，不是替身。

| 用例 | 前置 | 断言与实测 |
| --- | --- | --- |
| `SingleModeReachesEveryNodeByteForByteWithinThreeSeconds` | 配置一 | 50 个节点各按 `clear → mapping → commit → refresh` 收到 4 个包，**逐字节**等于 `Build("single")`；恰好 1 条「下发完成」日志、无重试噪声。实测 **224~240 ms** |
| `DoubleModeSendsEachHalfWallItsOwnMulticastWindowAndSourceRectangle` | 配置一 | 同上（**double** 200 包）；另从收到的字节里解出组播地址、窗口号和源矩形，按列坐标核对：左 5 列全是 `224.1.1.55`/窗口 1、右 5 列全是 `224.1.1.56`/窗口 2，源矩形 `768×432` 按列行平铺，包内两处窗口号一致。实测 **230~232 ms** |
| `NodeRefusingConnectionsAbortsAtTheClearPhaseBeforeAnythingElseGoesOut` | 配置一 | `192.168.5.101` 挂着别名但没人监听：报错点名 `192.168.5.101:4830 phase=clear`；其余 49 个节点**只收到清屏包**（映射/提交/刷新 0 个）；恰好 1 条 Error 日志。实测 **12.4 s** 后中止 |
| `UnreachableNodeBurnsFiveTwoSecondTimeoutsThenAbortsAtTheClearPhase` | 配置二 | `192.168.5.150` 无别名（连出去没人应答）：报错点名 `192.168.5.150:4830 phase=clear`；其余 49 个节点同样只收到清屏包；日志含「中止于阶段 clear」「失败节点 1 个」且**无**「下发完成」。实测 **12.5 s** |
| `CancellingMidDispatchPropagatesCancellationWithoutLoggingSuccessOrFailure` | 配置一 | 400 ms 时取消：**409 ms** 抛 `OperationCanceledException`（不是 `VideoWallException`）、**0 条日志**、收到的只有 12 字节清屏包——FR-017 的传输层重抛第一次有实测 |

前两条用例还各自断言了「恰好一条日志」，即正常路径上没有多余的失败/重试记录。

前置条件的表达方式：`RequiresVideoWallAliasesAttribute` 覆写 `FactAttribute.Skip`，在发现阶段探测 50 个地址能否绑上 `4830`；不满足就按各自前置条件跳过（要 50 个 / 要恰好少 `.150` 这一个），跳过原因里直接写着准备命令。因此 `--filter "Category!=Physical"` 和没做过准备的机器都看不到它们，也不会因为缺别名而变红。

### 回环假节点能测什么、不能测什么

**能测**：真实 TCP 上的连接超时、重试退避、失败即中止、取消传播、字节保真——正是原先「仅代码复核」的那部分。单节点故障的两个形态（**拒连**、**掉线**）都实测为 12.4~12.6 s，都中止在**清屏**阶段：掉线节点在每个阶段都会被联系一次，最早那个阶段就把它挡住了。也就是说，静态别名配置下造不出「失败发生在更靠后的阶段」——那要让节点在序列中途掉线，可用窗口只有毫秒级，断言会变成掷骰子。可以据此给出上界：若故障发生在最后阶段，把前三个阶段的耗时（整段四阶段实测也才 224~240 ms）加到 12.5 s 上，也不到 13 s。

**不能测**（写在这里，免得后人把回声当证据）：

- **帧内容与常量是否正确**：假节点收到的是自己发的包。回环只证明「发出的字节与实现一致」，不证明「这些字节对真实墙面是对的」。这条仍归 issue #2。
- **TCP_NODELAY**：发送端设不设 `NoDelay`，接收端的应用层读不出差别，要抓包看有没有 40 ms 延迟。仍为代码复核。
- **真实网段的时延与丢包**：本机别名上的「拒连」要约 2 秒才回来（等于把 2 秒超时用满），真实设备上的 RST 可能只要毫秒级——所以 2 秒是**上界**，不是现场的实际等待时间。
- **写失败**：回环上连上之后写 12/45 字节全部成功，2 秒预算实际只兜住了 `ConnectAsync`（真实链路上接收窗口满时 `WriteAsync` 也可能挂住，本机造不出来）。

## FR / SC 映射

| 需求 | 验证位置 | 状态 |
| --- | --- | --- |
| FR-001 两条入口真实下发 | 集成 `RuntimeModeSwitchDispatches…`、`ScenarioActivationDispatches…` | 通过（探针可证伪） |
| FR-002 布局语义（单屏铺满 / 双屏左右分） | `VideoWallGoldenPacketsTests` 两种模式 400 个包逐包比对旧实现黄金样本（含裁切区、组播地址、窗口号、校验和）；`VideoWallLoopbackTests` 在真实 TCP 收到后按列坐标复核左右半墙字段；`VideoWallSequenceTests` 分块与组播断言 | 通过（**仅证明与旧实现等价，不证明旧值对墙正确**，见末节） |
| FR-003 阶段顺序 + 200 ms 间隔 | `VideoWallControllerTests` 折叠时间线断言 `clear → wait → mapping → commit → refresh` | 通过 |
| FR-004 阶段内并发 ≤25、阶段间串行 | `VideoWallControllerTests` 并发观测 | 通过 |
| FR-005 单节点 2 s 超时 + TCP_NODELAY | 超时已**实测**：`VideoWallLoopbackTests` 配置二整段 12.5 s = 5 次尝试 × 2 s 超时 + 退避（每次尝试都等满超时窗口）；TCP_NODELAY 在回环接收端**不可观测**，仍为代码复核 | 部分 |
| FR-006 重试 5 次 + 退避 | `VideoWallControllerTests` 尝试次数与退避序列；`VideoWallLoopbackTests` 在真实 socket 上量到整段 12.4~12.6 s，正好是 5 次尝试加 0.2/0.4/0.8/1.0 秒退避 | 通过 |
| FR-007 失败即中止 + 摘要格式 | `VideoWallControllerTests`（含「其余 45 个失败」与 `IP:端口 phase=…`）；`VideoWallLoopbackTests` 两种故障形态（拒连/掉线）都点名 `phase=clear`，且其余 49 个节点只收到清屏包 | 通过 |
| FR-008 失败不改运行态 / 静音策略 + `video_wall_error` | 集成 `VideoWallFailureKeepsPersistedRuntimeModeAndMutePolicy` | 通过 |
| FR-009 预案失败中止整个激活 | 集成 `ScenarioActivationFailureLeavesRuntimeModeUnchanged` | 通过 |
| FR-010 静音策略只在下发成功后应用 | 集成同 FR-008；`RuntimeStateService.cs:91-94` 顺序代码复核 | 通过 |
| FR-011 非法模式下发前拒绝 | `VideoWallControllerTests` + `VideoWallSequenceTests` 未知模式抛错 | 通过 |
| FR-012 重复同一模式仍完整重下 | 服务层 `RepeatingTheCurrentModeStillDispatchesTheWholeSequence` + 控制器层 `DispatchingTheSameModeTwiceSendsTheWholeSequenceTwice`；探针可证伪（两层各注入一次短路，对应用例失败） | 通过 |
| FR-013 并发下发串行化 | `ConcurrentDispatchesAreSerializedInsteadOfInterleaving`（卡住首个映射发送后发起第二次下发，观察是否交错）；放开 `_gate` 后该用例失败 | 通过 |
| FR-014 网络等待在写事务之外 / 阻塞有界 | 代码复核（下发在 `writes.ExecuteAsync` 之前）+ 集成用例；回环实测单节点故障整段 12.4~12.6 s，可作 90 秒上界的对照；**真实网段仍无实测** | 部分 |
| FR-015 Simulation 不发包 | 集成 + 单测（Simulation 分支装配）；「不发包」由实现保证 | 通过 |
| FR-016 Hardware 装配可独立解析 | `VideoWallRegistrationTests` | 通过 |
| FR-017 取消显式传播 | 控制器侧：`CancelledDispatchIsLoggedAsNeitherFailureNorSuccess`；传输层：`VideoWallLoopbackTests` 在真实连接上 400 ms 取消 → **409 ms** 抛 `OperationCanceledException`（非 `VideoWallException`）、零日志 | 通过 |
| FR-018 成功/失败日志 | `VideoWallLoggingTests` 5 个用例（成功含模式+包数、失败含阶段+节点数+原因、重试成功含尝试次数、取消不记、Simulation 记跳过）；探针可证伪 | 通过 |
| FR-019 验证覆盖清单 | 本条即本文件 | 通过 |
| FR-020 同步规范与使用文档 | `docs/使用文档.md` §4.5 已补下发语义、失败表现、等待上界与 Simulation 边界，并标注 SC-007 未实机验证；另补 `docs/维护文档.md` §8.6 按日志定位 | 通过 |
| SC-001 ~ SC-004、SC-006 | 见上表对应 FR | 通过 |
| SC-005（3 秒 / 90 秒上界） | 回环实测：全节点 224~240 ms、单节点故障 12.4~12.6 s。**回环不等于现场**（无网络时延与丢包），不能据此判通过 | 部分 |
| SC-007（物理画面） | 无墙面，未实机 | **未验证** |

## 尚未验证（不得计为通过）

- **issue #2 帧内容抓包复核（2026-09-21 记录，未完成，本机做不了）**：`FBFC61FF…`／`FBFC61B8…`／`FBFC61B9…` 这组固定包与映射包参数，在仓库里只追溯到 `scp_cv/services/video_wall.py`，**没有任何来源记录**（无厂商协议文档、无 `.pcap`、无抓包日期）。第 4 轮补的黄金样本与第 5 轮的回环实测都只能证明「.NET 发出的字节与旧实现逐字节等价」，**不能证明这些字节对真实墙面是对的**——本机跑假节点收到的就是自己发的包，回声不构成证据。
  - **承接方**：GitHub issue #2（`Qintsg/SCP-cv`）。
  - **补完改哪一行**：现场整墙确认写进 SC-007 与 `docs/实机测试结论.md`；若抓包发现与旧值有差异，改 `VideoWallSequenceBuilder.cs` 的常量或映射参数，再重跑 `tools/generate_video_wall_golden.py` 刷新样本，并同步本文件与 `docs/使用文档.md` §4.5。
  - **已有的一半证据**：`docs/实机测试结论.md:39`（2026-07-26，旧 Python 实现）用 RGB 测试卡确认**双屏模式下大屏左半区**被准确占据、边界与灰阶完整；`:40` 另以「摄像机确认大屏左右不同内容」间接确认右半区。即 double 的组播地址、窗口号与半宽切分**在现场被物理确认过**。而 `:41` 的「单屏/双屏切换 通过」核的是隐藏右入口、窗口 2 静音与回切，**全是 UI/运行态，没有一处整墙画面**。
  - **结论**：结合第 4 轮的逐字节等价，`double` 的帧内容可判为已有充足证据；**`single` 整墙（`3840×2160` 按 `384×432` 切 50 块）至今没有任何现场物理证据**，是现场最该抓/最该拍的那一次。注意这条只覆盖「常量对不对」，**不能销 SC-007**——SC-007 要的是 .NET 运行时驱动真实墙的画面证据。
- **SC-007 物理画面**：本机不在 `192.168.5.0/24`，未以 `-SafetyMode Hardware` 启动并连真实墙面；`docs/实机测试结论.md`（2026-07-26）的单屏/双屏切换与 RGB 测试卡结论属于**旧 Python 实现**，不能当作本次证据。
- **SC-005 等待上界**：3 秒常态 / 90 秒最坏仍是**参数推算**。第 5 轮在回环上量到常态 224~240 ms、单节点故障 12.4~12.6 s，方向上印证了推算（远低于上界），但回环没有网络时延、丢包与 ARP/路由等待，**不能替代现场实测**；`docs/使用文档.md` §4.5 因此仍写「推算值」，本轮未改那里。
- **第 5 轮回环实测的边界**：见上节「回环假节点能测什么、不能测什么」——帧内容、TCP_NODELAY、真实网段行为都测不到。**回环里的假节点收到的是自己发的包，回声不构成现场证据**，别拿这轮的绿色当作帧内容问题的答案。
- **FR-018 日志缺口**：已于 2026-09-19 第 2 轮补齐（见上表与探针记录）。**日志本身未在真实 Hardware 链路上跑过**——自动化只验证了「记了什么」，没有验证现场日志文件里的实际落盘与轮转。
- **FR-020 文档缺口**：已于 2026-09-19 第 2 轮补齐。文档描述的下发语义来自自动化与代码复核；「3 秒常态 / 90 秒最坏」仍沿用 SC-005 的推算值，无实测，**已在 `docs/使用文档.md` §4.5 就地标注为参数推算值**，不再写成实测结论。
- `docs/known-pitfalls.md` 坑 2（预案音量不推 Windows Core Audio）：只有代码级证据，未实机复现，未修复，不属本规范范围。
- 本机未执行：Python 侧全量 `uv run pytest`（第 4 轮新增了 `tests/test_video_wall_golden_fixture.py` 与 `tools/generate_video_wall_golden.py`，但只跑了新文件，见「自动化执行记录」下注）、Ruff、前端 typecheck/build（未改前端）、`runtime-dotnet/scripts/runtime.ps1` 启停脚本。第 5 轮新增的 `runtime-dotnet/scripts/videowall-loopback.ps1` **已实跑**（`-Apply`/`-Apply -Except`/`-Status`/`-Clear` 四种动作都执行过，`-Clear` 后 `-Status` 为 0/50），但尚未在第二台机器上试过，脚本里的网卡名默认值按本机取的（`VMware Network Adapter VMnet1`，可用 `-Adapter` 换）。

## 第 1 次完整测试的偶发失败判定

`SseEventStreamTests.IdleSubscriptionEmitsHeartbeat`：该测试自建 hub（`SseEventStreamTests.cs:69-72`），不经过 `Program.cs` 的注册；断言 10 ms 心跳要在 2 秒 CTS 预算内到达（`:72-79`），失败那次该测试自身耗时 12 秒。第 2、3 次完整测试与 8 次重复均通过。

判定：既有计时敏感测试的偶发失败，与本次变更集无关（本次未触及 `SseEventStream.cs`；唯一交集是它也要解析 `RuntimeStateService`，新增参数有默认值且 Simulation 分支已注册）。加固建议（未实施）：把 `TimeSpan.FromSeconds(2)` 抬到 10 秒。
