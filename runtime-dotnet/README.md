# .NET 播控运行时

播放运行时仅支持 Windows x64 交互桌面。`ControlHost` 负责 REST/SSE、SQLite/EF Core 和持久命令队列；`Supervisor` 在同一交互会话仅启动窗口 1/2 两个大屏 PlayerWorker、AudioWorker、PowerPointHost 与可选 MediaMTX，控制桌面不占用。控制端（Web/Electron/Capacitor）不会直接访问 Named Pipe 或播放器对象。

开发构建：

```powershell
dotnet restore runtime-dotnet/ScpCv.sln
dotnet build runtime-dotnet/ScpCv.sln
dotnet test runtime-dotnet/ScpCv.sln --filter "Category!=Physical"
```

现场使用 `scripts/run-headless.ps1` 启动明确 DataRoot 的控制面，再通过认证 API 启停运行组；完整参数见 `../docs/qa/003-workstation-runbook.md`。旧 `scripts/runtime.ps1` 已明确退役：四种旧动作和默认状态查询均失败并提示替代入口，不再读写脚本目录状态或直接启动 Supervisor。状态查询用 `GET /api/runtime/`，组启动/重启与停止分别用 `POST /api/system/restart/`、`POST /api/system/shutdown/`（已认证会话及 CSRF）。停机先通过已认证管道通知 Worker/OfficeHost 协作释放，再由 Supervisor 对 PID/start-time/session 匹配的自有进程兜底；Office 无法证明所有权时不强杀。开发数据库位于独立 DataRoot，Git 仅恢复代码与规范，不删除数据库、媒体和日志。

`SafetyMode=Simulation` 使用单个虚拟显示器和数据库音量，不访问本机设备；
`SafetyMode=Hardware` 要求 Windows 交互桌面，`/api/displays/` 枚举实际设备名、坐标与
分辨率，系统音量经 NAudio/Core Audio 访问默认渲染端点。交互桌面、显示器或默认音频端点
缺失时接口返回明确的 `display_topology_unavailable` / `system_audio_unavailable`，不会把
数据库意图伪报成硬件已同步。
