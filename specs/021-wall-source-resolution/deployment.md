# 021双仓推送与D4部署（2026-10-21）

用户在本机验收后明确要求“推送并部署”。GitHub origin、内网GitLab均正常推送main；D4的`D:/SCP-cv`从既有内网origin以`pull --ff-only`更新，没有强推、重置工作区或修改远端。

运行包固定源码`ed904d659d48e815d0c9111983f52b653d7dbc44`（软件变更`fa18cd3`及发布授权文档），独立目录`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261021-021-fa18cd3`。最终记录另行同步，不重复构建或重启。

## 发布流程

- 原源码9960725干净，原020运行包七根均在admin交互会话1。窗口1当前确证播放网页源116，desired/observed同代；窗口2idle，虽有历史actual116但不恢复它。原背景音乐无来源但保留旧Supervisor退出的error投影，不把它当新故障或擅自清除。
- 先在原服务运行时生成完整新包：**969文件**逐SHA/数量核对。额外仅加载指定制品的Infrastructure、Domain和appsettings，在D4纯CPU生成30路已确认输入的全屏计划，验证裁剪完整及目的矩形独立，84未知不猜尺寸、四路D4为4K。未实例化TCP传输或访问业务库。
- 加载的Infrastructure SHA256为`3BC0E9AC4E47291F232A2C83A31C5876086711B70225C157213F3E45B252B72C`，与发布制品相等，不误验旧bin目录。
- 原Supervisor13748、Player3644/9776、Audio15924、MediaMTX16300协作退出，五个原句柄退出码全0。Host16956在映像/会话/启动ticks匹配后精确停止，退出码-1，不冒称协作0。原Node12760及网页任务不动。
- 项目写入进程退出后冷备`control.db`4222976字节、WAL4169472、SHM32768，各源/副本SHA一致；位于`.validation/deploy-021-20261021/backup-before-021`。旧包、旧任务XML和旧桌面入口保留，不覆盖用户库。
- 新任务`ScpCvRuntime-021-20261021`使用admin Interactive/Highest/IgnoreNew，原`ScpCvRuntime-020-20261021`Disabled；保留`ScpCvFrontend-020-20261021`Running。全部沿用正常RemoteSigned，不模拟桌面输入、修改分辨率或关闭安全机制。
- 桌面原`C:/Users/admin/Desktop/SCP-cv 一键启动.cmd`更新至021固定配置/脚本，保留正常UAC。CheckOnly和真实启动均通过；没有自动点击UAC。
- 停机API按原语义关闭当前来源，启动后从本轮当前快照仅发一次窗口1 OPEN116；实际source_id/actual_source_id及desired/observed均为116/1135，窗口2仍idle。不是从旧历史actual_source_id恢复。

## 运行与验证

| 成员 | PID | 归属 |
| --- | ---: | --- |
| 网页Node | 12760 | 原D:/nodejs/node.exe，启动ticks不变 |
| ControlHost | 3684 | 新021包，会话1 |
| Supervisor | 14496 | 新021包，会话1 |
| 两个Player | 18052 / 11316 | 新021包，仍仅两播放器 |
| AudioWorker | 16264 | 新021包，会话1 |
| MediaMTX | 12268 | 原工具路径，会话1 |

双窗在线，无pending/cleanup/error，原音量/静音/循环/显示绑定保持。窗口1playing116、窗口2idle；原10媒体ID/名称/路径/URI/目录摘要一致。背景音乐的来源空、音量70/非静音/循环配置不变；其旧error投影仍保留，不宣称全部音频现场已验。

运行组状态文件四成员与OS的PID/会话/启动ticks全部匹配。Windows DISPLAY2–5仍3840×2160，五显示器的分辨率/位置完全保持，没有新增原生3/4窗口。

大屏与电视路由记录和时间戳均保持：墙上次sent_unconfirmed及代码1IHdoxVLz，电视两路unknown。部署不重新应用墙面/预案，不将历史写出记录当画面确认。预览租约/上游均0，MediaMTX路径0。

三种默认Render角色及四个活动端点的总音量/静音前后严格相等；证书、FFmpeg私有路径、声音引擎和软件输出绑定沿用020。

本机经正常TLS跨网络检查原**86个网页文件**全部200且逐SHA一致，HTTP80保持307并保留路径/查询跳至HTTPS443，HTTPS代理API200；没有忽略证书或改信任。网页无变更，Node不重启。再次执行桌面同核心启动脚本后七根PID/启动ticks完全不变。

## 验证与未验边界

本机基线：完整后端1793/1793、Node工具14/14、Release构建0警告/错误、Spec Kit通过；见[本机记录](verification.md)。D4新增包级CPU、部署身份/数据/正常TLS及重复启动验证通过，不据部署宣称实体墙面、电视画面、摄像机机械动作、音量可听或所有源解码通过。

本轮没有显式墙/电视映射、电源、云台或音量请求，仅正常协作停组/启动和上述一次当前网页恢复。原生播放恢复为API完成/代次证据，不等于截图确认页面内容。没有重跑D4全部测试或改变原有现场欠账。

本机第一次普通GitLab推送返回403；同一远端在单命令`git -c http.proxy=`直连后正常推送，未改全局代理、TLS或仓库远端。GitHub提示地址已移动但正常推送。所有失败和成功输出保留，不据失败重试强推。

双方证据目录`.validation/deploy-021-20261021`包含before、969文件manifest、package-cpu、stop-backup、startup、restored-current、audio前后、verification、repeat-start；本机另有正常TLS86文件摘要。数据库/口令/私钥/日志不入Git。恢复旧版本前须核对部署后新数据，不自动覆盖当前库，也不并开新旧任务。
