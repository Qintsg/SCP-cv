# 完整接口、两窗状态矩阵与断管修复

日期：2026-10-05。对应T073/T074；完整任务仍未验收。

## 实际接口与文件系统

测试启动完整发布`ScpCv.ControlHost.exe`，不是替代路由的小Kestrel。新DataRoot、随机仅内存口令、正式认证/CSRF；探针不引用内部生产类型或直接写库。
本机和D4分别执行图片/PDF/视频上传至`media/中文媒体QA/子目录/sample.*`，图片移到根目录、父目录改名后核文件SHA与下载SHA，全部可独占读取。路径没有日期层级，原素材未改。
三类RTSP/SRT/custom源与网页经真实登记/编辑接口成功，流尚未连接，登记成功不证明出画。

本机成功Host PID71100/start UTC2026-10-04T20:54:17.7969072Z，退出0；记录根`api-run-ac804c05a646471587b21ad4a2e7872a`。
D4首API根`api-run-9dbaaa7d721b4bb7a8a83f6692731984`，Host14112正常0，任务结果0。
QA首编译nint/null及父进程AllocConsole error5发生于业务启动前；两次子CreateNoWindow/信号继承错误使正常退出未确认，精确自有fallback记录保留。改用新独占控制台、父委托处理器及子继承控制台后正常退出，未改生产生命周期。
SSH后台子进程未形成结果，改用已验证的InteractiveToken任务；不得把无日志视为业务通过。打包暂存曾复制本机QA运行根，上传前将副本移出并保留本机，未上传其测试库/认证数据。

## 两窗组与两个实际缺陷

真实Hardware ControlHost→Supervisor→PlayerWorker1/2+AudioWorker，仅DISPLAY2/3；不启动MediaMTX，不发送墙面、电源或系统音量命令。
新组生成实际`runtime-processes.json`，角色/PID/start/session读回；另一窗保持图片，窗口1执行三轮PDF打开/第2页、视频打开/实际时钟变大、暂停及关闭，窗口2最后关闭。

- 首探针将翻页错误发至control而非navigate。非法动作本应400，实际Kestrel500；服务同步校验抛出发生在Task错误包装外。公开HTTP回归真红→调用前捕获PlaybackServiceException，400/playback_error且原会话保持绿；只修该错误边界，不增加next为合法control动作。
- 900ms两次GET处于同一秒采样内，视频时钟未变是QA误判。改为既有15秒范围内等待实际position大于前值，不改生产采样或扩大超时。
- 全套集成捕获丢音频结束ACK时Flush遇reader已释放流，ObjectDisposed没有进入既有IOException重连。4096轮真实对端断开回归复现同类异常，发送段包装为IOException后相关12项绿，保留同message_id/event_id和停止闩锁，不新增播放重放。

## 最终同版包与D4复验

正式脚本新包`.validation/008-full-runtime-final-1791148666778`包含完整组件；与上一包比对，只有Contracts和ControlHost DLL/PDB变化。
最终远端新根`D:/SCP-cv/.validation/008-full-group-final-1791148830000`从原包+摘要校验补丁构成，未覆盖之前失败根或用户库。

| 文件 | SHA256 |
| --- | --- |
| ScpCv.Contracts.dll | 63304E44484354E1AB2999621AE5B41190630CC1ADDA7C4BFF6415FB38304BC3 |
| ScpCv.ControlHost.dll | 5C1610030E7EE075E98C7F60C067BBC5A4CEF3E6CAAC9E6C73352E09A3FB40DF |
| LibreOfficeHost.exe | 404B7D8BCD9DA0B6EE99F57ADCC7A8E652166A40251F8E17C073E8E645823545 |

最终Host18308/start UTC2026-10-04T21:22:59.8681762Z/session1/loopback56397；player1=16648、player2=3784、audio=11388，三轮均通过实际状态取证。
shutdown200，Host正常0；最终任务Ready/instances0/result0，连同四个已终结任务精确移除，所有包/日志/媒体/数据库保留。
D4项目进程0、相关运行服务0，源码Git干净且仍未secondary推送/拉取。本轮上传QA包不是源码同步。

远端/本机证据摘要核对：前阶段`3E57801A62C19DAA3327557B0FF35DB5784DA1C97D58721E9C4C501448EE937E`；最终`009760551B6DD721278FAFA14A2EC37E08969118AAD9281EB4C67A06C2C61405`。
日志副本仅events/host-out/host-err，未导出认证表或数据库。

## 回归及剩余范围

最终Release构建0警告0错误；非Physical1137/1137、0跳过，六TRX：Domain41、Contracts322、Infrastructure127、ControlHost278、Integration242、Windows127。
首全套Integration240/241失败证据保留，不以重跑绿色掩盖。前端本轮未改，上一轮267不是本轮执行。
三轮只证明实际状态、源身份/页码/视频时钟与退出，不含截图，不能称物理画面或另一窗像素保持。真实PPT/网页播放、SSE与控制台完整联调、MediaMTX、直播失败恢复、UDP、10轮/100控制/60分钟及正式源码同步仍待验收。
