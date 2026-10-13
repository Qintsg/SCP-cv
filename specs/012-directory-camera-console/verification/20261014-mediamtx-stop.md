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
