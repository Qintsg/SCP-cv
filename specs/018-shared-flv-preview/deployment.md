# 018 D4部署记录（2026-10-20）

用户授权双仓推送、D4部署及桌面一键启动后，已完成015–018完整发布。软件制品固定提交`643da4b16a582101f5ac98bef55578df097fb660`，GitHub、内网GitLab均已正常推送，D4从原内网origin正常`pull --ff-only`快进。最终文档记录提交另行同步，不重复构建或重启服务。

入口仍为[HTTPS443](https://192.168.5.194/)，[HTTP80](http://192.168.5.194/)正常307跳转并保留路径、查询。未使用computer use；浏览器只读无头，仅GET/HEAD，无模拟输入。

## 发布前状态

- 本机工作区原干净，main较两个远端共同的b71a379领先8提交，包含015–018；D4源码也为b71a379，工作区干净。
- D4交互用户admin会话1，正常RemoteSigned；仅原Node23000及012l Host27544存活，两窗都离线、source_id为空，不恢复旧actual_source_id116。目标DISPLAY2/3保持，不修改Windows分辨率。
- 原10媒体引用、两窗及背景配置、系统音量、任务身份与旧网页摘要冻结在D4忽略目录`.validation/deploy-018-20261020/before.json`。实际冷备须在原Host退出后进行。
- 后摄像机意图stop_pending，不是held；只读TCP连接84:8889未连通，未发送新控制载荷。保留旧状态证据，不将新Host内存重置误称摄像机已物理停止；摄像机现场核验继续按用户要求暂挂。
- CERNET的辅助工具索引跳转南大下载超时，临时uv工具用独立`--no-config`阿里云索引取得Paramiko4.0.0；不改仓库配置、不加Python运行时服务。SSH严格使用已有known_hosts，口令仅终端输入、不落盘。
- 内网GitLab正常直连并用Windows证书链验证，未关闭TLS或改known_hosts。两个既有远端不重命名。

## 一键启动验证

新增脚本6项合同／缺配置真实PowerShell测试通过，配置缺失实际退出1；无执行策略／证书绕过，无电源／墙面／音量／摄像机API。本轮ControlHost全量507/507通过；网页重新类型检查／构建成功。前轮前端1090与后端1685为既有完整基线，本轮未假称重新跑所有项目。

D4桌面文件为`C:/Users/admin/Desktop/SCP-cv 一键启动.cmd`，正常UAC启动固定部署脚本，不含密码；核心为仓库`runtime-dotnet/scripts/start-deployed.ps1`的本次固定副本及`startup.json`。已实际执行`-CheckOnly -NoPause`与首次启动，再重复执行一次；第二次前后七根PID、启动ticks、映像完全一致，未重启在线组。没有通过computer use点击桌面或UAC，不能据此声称验证过那一层人工点击；核心同一路径已真实运行。

两个新登录任务`ScpCvRuntime-018-20261020`、`ScpCvFrontend-018-20261020`均Running、Interactive admin／会话1、Highest、IgnoreNew；旧012l运行任务和012h网页任务已导出并Disabled。其余旧任务没有登录触发器，不为本次启动广泛删除它们。

## 服务端预览依赖

使用本机已验证的Gyan FFmpeg9.0.2独立exe及LICENSE，复制到本次D4部署私有目录，不改PATH或全局程序。exe SHA256：`589E50B766D251AFDF181DD664D40BD94407E200B019989FD7468C7D118A28D0`；传输后核同摘要且D4实际version成功，含libx264。来源为Scoop既有清单指向的[Gyan9.0.2发布](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2)，不是声称本轮重新下载验证整个归档。服务器依赖不要求观看端安装。

不自动配置系统QoS或新增预览公网端口，仍复用原MediaMTX。

## 实际发布、保留与验证

- 完整运行包：`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261020-018-643da4b`，969文件；先逐文件SHA256和数量核对才停机。首次SSH分离Start-Process返回后子进程被回收，0字节日志、原PID消失、没有发布目录；保持exec通道到实际构建退出后才取得完整成功证据，没有关闭NuGet审计或覆盖旧包。
- 原Host27544和Node23000已在固定句柄／启动时间／会话／映像核对后定向停止，实际退出码-1，不伪报协作退出0。原运行组部署前已经停止，没有仍在运行的Supervisor／Worker被强杀；正常shutdown确认后才停止Host和冷备。
- 冷备：`.validation/deploy-018-20261020/backup-before-018/`，control.db／WAL／SHM分别4202496／4169472／32768字节，源与副本摘要相同。旧网页完整保留于`frontend-dist-before/`；新网页82文件，130旧哈希资产附加保留，不删除原媒体、配置、证书或旧包。
- 新网页index SHA256：`1F48A72BCC1627209DE798B5CCBA1C8A7A43CE4C88891D20AD025E6560E3F0F1`；ZIP SHA256：`29997722B3D930FA128AE3E2E57046489959C3AC29BBA85A548EDF2988B081BB`。本机跨机器正常TLS读取全部82文件，200与摘要逐一一致；HTTP307、HTTPS API代理200通过，未放宽证书检查。
- 实际Chromium桌面1440和手机390登录页均通过，正常TLS、0未知错误／警告、0横向溢出、0业务写入、0输入事件；截图保留在本机忽略目录。

| 成员 | PID | 实际归属 |
| --- | ---: | --- |
| 网页Node | 24844 | 原D:/nodejs/node.exe，新018网页登录任务 |
| ControlHost | 11416 | 新018完整包，会话1 |
| Supervisor | 26052 | 新018完整包，会话1 |
| Player1／Player2 | 16472／19252 | 新018完整包，会话1，DISPLAY2／3 |
| AudioWorker | 29632 | 新018完整包，会话1 |
| MediaMTX | 2468 | 原工具路径，只一份，会话1 |

四成员runtime-processes.json的PID／启动时间／会话与OS完全匹配。两窗online、idle、source_id空、无pending／cleanup／error；未恢复旧网页源116。`actual_source_id=116`仍为原历史观测且observed_generation落后desired，不是当前播放确认；没有为消除该字段写库或发送额外OPEN／CLOSE。原10媒体引用摘要、窗口音量／静音／循环、背景配置保持，三个默认Render角色及四活动端点总音量／静音前后完全相同。

无人观看时lease_count=0、upstream_count=0，现有MediaMTX路径0；没有常驻FFmpeg拉全库。真实摄像机／63／64／81流、WSS实际解码、4K实体分辨率、NIC拥塞及全机QoS没有在本次部署轮测试。原012的实体、DSP和真人手势欠账不因此关闭。

## 证据与回退边界

双方忽略目录`.validation/deploy-018-20261020/`保留manifest、冷备摘要、before、activation-prepared、verification、repeat-start与audio-before／after；本机已独立取得并解析关键JSON，另有HTTP文件摘要和只读浏览器结果。数据库／Cookie／密码／私钥不提交Git。

旧012l包、旧任务XML、完整旧网页及冷备保留。回退前必须停止新运行组并核对部署后新增数据，不能自动把旧数据库覆盖到当前库，也不能仅重新启用旧任务而与新任务并跑。旧预案首次需要编辑保存一次生成YAML，保持016明确的迁移边界，不自动接管用户文件。
