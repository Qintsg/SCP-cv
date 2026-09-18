# 验证记录

**日期**：2026-09-19
**平台**：Windows 11 开发机（**不在视频墙网段**；未以 `-SafetyMode Hardware` 连接现场墙面）
**对应需求**：`spec.md` FR-001 ~ FR-020、SC-001 ~ SC-007
**变更集**：视频墙下发层补齐（`VideoWallController.cs` 新增，`VideoWallSequenceBuilder`、`RuntimeStateService`、`ScenarioService`、`Program.cs` 修改）+ 3 个测试文件

## 自动化执行记录

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| .NET 完整测试（第 1 次） | `dotnet test runtime-dotnet/ScpCv.sln` | **207/208**，失败 `SseEventStreamTests.IdleSubscriptionEmitsHeartbeat`（判定见末节） |
| .NET 完整测试（第 2 次） | 同上 | **208/208 通过** |
| .NET 完整测试（第 3 次，与 8 次 SSE 类重复同时运行，人为加负荷） | 同上 | **208/208 通过** |
| SSE 类重复（负荷中） | `dotnet test runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScpCv.ControlHost.Tests.csproj --no-build --filter "FullyQualifiedName~SseEventStreamTests"` ×8 | 8/8 通过 |
| 集成层硬件入口 | `dotnet test runtime-dotnet/tests/ScpCv.Integration.Tests/ScpCv.Integration.Tests.csproj --filter "FullyQualifiedName~HostHardwareIntegrationTests"` | 7/7 通过 |
| Spec Kit 校验 | `python .specify/scripts/python/validate_specs.py --specs-dir specs` | 通过（exit 0，无未完成占位符） |

第 2 次分项：Domain 38 / Windows 11 / Contracts 18 / Infrastructure 28 / Integration 57 / ControlHost 56 = **208**（补下发层前为 197，本次新增 11 个测试）。

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
| FR-017 取消显式传播 | **仅代码复核**（传输层 `OperationCanceledException` rethrow，不包装为节点失败） | 部分 |
| FR-018 成功/失败日志 | **未满足**：`grep -rn "ILogger\|Log\(Information\|Warning\|Error\)" src/ScpCv.Infrastructure/VideoWall` = 0 命中，`RuntimeStateService.cs` 亦无；ControlHost 侧唯一日志是启动行（`Logging/ControlHostLog.cs:8-15`）。成功路径无「模式 + 包数」，重试成功无记录，失败只经异常消息进 HTTP 响应体 | **缺口** |
| FR-019 验证覆盖清单 | 本条即本文件 | 通过 |
| FR-020 同步规范与使用文档 | `spec.md` 已建；`docs/使用文档.md` §4.5「大屏模式」只写了映射与静音，未提下发、失败与等待 | **缺口** |
| SC-001 ~ SC-004、SC-006 | 见上表对应 FR | 通过 |
| SC-005（3 秒 / 90 秒上界） | 无实测数据 | 未验证 |
| SC-007（物理画面） | 无墙面，未实机 | **未验证** |

## 尚未验证（不得计为通过）

- **SC-007 物理画面**：本机不在 `192.168.5.0/24`，未以 `-SafetyMode Hardware` 启动并连真实墙面；`docs/实机测试结论.md`（2026-07-26）的单屏/双屏切换与 RGB 测试卡结论属于**旧 Python 实现**，不能当作本次证据。
- **SC-005 等待上界**：3 秒常态 / 90 秒最坏值来自参数推算，没有真实丢包或慢节点的实测。
- **FR-018 日志缺口**：需要给控制器注入日志实现（模式、包数、失败阶段与节点数、重试成功），改动落在下发层内部。
- **FR-020 文档缺口**：`docs/使用文档.md` §4.5「大屏模式」未说明下发失败时接口报错、界面保持原模式。
- `docs/known-pitfalls.md` 坑 2（预案音量不推 Windows Core Audio）：只有代码级证据，未实机复现，未修复，不属本规范范围。
- 本机未执行：Python 侧 `uv run pytest`（未改 Python 代码）、前端 typecheck/build（未改前端）、`runtime-dotnet/scripts/runtime.ps1` 启停脚本。

## 第 1 次完整测试的偶发失败判定

`SseEventStreamTests.IdleSubscriptionEmitsHeartbeat`：该测试自建 hub（`SseEventStreamTests.cs:69-72`），不经过 `Program.cs` 的注册；断言 10 ms 心跳要在 2 秒 CTS 预算内到达（`:72-79`），失败那次该测试自身耗时 12 秒。第 2、3 次完整测试与 8 次重复均通过。

判定：既有计时敏感测试的偶发失败，与本次变更集无关（本次未触及 `SseEventStream.cs`；唯一交集是它也要解析 `RuntimeStateService`，新增参数有默认值且 Simulation 分支已注册）。加固建议（未实施）：把 `TimeSpan.FromSeconds(2)` 抬到 10 秒。
