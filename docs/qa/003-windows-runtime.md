# Windows 播放运行

状态：T126 软件接线与本机只读探针通过；真实运行时可在 Hardware 模式一次拉起全部 7 个受管进程并到达就绪；待执行 T116 的 60 分钟四屏/媒体/音频混合测试。

## 本机只读探针（2026-09-10）

在当前 Windows 交互桌面执行只读探针：Hardware provider 通过 Per-Monitor-V2 上下文识别 1 台 `2560×1600` 主显示器，
默认 Core Audio 渲染端点可读取；未修改系统音量，也未移动播放窗口。`HostHardwareIntegrationTests` 覆盖负坐标、无效目标、
硬件观测值持久化和端点 unavailable 时的 fail-closed 行为。

## 真实运行时启动（2026-09-11）

`SafetyMode=Hardware` + Supervisor 控制通道，`POST /api/system/restart/` 返回：

```
{"success":true,"group_epoch":3,"detail":"Supervisor restart 的全部 Worker 已就绪。"}
```

4 个 PlayerWorker、AudioWorker、PowerPointHost、MediaMTX 全部在线，PID/start-time/session 证据写入 `runtime-processes.json`。
本次启动过程中修复的三个真实缺陷见 `003-runtime-lifecycle.md`。

## 仍待工作站验证

- 四屏真实显示拓扑与跨屏 DPI（当前开发机只有 1 台显示器）。
- Office COM/HWND 附着与实际放映效果（本机未安装可用 Office 素材流程）。
- VLC 视频、SRT/RTSP 流与 MediaMTX 转发。
- 真实音频输出（本机只做了端点只读探针，未播放）。
- 连续 60 分钟混合播放的稳定性、内存与残留进程。

在这些条件满足前，不得据本页结论宣称四屏或混合播放通过；T118 的旧实现删除仍被 T107–T116 门禁阻塞。

## 工作站 `d2` 硬件探针（2026-09-11）

在交互会话内以无头方式（计划任务 + 隐藏窗口）启动 `SafetyMode=Hardware` 的 ControlHost 后：

- `/api/displays/`：4 块真实显示器，均为 `3840×2160`，坐标 `x=0 / 3840 / 7680 / 11520`，`DISPLAY1` 为主屏；
  即工作站已具备 T116 所需的四屏拓扑。
- `/api/volume/`：`level=51, muted=false, system_synced=true, backend=windows_core_audio`，Core Audio 可用。

这一条与开发机的“1 台 2560×1600 主显示器”形成对照：SSH 会话（session 0）看不到真实显示拓扑，
硬件结论必须取自交互会话内运行的 ControlHost。

部署方式与命令见 `003-workstation-runbook.md`；上述探针只证明硬件可见，不代表四屏播放已通过。

### D4 全部 Worker 启动与显示落位（2026-09-17）

- `POST /api/system/restart/` → `{"success":true,"group_epoch":5,"detail":"Supervisor restart 的全部 Worker 已就绪。"}`；
  4×PlayerWorker、AudioWorker、PowerPointHost、MediaMTX 均在线（session 1），`control-host.err.log` 为空。
- 启动过程中发现并修复 `RuntimeProjectionPublisher` 把 JSON `null` 当数字读取、导致 Worker 收到 `error` 帧后停机的缺陷，详见
  `specs/003-dotnet-runtime-refactor/verification.md`。
- 按用户指定，四路输出使用 `\\.\DISPLAY2`–`\\.\DISPLAY5`（集显 `\\.\DISPLAY1` 不参与播放），四个窗口经 `POST /api/displays/select/` 落位成功。
- 四块输出已切换为 `3840×2160`；EDID 只提供 ≤30Hz 模式，刷新率由 60Hz 降到 30Hz，虚拟桌面已重排为 y=0 横排（`0 / 3840 / 7680 / 11520`），控制屏移至 `15360,0`。
- 本轮仍只验证到“运行时就绪 + 显示落位”，未播放真实媒体；T116/T129 未完成，T118 继续阻塞。

## D4 部署与控制面验证（2026-09-17）

`D4`（`192.168.5.194`，Windows 10 Pro 1909 / build 18363.1556）按用户批准的兼容性例外完成部署，未改变受支持基线：

- `dotnet restore ScpCv.sln --locked-mode`：15 个项目全部还原成功。
- `dotnet build ScpCv.sln -c Debug --no-restore`：**0 警告 0 错误**，含 `net10.0-windows10.0.19041.0` 的
  PlayerWorker/PowerPointHost/Windows 测试工程。
- `dotnet test ScpCv.sln -c Debug --no-build --filter "Category!=Physical"`：**191 项通过、0 失败**
  （Domain 38、Windows 11、Infrastructure 19、Contracts 18、Integration 51、ControlHost 54）。
- 前端：`pnpm test` 40 项通过；`pnpm run build:web` 成功（Vite 8.2 / rolldown）；`pnpm run dev:web` 在 `0.0.0.0:5173` 常驻。
- Hardware ControlHost（**未启动任何 Worker**）监听 `0.0.0.0:18443`，本机 `/health/ready` 返回 200 `Healthy`；
  本机 `csrf → login → me → logout` 全部 200。

`/api/displays/` 返回的真实拓扑：

| index | 设备 | 分辨率 | 坐标 | 主屏 |
| --- | --- | --- | --- | --- |
| 1 | `\\.\DISPLAY2` | 1920×1080 | (0,0) | 是 |
| 2 | `\\.\DISPLAY3` | 1920×1080 | (1920,0) | 否 |
| 3 | `\\.\DISPLAY4` | 1920×1080 | (3840,0) | 否 |
| 4 | `\\.\DISPLAY5` | 1920×1080 | (5760,0) | 否 |
| 5 | `\\.\DISPLAY1` | 1920×1200 | (7680,-6) | 否 |

四块 1920×1080 横向排在 y=0，另有第五块 1920×1200 控制屏。SSH 会话（session 0）里的 `Screen.AllScreens` 只看到 1 块
`WinDisc 1024×768`，硬件结论必须取自实际运行 ControlHost 的会话，与 D2 记录一致。

从开发机（`192.168.1.109`，经 `192.168.5.1` 一跳跨网段）验证：

- `/health/ready` → 200（约 10 ms）。
- `Origin: http://192.168.5.194:5173` 的凭据 CORS 预检 → 204，精确回显 Origin 且 `Access-Control-Allow-Credentials: true`。
- 白名单外的 `Host` 头 → 400。
- `http://192.168.5.194:5173/` → 200，返回 SCP-cv 控制台 HTML。

本轮没有启动 PlayerWorker/AudioWorker/PowerPointHost/MediaMTX，没有创建播放窗口、选择显示器、调整音量或写入任何设备；
因此**不改变 T116/T129 状态**。D4 尚未安装 PowerPoint（用户后续安装），四屏实际播放、Office COM/HWND、VLC/SRT、
真实音频与 60 分钟混合测试仍待执行。

## D4 更新与常驻启动（2026-09-24）

本机 `main` 经 secondary（GitLab）同步到 D4 的 `D:\SCP-cv`；D4 从 `9d2a782` 更新至 `47874b7`。
现场在 `D:\dotnet` 使用 .NET 10.0.400，在 `D:\nodejs` 使用 Node 24.13.0 / pnpm 11.22.0；
按仓库 `.npmrc` 使用 CERNET npm 镜像。此轮没有恢复旧 Python 运行时，也没有改动媒体、数据库或凭据。

- D4 Debug 构建 0 警告、0 错误；前端 40/40 测试、类型检查、Web 构建通过。
- 本机更新后的非 Physical .NET 测试为 221/221 通过；首次运行受本机 `http_proxy` 等环境变量影响，
  两项回环 HTTP 断言得到代理的 502，清除该次测试进程的代理变量后全套通过。
- 首次 Hardware 启动失败：`/health/ready` 虽为 200，运行组却在约 20 秒后因 `runtime_restart_cancelled` 进入 `Faulted`。
  排查发现脚本对 restart 请求固定 15 秒 HTTP 超时，短于 Worker 的就绪预算；修复后使用 `ReadyTimeoutSeconds=120`。
  同轮补上 Supervisor 已认证心跳，并对 SQLite 测试临时文件占用增加清理重试。
- 修复后由计划任务在交互会话（session 1）启动 `SafetyMode=Hardware`；`run-headless.log` 记录
  `group_epoch=29` 和“Supervisor restart 的全部 Worker 已就绪”。运行组数据库状态 `Armed`；
  ControlHost、Supervisor、4×PlayerWorker、AudioWorker、PowerPointHost、MediaMTX 均在运行，
  `runtime-processes.json` 中七个受管角色均在 session 1，`control-host.err.log` 为空。
- 前端由 `ScpCvFrontend` 计划任务常驻启动，Vite 监听 `0.0.0.0:5173`；
  本机经 `192.168.1.104` 跨网段访问 `http://192.168.5.194:5173/` 返回 200，
  `http://192.168.5.194:18443/health/ready` 返回 200 `Healthy`。

本轮未发起媒体播放、显示落位变更、音量或设备命令，未执行 T115/T116/T129 的性能与 60 分钟混合测试；
`Armed` 只证明运行组就绪，不代表物理画面、Office 放映或音频验收通过。D4 的 Windows 10 1909
仍属已批准的兼容性例外，不改变受支持平台基线。

## D4 多源上传、打开与画面核对（2026-09-24）

在同一工作站继续调试至 `2528f63`，最终 Hardware 运行组 `group_epoch=35`、状态 `Armed`。
本轮只用既有 DISPLAY2–DISPLAY5 落位，不切换大屏模式、分辨率、设备电源或系统音量。
实测拓扑为四块 `1920×1080` 输出（x=0/1920/3840/5760）加 `1920×1200` 控制屏；
这是本轮观测值，不沿用 2026-09-17 的 4K 结论。

| 源与路径 | 上传/录入 | 打开与证据 |
| --- | --- | --- |
| PNG 图片 | 浏览器文件选择上传 | 2 号窗口 `playing`；交互桌面截图出现测试渐变图 |
| H.264 MP4 短视频 | 浏览器文件选择上传 | 2 号窗口 `playing`；交互桌面截图出现测试视频帧 |
| PDF | 浏览器文件选择上传 | 2 号窗口 `playing`；交互桌面截图确认 PDF 页面渲染 |
| 网页 | 浏览器录入 D4 本机可访问的静态 HTML | 2 号窗口 `playing`；交互桌面截图确认 WebView2 页面内容 |
| 低音量 MP3 | 浏览器文件选择上传、背景音乐立即播放 | AudioWorker 从 `loading` 正确进入 `playing`；仅核对 Worker 状态，未做人耳/声卡回录验收 |
| PPTX | 浏览器上传已有有效 9 页文稿的测试副本 | 3 号窗口 `playing/powerpoint`；`next` 到第 2 页、`prev` 回第 1 页；交互桌面截图确认真实放映 |
| 播放主机本地路径 | `POST /api/sources/local/` 引用 DataRoot 内测试图片 | 4 号窗口 `playing`；测试副本和源随后删除 |

现场发现与复核：

- 网页初次打开时命令 108 持续 `Processing`。阶段探针确认 `CoreWebView2Environment.CreateAsync` 已完成、
  `EnsureCoreWebView2Async` 在脱离视觉树的控件上不返回。修复视觉树预备与阶段超时后，同一页面约 2 秒导航完成，
  API 状态和真实画面一致；临时探针代码、日志已清除。
- 首次受控停机后旧网页命令 108/127 仍在 `Processing`，使新命令 `Pending` 无法领取。
  `CompleteStopAsync` 现于 Supervisor 确认退出后终结旧租约；D4 复测两个旧命令均为
  `Superseded/runtime_group_stopped`，没有手改数据库。
- PPT 初次报 STA 错误；移除 WPF 路径的 `ConfigureAwait(false)` 后，遇到另一个已有 PowerPoint
  自动化实例占用。该实例无主窗口，用户明确授权后仅终止核对过身份的单个进程，再试真实 PPT 成功。
  本轮自己创建的 Office 测试孤儿进程亦在停机后按 PID/启动时间清理，未触碰其他 Office 进程。
- 音频初次首帧回报仍是 `loading`，不改音量数值地再次下发命令才变 `playing`；
  现在等待原生播放器离开加载态后再确认。前端 `prev` 与服务层 `previous` 的动作词汇不一致也已在 REST 边界修复。

未完成边界：MediaMTX 上短时测试 RTSP 路径 `ready=true`，FFprobe 从另一机器读到 H.264 640×360，
停流后路径消失；但控制台/ControlHost 当前没有直播 URL 创建入口，不能宣称应用直播源已打开（T134/T135）。
短视频播放自然结束后最后一帧留屏，API 仍显示 `playing`，由 T136 补状态上报；Office 残留进程由 T133 处理。
本轮没有 60 分钟四屏/Office/直播/音频混合稳定性、声卡回录或真实 4K 门禁，T115/T116/T129 仍未关闭。

收尾：本轮创建的 6 个上传/网页测试源（ID 3–7、9）、本地路径源 ID 8、后台音乐列表与
临时截图任务/脚本/日志均已清理；D4 原有源 ID 1/2、原始媒体、数据库及凭据保留。
四窗口恢复 `idle`，本机忽略目录 `.validation/source-debug-20260924/` 保留五张截图证据（不提交 Git）。
本机非 Physical .NET 测试 226/226 通过；D4 Debug 构建、针对性回归与真实源操作通过。

## 已归档工作站 `D2` HTTP 控制面（2026-09-14）

- 安装 .NET SDK 10.0.400 后，`ScpCv.sln` 在 D2 完成 locked restore 和 Debug build，0 警告、0 错误。
- Hardware ControlHost 监听 `0.0.0.0:18443`，Host 白名单包含 localhost、127.0.0.1 和当时的 D2 固定地址；防火墙仅允许 Private/LocalSubnet 的 TCP 18443。
- 开发机通过当时的 D2 固定地址请求 `/health/ready` 返回 200，对应的凭据 CORS 预检返回 204。该地址已退役，不再是项目活动配置。
- 这次只启动 ControlHost，受管 Worker/MediaMTX 数为 0，没有创建播放窗口或执行任何设备写入。

因此这一结果不改变 T116/T129 状态；真实画面与 60 分钟测试仍需用户在工作站交互桌面上明确开始。
