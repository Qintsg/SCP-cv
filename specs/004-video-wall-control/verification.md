# 验证记录

**日期**：2026-09-19
**平台**：Windows 11 开发机（**不在视频墙网段**；未以 `-SafetyMode Hardware` 连接现场墙面）
**对应需求**：`spec.md` FR-001 ~ FR-020、SC-001 ~ SC-007
**变更集**：
- 第 1 轮——视频墙下发层补齐（`VideoWallController.cs` 新增，`VideoWallSequenceBuilder`、`RuntimeStateService`、`ScenarioService`、`Program.cs` 修改）+ 3 个测试文件。
- 第 2 轮（同日，补 FR-018/FR-020 缺口）——`VideoWallLog.cs` 新增，`VideoWallController.cs` 注入日志；`docs/使用文档.md` §4.5、`docs/维护文档.md` §8.6、`docs/CHANGELOG.md`、`docs/known-pitfalls.md` 同步；新增 `VideoWallLoggingTests.cs` 与测试替身 `RecordingLogger.cs`。

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

第 1 轮第 2 次分项：Domain 38 / Windows 11 / Contracts 18 / Infrastructure 28 / Integration 57 / ControlHost 56 = **208**（补下发层前为 197，本轮新增 11 个测试）。

第 2 轮分项：Domain 38 / Windows 11 / Contracts 18 / Infrastructure 33 / Integration 57 / ControlHost 56 = **213**（新增 5 个日志测试，未改动其它分层）。

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

### 日志测试确实能发现该缺陷（探针观察，已撤销）

2026-09-19 把两处构造函数的日志注入改为恒定的 `NullLogger.Instance`（模拟「日志全部不可达」）后重跑基础层：

```text
失败!  - 失败: 4，通过: 29，已跳过: 0，总计: 33 - ScpCv.Infrastructure.Tests.dll (net10.0)
```

失败的正是新增 5 个中的 4 个（`SuccessfulDispatch…`、`FailedDispatch…`、`RetryThatSucceeds…`、`SimulationLogsTheSkip…`）；第 5 个 `CancelledDispatchIsLoggedAs…` 断言的是「不记日志」，不受该探针影响，故仍通过——这是该用例的预期行为，不是漏检。

探针已撤销，撤销后文件与探针前逐字节一致（md5 `a0a61b4426c29df864901fc73c2beb2f`），源码内无探针残留；该字节状态即上表第 2 轮完整测试的状态。

## FR / SC 映射

| 需求 | 验证位置 | 状态 |
| --- | --- | --- |
| FR-001 两条入口真实下发 | 集成 `RuntimeModeSwitchDispatches…`、`ScenarioActivationDispatches…` | 通过（探针可证伪） |
| FR-002 布局语义（单屏铺满 / 双屏左右分） | `VideoWallSequenceTests` 分块与组播断言 | 通过 |
| FR-003 阶段顺序 + 200 ms 间隔 | `VideoWallControllerTests` 折叠时间线断言 `clear → wait → mapping → commit → refresh` | 通过 |
| FR-004 阶段内并发 ≤25、阶段间串行 | `VideoWallControllerTests` 并发观测 | 通过 |
| FR-005 单节点 2 s 超时 + TCP_NODELAY | **仅代码复核**（`TcpVideoWallTransport`），无自动化断言 | 部分 |
| FR-006 重试 5 次 + 退避 | `VideoWallControllerTests` 尝试次数与退避序列 | 通过 |
| FR-007 失败即中止 + 摘要格式 | `VideoWallControllerTests`（含「其余 45 个失败」与 `IP:端口 phase=…`） | 通过 |
| FR-008 失败不改运行态 / 静音策略 + `video_wall_error` | 集成 `VideoWallFailureKeepsPersistedRuntimeModeAndMutePolicy` | 通过 |
| FR-009 预案失败中止整个激活 | 集成 `ScenarioActivationFailureLeavesRuntimeModeUnchanged` | 通过 |
| FR-010 静音策略只在下发成功后应用 | 集成同 FR-008；`RuntimeStateService.cs:91-94` 顺序代码复核 | 通过 |
| FR-011 非法模式下发前拒绝 | `VideoWallControllerTests` + `VideoWallSequenceTests` 未知模式抛错 | 通过 |
| FR-012 重复同一模式仍完整重下 | **仅代码复核**（`SetRuntimeModeAsync` 无相同模式早退） | 部分 |
| FR-013 并发下发串行化 | **仅代码复核**（`TcpVideoWallController._gate`），无并发测试 | 部分 |
| FR-014 网络等待在写事务之外 / 阻塞有界 | 代码复核（下发在 `writes.ExecuteAsync` 之前）+ 集成用例；**90 秒上界无实网复现** | 部分 |
| FR-015 Simulation 不发包 | 集成 + 单测（Simulation 分支装配）；「不发包」由实现保证 | 通过 |
| FR-016 Hardware 装配可独立解析 | `VideoWallRegistrationTests` | 通过 |
| FR-017 取消显式传播 | 控制器侧：`CancelledDispatchIsLoggedAsNeitherFailureNorSuccess`（取消传播为 `OperationCanceledException`、零日志）；传输层 `TcpVideoWallTransport` 的 rethrow **仍为代码复核** | 部分 |
| FR-018 成功/失败日志 | `VideoWallLoggingTests` 5 个用例（成功含模式+包数、失败含阶段+节点数+原因、重试成功含尝试次数、取消不记、Simulation 记跳过）；探针可证伪 | 通过 |
| FR-019 验证覆盖清单 | 本条即本文件 | 通过 |
| FR-020 同步规范与使用文档 | `docs/使用文档.md` §4.5 已补下发语义、失败表现、等待上界与 Simulation 边界，并标注 SC-007 未实机验证；另补 `docs/维护文档.md` §8.6 按日志定位 | 通过 |
| SC-001 ~ SC-004、SC-006 | 见上表对应 FR | 通过 |
| SC-005（3 秒 / 90 秒上界） | 无实测数据 | 未验证 |
| SC-007（物理画面） | 无墙面，未实机 | **未验证** |

## 尚未验证（不得计为通过）

- **SC-007 物理画面**：本机不在 `192.168.5.0/24`，未以 `-SafetyMode Hardware` 启动并连真实墙面；`docs/实机测试结论.md`（2026-07-26）的单屏/双屏切换与 RGB 测试卡结论属于**旧 Python 实现**，不能当作本次证据。
- **SC-005 等待上界**：3 秒常态 / 90 秒最坏值来自参数推算，没有真实丢包或慢节点的实测。
- **FR-018 日志缺口**：已于 2026-09-19 第 2 轮补齐（见上表与探针记录）。**日志本身未在真实 Hardware 链路上跑过**——自动化只验证了「记了什么」，没有验证现场日志文件里的实际落盘与轮转。
- **FR-020 文档缺口**：已于 2026-09-19 第 2 轮补齐。文档描述的下发语义来自自动化与代码复核，其中「3 秒常态 / 90 秒最坏」仍沿用 SC-005 的推算值，无实测。
- `docs/known-pitfalls.md` 坑 2（预案音量不推 Windows Core Audio）：只有代码级证据，未实机复现，未修复，不属本规范范围。
- 本机未执行：Python 侧 `uv run pytest`（未改 Python 代码）、前端 typecheck/build（未改前端）、`runtime-dotnet/scripts/runtime.ps1` 启停脚本。

## 第 1 次完整测试的偶发失败判定

`SseEventStreamTests.IdleSubscriptionEmitsHeartbeat`：该测试自建 hub（`SseEventStreamTests.cs:69-72`），不经过 `Program.cs` 的注册；断言 10 ms 心跳要在 2 秒 CTS 预算内到达（`:72-79`），失败那次该测试自身耗时 12 秒。第 2、3 次完整测试与 8 次重复均通过。

判定：既有计时敏感测试的偶发失败，与本次变更集无关（本次未触及 `SseEventStream.cs`；唯一交集是它也要解析 `RuntimeStateService`，新增参数有默认值且 Simulation 分支已注册）。加固建议（未实施）：把 `TimeSpan.FromSeconds(2)` 抬到 10 秒。
