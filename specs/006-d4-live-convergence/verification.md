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
- 核对旧 PID 4904 的进程名、session 1、启动时间 `20260928073450` 后，仅终止该上轮项目自有测试实例；没有关闭其它 Office。D4 更新到 `2b47b1e` 并构建退出码 0，重启后再次实验放映源 45，PowerPoint 新 PID 45376、1/9 `playing/powerpoint`。在测试文稿仍放映时恢复开关 false 并执行组 shutdown，约 5.47 秒后状态文件消失，PlayerWorker 39724/49160、AudioWorker 40572、OfficeHost 44824、MediaMTX 52188 与 POWERPNT 45376 全退出，实际 PowerPoint 数 0；此次没有对 Office PID 额外强杀。5 秒整体耗时不能证明所有组件都走协作退出（MediaMTX 仍靠 Supervisor 兜底），但项目 Office 残留的新版实机对照已通过；用户文稿并存保护与异常超时矩阵仍待 T014/T017。
- 再启运行组，实验开关 false 时窗口 1/2 同时打开源 45，均 `playing/slide_images/1/9`，PowerPoint 进程数持续 0。窗口 1 NEXT=2、PREVIOUS=1、GOTO=3；窗口 2 GOTO=9、PREVIOUS=8、FIRST=1，全无命令/会话错误。窗口 2 第 9 页实机截图与导出的 `page-0009.png` 逐一视觉一致，原件 SHA-256 仍为 `2401652F7CD610DFCDFE4B5F2F32CD112D9645F1E30CEF44ECB3D716B82A55F0`；随后两窗正常关闭为 idle，Office 0。T015 的原件/页图/默认两窗播出已完成，实验模式更广故障测试仍未完成。

## 单源删除一致性红→绿与 D4 复测

- `DeleteSourceAsync` 原先先删除 SQLite 记录，再由 `TryDeleteFile` 吞掉 IOException/权限异常。临时 Windows 文件的占用、只读两条服务回归都因“未抛异常”失败；真实端点回归返回 200 而非 400，确认假成功而非测试误判。
- 改为活跃源先拒绝、受管理原件先移到 `media/.staging/deleted-source-*` 并附原路径清单，再删除数据库。提交前异常查持久化记录并恢复原件；提交后回执异常按查询结果继续清理而不搬回旧路径。锁定/只读失败保留原件与记录；提交结果不明或隔离清理失败返回 503 `media_cleanup_pending` 并保留清单。源只是登记的本地路径时，不删除外部原件。
- 新增提交前失败、已提交但回执异常、清理文件锁失败、活跃源拒绝、本地原件保留共 5 条故障回归；加上占用/只读与端点回归，完整非 Physical .NET **317/317**。前端删除失败后刷新真实列表但保留原始错误，3 条行为测试与原测试合计 **45/45**，类型检查和 Web 构建通过；主 chunk >500 kB 提醒仍存在。Redocly 和 Spec Kit 校验通过。D4 上传文件的占用/只读/正常删除及实际页面反馈仍待部署验证，T020 保持未完成。
- D4 停机更新到 `cb2590f`，.NET 与 pnpm Web 构建均退出 0。仅启动 Hardware 控制面（无 Worker/Office），重新上传本轮图卡副本为源 49/50；分别持有无 Delete 共享的文件句柄和设置只读属性，DELETE 都返回 400 `media_error` 与明确原因。两条记录/原路径均仍存在，SHA-256 均保持 `49F9A45EBFD65FBF5DD11D42FD96C0385BD90AB65A68D45FBC9C1240CE91C1E8`，隔离残留 0。解除限制后两次 DELETE=200，记录/副本文件和隔离清单都为 0；原图卡与原源 1/2 保留，摘要未变。T020 同条件 D4 API 复测完成；503 清理故障仅由自动化故障注入验证，页面反馈仍由 T023 承接。
- 页面确认文案原先说“正在使用此源的窗口将停止播放”，与新活跃源拒绝合同冲突，已改成先停止播放/转换及占用/只读会拒绝。新增参数化浏览器脚本 `frontend/scripts/media_delete_qa.py`，仅接受 `qa-delete-*` 测试源，使用忽略目录的临时 Cookie 文件；D4 源 51 由夹具持有文件锁，桌面/390px 页面回归待执行。
- 本机真实 Chromium 访问 D4 Vite + Hardware ControlHost，在 1440×900 与 390×844 两次点击删除锁定源 51，均收到 DELETE=400、随后 GET 列表=200，列表保留源且错误原文可见，无页面异常/横向溢出。初跑在第二次错误通知上因定位器严格匹配两条相同文本失败，是 QA 定位问题；改为核对最新通知后通过。截图须等确认框离场并滚回顶部，否则过渡态空弹层/固定页头落在长截图中会误导视觉结论。
- 稳定帧视觉复核确认手机重复持久错误堆叠遮挡大半页面，新增 Toast 行为回归先失败（重复错误生成两个 ID），修为无按钮的相同持久错误复用单条；不同描述与带动作回调仍独立保留。前端完整 **48/48**、类型检查和 QA 脚本语法通过，脚本新增单通知断言；D4 热更新后的视觉复测及源 51/临时 Cookie/Vite 清理待完成，不能把前一版两通知截图当新版本结论。
- D4 快进到 `6537375` 后重跑真实浏览器：桌面/手机 DELETE=400 后都刷新 200、源仍在、无页面异常或横向溢出，重复操作后 `.n-notification` 恰好 1 条。稳定帧桌面及手机视口截图已逐一检查，确认只留一张可关闭错误卡；原始图在本机忽略目录 `.validation/qa-d4-006/browser-delete/`。D4 与本机 pnpm Web 构建均通过，chunk 提醒保留未掩盖。
- 源 51 释放锁后仅删除本轮 QA 上传副本，DELETE=200、文件和隔离清单消失，原始图卡保留；D4 Vite PID 50064 核对命令行后停止，临时浏览器 Cookie 文件在 D4 与本机均移除，未删除开发口令文件。D4 ControlHost PID 11084 随后经精确 DataRoot 脚本停止：SCP-cv/MediaMTX 进程 0、PowerPoint 0、18443/5173/8554/9997 监听 0、运行组状态文件无（符合停机语义）。原源 1/2、源 39–48 和目录 9 及快照保留供后续目录/PPT/长稳测试；没有把本轮片段收尾当作全部 T034 完成。

## D4 中文目录与移动占用回归（2026-09-28）

- 本轮开始重新确认本机/D4 均为 `4fdb7d4`、工作区干净，D4 项目/Office/端口均停机且 console session 1 活跃；仅启动 Hardware 控制面。通过显式 UTF-8 JSON/上传创建测试目录 10「QA-006-资料」、11「PPT文件」（子目录）和 12「QA-006-目标」，原目录 9 与源 1/2、44/45 未改动。
- 两种不同内容的 PNG 分别在媒体根和两级中文目录以同名上传，新增源 52–55；根目录及子目录各保留 `同名测试.png` 与 `同名测试 (2).png`，各自摘要保持 `49F9A45E...` 与 `255B0457...`，没有覆盖。四份下载均 200，下载字节摘要与各自原件相同，UTF-8 下载文件名正确。
- 根源 52 移到已有同名的目录 11 时自动选 `同名测试 (3).png`，旧根路径消失。目录 11 整体移至目录 12，再把目录 12 重命名为「QA-006-目标已整理」，三个受管理源 URI 同步成为 `media/QA-006-目标已整理/PPT文件/` 下的实际路径；旧目录消失、根源 53 仍在原根目录。四份磁盘与再次下载摘要均相同。
- 目录循环（12 移到其子目录 11）、重名重命名和同名创建都返回 400 及明确原因，记录与实体路径不变。另对源 54 持有禁止 Delete 共享的文件锁，源移动返回**空的 HTTP 500**；原 URI/文件/摘要虽仍正确，错误反馈不可用。真实端点回归复现 `File.Move → IOException → WriteCoordinator → MoveSourceAsync → REST` 原样穿透；移动模块仅捕获补偿后重抛，与删除模块显式转换文件错误的行为不同。
- 最小修复保留已有路径补偿和提交结果核对，仅把占用/权限异常转换为已有 400 `media_error`，明确“移动未生效”；回归同时验证库列表、原路径与下载字节。目标测试先红后绿，完整 .NET 非 Physical **318/318**；D4 新版同条件复测、PPT 原件移动/活跃拒绝及目录浏览器仍待执行，T021/T022/T023 保持未完成。

## 目录/PPT 移动复测与暂停恢复检查点（2026-09-28）

- D4 更新到 `130c368` 后，对源 54 重新持有禁止 Delete 共享的原件句柄：移动返回 400 `media_error`，明确说明占用/权限且“移动未生效”；源 URI、实体路径、下载 SHA-256 都不变。该同条件实机对照已补，不把最初空 500 当作最终结果。
- 已准备源 45 原件移入 `media/QA-006-目标已整理/PPT文件/AllinOne.pptx`，原始/下载 SHA-256 保持 `2401652F...`，缓存目录未变，页 1/9 PNG 与资源清单仍可读（9 页）。默认两窗分别显示 1/9 `playing/slide_images`、Office 0；播放期间移动源与重命名所属目录均返回 400，路径/摘要不变。
- 实屏归档 `ppt-moved-evidence-130c368.zip` 已从 D4 复制到本机忽略目录并逐张视觉复核，独立 fresh-eyes 再核对解码像素：窗口 1 与页 1 全部相同，窗口 2 与页 9 仅一个不可见微小像素差异。无新增裁切/拉伸/错页/黑屏或桌面泄露；页 1 图内右边缘内容截断、页 9 扫描小字模糊均参考导出已存在，不修改用户原稿。
- 在两窗页图播出时上传源 56 后最终 9 页 ready、作业 Succeeded、Office 0。早期 20 秒轮询只见 queued，未观察 Running，也没有实际提交转换中移动请求；因此**不能**将“转换中移动拒绝”的 D4 证据标为完成，T022 仍留该缺口。
- 用户要求暂停时，09:18 已认证组 shutdown=200，精确 DataRoot 停止控制面，项目/MediaMTX/POWERPNT 进程与 18443/5173/8554/9997 监听全部为 0，状态文件无（正常停机），捕获任务 Ready；原媒体、目录和快照保留。
- 用户恢复后再次只读核验 D4 干净 `main/130c368`、console session 1、项目/Office/监听无残留；18:39 只启动无 Worker 的 Hardware ControlHost（ready=200），随后启动本轮隐藏 Vite PID 40604 供真实目录页面 QA。未发送墙面/电视电源或系统音量写操作。

## 本轮非 Physical 红→绿与全量门禁（2026-09-28）

- 后台转换测试扩为 19/19：转换失败/异常、显式重试、有序有效 PNG、停止/遗留作业、源和中文目录移动、转换中移动拒绝、暂时 Office 拒绝后恢复及 uncertain 禁止重试。此处是真实服务/SQLite加外部 converter 替身，不启动 Office。
- 原生 Adapter 新增 15 条归属回归：复用及可重入用户并入、PID/启动时间错配、Run/Close 失败、补偿槽位保留、共享/不可读集合保护。相关缺陷均有红→绿对照；外部 STA/COM/Win32 边界替身不启动 WPF/Office。类型库固定 DispId 的真实调用由 T016/T017 实机承接。
- 预案音量新增 11 条真实 Host 装配/SQLite故障回归，先证实控制器调用为空、部分完成消息缺失与提交异常穿透，再修为墙面→音量→原子提交→媒体命令；保存真实音量/静音读回。提交不明不盲目补偿或派媒体。音量实体写入/恢复尚未获得本轮授权，T025 未整体完成。
- 独立 `runtime.ps1` 统一明确退役，四动作与默认查询均非零失败并给当前认证流程。7 条受控脚本合同先 6 失败、修后 7/7，PS5.1 语法与 UTF-8 BOM/LF通过；没有启动真实 Supervisor。
- 本机完整 Release build 0 警告/0 错误；重构建后的非 Physical 全套 **369/369、0 跳过**（Domain 41、Contracts 18、Infrastructure 80、ControlHost 89、Windows 41、Integration 100）。前端目录 DOM/浏览器及最终 D4 部署尚在进行，不以本机绿色替代实机。
- 后端四个独立提交 `02e170d`、`6591c90`、`30d1cdd`、`ad31e73` 已推送 GitHub 与内网 GitLab；本机直连 GitLab 502/空回复后，经临时 D4 SSH SOCKS 通道推送成功，未改远端配置。D4 服务停止后从内网 origin 快进到 `ad31e73`，Debug 构建 0 警告/0 错误、non-Physical 同样 **369/369、0 跳过**。
- D4 重新启动控制面、API 拉起两窗 epoch 81，再显式开启实验模式打开源 45：返回受理后会话终态为 `error/office_process_unavailable`，两窗 Worker 均在线且无 pending，项目 POWERPNT PID 46088/session 1 已出现。说明新版 Application HWND 取证尚不可用，**不能**把本机 15 条 fake 归属绿色当作原生出画通过；继续记录 COM 证据诊断，不回退全局窗口猜测。
- 此次失败后恢复实验开关 false 并认证 shutdown，重新以 JSON 查进程仅剩 ControlHost 47432，Office/Worker/MediaMTX 为 0，运行组状态文件消失。没有按进程名强杀 Office。诊断信息不得只写不可见的 WPF stderr，下一轮通过安全 OfficeResult Detail 取证。

## 待执行门禁

### 最新统一门禁与 D4 对照（截至 12f0094）

- 本机 Release 0 警告/0 错误，完整 non-Physical **420/420、0 跳过**（Domain41、Contracts18、Infrastructure80、ControlHost124、Windows53、Integration104）；前端 **65/65**、typecheck/Web build通过，主 chunk >500 kB提醒仍未消除。Spec Kit/Redocly通过。独立变更均已推送 GitHub/GitLab，D4快进到12f0094并构建0；前一统一b35dd50的D4 non-Physical为417/417、前端65/65，typed新增3个ABI回归不以本机结果替代D4原生调用。
- T041 的 caller取消在循环边界遗漏由真实pipe同步采样取消4/4红复现，补退出清理后的caller检查；服务端shutdown正常返回不变。8用例10轮80/80、相关16/16，本机全套后绿色。T042 同步/异步factory清全进程池使另一工厂TEMP标记消失2/2红；改为关主机后仅ClearPool自有实际连接串，HTTP认证并发回归与ControlHost默认并行124两轮均绿，未关闭并行或skip。此前native sqlite3释放异常与该竞态相符，但稳定红例直接证明的是清理越界。
- T039 缺准备/过期/空摘要/畸形JSON不假报默认可用，各CRUD投影读取同一持久实验设置。32公开DTO/REST/SQLite回归通过；旧摘要流读取置于写门禁外，独立写等待测试先红后绿，事务内重核URI/版本/未决作业，同源匹配排队保持幂等。D4源1/2初始均false/0页，源45/56仍true/ready9；源1显式prepare后补摘要，作业2CE8CD66…Succeeded、ready9，原URI保持uploads/78f0…、原件SHA2401652F…不变；默认在窗口1实际显示第一页，Office0，未修改无效旧源2。
- T022 再上传独立副本源72/73。首轮捕获Running但PS5 HttpClient无PatchAsync，实际未发移动，不算通过；第二轮改标准SendAsync，源73的真实作业Queued→Running（21:22:36），当场PATCH move到根=400 media_error，说明播放/转换中不可移动。后续作业Succeeded/ready9、URI仍目录9、SHA2401652F…，默认窗口2第9页实屏与参考一致、Office0。仅源72/73测试原件通过API删除，原源1/2及其快照保留；缓存图和截图作为证据留在忽略目录。
- T040 D4第三轮目录浏览器完整通过；通知真实矩形最高bottom300.5，底栏top774.625，无相交；编辑态通知bottom170.5、按钮区top796，无相交且取消/保存可见。图片截图前新增complete/naturalWidth/decode：缩略图1920×1080完成、预览200，黑块消失，不修改缩略图生产逻辑。新夹具源69–71/目录33–36已清理。
- 原生窗口取证先在D4确认dispatch-only getter与同对象raw2031均80020003，而该对象TypeInfo确含FRESTRICTED成员；现用已有完整PIA的Dual getter，经No-PIA正规Csc/link嵌入（无新包/版本/资产）。默认完整接口槽45/20与D4偏移360/160一致，不依赖未用office/Vbe/GAC。12f0094窗口1实际1/9→NEXT2，关闭后窗口2实际1/9→GOTO9；两张实屏分别与页图1/9对照一致，无桌面/Office功能区泄露。玩家仅x0与1920两块输出；Office编辑Frame x364/y71在DISPLAY2范围，没有控制桌面x7680或其它输出占用。源45仍SHA2401652F…；恢复实验false并组shutdown后Office0，无额外强杀。实际用户文稿并存/超时/混合DPI尚未全部复测，T016/T017仍留边界。

### SRT 控制包的新因果证据（本机，不替代 D4 长稳）

原日志UMSG6/负载0对应ACKACK而非H264数据或接收buffer。[协议草案](https://github.com/Haivision/srt-rfc/blob/main/draft-sharabayko-srt.md#ackack-acknowledgement-of-acknowledgement-ctrl-pkt-ackack)规定该包无CIF；[libSRT检查提交](https://github.com/Haivision/srt/commit/fcae57145c000a9e7b72aa777adb8f85c2463242)要求控制负载非零且4字节对齐，与gosrt无填充实现形成互操作冲突，不能简单称Go侧违反协议。相同SHA589E50B7…的FFmpeg与18秒合成fixture本机A/B：MTX→FFmpeg12秒300帧/0H264，抓335个16字节ACKACK/0负载，恰335条尺寸告警；FFmpeg→FFmpeg同300帧/0H264，311个20字节/4零填充ACKACK，0告警。透明relay收发长度/包数一致、无发送错/意外peer，不改媒体字节；记录在`.validation/qa-srt-interop-006/run-20260928-205816/`，所有自有进程/端口已清理。参考发布端在限时读端结束后I/O退出-5，未写成双端全成功。该证据支持控制包互操作根因，不证明正式VLC链路受同影响、D4/60分钟稳定或已修第三方库；未改正式二进制/配置、未降低日志级别，T026继续承接。

### 当前目录页面与跨 Node 测试边界

- D4 `c80067e` 安装 happy-dom 成功、Web 构建成功（66.3 秒）；原 Node 24.13 前端测试实际为 48 通过/13 hook 失败，不能以逐项绿勾忽略退出码 1。首次错误是 SSR 外部化的 Naive UI CJS 缺少 NTag，另有 24678 WebSocket 冲突。
- 本机 Node 24.15 与 D4 24.13 的 CJS 分析差异有官方版本依据；[Node 24.14 发布记录](https://nodejs.org/en/blog/release/v24.14.0) 记载分析器替换。用校验过 SHA 的官方 24.13 单 exe 在本机准确复现 13 条失败，再以共享真实组件加载器只解析 naive-ui/vueuc ESM、ws=false 改为完整 61/61；24.15 也为 61/61、类型检查通过。该对照支持环境差异根因，D4 新加载器复测仍待部署。
- 真实 Chromium 在 D4 控制面无 Worker 条件下，以 `media_folder_qa.py` 连跑两轮：中文根/两级目录、同名上传不覆盖、源移动、父目录重命名、子树搬迁后页面刷新与三次下载摘要、空名/同名 400反馈、手机无横向溢出、PPT queued/原件保留说明均通过；每个 UI move/rename PATCH 恰好一条。两轮仅清理各自新建的源 63–68、目录 25–32，原源/目录/快照保持。
- 截图等待过渡后，PPT 编辑抽屉已完整进入视口，先前半截是 QA 截取过早。稳定帧仍有手机通知遮挡底栏/抽屉 footer，T040 承接；缩略图黑块需先核图像 decode，不据此声称实体播放黑屏。T021/T038 完成，T023 更广视觉矩阵未完成。
- D4 旧源 1/2（没有准备状态、页数 0）却返回 is_available=true，默认 OPEN 才拒绝；旧源元数据、空摘要与显式重试恢复由 T039 承接，没有删除原件或启动静默批迁。
- Office `edae159` 精确实机证据为 application/com_hwnd 的 COMException `0x80020003`，而不是 cast/IsWindow 阶段；D4 只读 MSPPT.OLB 与本机一致（2.12/SYS_WIN32、2031/2010 PROPERTYGET/FRESTRICTED/VT_I4）。下一步只在同一对象比较 raw Invoke/TypeInfo，保持原拒绝，不以 ABI 诊断成功替代归属验证。

前端新增目录 5 条、菜单 8 条真实 Vue/Naive UI DOM 回归，均有红→绿对照。目录输入
原生 aria、pending 回车重入和删除失败保留已修复；菜单统一 action/select，鼠标/键盘一次、
禁用零次，应急 HTTP 仅替身。完整 pnpm test **61/61**、typecheck 与 build:web 通过，
主包 >500 kB 提醒仍保留。新增 happy-dom 仅用于测试；CERNET 跳转节点 tarball 404 后，
获用户允许临时用官方源正常安装，仓库 `.npmrc` 始终仍是 CERNET，未提交 Node 锁文件。

Office 安全 Detail 传播新增 4 条公共路径回归，本机完整重构建后非 Physical 为
**373/373、0 跳过**；诊断版 `edae159` 已推送两处远端并在 D4 快进/构建（退出 0）。
Application 真实证据失败的精确阶段尚待下一次 OPEN，门禁和取证顺序未放宽。

目录浏览器进一步发现 Dropdown 动作重入：受控真实 Naive UI DOM 中鼠标事件为
`select→action→action`，键盘为 `select→action`，禁用叶子仍触发一次 action。
根因是 `option.props.onClick` 被 Dropdown 自身 mergeProps 执行，同时页面的 onSelect 又手动
调用同一回调；媒体菜单和应急菜单均有此模式。D4 已停机，T038 登记修复；任何真实应急
电源/重启按钮均未因本轮测试被点击。菜单稳定帧等待与此重复派发是两个不同问题。

独立 `runtime-dotnet/scripts/runtime.ps1` 已按 T037 明确退役并经 7 条非物理合同验证，现场入口仍为 `run-headless.ps1` + 认证 API；D4 下次更新将取得同一退役脚本。

- [x] D4 数据快照
- [x] D4 更新与构建（截至本轮 `12f0094`，后续代码改动须再次更新）
- [ ] 窗口 1/2 实体显示器落位、停启和两个固定预设
- [ ] 多源实体画面与直播源登记/解码
- [ ] PPT 原件/页图/默认与实验放映、Office 归属
- [ ] 目录/删除一致性与预案音量
- [ ] 两窗 1000/100 基准和 60 分钟混合稳定性
- [ ] 桌面/手机/可用封装端 UI、文档和停机恢复

## 本轮收口停机检查点（2026-09-28 21:34）

认证组shutdown后仅按已记录命令行停止本轮Vite PID54300，再按精确DataRoot停止
ControlHost PID52216并移除其启动任务。复查项目/MediaMTX/POWERPNT进程0，
18443/5173/8554/9997监听0，runtime-processes.json无（正常停机）；捕获任务未运行。
本机和D4临时浏览器Cookie已移除，开发口令文件、原媒体、原快照和QA截图/控制包证据均保留。
恢复实验false、两窗idle/volume100/loopfalse，不改系统音量或墙面/电视电源。
这只是本轮恢复基线，不代替仍未完成的T029/T030/T031/T034最终门禁。

## 2026-09-28 晚间继续：退出闭环、基准与失效流

- 21:47 重新只读核对：本机/D4 同为干净 `fa0b840`，D4 项目/MediaMTX/PowerPoint 进程及 18443/5173/8554/9997/8890 均为 0，状态文件无。按明确 DataRoot 启动 Hardware ControlHost 与仅两个播放器，目标仍 DISPLAY2/3，控制 DISPLAY1 不在播放目标。
- 完成 VLC 生命周期的生产时序提取与 13 条新增回归。所有 Stop 在线程池等待，返回 UI 上按 detach→VideoView→Media→MediaPlayer 顺序释放；同源重开和循环保持真实 Play 结果。同步 Stop／漏处置 VideoView 的受控反例各 1/1 红，恢复后 Windows 非 Physical 66/66。没有用替身宣称原生资源长期释放已被证明，T027/T030 的实机部分继续保留。
- 普通基准受控 10 条命令全部 Failed，却因 10 ms Started 延迟被旧脚本判通过。新脚本只关联本轮精确请求 ID，全部完成、有效时间／摘要／实例且零失败、状态不明、折叠、未排空才通过；新增显式 HotSwitch，要求健康异源、真实完成和 owner/generation/actual_source 一致。29/29 脚本合同通过，真实独立 SQLite 查询排除了其它目标/动作。逐请求相关性 SQL 会改变提交节奏，新 1000 样本不得与旧快发条件无说明地等价比较；本轮 D4 1000/100 尚未执行。
- 首次退出注入脚本把 ConvertFrom-Json 的整个数组当成一个管道项，误筛出本运行组五个成员；已明确保留为无效“单 player”用例。随后以显式数组展开、唯一整数 PID、原生启动时间／session 和终止错误块严格重跑，只结束 player-1 PID8112。整组退出、状态文件消失后超过两分钟仍 `Armed|93|`，确认缺少控制库退出回传，并非等待不足或混用 DataRoot。
- ControlHost 现在只读持有已认证 Supervisor 的原生句柄，真实退出才把当前组原子标为 Faulted、撤销两窗/音频 ownership、保留源/命令诊断并通过现有 SSE 发布，不伪称整组 Stopped。EOF、取消、旧组迟到和正常停止不故障化。首次故障提交失败有 2/2 红例；修复保留确证证据、250 ms 指数退避至 5 s，回执丢失幂等补 SSE。该构建完整非 Physical **488/488**：Domain41、Contracts18、Infrastructure80、ControlHost152、Windows66、Integration131，0 跳过，总数由六份 TRX 相加，不依据口头增量推算。证据 `.validation/t033-20260928-after-retry/`，Release 0 警告/错误；前端 65/65、Web/类型、Redocly/Spec Kit 通过，主包 1,090.61 kB 的 >500 kB 提醒仍保留。
- `2db189c` 已推送两远端、D4 干净快进并 Debug 构建 0 警告/错误。epoch97 精确 player-1 PID51708 退出后，仅 ControlHost PID52236 留存，状态文件无，库正确 `Faulted` 并有 Supervisor PID54860／实例原因，两窗 error/offline、源39/40保留。显式 restart 到 epoch99 后两窗可重新开图；原始 before/after/recovered 会话与成员记录在 D4 `qa-006/fault-player-2db189c/`，恢复截图在 `fault-player-2db189c-recovered/`。这只关闭该失败路径，不代表 T031 的文件暂失、转换失败和其它中断矩阵全部通过。
- 独立真实管道又发现旧 player/audio/office 可跨组认领新身份：重连 10/10 红、Supervisor 旧登记延迟首次 hello 4/4 红。T043 将 immutable instance/group 绑定扩展全部五类角色，在子进程登记时即绑定；同组重连和新组新实例保留。针对性 23/23、Integration 154/154、Host 152/152；Host 首轮继承代理有单项 502，清本次进程代理后完整重跑绿、原 TRX 保留。`b6ff30b` 本机已提交，D4 尚待更新该块。
- D4 默认页图源45 goto9→短视频48，OPEN完成且 source_type=video，却仍 current_slide/total_slides=9/9。T044 公共隐藏 STA 回归准确复现 (2,2)，新图片/source/generation/mode 其它断言均已正确；成功非文稿切入后清零，保留原生 PowerPoint 页码、页图切源失败旧状态。Windows 非 Physical 68/68，`b35c5fb` 已本机提交；D4 同条件新构建复测仍待进行。前端外层 ppt category 有守卫，不能由陈旧数字断言视频页面一定显示页码条。

### 直播有限对照及当前未闭环失败

- D4 `fa0b840` 两窗 RTSP46/SRT47 实际加载 NuGet 输出 `libvlc/win-x64`，插件 SHA 为 `D82A5B3972FD1A590CDA4DA70888FCD145CA2897F53C45DEA518AAAEC41B709D`，与本机 repo/NuGet/Release 相同。两组实际输出截图计数 375→447／2，证明各窗变化帧；约六分钟心跳和进度持续增长。辅助同 SHA FFmpeg 两路各 12 秒 300 帧、退出 0、H.264 错误 0，SRT 仍有 332 条尺寸告警；自有 90 分钟发布器截至停止时日志 0 告警，两有限读端已退出，发布器按精确 PID13916／启动时间停止。
- 本机同发布源 VLC 的实际握手是 SRT1.5.3，而辅助 FFmpeg 是1.5.7；343 个16B ACKACK→VLC尺寸告警0、328个→FFmpeg告警328。VLC decoded170→400、dummy displayed76→191，corrupt/discontinuity0、lost2未继续增长；两条启动迟显告警保留。不能把 NEWS 历史1.4.4或证书 OID1.5.5当版本，也不能把当前 VLC 未复现告警说成 MTX 对较新外部 peer 已修复。完整 primary 来源、原始包与清理范围见 [独立记录](../../docs/qa/006-srt-interop.md)。
- 窗口1网页43和窗口2 PDF42 第1/2页的输出截图已目视核对 Healthy／蓝绿页序，证据 `matrix-initial-fa0b840/`；两路直播图已目视核对并非纯黑。所有图仅是 DISPLAY2/3 交互输出像素，不代替关闭状态的墙体点亮/映射观察。
- 混合采样脚本先有限 **1 分钟 pilot**，70.72秒、两个分钟样本、视频循环与图片保持，未报告采样错误。首版收尾快照截在 CLOSE/SET_LOOP尚未完成时，不把该请求侧快照当完成；随后 API 两窗 idle、进度0、pending空，新版 helper 增加等待清理完成。D4 `qa-006/mixed-pilot-fa0b840-2207/` 保留，**没有做60分钟**，T030不勾选。
- D4新 QA source74 指向在线 MTX 的不存在路径 `rtsp://127.0.0.1:8554/qa006_missing_20260928`，独立 FFmpeg立即 DESCRIBE404并退出-875574520；命令4438却 Completed/ok，超过一分钟会话仍 playing/online、pos0、错误空，窗口1实际灰场、窗口2既有图不变。T045 承接：LibVLC Play返回只表示输入线程已启动，异步 EncounteredError 必须按当前资源/attempt保留和上报，Error可能瞬时转Ended，不能只轮询Error或把time0当失败。原始 API、解码日志和截图为 D4 `qa-006/offline-stream-74-*`／本机 `.validation/qa-d4-006/offline-stream-74-2db189c/`；该源仅用于可识别测试，尚未删除。

当前继续实现/验证 T043–T045；原 T011–T036 未满足的实体、用户Office并存、较广UI、1000/100、60分钟和停机恢复门禁保持未完成。拼接屏实体电源/已知预设与系统音量本轮新确认问题尚无回复，当前未写这些状态、未操作电视。正式长稳会在这批假状态和归属缺口修复后开始，不用 pilot 或接口受理替代。

### D4 文件暂失与转换失败恢复（仍为 `2db189c`）

- 只通过上传 API 新建图片源75，暂移受管理原件到同仓库明确 QA held 路径，源39/40和原始桌面资源不动。OPEN75之后正确为 error／“图片文件不存在”、pending空；finally 无覆盖搬回原路径，SHA一致，同ID新OPEN正确playing。原始 before/missing/recovered 记录在 D4 `qa-006/fault-held-2db189c/`；随后切回源39，仅API删除源75的本轮上传副本，原39素材与证据保留。
- 使用 pptx skill 的只读 ZIP/XML 核验旧有效原件，9个slide XML；停组但保留ControlHost后上传新QA副本76，状态queued。暂移该副本，再启动OfficeHost，作业 `8A5D1971-7B62-4B8A-870A-E60D861A2F0D` 正确Failed／“文稿原件文件不存在”，DTO failed、is_available=false、pages0，默认OPEN返回400，未假造页图。finally搬回且SHA不变，显式prepare产生新作业 `32D8468A-DAA5-4EA6-9624-B4E9CF543253` Succeeded／ready9。
- 源76原URI及SHA `2401652F7CD610DFCDFE4B5F2F32CD112D9645F1E30CEF44ECB3D716B82A55F0` 保持。默认窗口2可打开并goto9，slide_images/playing/pending空，PowerPoint进程0；截图和完整恢复DTO保留在 `fault-ppt76-retry-page9-2db189c/`、`fault-held-2db189c/ppt-recovered-default-playback.json`。切回源40后仅删除源76测试副本，原件1/45及数据库起点快照保留。这证明本轮暂失/显式恢复路径，未将强断转换的uncertain、用户Office并存、T031整体或60分钟判为完成。

- 输出图已分别目视核对：图片75恢复为原“QA IMAGE STABILITY”，文稿76第9页为对应原末页拼图。早先`fault-player-2db189c-recovered`截图异步任务尚未完成就紧接切PPT，窗口1记录了黑色过渡，不能当稳定恢复成功或产品黑屏结论；随后在同一构建重开源39/40，等待捕获任务Ready且唯一capture JSON落盘，再做`fault-recovery-stable-2db189c-2306`，两窗稳定图均与预期源对应。这一QA时序问题不放宽实际播出门禁。
- 当前构建旧窗口3/4共18次GET/open/control/close/loop/volume/mute/navigate/ppt-media均拒绝，前后command_records数量差0，结果在D4`qa-006/retired-window-http-2db189c.json`。该用例尚未覆盖旧预案/显示器选择的完整矩阵，T013保留未勾选。

- 23:11 本轮受认证组shutdown后按同一DataRoot停止ControlHost PID52236、清理其启动任务；精确核对项目/MediaMTX/PowerPoint进程0，TCP18443/5173/8554/9997与UDP8890均0，状态文件无，capture/mixed任务Ready。两窗loopfalse/volume100与实验false保持，未触碰系统音量或墙面/电视。QA75/76副本已删除、失效流74保留供T045同条件复测，原件1/45和完整起点快照保留。

### T045 本机显式原生回归的当前边界（未交付草稿）

- 真实回环服务器仅OPTIONS200/DESCRIBE404，无音视频载荷，隐藏Host的公共Execute/Sample回归先红：预期error、实际playing。独立原生探针记录Play True、NothingSpecial→Opening→Ended、EncounteredError与Stopped；没有假称周期轮询捕获到了短暂Error。当前两个404回归及纯状态/页码/生命周期子集转绿，但还不是完整交付。
- 同源重开／STOP→PLAY后循环的首个fixture同时`--no-video/--no-audio`禁用了所有ES，时间轴为0并立即结束，误名`native-loop-and-reopen-green.trx`实际2/4失败，明确排除。改为测试-only CPU解码＋dummy输出后，`native-cpu-dummy-valid-batch.trx`仅有两个RTSP Passed，testhost发生原生崩溃、整体Aborted/退出1；不能当4/4绿，也不能在尚未区分测试配置/WPF/生命周期原因时宣称产品已修好。
- 原生测试均Category Physical、显式选类，本机不Show/无真实声卡或图像输出，产品默认LibVLC配置未改。草稿保持未提交/未部署，正做逐例隔离／crash证据及独立只读寿命复审；正式两窗恢复、热切换及60分钟保持未执行。
