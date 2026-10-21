# 020双仓推送与D4部署（2026-10-21）

用户本轮明确要求“推送并部署”。已推送GitHub origin与内网GitLab，D4在`D:/SCP-cv`从既有内网origin正常`pull --ff-only`更新。未重命名远端、强推或重置工作区；GitHub仅提示仓库已移动，仍正常推送。

运行包固定软件提交`6c528ad90ac8091912ed2e431f90c318470d01eb`；部署发现的Vite代理预检修正为`92769c0d0ee7d25bfb975995d99e01ed1aa502f0`，正式网页与Node配置均采用该修正。最终部署文档提交另行同步，不重复构建或重启。

入口仍为[HTTPS443](https://192.168.5.194/)，HTTP80保持307跳转。同一D4 admin交互会话1、正常RemoteSigned；不使用computer use或模拟鼠标键盘，不改Windows分辨率、系统信任、设备电源或墙面映射。

## 部署、备份与启动

- 原D4源码309ef98干净，旧018七根服务正常；两窗都online/idle/source_id空，背景音乐stopped，保留10个媒体引用。不恢复历史actual_source_id116，不新增OPEN或CLOSE。
- 新包先独立构建：`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261021-020-6c528ad`，**969文件**逐SHA和数量核验后才停原服务；没有覆盖旧包或用户资料。
- 原Supervisor、两Player、Audio、MediaMTX五个原句柄协作退出码全部0。Host11416、Node24844在完整映像／会话／启动ticks匹配后定向停止，实际退出码-1，不冒称协作0。未按进程名清理用户应用。
- 冷备位于`.validation/deploy-020-20261021/backup-before-020`：control.db 4206592字节、WAL4169472、SHM32768，源／副本摘要均一致。原完整网页、两个旧任务XML和旧桌面入口保留；原018任务Disabled，旧012及018包未删。
- 新任务`ScpCvRuntime-020-20261021`、`ScpCvFrontend-020-20261021`均Running、admin Interactive／Highest／IgnoreNew。桌面原`C:/Users/admin/Desktop/SCP-cv 一键启动.cmd`已更新至020固定脚本／配置，旧文件先备份。没有模拟UAC点击；同核心脚本的真实启动与重复启动已验证，七根PID和启动ticks保持。
- 服务端FFmpeg继续使用原018私有路径、原摘要589E50…28D0，不改PATH、不复制客户端媒体运行时到服务器。原私有声音引擎、显式63号输出绑定、DISPLAY2/3与证书配置保持。

## 实际服务与数据核对

| 成员 | 最终PID | 映像／归属 |
| --- | ---: | --- |
| 网页Node | 26312 | D:/nodejs/node.exe，020网页登录任务，会话1 |
| ControlHost | 11580 | 新020完整包，会话1 |
| Supervisor | 22024 | 新020完整包，会话1 |
| Player1／Player2 | 11776／31576 | 新020完整包，DISPLAY2／3 |
| AudioWorker | 13256 | 新020完整包，会话1 |
| MediaMTX | 20248 | 原工具路径，单份，会话1 |

运行组状态文件四成员的PID／启动ticks／会话与OS完全一致，恰两播放器在线且idle，无pending／cleanup／error。窗口音量、静音、循环、显示目标与原值一致；actual_source_id116仍为原落后代次历史观测，不是当前播出证明。

原10媒体的ID／名称／路径／URI摘要／目录引用均保持；新受保护`/工作站`已建，31路输入中24工作站51–74、65/66别名正确、75不存在、监控76正确。仅GET缓存，所有发现状态unknown，没有为了部署扫描所有设备或拉取所有流。

大屏与两电视路由初态均unknown／last_sent空，未根据旧布局伪造实体状态或自动补发映射。新预览租约／上游均0，MediaMTX路径0。三种默认Render角色和四个活动端点实际总音量／静音前后完全相同；背景及系统音量投影保持。

原后摄像机stop_pending为历史内存意图；重启后的新控制器状态不证明机械已停止或设备可达，本轮未执行云台测试。

## Electron实际入口配置及代理修复

D4启动配置精确加入`app://scp-cv`，HTTPS使用HttpOnly／Secure／SameSite=None会话Cookie；HTTP独立协议Cookie仍由既有ProtocolCookieManager保持同站保护。不是通配CORS，也没有关闭TLS、sandbox或webSecurity。

首次真实预检发现：直接18443后端的OPTIONS正确允许app来源与凭据，而443网页代理提前返回无Allow-Origin／Credentials的204。登录POST可成功，不代表renderer能先过预检。根因为Vite默认cors先于proxy结束OPTIONS。

`92769c0`把dev／preview自身cors设false，将API策略唯一交给ControlHost。两模式实际随机回环测试修前2项失败、修后2项通过；同组入口／TLS43项、前端全量**1146/1146**、类型／Web构建通过，不删除原断言。之前本机.NET1766完整基线保持，本次D4实际完整发布构建成功，没有声称在D4重跑所有测试。

修正只重启新网页Node10552→26312；Host及五个后端运行组成员共六根身份保持。第二份完整旧网页及manifest在`web-cors-92769c0/frontend-before`保留。最终新网页**86文件**，另保留215旧哈希资源。index SHA256=`E53702D987389539B8E83B5ABC109DA6708B62F8A1CC25E3AFDE3D53FF4A34D7`；网页ZIP SHA256=`BBD6D4DC3C6BE9A81FE029B55125D328923B337041B6A0C684965B528355D33C`。

通过正常证书链实测D4 HTTPS：app来源预检204且精确Origin／Allow-Credentials，登录200、Secure/HttpOnly/None，原生描述符200/no-store；窗口3与IP65共享身份相同、低清chn1。请求只解析描述符，不消费媒体，服务器租约／上游仍0。未将原始URI、票据或口令写入验证记录。

## 网页与桌面验收

本机跨网络正常TLS读取全部86发布文件，状态200与逐项摘要一致；HTTP307保留路径／查询，HTTPS API代理200。1440×900与390×844实际Chromium登录页只读验收通过，0错误／横向溢出、0业务写入、0输入事件，不忽略HTTPS错误。正式控制页面本机18组几何验证见[本机记录](verification-preview.md)，不把登录页截图当全部现场交互通过。

重复执行桌面同核心启动脚本后七根身份不变，未重复开运行组。两台实体电视实际画面、摄像机机械位置、所有设备SPS/4K解码、真实D4媒体/WSS出画、长期弱网及系统级QoS未在部署轮测试；原012现场与真人手势欠账不因此关闭。Electron客户端本机目录包的原生验证仍见[Native记录](verification-native.md)，未在D4自动安装／打开客户端或公开发布安装器。

## 证据与回退

双方忽略目录`.validation/deploy-020-20261021`保留before、制品清单、冷备摘要、stop-backup、activation、verification-post-cors、native-origin、声卡前后、重复启动、HTTP摘要和截图。本机已独立下载并解析关键记录；数据库、凭据、私钥、日志不进Git。

两项包装器失误保留证据：文本EncodedCommand前缀破坏param位置，改为正常-File／独立脚本调用；CheckOnly的return未设置父作用域LASTEXITCODE，外层误判，改核实际powershell.exe进程退出码，不改生产启动脚本或降低门槛。均未导致误停或伪报成功。

回退不能直接覆盖旧数据库：先停止新组、核对部署后新增数据及源引用，再按保留包／任务／网页制定操作。禁止仅启用旧任务与新任务并跑，禁止把旧冷备自动覆盖当前用户库。
