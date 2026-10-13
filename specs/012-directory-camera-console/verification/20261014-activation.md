# 012h常驻激活与完整停机回归（2026-10-14）

## 交付范围与状态

代码固定到三个有效GPG签名提交f3fe1d4、7b50437、d99a851，GitHub/GitLab及D4源码均已核d99a851。重新从该干净提交发布，不将先前工作区r46隔离包当作正式发布。包ProductVersion为1.0.0+d99a851f78af2e83bee36638c963a6755c180cbf；没有应用代码改动，沿用上一轮非Physical1593及前端476、类型/构建证据。本轮验证包、激活与实际失败，不把历史测试称本轮重跑。在线NuGet审计仍未完成，缓存locked依赖不等于审计通过。

正式包967文件，归档SHA256`0E7C0C528D555898694BA324A190637F136CA7440C41980D8B9931C44D50DB96`，Host SHA256`5469D4E56B70401B64C8336D268E1AA868BC30CB62AC715295FDF2BA97C18757`，Contracts SHA256`620C75CE67E5A07233CD4DB0D23821EF49B67EABEA7F9E7D21DDA640EA6ADDD8`。网页index SHA256`BB4CCB0FBFD149C0F3175486D9803DA2B464156F21C46DBA7CDDF3A5988D3B2B`，完整归档SHA256`005E3AD2E6552450C8E5E53B6A93A59756C56521BE4890A7044C576424E83AD0`；全部967/9文件逐摘要检查。

最初网页归档仅包含index/assets，漏掉unsupported-webview.html；逐文件校验在任何停机前拒绝。修正完整归档并保存原失败目录/包，不跳过缺失检查。旧网页启动器仍硬编码4A22023...初期摘要，与已部署3A2813...不符，重启会拒绝；新启动器改核本轮manifest，支持精确现有Node接管或无占用时启动，严格TLS保持。

## 停机、数据与常驻

旧六原句柄确证退出；两Player/Audio为0，旧Supervisor已知停机问题返回1，MediaMTX强制-1，旧Host精确结束-1，均如实保留，不称正常整组退出。无运行时写入者后冷备原control.db/WAL/SHM到`D:/SCP-cv/.validation/deploy-012h/backup-before-012h`，逐文件SHA一致：3227648/4169472/32768字节。原网页精确移动到同stage的frontend-dist-before-012h，新全部静态文件换入，Node23616及其原启动时刻保持；媒体/原件/证书未移动或删除。

两项新持久任务`ScpCvRuntime-012h-20261014`/`ScpCvFrontend-012h-20261014`使用正常NoProfile/Hidden/File及RemoteSigned，不使用Bypass、不改系统策略；admin交互登录触发、无执行时限、忽略重复实例。旧Runtime/Frontend禁用，未终止旧网页Node。新网页任务精确接管23616且保持Running；新运行组也保持Running。

可信声音环境提供63的完整RenderID、`D:/SCP-cv/runtime-dotnet/private-audio-engines-20261013`及原DataRoot下独立webview-profiles-012h。系统Console/Multimedia/Communications默认端点、四个端点总音量和静音前后相同；这里只读，不设置默认/总音量。正式GET两来源能力均true，页面二选一均可用但未配置值保持，未下发实体选源/滑杆/云台/电源/墙面帧。

## 实际完整组失败与恢复

首次Host26384、Supervisor28780、Player27480/26468、Audio27992、MediaMTX23896均会话1，新包归属与四成员状态文件核对。目标仍DISPLAY2/3，两窗在线idle，原11源、空背景列表保持；仅这些状态不能证明现场清晰度/可听。

正式认证API空闲组shutdown受理200，原句柄证明两Player/Audio正常0，MediaMTX-1、Supervisor1，passed=false；四成员及Supervisor均确证退出、状态文件消失，之后仅调用一次restart202恢复。当前Host仍26384，恢复后Supervisor25396、Player23604/23668、Audio17084、MediaMTX3976，均会话1，Node23616保持。两任务Running、两窗idle/online/pending空，背景stopped/error空，系统声音基线仍一致。任何状态文件缺失或API200均不代替正常退出证明。

根因范围明确：RuntimeLauncher将MediaMTX列入完整owned；ShutdownCoordinator只CloseMainWindow，控制台服务不退出，五秒后Kill导致-1；严格Supervisor最终检查因此正确拒绝。新nonce修复解决三Worker正常退出误判，没有提供MediaMTX的协作信号。不能通过接纳非零退出或删掉MediaMTX监控消除问题。T040/HIGH partial已追加，T033仍未勾。

后续候选需先隔离证明定向控制信号与退出0。实际MediaMTX v1.17.1[官方源码](https://github.com/bluenviron/mediamtx/blob/v1.17.1/internal/core/core.go)响应os.Interrupt并清理资源；[Windows控制信号](https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent)的CTRL+C不能按非零组ID限定，必须避免向共享控制台广播。[创建标志](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags)和[Go Windows信号处理](https://go.dev/src/runtime/signal_windows.go)仅是候选设计依据，不是已实施/验证证明。

## 前端及剩余边界

真实HTTP/HTTPS普通登录与摄像机页通过；HTTPS安全上下文，单独未放宽TLS的curl HTTPS200。HTTP静态兼容页200。手机390px来源卡标签/应用均44px，页面无横向溢出；两选项可用、未配置不发包、最后请求不是读回。HTTPS新导航控制台0错误；首次HTTP页面在部署启动期间两项网络错误保留，收尾注销时并发认证读的401也保留，不声称整轮零错误。两协议自有会话正常注销200，仅自建页关闭。

私有证据位于`.validation/deploy-012h`的manifest、activation-preflight、stop-backup-result、frontend-start、audio-before/after、full-stop-result、activation-verification及原日志/失败归档。只有本机测试口令副本临时用于正常登录，结束精确删除，不删除D4原凭据。

远端证据归档SHA256`15FB3381C4FE8FDCF1CA402E183B7BD2A6DD73D657E020BAD95D99C0BB167788`，不含数据库/媒体/证书私钥/口令原件。

T040正常完整退出、正式声音实际播放/实体来源、机械/DSP及T010清晰度/人工选择/更长混合测试仍未完成；当前40项、28完成、12未完成。常驻已激活不是全部验收通过。
