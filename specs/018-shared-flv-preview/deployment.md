# 018 D4部署记录（2026-10-20）

用户授权双仓推送、D4部署及桌面一键启动。当前部署进行中；此处在验收前不宣称完成。

## 发布前状态

- 本机工作区原干净，main较两个远端共同的b71a379领先8提交，包含015–018；D4源码也为b71a379，工作区干净。
- D4交互用户admin会话1，正常RemoteSigned；仅原Node23000及012l Host27544存活，两窗都离线、source_id为空，不恢复旧actual_source_id116。目标DISPLAY2/3保持，不修改Windows分辨率。
- 原10媒体引用、两窗及背景配置、系统音量、任务身份与旧网页摘要冻结在D4忽略目录`.validation/deploy-018-20261020/before.json`。实际冷备须在原Host退出后进行。
- 后摄像机意图stop_pending，不是held；只读TCP连接84:8889未连通，未发送新控制载荷。保留旧状态证据，不将新Host内存重置误称摄像机已物理停止；摄像机现场核验继续按用户要求暂挂。
- CERNET的辅助工具索引跳转南大下载超时，临时uv工具用独立`--no-config`阿里云索引取得Paramiko4.0.0；不改仓库配置、不加Python运行时服务。SSH严格使用已有known_hosts，口令仅终端输入、不落盘。
- 内网GitLab正常直连并用Windows证书链验证，未关闭TLS或改known_hosts。两个既有远端不重命名。

## 一键启动验证

新增脚本6项合同／缺配置真实PowerShell测试通过，配置缺失实际退出1；无执行策略／证书绕过，无电源／墙面／音量／摄像机API。实际D4重复启动及服务验证在部署后补记。

## 服务端预览依赖

使用本机已验证的Gyan FFmpeg9.0.2独立exe及LICENSE，复制到本次D4部署私有目录，不改PATH或全局程序。exe SHA256：`589E50B766D251AFDF181DD664D40BD94407E200B019989FD7468C7D118A28D0`；传输后核同摘要且D4实际version成功，含libx264。来源为Scoop既有清单指向的[Gyan9.0.2发布](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2)，不是声称本轮重新下载验证整个归档。服务器依赖不要求观看端安装。

不自动配置系统QoS或新增预览公网端口，仍复用原MediaMTX。
