# .NET 播控运行时

AudioWorker在同一Worker执行循环的空闲路径只读报告当前成功源的实际时钟，不靠重复控制刷新进度。
自然Ended/stopped留给Finished列表推进或显式STOP结果，失败OPEN使旧采样身份失效；有限D4证据见[周期音频进度](../specs/008-libreoffice-playback/verification/audio-idle-progress-20261004.md)。

背景音频按列表循环：Worker始终报告自然结束，ControlHost同事务选下一曲或末曲STOP，保持代次和事件幂等。
D4真实音频管道有限通过；SQLite生产已pin2.1.13，旧高危告警来自未继承CPM的外部QA，现已对齐实际3.53.3并复测。最终完整包仍须核实际DLL，见[更正与对齐](../specs/008-libreoffice-playback/verification/sqlite-qa-alignment-20261004.md)。

AudioWorker换源先停止旧原生输入再发布新Media/身份，不能把non-autoplay新源标为旧Playing。
MP3/WAV/OGG静音原生控制与退出已有D4有限证据，声音/真实列表循环等仍未验收，
见[音频检查](../specs/008-libreoffice-playback/verification/native-audio-d4-20261004.md)。

直播状态不能只按原生Playing判断。rtsp/srt/custom_stream尝试在Playing下，输入、解复用、解码和呈现
四累计计数连续5秒无推进时报告错误；暂停/停止/缓冲与普通媒体文件不套用该规则，不自动重放。
修复的D4有限验证及UDP恢复未完成边界见[直播取证](../specs/008-libreoffice-playback/verification/native-stream-d4-20261004.md)。

UDP独立接收/解码正常不等于Player重开正常；强制TS、新MediaPlayer和软件解码的QA对照仍失败，
这些参数未接正式默认，见[分层诊断](../specs/008-libreoffice-playback/verification/udp-reopen-diagnostic-20261004.md)。

播放运行时仅支持 Windows x64 交互桌面。`ControlHost` 负责 REST/SSE、SQLite/EF Core 和持久命令队列；`Supervisor` 在同一交互会话仅启动窗口 1/2 两个大屏 PlayerWorker、AudioWorker 与可选 MediaMTX，控制桌面不占用。LibreOffice播放侧车由各Player按文稿独立创建，转换侧车由ControlHost按作业创建；不再启动PowerPointHost。控制端（Web/Electron/Capacitor）不会直接访问 Named Pipe 或播放器对象。

开发构建：

```powershell
dotnet restore runtime-dotnet/ScpCv.sln --locked-mode
dotnet build runtime-dotnet/ScpCv.sln
dotnet test runtime-dotnet/ScpCv.sln --filter "Category!=Physical"
```

打包（只生成本机产物，不部署或启动服务）：

```powershell
powershell -File runtime-dotnet/scripts/publish-runtime.ps1 -CheckDependencies
powershell -File runtime-dotnet/scripts/publish-runtime.ps1 -OutputDirectory runtime-dotnet/artifacts/runtime-new
```

输出目录必须尚不存在。四个.NET10程序在发布根，Framework4.8宿主及其协议DLL隔离在`libreoffice/`；同名依赖内容不同会明确失败而不覆盖。构建机必须有Framework4.8 targeting pack及官方LibreOffice.NET互操作组件。该包不内置LibreOffice、.NET运行时、WebView2运行时、MediaMTX配置或用户数据，详情见[文稿运行指南](../docs/libreoffice-playback.md)。

发布末端会调用现有AudioWorker的显式`--generate-plugin-cache <当前发布根>`分支，仅生成`libvlc/win-x64/plugins/plugins.dat`；不启动播放适配器/服务、业务管道或用户配置。额外`VLC_PLUGIN_PATH`、重解析路径和已有索引拒绝生成，失败保留部分包而不报告完成。
索引匹配固定LibVLC/插件内容与修改时间；复制/打包应保留插件时间戳。缓存失效时仍由原生正常扫描，不关闭扫描兜底；手动更新插件后应重新生成全新包，不直接覆盖旧运行目录或索引。缓存存在不是出画、签名可信性或整组就绪证明。

现场使用 `scripts/run-headless.ps1` 启动明确 DataRoot 的控制面，再通过认证 API 启停运行组；完整参数见 `../docs/qa/003-workstation-runbook.md`。发布包使用时明确指定`-RuntimeRoot <发布根>`、`-ControlHostPath <发布根>\ScpCv.ControlHost.exe`和`-SupervisorExecutable <发布根>\ScpCv.Supervisor.exe`。旧 `scripts/runtime.ps1` 已明确退役：四种旧动作和默认状态查询均失败并提示替代入口，不再读写脚本目录状态或直接启动 Supervisor。状态查询用 `GET /api/runtime/`，组启动/重启与停止分别用 `POST /api/system/restart/`、`POST /api/system/shutdown/`（已认证会话及 CSRF）。停机先通过已认证管道通知Worker协作释放，Player等待自有文稿/侧车退出，再由Supervisor对PID/start-time/session匹配的自有进程兜底；不按Office进程名全局清理。开发数据库位于独立DataRoot，Git仅恢复代码与规范，不删除数据库、媒体和日志。

`SafetyMode=Simulation` 使用单个虚拟显示器和数据库音量，不访问本机设备；
`SafetyMode=Hardware` 要求 Windows 交互桌面，`/api/displays/` 枚举实际设备名、坐标与
分辨率，系统音量经 NAudio/Core Audio 访问默认渲染端点。交互桌面、显示器或默认音频端点
缺失时接口返回明确的 `display_topology_unavailable` / `system_audio_unavailable`，不会把
数据库意图伪报成硬件已同步。
