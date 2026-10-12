# D4 012部署记录（2026-10-12）

## 当前状态

实际运行包仍为`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261012-012e`，网页入口HTTP80/HTTPS443，内部ControlHost仍18444/18443。运行任务`ScpCvRuntime-012e-20261012`及网页任务`ScpCvFrontend-012-20261012`运行中；011、012b和012c旧运行任务停止并禁用，不删除历史包。固定监控修正`d7c0487`与可选声音绑定/接线记录`b04f65b`已签名提交、推送GitHub与现场GitLab，并由D4快进拉取。012f新包文件已上传校验，但启动入口被执行策略拒绝，未切换运行版本；不能将源代码同步称为新服务生效。

运行包ControlHost摘要`BEA3F98AB8043F041D91CC584CB6846C7D9211535D18C22079825B88933C9854`，Contracts摘要`DED7A5C24070FA7A8F3FE44AFCD0601681FE6A1ADCA311E88B4322281BA14FAC`；网页index摘要`4A22023DF43085E57133F6E13C7B4D7110B1602ED839BE0F5BBCC661B1AE08D2`。完整012e包SHA256为`57EFE45645DF84EA914294C49A114C5689D2FD9BEFC26B3570CF81EBFA4AFB9A`，本机与远端一致。012d为被合并检查拒绝的本机部分产物，未部署。

## 已核对

- 原`t129-workstation`数据库在旧组停止后连同WAL/SHM备份到`.validation/deploy-012/backup/`；增量迁移保留原三源ID1/2/53及其URI，新增六路Camera，共九源。原件、缓存、证书没有删除或重排。
- 两个PlayerWorker在线，目标仍DISPLAY2/3，各1920×1080；DISPLAY1控制桌面及DISPLAY4/5不是播放目标。Supervisor/AudioWorker/MediaMTX与ControlHost属于新包/运行组。
- 本机直接访问HTTP/HTTPS发现页均200并公布443；正常证书链验证通过。普通Chromium不忽略证书也访问成功：HTTP非安全上下文且没有采集API，HTTPS安全上下文且有采集API。
- OS监听80/443为同一Node进程，旧5173/5174没有监听。新防火墙例外只允许D4两个现场地址、三个现场网段访问Node TCP80/443；原TCP阻止调整为`1–79,81–442,444–65535`，UDP及其它限制保留。
- 新服务实际查询155:4830音量100，发送同值一次后读回100且confirmed。未改变电脑系统音量，不把读回一致当现场扬声器可听。
- 012c替换前通过API协作停止Workers，验证退出后仅按旧包路径、DataRoot及精确PID停止ControlHost；冷备份当前数据库/WAL/SHM到`.validation/deploy-012/backup-012c/`。网页Node未重启、入口与证书不变。新Host健康检查正常，两窗在线空闲且仍绑定DISPLAY2/3。
- 012e按相同流程协作停止012c并确证退出，原数据库冷备份到`.validation/deploy-012/backup-012e/`后切入；新Host健康检查正常，完整五组件归属新包，网页仍为原Node进程。共享复测期间不提交、拉取或重启服务。
- 012e最终正常TLS真实双路共享312.15秒及三轮交错关闭/重开/旧ID停止通过，实际采集/发送/接收均1080P，停止1.28–2.65秒，零页面/网络/诊断请求错误；[共享记录](share-quality.md)保留此前失败及视觉/长稳边界。正式屏控根上传、真实图片打开/关闭及精确删除测试源89通过。
- 测后健康检查Healthy，两窗在线idle，actual_share_id为空、cleanup_pending为false；仍只有DISPLAY2/3为播放器目标。ControlHost3776、Supervisor23140、Player3204/18968、Audio18388、MediaMTX24280及Node23616未退出或换PID；两个012任务运行，80/443仍由Node23616监听，网页摘要一致。仅在本轮共享和上传测试全部结束后同步最终文档，不触发服务重启。

## 可回退资料

旧网页保存在`.validation/deploy-012/frontend-dist-011-backup`；旧包与旧任务保留。精确防火墙回退脚本和原规则备份位于私有部署目录，证书仍使用原私有目录。

回退前先协作停止并确证新组退出，再切旧网页/任务和精确规则。数据库备份含WAL，不可只覆盖单个control.db；新版本期间产生的数据应先单独备份，禁止直接丢弃后倒退数据库。回退不是本轮实际执行项目。

## 012f文件部署（尚未激活）

- 固定`b04f65bdcbb8b5cde194395fdee9bc9e0e8f9377`完成锁定还原、Release构建及完整五组件发布；未在构建途中改变Git HEAD。后端常规1477、前端458全部通过，详见[发布前门禁](test-results.md)。
- 新包已解压到独立目录`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261012-012f-b04f65b`。ZIP摘要`2571DD06176416D6A23C512B9B2C302A2EA4CF308B38C6697BE3E1D6EBCFA49B`，ControlHost摘要`BE2A9278E1F3B390A71B60EEE30E793F0DF4F20F73528D62BD5E4384B5477A6D`，Contracts摘要`B4DA8987A31EA2B8A49D2446B4E1E40AFEC471227ECB35F5E93A3E9976BBAE60`，本机与D4一致；四个顶层EXE、独立`libreoffice/ScpCv.LibreOfficeHost.exe`、323个VLC插件及索引齐全。
- 网页摘要与原012e一致，未覆盖网页或重启Node。传输清单及启动入口保存在`.validation/deploy-012f/`；临时18770文件传输服务已关闭，不上传私钥或生产账号。
- D4正常执行准备脚本返回`UnauthorizedAccess`。只读查询显示各执行策略作用域均Undefined，有效策略为Restricted；未修改策略、未使用Bypass、未改为读取脚本文本执行。新运行任务尚未注册，旧组未协作停机、未杀进程，也未对运行中的数据库做所谓冷备份。
- 阻挡发生后再次核对：原ControlHost3776、Supervisor23140、Player3204/18968、Audio18388、MediaMTX24280及Node23616仍运行于桌面会话1；健康Healthy。两窗仍DISPLAY2/3、online idle、代次一致、无源/共享身份和清理残留；本机严格TLS访问HTTP80/HTTPS443均200。
- 原九源保留，固定监控仍为ID88、受保护`/Camera/固定监控`、URI为空且不可用，说明76默认尚未被新服务同步。User/Machine软件输出绑定均未设置，实际音频绑定没有启用。
- 后续须由管理员通过正常流程允许项目启动脚本，之后才能执行旧组协作停止/退出核验、原数据库冷备份、新组启动及76源同步验收。T033/T034维持未完成，不能把本次文件部署标为运行切换成功。

## 未完成实体项

固定监控76地址、音频处理器/中控及话筒接线已经确认；固定源新默认尚未激活，云台控制端点/停止/位置、话筒控制协议、IP81/SCP-cv音频路由和完整软件输出绑定未闭环，未知控制仍禁用且API拒绝。12种组合的新包现场视觉、摄像机真实播放、实际可听声音与人工共享选择仍不能据自动化或旧010结果声称完成。
