# 正式Player承载基线、真实输出条件与最新包（2026-10-03）

## 当前结论

正式PlayerWindow/PlayerRuntimeHost→生产PresentationViewport→独占LibreOffice基线两轮完成：原版第六页、两步、采样、关闭、正常job退出及副本释放。
原样图首步仅标题、次步标题加字幕，无菜单/导航。原“约35px黑带”粗估后经原PNG像素核对撤回：两轮四图仅底部连续2行全黑，仍不能据此判完整viewport通过。
本机真实Screen枚举仅主DISPLAY1/2560×1440，没有两个非控制桌面输出；不以Simulation或同屏两窗冒称物理绑定验收。
最新五组件包真实生成且x64/TFM隔离、16只读依赖通过；MediaMTX外部路径未运行，整个App/管道/组/两输出与60分钟尚未验收。
T058有限完成，不改正式代码/几何，T004/T011/T044/T045/T046/T053等完整门禁仍未完成；D4未访问或启动。

## 实际Player基线

`.validation/008-native-player/`只引用正式Player源码输出，无PresentationFactory替换。窗口使用产品PlayerWindow/AssignBounds在(80,80)800×450小窗，实际DPI96；不是全屏、不是App启动或监督运行组。
只读Resources TestPPT原件的独立副本，正式OPEN租约source11、group/owner1、generation1、摘要、target6、fitfalse；原预算与侧车身份/父job/Office独占job保持。
运行通过公开Execute/Sample/CLOSE/Dispose，没有假引擎或业务数据库；1.5秒仅有限QA采样，不变成生产动画完成规则。

| capture根（`.validation/008-native-player/`） | parent/host | Office launcher/listener | 正常父退出 |
| --- | --- | --- | --- |
| capture-6e81a1cf4ffc47e69b40925d9b178ecb | 26396/60456 | 53968/58652，port2357 | exit0，11.85秒 |
| capture-467d869edea249c59b99479868b21bb5 | 27264/44404 | 68700/15060，port55537 | exit0，11.22秒 |

第二轮SCP_CV_LIBREOFFICE_HOST只在子执行环境明确指向最新包libreoffice目录，之后恢复。正式Player DLL开发输出与包中SHA一致：919AF0FD9292F5CAC7A2836FEABDEAE979F81D3ABE7BA581148178C550AA2B56。
它加强当前包内侧车的真实运行证据，不把源码引用的小窗说成整个发布App/两屏已运行。
两轮关闭actual源为空、idle、页码0、布局null，副本FileShare.None成功、原件/副本摘要保持；两个host job active0/members空，精确PID/监听消失。

图片在临时`SCP-cv-native-player-shots/2ccaf58f1e954bb9af7bc330e081faed`及`5e89f0d449dd471d9a63ae19dec443ec`，各first-effect.png/second-effect.png。
root逐张核四图，pptx技能明确要求的独立视觉agent核第一组两图：完整标题→标题/字幕，无文字重叠或可见文字裁切，无菜单/导航。初次工具图像中的底部约35px粗估经后续只读原PNG量化撤回，四图800×450均仅最底2行全宽A255/RGB0，原图SHA未改。
此更正不是产品几何修复，也不反向证明完整内部viewport或其它DPI/宽高；不沿用旧WinForms图的70px，更正与原图摘要见[视频与图片量化记录](native-video-close-20261003.md)。没有叠第四个几何修补，旧承载架构讨论仍待人类。

## 审计失败定位与安全恢复发布

第一次发布根`008-package-current-7de0d33228664886bd722d344337c06c`因ControlHost/Infrastructure NU1900失败，部分目录保留，不标可用。
当前普通.NET HttpClient/curl通过代理均能读取官方service index；直连曾302转nuget.azure.cn，不据此改DNS/hosts或来源。
独立官方data.nuget.org审计配置仍NU1900/1905；force/no-http-cache/disable-build-servers未改善，试验和旧错误保持，仓库源与全局缓存不改。
忽略QA诊断直接引用当前SDK10.0.400的NuGet客户端，取得内层NotSupportedException：代理scheme=socks5h不被支持，只有http/https/socks4/socks4a/socks5。
这是具体客户端不兼容证据，不将普通HTTP成功当NuGet就绪，也不把网络诊断失败当Office启动缺陷。
仅本轮构建进程的三个代理变量将协议前缀socks5h→socks5，地址/端口/凭据不改且不打印；原锁模式、漏洞审计、TLS验证、告警as-error均保持。
随后canonical NuGet.Config的locked/no-http-cache restore成功，正式发布脚本五组件成功，执行finally恢复原环境scheme=socks5h；没有永久修改机器代理/源、禁审计或放宽NU1900。
NuGet对代理和审计的官方说明：[代理配置](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file)、[独立审计端点](https://learn.microsoft.com/nuget/concepts/auditing-packages)；协议不兼容以本机SDK真实异常为证。

## 当前包核验

全新成功根`.validation/008-package-current-9ce5436f55854ec6b07e1c1e5c74dca7`，ControlHost/Supervisor/Player/Audio平铺及Framework Host独立libreoffice，五EXE PE均Amd64。
Net10协议SHA3AEED28A37C321D256CBACCE18AECA29CD48677AD15F3EB2E3C780006B45…，Net48协议163CD818592A088623594A159BE40B3C51B098C011DEE7964C605A42106C…，文件不同且正式只读probe协议/TFM检查通过。
16依赖installed/availabletrue，.NET10域未加载cli_程序集，检查不启动Office或要求运行机SDK；不是完整CLR/native loader、签名或播放证明。
包无退役PowerPointHost，也无mediamtx.exe；源码现有`tools/third_party/mediamtx/mediamtx.exe`及配置需通过既有MediaMtxPath明确提供，本轮没有启动或下载它。
只执行包中Supervisor status并指定自有不存在状态路径，返回[]且未创建文件；没有启动Player/Audio/MediaMTX或整个运行组。
QA项目位于构建约定外，最初WindowsAppSDK传递win10 RID触发NETSDK1083；补与生产相同win-x64列表及禁非WPF工具后可构建，不改生产依赖。SDKNuGet扩展缺using编译失败不算业务红。
QA旧assets的NU1900警告保留、未禁audit；正式发布的真实成功还原与它的旧警告分开。SDK引用只在忽略诊断中，不进入正式包。

## 收尾与剩余

自有小窗/Host/Office/Supervisor已退出，port2357/55537无监听，原件SHA保持；包、副本、profile、旧失败/dump保留。
正式源码本轮未改，全非Physical1002/前端177为上一轮，不冒称本轮重跑。Spec/diff/缓存Redocly复核；restore生成锁文件行尾机械恢复UTF8/LF，不改锁语义或用户其它改动。
没有提交、推送、部署或D4/墙面/电视电源/系统音量操作。旧包与数据未覆盖；发布脚本清掉的是经绝对路径验证的本次GUID临时构建目录，可重新构建，最终包仍保留。
剩余真实双输出条件、承载架构/满幅、媒体时钟、复杂/末页、用户LO共存、完整组和60分钟；不能据本轮有限通过完成整个目标。
