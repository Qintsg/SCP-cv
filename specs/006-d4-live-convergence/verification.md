# Verification: D4 大屏实机收敛

## 起点（2026-09-27，只读勘察）

| 项目 | 观察 | 边界 |
| --- | --- | --- |
| 本机代码 | `main` 为 `c28f609`，005 的代码和文档已推送两处远端；006 规范在当前工作区尚未提交 | 先提交/推送 006 再让 D4 拉取相同提交 |
| D4 代码 | `D:\SCP-cv` 是干净的 `main`，HEAD `6fdbca7`，`origin` 为内网 GitLab | 明显落后于两窗/PPT/目录重构；不能直接测试旧二进制 |
| D4 服务 | `ScpCv*`、MediaMTX、POWERPNT、node 当前均无进程；18443/5173/8554/9997 无监听；`runtime-processes.json` 不存在 | 服务停机为当前基线，测试后应恢复 |
| D4 数据 | `D:\SCP-cv\.validation\t129-workstation` 有 `control.db` 851,968 B、WAL 4,140,632 B、SHM 32,768 B；媒体仅见旧 `uploads/` 结构 | T002 必须做三文件同组快照后才升级/迁移 |
| D4 桌面 | 控制台 session 1 活跃；PnP 可见四个 CRA2400 及一个通用非 PnP | SSH 的 `Screen::AllScreens` 仅虚拟 `WinDisc 1024×768`，实体绑定须从交互会话 `/api/displays/` 与画面复核 |
| D4 工具 | .NET SDK 10.0.400、pnpm 11.22.0、PowerPoint COM 注册可用 | 不代表新代码已编译或 Office 放映可用 |
| 墙面与电视 | 先前向拼接屏发送 OFF、两台电视各一次 toggle；协议无真实状态读回 | 本轮尚未发电源命令；电视不在测试范围，墙面电源实体测试待确认 |

本机到 D4 的当前有效来源地址为 `192.168.1.103`，绑定来源的 SSH 已只读登录验证；地址可能变化，不写入自动化常量。没有读取或输出任何口令。005 的本机 290/290 .NET 非 Physical、42/42 前端、9 页 Office 导出和浏览器目录测试仅作为起点，不替代本轮 D4 实体证据。

## 停机数据快照（2026-09-27）

- 在再次确认 D4 无项目/MediaMTX/Office 进程后，将精确 DataRoot 中 `control.db`、`control.db-wal`、`control.db-shm` 复制到 `D:\SCP-cv\.validation\backup-006-20260927-211531\`。三份副本逐一与原件 SHA-256 比对均相同；没有删除或覆盖源数据库。
- 同目录 `media-manifest.csv` 列出既有媒体相对路径、大小和 SHA-256：2 个文件，合计 15,594,200 字节。快照与清单均在 D4 忽略目录，不提交到 Git；它们是本轮更新前的恢复依据。

## 两窗现场脚本基线

- `benchmark-commands.ps1` 原先默认轮询 1–4，D4 又没有 `sqlite3` 命令，旧基准不能直接用于当前两窗。先写回归使 2/2 失败，再修为仅 1/2、从 ACL 私有文件或环境变量取口令、可显式指定 SQLite CLI 与保存原始样本；回归 2/2、PowerShell 5.1 语法解析通过，UTF-8 BOM 保留。
- `docs/qa/003-workstation-runbook.md` 的现行操作段已改为两窗和正确的 DataRoot 停机入口，早期四窗观测保留并标明历史。D4 SQLite CLI 的实际部署与 1000/100 样本执行仍待 T029，不能把脚本改动算作性能通过。

## D4 代码与构建（2026-09-27）

- 本机将 `15087a5`（两窗基准/口令）与 `d4c69ed`（006 规范）推送 GitHub `origin` 和内网 `gitlab`。D4 在干净 `main` 上从其内网 `origin` fetch、`merge --ff-only`，由 `6fdbca7` 更新到 `d4c69ed`，再次检查无跟踪文件改动。
- D4 使用 `D:\dotnet\dotnet.exe` 对 `runtime-dotnet/ScpCv.sln` 执行 `restore --locked-mode` 与 Debug `build --no-restore`，两者退出码均为 0。`D:\nodejs\pnpm.cmd --dir frontend install` 和 `build:web` 退出码均为 0；Vite 仍提示主 chunk 超过 500 kB，不把该提醒写成构建失败。
- 这一步只更新和编译 D4 文件，没有启动 ControlHost、Worker、前端或改变墙面/电源。下一步先验证无 Worker 的 Hardware 控制面及交互桌面的真实显示拓扑。

## D4 Hardware 控制面（尚未启动播放器）

- 首次在 SSH PowerShell 直接调用脚本被本机 ExecutionPolicy 拒绝；改用**仅进程范围** Bypass 后，`run-headless.ps1 -Detach` 成功创建任务 `ScpCvHeadless-b3089cd7`。任务拉起 ControlHost PID 51192，`SessionId=1`，`/health/ready=200`；日志明确“未指定 `-StartWorkers`，跳过 Worker 编排”。此前一次调用后的 `LASTEXITCODE=0` 是 PowerShell 保留的旧 native 返回值，不作为成功证据。
- 交互会话的 `/api/displays/` 返回五块实体显示：窗口 1 绑定 `DISPLAY2`（1920×1080，x=0）、窗口 2 绑定 `DISPLAY3`（1920×1080，x=1920），两者标为 playback target；`DISPLAY4/5` 和 1920×1200 的 `DISPLAY1` 不在播放目标。历史 D4 记录确认 `DISPLAY1` 为控制屏；实体画面和窗口真实落位仍待 T006/T011。
- 旧 DataRoot 启动后，`/api/runtime/` 保持 `double`，`/api/sessions/` 恰好两会话，实验性 PowerPoint 设置默认关闭，布局接口可读；这表明新合同与数据库迁移路径可用，尚不证明任何实体画面。

## D4 两窗启动与交互桌面像素（epoch 55）

- `POST /api/system/restart/` 返回“全部 Worker 已就绪”，epoch 55。PID/Session：ControlHost 51192、Supervisor 50836、PlayerWorker 1=8688、PlayerWorker 2=48020、AudioWorker 46268、PowerPointHost 28084、MediaMTX 51504，全部位于交互 session 1；没有 3/4 播放器。`runtime-processes.json` 恢复存在，两会话分别绑定 `DISPLAY2/3`。
- 在 D4 DataRoot 的本轮测试目录登记两个只读本地 PNG 源 ID 39/40（QA-006-LEFT/RIGHT），API 打开后两会话均为 `playing`、`player_online=true`、无待处理命令/错误。测试源位于 `D:\SCP-cv\.validation\t129-workstation\qa-006\`，原有源 ID 1/2 未改动。
- 交互会话计划任务 `ScpCvQaCapture006` 运行本轮 QA 截图脚本，结果码 0，JSON 记录 `session_id=1`、两个 1920×1080 目标、Worker PID/私有内存/句柄。基线空闲截图分别是黑场；打开源后 `DISPLAY2` 截图显示“QA IMAGE STABILITY”图卡，`DISPLAY3` 截图显示另一张控制台测试图，左右无串台。原始证据保存在 D4 `.validation\qa-006\` 及本机 `.validation\qa-d4-006\remote\`，不提交。Windows `Get-Process.MainWindowHandle` 在此任务仍为 0，故没有用它单独推断窗口位置；以受管命令行目标、会话和目标输出像素交叉证明。
- 这证明 D4 Windows 两路播放输出可出画，不等于已观察到关机状态的拼接墙面实际点亮，也不替代两个固定预设的实体切换测试。

## 直播源登记缺口的本机红→绿回归

- D4 旧构建对 `POST /api/sources/streams/` 返回 405；源码中已有流类型枚举和 VLC 打开分支，却只有文件/网页登记入口，`StreamDiscoveryService` 也未接入 RTSP/SRT 创建链路。根因在媒体登记的 REST→模型接口缺失，不是单纯少一个页面按钮。
- `MediaEndpointTests` 先红后绿覆盖 RTSP、SRT、自定义 HTTP 媒体流登记、下次打开的流地址修改、无效协议/内嵌凭据拒绝且不新增源、匿名请求拒绝。新源只标 `stream_status=unverified`，`is_available` 仅表示可以尝试播放，不表示在线。
- 本机真实 Chromium 在桌面与 390px 视口验证“添加源 → 直播流”、三种协议选择、源列表“直播 · 待验证”、地址编辑及错误地址 HTTP 400 的原文反馈；无页面异常或横向溢出。页面截图在忽略目录 `.validation/qa-big-screen-20260927/stream-*.png`，本机临时 ControlHost/Vite 均按 PID 停止。D4 实际流画面仍待更新部署和 T011/T026，不能把登记回归写成已出画。

## D4 多源画面矩阵（仍在旧运行组二进制，2026-09-28）

- 将桌面 `Resources/机械臂.mp4` 只读复制成 ASCII 名称测试副本，SCP 传至 D4 DataRoot 的 `qa-006/`；本机副本与原件以及 D4 副本的 SHA-256 均为 `32B725D36D334AF0E5B226CF6C2875A6EB75AD0F9FEDED39491C6B8860DC4EDF`。三页 PDF 取本机既有 QA 测试文件，上传到 D4 忽略目录；未改动原始素材。
- D4 登记视频 ID 41、PDF ID 42、网页 ID 43。窗口 1 打开视频后，会话为 `playing`，两次交互输出截图（`window1-20260928-042340.png` 与 `...042405.png`）呈现不同机械臂画面，证明不只是 HTTP 受理。但会话持续报告 `position_ms=0/duration_ms=0`，与实际视频帧变化不一致，留给 T027 定位，不把进度功能判为通过。
- PDF ID 42 在窗口 1 `playing/pdf`，页码经打开、next、goto 依次为 1/3、2/3、3/3；三个 D4 输出截图分别为蓝/绿/红的 `QA PDF PAGE 1/2/3`，与本机独立渲染源 PDF 的三页一致。网页 ID 43 打开本机 ControlHost 公开健康页后，窗口 1 截图实际显示 `Healthy`，会话为 `playing`。窗口 2 始终保持独立测试图，未观察到串台。
- 在两个窗口持续播放时，D4 把旧源 ID 1 的 9 页测试 PPT 原件作为只读输入重新上传到 `media/QA-006-PPT-20260928/AllinOne.pptx`，新源 ID 44 返回 201 且原件目录正确，随后超过 45 秒仍 `queued`。代码追踪确定 `MediaPreparationService.ClaimNextAsync` 对**所有**作业要求两窗全空闲，现场长期播放导致 PPT 作业饥饿。新增“窗口 1 正在播图时仍可领取 PPT 转换”回归先红后绿；本机全部非 Physical .NET 302/302、前端 42/42、类型检查/Web 构建、Redocly 与 Spec Kit 校验通过。D4 更新到修复后须在仍有画面的条件下证明页图真实生成。

## D4 并行 PPT 转换与默认页图复测（更新到 `92fb56b`）

- 旧运行组经受认证 shutdown 后，两个 PlayerWorker、AudioWorker、PowerPointHost、Supervisor、MediaMTX 均退出，状态文件消失；再由精确 DataRoot 的脚本停止 ControlHost。D4 快进到 `92fb56b`，.NET Debug 与 pnpm Web 构建退出码 0；分阶段重启 ControlHost 与运行组到 epoch 57。
- 升级启动时，先前排队的源 44 在空闲时准备为 `ready/9`。随后让窗口 1/2 分别保持图片源 39/40 `playing`，**在该条件下**再次上传原件为源 45：返回 `queued` 后转为 `ready/9`，两会话仍为 `playing`、源未切断。原件存于 `media/QA-006-PPT-20260928/AllinOne (2).pptx`，SHA-256 与旧源完全相同；制品有 `page-0001.png` 至 `page-0009.png`，PPT 资源接口返回 9 项；转换结束后 `POWERPNT` 进程数为 0。这是对排队修复的实体进程组证据，不只是单元测试。
- 实验开关关闭时，窗口 1 打开源 45 后为 `playing`、1/9，NEXT 为 2/9，`POWERPNT` 数保持 0；D4 两张输出截图与其导出的第一页、第二页 PNG 逐一视觉一致。原始页图与屏幕截图在 D4 `qa-006/ppt45-pages-20260928.zip` 和本机忽略目录 `.validation/qa-d4-006/ppt45/`。
- 同时发现会话的 `playback_mode` 返回空字符串，虽然页图已显示；根因是 ControlHost 把 `slide_images` 交给不识别下划线的枚举解析。新增真实投影→对外会话回归先红后绿，显式映射为 `SlideImages`。D4 当前截图来自修复前二进制，模式读回要在下次更新后复测。

## 视频进度与页图模式复测（D4 更新到 `1f06f0b`）

- D4 源 41 的两个屏幕截图存在不同视频帧，但在无新命令的 6 秒中会话 `position_ms=0/duration_ms=0`；发送不改变画面的循环设置后立即为 `42903/158322 ms`，随后 5 秒无命令又停在 `42903`。代码显示 VLC 时间只在命令结果快照和自然结束报告中采集，空闲循环仅发传输心跳，根因明确。
- 新增真实 Named Pipe 集成测试，要求无新控制命令也能收到带 generation 的进度 `state_report`；测试先编译失败，接入可选周期采样后通过。首轮并行测试中假服务器过早关闭管道造成竞态，调整测试后目标测试 3/3、完整非 Physical 套件 304/304 通过。
- D4 更新构建并重启到 epoch 59 后，源 41 在没有新控制命令的三个会话采样中进度连续为 `70909→74150→77158 ms`，总时长 `158322 ms`。源 45 默认页图会话返回 `slide_images/playing/1/9`，PowerPoint 进程数 0；旧版空模式已闭环。视频自然结束与资源长稳仍待 T027/T028。

## D4 RTSP/SRT 解码定位与配置对照（2026-09-28）

- 新直播入口在 D4 登记 RTSP 源 46 与 SRT 源 47。两会话均报告 `playing`，但窗口 1 的旧版 RTSP 输出截图仅约 10 KB、实际为空白；窗口 2 的 SRT 输出截图约 562 KB、可见动态测试图。API 的 `playing` 不能代替实际出画。
- 从官方发布包取 MediaMTX v1.21.1 到 D4 忽略目录的独立端口做对照，未覆盖产品 v1.17.1。独立 FFmpeg 解码：同一合成源 SRT 直读 8 秒无 H.264 错误；旧产品配置 `writeQueueSize: 8` 的 RTSP 读端 10 秒出现 33 条 H.264 损坏/缺参考帧告警。新版默认队列 512 的 RTSP 读端 10 秒为 0 告警且窗口 1 有画面。
- 为排除单纯版本因素，在相同 D4 上另起**原产品 v1.17.1**，只用环境变量把写队列由 8 改为 512，保留原配置的读/写超时。RTSP 读端 10 秒 0 告警；播放器窗口 1 截图可见连续测试图；在 VLC 同时读流时，独立 FFmpeg 再读 30 秒为 0 告警。此 A/B 对照把主要原因定位到过小的写队列，而非必须升级 MediaMTX。仓库配置现改为 512；仍须部署到 D4 的正式运行组并以默认端口复测后才算 T026 完成。
- SRT 读端虽能出画，旧 MediaMTX/FFmpeg 组合仍有 libSRT `UMSG: 6 INVALID SIZE: 0` 告警；尚未判明严重性或完成长稳，不记为完全稳定。全部合成推流、对照实例与截图均在 `.validation` 忽略目录；未改用户原媒体。

## D4 正式端口复测与视频资源红线（2026-09-28）

- D4 正式运行组协作 shutdown 后，精确 DataRoot 状态文件消失，ControlHost 停止；拉取 `d28e881` 后确认仓库配置 `writeQueueSize: 512`，重启后默认 8554/8890/9997 端口属于正式 MediaMTX。受控 SRT 发布源分别通过正式 RTSP/SRT 地址打开窗口 1/2，两张交互桌面 1920×1080 截图均可见相同时间附近的动态测试图，无串台。两窗同时播放时独立 FFmpeg 并发读 30 秒：RTSP 0 条 H.264 告警、SRT 0 条 H.264 告警；后者仍有 832 条 libSRT `INVALID SIZE` 告警。另起新版 MediaMTX v1.21.1 对照，SRT 10 秒仍有 284 条相同告警，故不把它误归因于产品 v1.17.1 或宣称已解决。
- D4 `GET /api/sessions/3/`、`/4/` 均返回 400；实体运行组仍仅有 player-1/2。未向旧窗口发送打开命令。
- 从现有视频的只读副本生成 4 秒测试片（D4 忽略目录），源 48 在窗口 1 关闭循环后由 `playing` 自然转为 `stopped`、总时长 4000 ms；开启循环后仍 `playing`，进度重新开始。用同源反复打开/关闭 10 次，每次等实际 `playing` 和 `idle` 后采样，PlayerWorker 1 私有内存从第 2 次 278.1 MB 增至第 10 次 469.6 MB，句柄 860→972，近似每轮 +24 MB/+14 句柄，不能判为稳定。
- 代码检查发现 `PlayerRuntimeHost` 只置空 `VideoView.MediaPlayer`，却未调用 `VideoView.Dispose()`；LibVLCSharp.WPF 的本地 API 文档明确此方法负责释放前景窗口。第一轮修复拆至 `PlayerRuntimeHost.Vlc.cs`，先从视觉树移除再显式释放 view、媒体、播放器与 LibVLC；本机非 Physical 304/304 通过。
- D4 更新到 `175b8fc` 后同源再跑 20 次，私有内存第 6/8/10 次为 255.1/258.8/260.3 MB，停播 15 秒回落到 169.3 MB；但句柄仍从第 2 次 827 升到第 20 次 1040，15 秒后仍为 1038，**第一轮修复不完整**。微软签名的 Sysinternals Handle（只读诊断）显示再做 5 次后 Semaphore 160→200、Thread 75→88、Event 312→329，确认存在内核句柄增长而非仅 GC 峰值。
- 官方 LibVLCSharp 最佳实践建议应用生命周期仅创建一个 `LibVLC` 实例；原实现每开一次视频都新建/销毁一次。下一轮修复改为每个 PlayerWorker 复用单一 `LibVLC`，仅在 Worker 停机时释放；每次切源仍单独释放 MediaPlayer/Media/VideoView。D4 同样 10+ 次句柄曲线尚待再次更新部署；不能把第一轮内存回落当作全面通过。
- D4 再次协作停机、快进到 `b63327d` 并零错误构建后拉起新的两窗运行组，窗口 1 用同一个 4 秒视频每轮等待 `playing→idle` 完成 20 次。第 2/4/6/8/10 次句柄全为 **832**；第 12/14/16/18/20 次为 838/838/840/847/847，停播 15 秒后回到 **837**，远小于第一轮第 2→20 次的 827→1040。私有内存第 2→10 次 263.5→270.3 MB、第 20 次 297.3 MB，停播 15 秒回落到 178.3 MB。未触发命令/会话错误，固定 Worker PID 50124 未重启。此对照支持句柄泄漏已消除；仍需 T027 的自动回归及 T030 的 60 分钟混合长稳，不能以 20 轮替代。
- 为避免以后只看内存峰值，新增受 `HardwareNote` 门禁的 `runtime-dotnet/scripts/test-vlc-resource-stability.ps1`：从状态文件锁定交互会话 Worker PID，每次确认真实 `playing→idle`，记录私有内存/句柄和停播后的增长阈值。脚本语法已本机解析，D4 自身运行结果待补；T027 的 Windows.Tests 视频生命周期回归仍需完善。
- D4 快进到 `729e630` 后运行上述正式脚本 20 次，`verdict=通过`：预热第 2 次 877 句柄，结束并闲置 15 秒为 879，增长 2（阈值 30），私有内存回落到 185.3 MB；原始样本在 D4 `.validation/qa-006/vlc-stability-729e630.json`。这证明短时同源切换的句柄门禁，但不替代长时间循环播出。

## 1000 命令基准揭示的循环播放死锁（修复待复测）

- 从 SQLite 官方下载页取得 Windows x64 命令行工具到 D4 忽略目录，下载包 SHA3-256 与官网列值一致。窗口 1/2 都循环 4 秒视频源 48 时，以 50 ms 间隔向两个窗口交替提交 1000 条窗口音量命令（没有改变系统音量或设备电源）。提交 64.36 秒，全部入队；494 条开始/完成、505 条被覆盖、1 条仍待处理，排空等待 180 秒超时，因此**样本无效**，即使已执行样本的开始 p95 为 16.8 ms 也不能算 T029 通过。原始命令样本和统计在 D4 `.validation/qa-006/benchmark-1000*`。
- 基准结束时窗口 2 `player_online=false`、最后一条 `SET_VOLUME` Pending，PID 50488 仍存活而 CPU 三秒无增量；交互桌面截图为黑场。只读 `dotnet-stack` 报告显示该进程 UI 线程阻塞在 `PlayerRuntimeHost.HandleVlcEndedAsync → MediaPlayer.Stop()`，而非真正退出。LibVLCSharp 官方 API 说明 `Stop()` 会同步等待 VLC 线程，若从 VLC 回调调用可能死锁；当前实现虽投递到 WPF Dispatcher，结束回调与投递执行仍可能重叠。
- 第一版先让循环结束分支直接 `Play()`，不执行 `Stop()`。D4 更新到 `8f4c6be`，两窗 40 秒均在线且未死锁，但进度分别卡在 `3835/4000` 和 `3831/4000 ms`，交互截图停在视频末帧；`playing` 是假成功。第二版 `Time=0` 再 `Play()`（`82626a9`）在窗口 1 把进度归零却保持 0/4000 超过 20 秒，也未真正重播。下一版按 LibVLCSharp 的显式媒体切换用 `Play(media)` 重新指定同一媒体，不调用同步 Stop；须 D4 出画/进度与 1000 样本复测。T029、T030 保持未完成。
- D4 更新到 `dfb837d` 后，`Play(media)` 确实重播：窗口 1 多次进度回绕、窗口 2 加入后两窗 30 秒一直在线且进度变化，交互截图都有机械臂视频帧。相同 50 ms 间隔的 1000 命令重测 **1000 入队/开始/完成、0 折叠/残留**，开始 p95=12.8 ms、最大 32.9 ms；HTTP p95=12.9 ms，提交 63.77 秒、排空 0.29 秒。普通命令样本有效，热切换 100 样本仍未执行，T029 不能整体完成。
- 继续观察循环资源发现下一层缺陷：约数分钟两窗循环后 PlayerWorker 句柄达到 1320/1271；再观测 30 秒窗口 1 从 1343→1363、窗口 2 从 1298→1319，内存约 285–309 MB 波动。Handle 显示窗口 1 Semaphore=176、Thread=151、Timer=84，说明不做 Stop 的重绑媒体不断保留原生线程/句柄。下一版把原生 Stop 放在线程池并 `await`，使 UI Dispatcher 可以处理输出 HWND 清理；以媒体异步门禁串行化命令/结束回调/释放，进度采样避开停止中的原生资源。协作关窗也改为异步清理，避免 OnExit 同步等待反过来阻塞 Dispatcher。此修复已本机编译，D4 同条件循环趋势与停机仍待复测，T028 重新打开。
- D4 更新到 `42cbdd8` 后，两窗循环采样出现进度回绕且都在线；句柄在播放/原生释放阶段有峰谷而不再单向增加（窗口 1 一度 1251 后回落 931，窗口 2 1315 后回落 1105）。再做相同 1000 命令，1000 全开始/完成、0 折叠/残留，开始 p95=18.4 ms、最大 110.2 ms。关闭并闲置 15 秒后两会话 `idle`、音量 100、循环 false，句柄 854/897、私有内存 182.0/193.8 MB。新版实机脚本分别在窗口 1/2 各跑 20 次开关，判定均通过，预热后句柄增长 +5/-4，停播内存 202.8/207.9 MB。短时循环与切源已有绿色对照；60 分钟混合仍未执行。

## D4 实验性 PowerPoint 与停机残留（2026-09-28）

- 测前 PowerPoint 进程数 0、两窗 idle，实验开关 false。显式开启后窗口 1 打开源 45，经 loading 转为 `playing/powerpoint/1/9`；交互桌面截图实际显示第一张文稿，NEXT=2/9、GOTO=3/9，无会话错误。关闭窗口 1 后 idle，再在窗口 2 打开同一原件，`playing/powerpoint/1/9`；窗口 2 第一页输出截图 SHA-256 与窗口 1 第一页完全相同，NEXT=2/9。两次均来自项目本轮源，不涉及用户桌面文稿。
- 关闭窗口 2 并恢复实验开关 false 后，两窗 idle；项目创建的 POWERPNT PID 4904（session 1，启动于 07:34:50）仍作为空闲自动化实例存在。受认证运行组 shutdown 用时 5.51 秒，状态文件移除、两个 PlayerWorker/OfficeHost/MediaMTX 均退出，却仍保留该 POWERPNT。原生放映与导航可用，**Office 停机清理不完整**，T017 保持未完成。
- 根因追踪：Worker/OfficeHost 已有 `shutdown_request` 接收分支，但生产 broker 没有发送方，Supervisor 对隐藏 WPF/控制台进程只靠 `CloseMainWindow` 后定向强退；OfficeHost 无机会执行自有 Application 的 COM 退出。另有 OfficeHost 收到 shutdown 后等待永不取消的心跳任务的问题。新增真实管道退出回归先因 `OperationCanceledException` 失败，修复后 5 条目标用例通过；本机全套非 Physical 309/309 通过。新实现先向已认证当前角色发退出帧，再由 Supervisor 兜底；OfficeHost 取消心跳并只对自有且无用户文稿的实例 Quit，PlayerWorker 异步释放后退出。D4 原 PID 4904 的精确清理及新版退出复测待执行，不按名称强杀其它 Office。

## 待执行门禁

- [x] D4 数据快照
- [ ] D4 更新与构建
- [ ] 窗口 1/2 实体显示器落位、停启和两个固定预设
- [ ] 多源实体画面与直播源登记/解码
- [ ] PPT 原件/页图/默认与实验放映、Office 归属
- [ ] 目录/删除一致性与预案音量
- [ ] 两窗 1000/100 基准和 60 分钟混合稳定性
- [ ] 桌面/手机/可用封装端 UI、文档和停机恢复
