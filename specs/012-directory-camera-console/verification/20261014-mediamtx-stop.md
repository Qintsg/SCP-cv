# T040 MediaMTX协作退出（2026-10-14）

原012h完整停机失败保留于20261014-activation.md：两个Player/Audio退出0、MediaMTX-1、Supervisor1。根因是控制台服务CloseMainWindow不执行退出，旧五秒后Kill；严格最终退出码检查正确拒绝，不放宽检查。

## 实现与门禁

MediaMtxConsoleLifetime以小接口承接隐藏独立控制台创建和协作退出。原RuntimeLauncher及MediaMtxProcess均走同一实现；不脱离父job、不改默认声音。仅创建时Registry内存记录IsolatedConsole，SupervisorStateStore四字段/HTTP合同不变，CLI从状态文件附着不会获得创建证明。原Supervisor收到当前认证nonce后调用模块；同包短寿命助手核原PID/UTC时刻/会话/精确MediaMTX映像，Attach后只允许原目标与自有助手两个控制台成员，再发送BREAK并确认原句柄退出0。助手失败/超时、未知身份、额外成员均不被当成功；最终Supervisor仍拒绝任何非零成员退出。

[Windows控制信号](https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent)的零组ID会作用于当前控制台所有成员，因此只能在已核独立范围内执行；[独立控制台创建](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags)使用正常隐藏启动，不使用breakaway或权限绕过。MediaMTX v1.17.1[官方源码](https://github.com/bluenviron/mediamtx/blob/v1.17.1/internal/core/core.go)收到Interrupt后清理资源。文档依据不代替实际退出证明。

## 回归与隔离D4

本机原MediaMTX公共Start/ShutdownCoordinator测试先失败Expected0/Actual-1，再真实正常0；构建检出StringBuilder P/Invoke及取消令牌分析器问题，改可写UTF16缓冲区与明确不传播取消的助手清理预算，未禁用分析器。相同真实进程验证旧启动时刻、错误PID/会话/角色、缺原创建证明、已取消请求拒绝，目标仍活；测试专用第三控制台成员加入后，助手不发信号，两目标仍存活；移除自有第三夹具后原MediaMTX正常0。副作用均只及自有夹具，测试主机不进入运行时包。

首轮完整六套1600通过（Windows259），随后补提前取消单例，针对性8项通过；最终六套结果另核，不将已运行的1600说成包含后来用例。前端未改，未重新执行476项；在线NuGet审计仍未完成，隔离包使用缓存locked依赖。

最终六套1601/1601、0跳过实际执行完成：Contracts345、Host466、Domain41、Infrastructure206、Integration283、Windows260，TRX位于`.validation/mediamtx-stop/final-tests/`。八个新增真实进程用例均包括，分析器不禁用；Spec Kit/diff通过，改动代码未超过500行。r51包在补提前取消门禁前生成，仅证明所执行正常组矩阵，正式包会从最终签名提交重新生成。

工作区隔离r51包tar SHA256`CC399CC985AB908F97DBD95E7F44A60ADED57B04858FE32706A0E08B0933B3BE`，Supervisor SHA256`1DDDAEDECEBC14ED7CEC527176A654E5A5524EF2EC71C5CA76AD9808ECB66222`。D4 r52独立Hardware Host/数据库/正常Cookie+CSRF REST/队列/认证IPC，MediaMTX为同摘要副本，所有监听关闭的独立配置，不占正式8554/8890/9997；仅原两块输出目标。

两轮restart202/shutdown200共10个原句柄全部退出0：首轮Player30632/20904、Audio25216、MediaMTX26536、Supervisor28692；次轮Player25856/22892、Audio18728、MediaMTX27344、Supervisor29728。每轮状态文件删除、精确自有Host在全部原成员正常退出后结束，端口28153释放；任务最终0、passed=true。旧身份/额外成员是在本机真实进程另验，不能说D4同矩阵也执行了这些负例。正常空闲组结果不代替所有媒体动态关闭。

正式七原PID26384/25396/23604/23668/17084/3976/23616未改；隔离任务只有Ready终态及自有映像/端口残留0后移除，失败/数据目录保留。固定提交重新发布和正式配置的完整停机/恢复尚未验，T040暂不勾完成。

隔离归档SHA256`E2B7A68B7FC945863D1904D0E46FF564D6C33D185CE4ADFA0623B24AFB1B5514`已下载本机；保留result/progress与日志，既有失败记录不删除。

## 012i正式换包与复测

固定签名提交`d67ce53db8427d35523159798a978a9bfd9819f5`已推送两仓库，D4源码快进且工作区干净。完整五组件包独立发布到`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261014-012i-d67ce53`，967文件逐SHA一致；Net48隔离与323个VLC插件保持。归档SHA256`73816F334AD5AD90D05456D51904B6D9CC12800FB46D88CF8D35C6F0909AAA44`，Host`63B07694AE28BD00DE5243345F12167B14B7249AEB93B8230B07FBFFB47F2D6C`、Contracts`522EEA753DC9877346AC30F0FE131C82288969E83D86C29E0B4E54B2BBB69C4F`、Supervisor`60AFE0FA0C1AB75E31508771595C424B74417E00936F28FC03518315AD050235`。

预检原两窗/背景空闲、精确原六身份及网页后，用旧协议停止旧组。旧Player/Audio正常0、MediaMTX-1/Supervisor1与旧缺口一致，全部原句柄退出及状态文件删除后才精确停止旧Host26384；旧Host结束-1不冒称自然退出。无写入者后冷备DB/WAL/SHM至`.validation/deploy-012i/backup-before-012i`，大小3231744/4169472/32768字节，源/备份逐摘要相同。原件、媒体、证书及失败材料未删除。

仅禁用旧运行任务并注册正常RemoteSigned/NoProfile/File的`ScpCvRuntime-012i-20261014`，未使用Bypass；原`ScpCvFrontend-012h-20261014`与Node23616保持。新Host19272首次创建完整组后执行一次正式认证shutdown200：Player18792/21484、Audio23868、MediaMTX15336及Supervisor26172五原句柄均真实退出0，状态文件删除，passed=true；随后才明确restart202恢复，不根据API200推断退出。

恢复后Host19272、Supervisor26756、Player30188/27492、Audio28424、MediaMTX26196均会话1，映像路径/四成员状态文件PID与启动时刻和OS一致。两个Player实际参数及公开target_display_label仍DISPLAY2/3，在线idle，无源/共享/错误/待执行/清理残留；闲置重启的旧观察代次不冒充新播放回执。原11源ID1/2/53/83–88/90/91与保护标志保留，背景空列表stopped且错误为空。只读来源能力IP81/SCP-cv均true，不下发任何实体选源、滑杆、云台、墙面或电源控制。

系统三个默认音频角色和四端点总音量/静音前后完全相同；可信声音63、私有引擎与原独立profile根保留。网页摘要与Node23616原启动ticks`639273922920992980`不变，80/443监听仍归该Node；本机普通HTTP与不忽略TLS的HTTPS均200。新运行/原网页任务Running，旧运行任务Disabled。

证据归档`.validation/deploy-012i/activation-evidence.zip`只包含明确列举的八个JSON，不含数据库、私钥或口令文件；D4与下载本机SHA256均`C996B46EE25E98F46E7447694EAD46F599E14DCFFD880862A61E310E636BCE91`。T040完成，40项/29完成/11未完成；T033/T034正式媒体声音、实体路由/机位/DSP及T010清晰度/长稳仍分别追踪，不用完整空闲组成功代替。
