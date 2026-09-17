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

## 已归档工作站 `D2` HTTP 控制面（2026-09-14）

- 安装 .NET SDK 10.0.400 后，`ScpCv.sln` 在 D2 完成 locked restore 和 Debug build，0 警告、0 错误。
- Hardware ControlHost 监听 `0.0.0.0:18443`，Host 白名单包含 localhost、127.0.0.1 和当时的 D2 固定地址；防火墙仅允许 Private/LocalSubnet 的 TCP 18443。
- 开发机通过当时的 D2 固定地址请求 `/health/ready` 返回 200，对应的凭据 CORS 预检返回 204。该地址已退役，不再是项目活动配置。
- 这次只启动 ControlHost，受管 Worker/MediaMTX 数为 0，没有创建播放窗口或执行任何设备写入。

因此这一结果不改变 T116/T129 状态；真实画面与 60 分钟测试仍需用户在工作站交互桌面上明确开始。
