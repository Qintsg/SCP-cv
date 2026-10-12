# D4 012部署记录（2026-10-12）

## 当前状态

完整运行包为`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261012-012e`，网页入口HTTP80/HTTPS443，内部ControlHost仍18444/18443。运行任务`ScpCvRuntime-012e-20261012`及网页任务`ScpCvFrontend-012-20261012`运行中；011、012b和012c旧运行任务停止并禁用，不删除历史包。功能提交`88dcdcd`、`79dd35b`、`e2d4b50`、`b998ec9`、接收统计修复`1e1c773`、长命令心跳`1e08f0c`及夹具屏障`11edf8e`已推送两仓库并在D4快进拉取。

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

## 未完成实体项

固定监控地址、云台端点/停止/位置、话筒通道、IP81/SCP-cv音频路由和软件输出端点绑定未闭环，相关入口禁用且API拒绝。12种组合的新包现场视觉、摄像机真实播放、实际可听声音与人工共享选择仍不能据自动化或旧010结果声称完成。
