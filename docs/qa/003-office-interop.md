# 003 Office/窗口互操作验证记录

状态：D4 当前12f0094已验证精确Dual窗口取证、两窗原生出画/导航与清洁自有实例协作退出；用户并存异常矩阵、混合 DPI 和长导出仍待验证。下方2026-09-25/27四窗记录是历史证据，当前仅使用大屏窗口1/2。

## 2026-09-28 两窗当前实现与验证边界

- 后台转换编排的非 Physical 回归为 19/19，包含失败重试、停止/遗留 running、原件移动、页序与暂时 Office 拒绝；默认两窗页图播出与下载摘要已有 D4 证据，详见 006 verification。
- 原生 Adapter 新增 15/15 公共路径归属回归：复用 Application、Open/Run 可重入时用户并入都拒绝；只关闭本次自有文稿，失败补偿无法关闭则保留槽位供协作 Dispose 重试。用户文稿或集合不可读时不 Quit。
- 从当前Application及本次`SlideShowSettings.Run()`返回对象读HWND，再核PID/启动时间。D4的2031/2010为受限成员：dispatch-only及raw Invoke均80020003，现使用已有完整PIA Dual槽45/20、编译器嵌入interop，未新增依赖/资产或全局猜窗。D4窗口1实际1/9→NEXT2、窗口2实际1/9→GOTO9，截图与参考对应，恢复实验false后shutdown Office0；用户并存与更广矩阵仍由006承接。
- 无证据时明确返回 `office_process_unavailable`、`slideshow_hwnd_unavailable` 或 `slideshow_owner_mismatch`；不降低门禁。新版 D4 原生出画、用户文稿并存和超时/混合 DPI 由 006/T016/T017 继续承接。

## 已覆盖的软件边界

- `PowerPointOwnershipGuard` 以主机唯一命名 Mutex 和 PID/start-time 证据持有 Office 所有权；释放顺序已覆盖正常和异常路径。
- `OfficeStaDispatcher` 使用独立 STA 与 WPF 消息泵，operation ID 在完成后保留结果，重复请求不会再次触发 COM。
- `PowerPointComAdapter` 只关闭由当前实例打开并登记的 Presentation；无法证明所有权时不退出用户 Office。
- `SlideShowWindowAttacher` 在嵌入前校验 HWND、PID、进程启动时间和 DPI，并在附着/调整失败时返回明确错误。

## 开发机探针（待执行）

在安装 PowerPoint 的交互桌面运行：

```powershell
dotnet test runtime-dotnet/tests/ScpCv.Integration.Tests --filter FullyQualifiedName~OfficeOperation
dotnet run --project runtime-dotnet/tools/ScpCv.InteropProbe -- --office-hwnd <放映窗口HWND>
```

需要记录：PowerPoint 版本、Office 是否已有用户文档、放映窗口 HWND/PID/start-time、每台显示器设备路径与 DPI、模态对话框行为、长导出耗时及超时后的 Office 状态。当前环境未宣称已完成上述实机验证，也不因缺少 Office 而放宽所有权或强杀策略。

## D4 旧自动化实例清理与待续测（2026-09-25）

用户明确允许关闭 D4 上阻塞放映测试的特定 PowerPoint 自动化实例。关闭前核对全机仅有一个 `POWERPNT.EXE`：PID 49584、2026-09-24 13:20:28.8787657 +08:00 启动、交互会话 1、命令行为 Office16 `POWERPNT.EXE /AUTOMATION -Embedding`。交互会话只读 COM 检查显示 2 份已保存文稿、0 份未保存文稿、2 个放映窗口。

先在交互会话请求该 Application 正常 `Quit()`，调用返回但进程仍在；再次检查仍有 2 份已保存文稿、1 个放映窗口，实体截图在本机忽略目录 `.validation/qa-next/ppt-quit-screen.png`。SSH 所处 session 0 报 `MainWindowHandle=0`，交互会话实际报非零；因此不可把 session 0 的 0 句柄作为“没有用户窗口”或进程身份判据。保持 PID、启动时间、命令行、会话和全机唯一实例校验后，仅对获批 PID 49584 执行定向强制结束，随后读到该 PID 不存在、全机 `POWERPNT.EXE` 数量为 0；没有按进程名批量终止。

紧接着本机 `以太网` 适配器变为 `Disconnected`，`192.168.1.104` 无法发包；WLAN 也无法到达 `192.168.5.194`。此时 SSH 会话断开，无法从 D4 继续核对运行组、打开原有 9 页 PPTX 或观察实体画面。**本次不宣称 T133/T143 通过，也不能据此断言 D4 服务中断或正常。**断链前最后一次已知运行组为 epoch 45 / `Armed`，状态文件存在；它不是断链后的新鲜证据。

恢复连接后先检查 `D:\SCP-cv` 与 `runtime-processes.json`、ControlHost/Frontend 健康及单组进程树；清理本轮创建的计划任务 `ScpCvQaPptClose`（其脚本强制校验上述已退出的精确 PID 与启动时间，不会处理新 Office 进程）；然后重新登录 API，在 3 号窗打开原有 ID 1 PPTX，核对 `current_slide`、实体放映画面、导航和关闭后的 Office 归属。只有此轮通过后再考虑勾选 T143。

## D4 恢复后的放映与清理复测（2026-09-27）

- 开发机有线网卡仍断开，但 WLAN 改为 `192.168.1.103` 后可直连 D4；ControlHost 健康检查 200。D4 的 `ScpCvQaPptClose` 遗留计划任务经路径核验后删除，D4 拉取至 `d4ac6fe`。运行组 epoch 45 / `Armed`，状态文件 7/7 PID、启动时间及 session 匹配，旧 PID 49584 未再出现。
- 原有 ID 1、15,594,183 字节 PPTX 在 3 号窗打开后为 `playing/powerpoint`、9 页且实体目标屏出画，不再报 `slideshow_hwnd_unavailable`。旧版首次上报 `current_slide=0`，而第一次 NEXT 返回第 2 页；`next×3` 到第 4 页，`prev×2` 回第 2 页，实体截图随之改变。CLOSE 后 API 为 idle。
- 旧版受控 shutdown 使七个项目子进程退出、状态文件移除、数据库 `Stopped`，但新出现的 Office PID 52048 仍在。交互会话只读 COM 发现其承载两份 `C:\Users\admin\Desktop\` 下的已保存用户文稿及一个放映窗口，均非 ID 1 测试源。**不能凭“由项目打开时出现”就持续视为自有，故未在未授权时结束它。**用户随后明确批准关闭这两份文稿；正常 `Quit()` 返回后进程仍在，复核两份均已保存、放映窗口为 0，再核对 PID/启动时间/命令行/session 后只定向结束 PID 52048，未删除文稿文件。
- 修复 `PowerPointOpenResult` 实际页码、Office IPC `current_slide` 和 PlayerWorker 的 1-based 兜底；新回归 4/4、.NET 非 Physical 240/240。D4 拉取 `6b477f0` 并构建 0 错误后重启到 epoch 49 / `Armed`，7/7 受管进程、状态文件存在。ID 1 再开为 `playing/powerpoint`、`current_slide=1/9`，实体第 3 屏出画；NEXT 为 2/9，CLOSE 后 idle 且目标屏回黑。
- 本轮期间 D4 右侧屏幕又出现用户桌面文稿编辑窗口；项目 CLOSE 后该文稿与 Office 进程仍在，没有被误杀。当前不能用此环境证明“没有用户文稿时 Office 进程必然退出”，T133 继续保持未完成。四窗最终 idle、背景音频 stopped；`ScpCvQaPpt*` 临时计划任务为 0。原有 `ScpCvFrontend` 任务曾停止，重新运行后 Vite 监听 0.0.0.0:5173；本机经 WLAN 访问 Web/ControlHost 均为 200。

实体截图在本机忽略目录：`.validation/qa-next/ppt-reopen-20260927.png`（修复前打开）、`ppt-next3-20260927.png`（第 4 页）、`ppt-open-fixed-20260927.png`（修复后第 1 页）、`ppt-after-close-20260927.png`（目标屏回黑且用户文稿保留）。这些截图仅作 D4 开发机证据，不代替混合 DPI、长导出或 60 分钟稳定性门禁。
