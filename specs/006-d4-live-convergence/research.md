# Phase 0 Research: D4 大屏实机收敛

## D4 更新与远程会话

- **Decision**: 从已推送的 secondary/GitLab `main` 更新 D4 `D:\SCP-cv`，先核对工作区与提交、停机进程及数据根目录，再执行构建。SSH 仅做维护入口；实体显示拓扑、Worker 和 Office 测试都由交互会话的 ControlHost/受管进程执行。
- **Rationale**: 2026-09-27 只读勘察发现 D4 工作区干净但停在 `6fdbca7`，本机已到 `c28f609`；D4 登录 Shell 未包含 Git PATH，需使用安装位置。SSH 会话的显示枚举只有虚拟 `WinDisc 1024×768`，不能证明四个实体输出；D4 控制台 session 1 当前活跃。
- **Alternatives considered**: 在 SSH session 0 直接启动 WPF/Office 并判断画面（会测错桌面）；拷贝未构建的散文件覆盖现场（无法证明版本一致）。

## 数据与停机快照

- **Decision**: 更新前确认 D4 项目进程及端口处于停机状态；对当前 DataRoot 中的 SQLite 主库和 WAL/SHM 作为同一组做可恢复快照，记录媒体目录清单和 Git HEAD。试验可以改 `D:\SCP-cv` 数据，但清理仅针对本轮可识别的测试源与产物。
- **Rationale**: 当前 `.validation\t129-workstation` 存在 `control.db`、4 MB 级 WAL 和 SHM，只复制主库可能丢失尚未 checkpoint 的提交。用户允许开发期数据操作，但不要求丢弃既有媒体。
- **Alternatives considered**: 直接重建空库（失去旧数据升级与兼容验证）；只复制 `control.db`（快照不完整）。

## 启动与显示器归属

- **Decision**: 先在 Hardware 模式启动不带 Worker 的 ControlHost，验证迁移、健康与交互会话里的显示拓扑；随后只启动 player1/2、audio、office 等当前受管角色。优先使用既有受控运行脚本/协作停机入口，测试后恢复先前停机状态。确认配置的 `DISPLAY2/3` 实际对应两块大屏，不能依靠设备枚举顺序。
- **Rationale**: `runtime.ps1` 使用独立状态文件，并非 `.validation\t129-workstation` 的运行组；旧工作站手册还写四屏/七进程。D4 当前无项目/Office 进程，18443/5173/8554/9997 无监听，适合分阶段启动与核验。
- **Alternatives considered**: 一次性启动所有服务并以 HTTP 200 判定成功（无法区分控制面和实体执行端）；按 SSH 的虚拟显示器选址（错误）。

## 实体画面与可靠性证据

- **Decision**: 对每项媒体操作同时记录 HTTP/会话与命令结果、目标 Worker 身份、交互桌面截图/画面变化；固定预设需观察墙面实体映射并恢复。60 分钟长稳包含重复切源、视频循环/自然结束、资源快照和失败恢复。性能基准脚本先改为仅窗口 1/2，再执行 1000/100 样本。
- **Rationale**: 旧 D4 回归中 100 次命令完成率不能证明持续实体画面；视频切源私有内存一度从 106.8 增至 813.3 MiB，Office 曾出现窗口不可用，RTSP/SRT 有损坏帧/协议告警。
- **Alternatives considered**: 只用 API `playing`、单张静态截图或短时 FFmpeg 成功退出判定稳定（证据范围不足）。

## Office 与 PPT

- **Decision**: 先用测试 PPT/PPTX 完成上传转 PNG、原件摘要、页序和默认页图播放；再单独开启实验开关验证原生放映，在关闭前核对 Office 进程归属与用户文稿。转换失败和不确定状态不得盲目重放，原件移动后页图引用应保持有效。
- **Rationale**: 本机的 9 页真实 COM 导出已通过，但 D4 的新路径尚未部署；旧现场测试有 `slideshow_hwnd_unavailable`/`com_open_failed` 与共享 Office 归属风险。当前 D4 没有 POWERPNT，COM ProgID 已注册。
- **Alternatives considered**: 仅本机 Office 导出作为实机证据；按进程名批量结束 PowerPoint（会误伤用户内容）。

## 已知功能缺口与流媒体

- **Decision**: 把预案音量硬件假成功、单源删除文件清理假成功、视频资源增长、SRT/RTSP 解码告警和直播源登记入口纳入 006 修复与同场景验证。流地址必须由控制端明确登记为流类型，播放实际结果由 Worker 状态和画面证明，不把简单 TCP 可达或 HTTP HEAD 当作流已解码。
- **Rationale**: 当前 `ScenarioService` 仅写 `VolumeLevel`，实际 Core Audio 只在 `RuntimeStateService.SetSystemVolumeAsync` 被调用；`DeleteSourceAsync` 先删库再吞文件删除异常；`StreamDiscoveryService` 仅探测已有记录且使用 HTTP HEAD，仓库没有创建直播源的 REST/UI 入口。
- **Alternatives considered**: 把 RTSP/SRT URL 当网页源（协议/播放器错误）；只修浏览器标签或暂时隐藏失败结果（违背如实状态）。

## 技术边界

- **Decision**: 保持 .NET 10、SQLite 单写入者、两个 WPF PlayerWorker、独立 Office STA、pnpm 前端和现有 REST/SSE/Named Pipe 合同；无需 MongoDB。D4 Windows 10 1909 仍按已批准兼容性例外验证，不把这次成功扩展为官方支持声明。
- **Rationale**: 006 是现场收敛和缺陷修复，不是重建技术栈；引入新数据库不能解决文件与事务一致性。
- **Alternatives considered**: 换存储/播放器或新增长期双运行时（增加风险且无需求依据）。
