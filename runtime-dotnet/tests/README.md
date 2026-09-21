# 测试分层

- `ScpCv.Domain.Tests`：纯业务规则，不访问文件、网络、数据库或 Windows API。
- `ScpCv.Contracts.Tests`：HTTP/SSE 与 IPC 序列化、OpenAPI 外观。
- `ScpCv.Infrastructure.Tests`：SQLite、文件边界和基础设施适配。
- `ScpCv.ControlHost.Tests`：ASP.NET Core 端点、认证与授权。
- `ScpCv.Integration.Tests`：simulation 模式下的跨层故障与恢复。
- `ScpCv.Windows.Tests`：需要 Windows API 的可自动化测试；真实 Office、显示器和播放器测试
  仍须在验证记录中单独标明。

默认测试不得启动真实播放器、Office、MediaMTX 或设备控制。需要物理副作用的测试使用
`Physical` trait，常规命令通过 `--filter "Category!=Physical"` 排除。

## 视频墙回环实测（`VideoWallLoopbackTests`）

视频墙节点是 `192.168.5.101~150:4830`，开发机不在这个网段，所以默认只做包级与替身时序测试。
要看传输层的真实行为（2 秒连接超时、重试退避、失败即中止、取消原样抛出），用管理员 PowerShell
把这些地址挂成本机别名，测试里的假节点就会在真实端口上收包：

```powershell
runtime-dotnet/scripts/videowall-loopback.ps1 -Apply     # 或 -Status / -Clear；别名 store=active，重启即消失
dotnet test runtime-dotnet/ScpCv.sln -c Debug --filter "Requires=VideoWallLoopback"
```

别名没挂时这 5 个用例按各自前置条件**跳过**（跳过原因里写着上面的准备命令），不会让常规测试变红。
它们的边界——能证明什么、不能证明什么（帧内容、TCP_NODELAY、真实网段行为都测不到）——见
`specs/004-video-wall-control/verification.md` 与 `docs/known-pitfalls.md` 坑 9。
