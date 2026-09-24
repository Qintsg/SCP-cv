# D4 运行组状态恢复与下一轮媒体/前端回归（2026-09-24）

## 环境与边界

- D4：`192.168.5.194`，仓库 `D:\SCP-cv`，Hardware ControlHost `:18443`、Web `:5173`；本轮基于 `5debb4c`。D4 Windows 10 Pro 1909 仍属已记录的兼容性试验机，不改变项目支持基线。
- 使用独立 `.validation/t129-workstation` 数据目录；原有源 1/2 未覆盖。测试源 25–28 和浏览器测试文件夹均通过 API 删除，四窗/背景音频最终为 idle/stopped。测试文件和截图留在忽略的 `.validation` 目录供本机复核；本机临时账号口令副本已删除。
- 不触碰墙面映射和设备电源。PowerPoint 自动化实例归属不明时不结束用户 Office。

## 运行组状态文件：复现、修复、D4 验证

旧版 API restart 返回新组 `Armed` 后，七个子进程存在但 `runtime-processes.json` 缺失。根因是旧 Supervisor 在新组写入同一路径后无条件删除状态文件。本轮新增 `SupervisorStateStore`：同目录临时文件原子发布、跨进程互斥、删除前比较整组 role/PID/启动时间/session。先行回归覆盖旧组清理不得删除新组及 PID 复用。

D4 上先按父 Supervisor PID、七个子进程的路径/role/PID/启动时间/session 逐项核对，短暂恢复缺失文件并执行受控 shutdown。旧版遗留项目自有 `ScpCv.PowerPointHost.exe` 经精确身份复核后单独结束；没有结束 `POWERPNT.EXE`。新版本把 `office` 角色按“项目 Host”处理：协作等待后只定向结束 Host，不沿进程树强杀 Office。部署后 API restart 到 `group_epoch=41`，新组 7/7 存活、旧组 0/7 存活、无未登记项目子进程，状态文件持续存在；后续更新到 `group_epoch=43` 仍为 `Armed` 且文件存在。

## 媒体源复测与修复

| 场景 | 修复前证据 | 修复后 D4 结果 |
| --- | --- | --- |
| 背景音频一次暂停 | 第一次 PAUSE 可反向恢复播放，API paused 时声卡仍有 880 Hz | `SetPause(true)` 代替切换式 `Pause()`，命令等待真实状态；一次 PAUSE 后 API `paused`、WASAPI 6 秒回录 0 字节；播放时 2,296,320 字节，STOP 后 0 字节 |
| 8 秒视频循环 | `loop_enabled=true`，实体画面停在末帧 `00:00:07.960` | OPEN 携带持久循环意图，原生结束回调按代次重播；间隔 13 秒截图帧计时 `00:00:01.960`→`00:00:06.180` 且画面变化，API 保持 `playing` |
| 同源视频再次 OPEN | API `playing`，实体第 2 屏黑屏 | 相同源 ID/revision/URI 复用当前 VLC/VideoView，不再叠建实例；再次 OPEN 后实体彩条画面正常，API `playing` |
| 视频自然结束且不循环 | API 仍 `playing` | 关闭循环、重新打开并等待 11 秒后 API `stopped`，`position_ms=7915`、`duration_ms=8000`、无 pending；由有效 source generation 的主动状态报告更新 |
| 3 页 PDF 文件释放 | 上轮 100 次混切后报告关闭仍锁源文件 | 本轮单独打开/关闭及连续 40 次 PDF 打开/关闭均完成；关闭后独占读写句柄可取得。未在相同“多源混切”条件复现，**T141 仍未关闭** |

以上实体画面截图在本机 `.validation/qa-next/`：`video-ended.png`、`video-reopen.png`（修复前）以及 `video-loop-new-a.png`、`video-loop-new-b.png`、`video-reopen-new.png`（修复后）。截图由 D4 交互会话的计划任务抓取，临时截图计划任务已删除。

## 前端文件夹回归

Playwright headless Chromium 以 1365×900 访问 D4 Web，API 会话登录后在真实页面操作。旧版空名称点击创建：弹窗关闭、无必填提示；“编辑”点击：不出现重命名菜单。改为创建回调在无效/失败时返回 `false` 并展示 `role=alert` 的必填提示；文件夹打开区与菜单改为并列按钮，消除嵌套点击冒泡。D4 拉取 `5debb4c` 后同一脚本复测：弹窗保留、错误可见、编辑菜单含“重命名”、URL 未误跳转；测试文件夹经 API 删除。

## 软件验证与未完成项

- .NET 非 Physical：236/236 通过（含旧播放器/旧 source generation 自然结束事件拒绝回归）；前端 `pnpm run typecheck`、`pnpm test` 40/40、`pnpm run build:web` 通过。D4 `dotnet build --no-restore` 0 错误。Web 构建仍有既有 >500 kB chunk 警告。
- PowerPoint 仍未宣称通过：旧 `/AUTOMATION -Embedding` 实例 PID 49584 无可见主窗口；交互桌面只读 COM 检查显示 2 个已保存文稿和 2 个放映窗口，无法证明其归属，未结束。`ResolveSlideShowHandle` 的新进程启动时间过滤会拒绝复用旧实例；关闭该实例需单独授权，T133/T143 保留。
- T115/T116/T129 的完整性能/60 分钟混合门禁、T134/T135 实流、T137 预案硬件音量仍未完成；本轮短测不能替代它们。
