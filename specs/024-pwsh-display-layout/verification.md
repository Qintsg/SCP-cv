# PowerShell 7与五屏排列验证

## D4当前状态

2026-10-23 21:35（UTC+8），D4通过10.88.50.194完成维护及CV启动验证。SSH沿用192.168.5.194既有主机密钥进行校验；本机网卡配置保持。

| 显示器 | 桌面位置 | 桌面尺寸 | 实际信号 | CV用途 |
| --- | --- | --- | --- | --- |
| DISPLAY1 | 0,0 | 1920×1200 | 1920×1200 | 主显示器、控制桌面 |
| DISPLAY2 | 1920,0 | 3840×2160 | 3840×2160，30Hz | 窗口1 |
| DISPLAY3 | 5760,0 | 3840×2160 | 3840×2160，30Hz | 窗口2 |
| DISPLAY4 | 9600,0 | 3840×2160 | 3840×2160，30Hz | 保留输出 |
| DISPLAY5 | 13440,0 | 3840×2160 | 3840×2160，30Hz | 保留输出 |

原生活动配置、CCD持久配置及CV的`GET /api/displays/`一致。13:35:25 UTC的63–66输入611A读回均为3840×2160，组播224.1.1.63–66、端口5000匹配。Windows没有重启，最后启动仍为2026-10-21 09:31:56 UTC。

## PowerShell入口

- 官方PowerShell **7.6.7**安装于`C:/Program Files/PowerShell/7/pwsh.exe`，实际签名为Valid/Microsoft Corporation；ZIP摘要与[研究记录](research.md)一致。
- D4实际验证Core版本、RemoteSigned、CIM和ScheduledTasks；221字节文本的有限JSON输出为264字符，没有序列化PSProvider。
- 桌面`SCP-cv 一键启动.cmd`的普通和提权入口均指向绝对pwsh路径。
- `ScpCvRuntime-022-release-20261021`及`ScpCvFrontend-022-release-20261021`动作改用同一路径，原参数、账号、权限、工作目录和登录触发器保持；`startup.json`同步动作及脚本摘要。CheckOnly通过。
- 当前固定022制品及manifest未重写。运行任务在PowerShell7内直接调用制品内的既有`run-headless.ps1`，本次没有Detach。
- 仓库的`start-deployed.ps1`与`run-headless.ps1`要求7.2/Core；Detach明确使用当前已核验的`$PSHOME/pwsh.exe`。Windows系统5.1与全局SSH默认程序保持。

## 显示排列及持久性

使用DisplayConfig6.0.0在原admin会话1内提交位置，保留设备完整路径、主显示器、尺寸、信号时序、旋转和缩放。提交前验证配置，应用时使用SAVE_TO_DATABASE，不使用ALLOW_CHANGES。

所有显示维护均通过无触发器的隐藏临时任务执行；子进程加入独占Windows Job，提交内存上限512MiB、执行上限90秒，记录原句柄实际退出。单次读回约1.2–1.8秒，位置维护约2.6秒；一分钟观察的峰值提交内存约61.7MiB。临时任务随步骤结束注销。

13:07:52 UTC首次提交后的即时读回通过，13:12独立验证发现位置被覆盖，期间4K尺寸保持。配置Timestamp为13:08:21.361 UTC；相关Windows事件及现有远程软件记录未提供调用者证据。该次结果不计为持久性通过。

重新读取活动及持久配置后，13:27:40 UTC单次提交目标位置并读回持久配置；随后一分钟12次采样均保持目标。13:31独立进程复查通过，13:35 CV启动后的活动、持久配置及四路输入再次通过。没有终止远程软件、修改驱动/EDID或添加后台自动重设机制。先前覆盖的调用者仍未确认，本次有限观察不构成长时间稳定性证明。

## 运行组与网页

先核对原AudioWorker和MediaMTX的映像、会话及原生启动时间，经固定Supervisor清理其残余；原生句柄等待确认退出。此次清理涉及重新附加进程，不将其记为原组正常协作停机。原运行状态、数据库及日志保存在本轮冷备目录；原媒体文件未改动。

网页Node及原5.1包装器通过原生句柄核验路径、会话和完整启动ticks后停止。随后正常调用新解释器的正式`start-deployed.ps1`，13:34:12 UTC返回0；后端与网页任务均为Running，两个实际包装器均为pwsh7。

| 成员 | PID |
| --- | --- |
| ControlHost | 19160 |
| Supervisor | 8656 |
| PlayerWorker窗口1 / 窗口2 | 16440 / 8932 |
| AudioWorker | 18856 |
| MediaMTX | 12960 |
| Node | 8312 |

七个成员均在会话1，映像属于当前固定制品；`runtime-processes.json`的四个子成员与原生启动ticks一致。两窗在线、idle，没有pending/cleanup/error；背景音频stopped且错误为空。窗口1的历史actual_source_id仍为144、当前source_id为空，本轮没有播放媒体，不能据此认定该文稿正在播放。

再次调用正式入口返回0，七个进程身份全部保持。跨机正常证书校验下，`http://10.88.50.194/`返回307至HTTPS，HTTPS网页和CSRF端点均200；正常认证查询及注销通过。未发送墙面、电视、电源、云台或现场音量控制。

13:38:50 UTC再次查询时，七个进程身份、显示位置及两窗状态保持；预览租约与上游均为0。Host标准错误文件为空，本轮临时显示任务为0。SSH维护连接随后正常关闭，CV继续运行。

## 本机验证

实际重新构建并执行：

```powershell
dotnet test runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScpCv.ControlHost.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~DeployedStartupScriptTests|(FullyQualifiedName~HeadlessScriptSecurityTests&FullyQualifiedName!~ExplicitAllowedHostsAcceptsDirectIpHostHeader)" --logger "console;verbosity=normal"
uv run --no-project .specify/scripts/python/validate_specs.py --specs-dir specs
git diff --check
```

15/15测试通过，覆盖真实PowerShell7解析、缺少配置/数据目录、旧5.1在执行业务前拒绝，以及入口合同。未运行Simulation用例。本轮不涉及前端或业务二进制修改，未重新执行全量媒体、视觉或内存长时间测试。

另在本机实际验证新`run-headless.ps1 -Detach`：PowerShell7.6.7、独立DataRoot、Hardware模式及回环随机端口，`StartWorkers=false`。21:43:31–21:43:43（UTC+8）计划任务结果0，真实`/health/ready`为200；没有Worker或物理控制写入。随后正式`-Stop`返回成功，原Host句柄确认退出码-1，与该入口的`Stop-Process -Force`语义一致。自有任务、进程及端口全部清理，执行策略RemoteSigned保持。证据为`.validation/pwsh-display-024/detach-hardware-result-02.json`；首次过早探测失败及对应精确清理记录分别保存，不计为通过。

## 证据保存

原始文件位于本机及D4的`.validation/pwsh-display-024/`，保持忽略：`installation.json`、`entries-updated.json`、`display-baseline.json`、`display-persistence.json`、`display-stored-Reapply.json`、`display-reapply-observation.json`、`display-verifyfinal.json`、`display-runtimeverify.json`、`runtime-verification.json`、`cv-finish-result.json`、`frontend-original-exits.json`和`final-cleanup.json`。各步骤`job-*`及`step-*`记录实际退出和临时任务清理；`backup/`保存原入口，`before-cv-start/`保存本轮冷备。

D4旧Windows10版本已结束官方支持；上述兼容性结论仅覆盖本次实际执行路径。显示回退根因和普通网页资源缓存调查继续独立记录，未在本轮宣称修复。
