# UDP重开：引擎与测试发布者句柄继承

日期：2026-10-05。对应T073的诊断，完整媒体验收仍未完成。

## 同版与范围

Player DLL SHA `1A1AE7F7A6334C716C72B54B879B2A294AAA7F162953F5433E3B24E5B6B836CC`、Contracts `63304E44484354E1AB2999621AE5B41190630CC1ADDA7C4BFF6415FB38304BC3`与之前正式组一致；本轮没有改生产代码。
只在D4 DISPLAY3的自有800×450窗口（物理矩形[2000,80,2800,530]）测试，DISPLAY1控制桌面不动；不启动Office、音频组、墙面、电源或系统音量。
QA独立根、原样视频副本与同一FFmpeg回环UDP/TS发布参数；默认GPU/解复用与既有15秒播放观察不变。截图按精确PID/start/session、DWM物理矩形/九点无遮挡，输出到自有临时目录再归档，不修改原图。

## 完整释放与新LibVLC：仍失败

首根`008-udp-engine-1791204100000/run-256afcc237a9456585def6efd3f71e5d`，PID20064/start UTC12:44:58.4997501、UDP53381、任务退出2。
已实际完整Dispose旧PlayerRuntimeHost/LibVLC，再在原自有窗口创建新Host及显式new LibVLC；同URI重开仍在15秒内loading/time0。首轮四PNG原图为灰/黑；即使首次原生Playing/输入计数推进，也没有初始真实视频画面证明，不把它标成“首播成功”。
证据ZIP SHA `35D72234D07174CD23ECB70DB88BABB40DC18EB5A87C6BA69525CB9FB58D0660`，本机`.validation/008-udp-engine-evidence-1791204500000/`。

第二根`008-udp-socket-1791204500000/run-7db49db2ebb3432d9c56896bda4a3be6`，PID20132/start UTC12:51:06.4671809、UDP55627，退出2。
仅QA增加netstat只读当前端口/PID：旧Host释放后仍有1条127.0.0.1:55627绑定；新引擎OPEN后有两条，15秒各次观测均存在，均标原PID20132。统计不代替套接字，更不从0统计推网络断流。
证据ZIP SHA `4530B0E7966729DDFEBE3701EB019A2B497A8ACF5FE68DB73D2C836129F52F9B`，本机`.validation/008-udp-socket-evidence-1791204900000/`。

## 单变量退出对照

第三根`008-udp-inheritance-1791204900000/run-ae89fa8997774773aa73d992b389e7f2`，PID18892/start由事件记录、UDP51969。
发布者2是在旧接收器仍存在时由QA父进程启动。旧Host正常释放后绑定仍存在；仅结束缓存的该发布者句柄，原绑定立即消失。新发布者3在无接收器时启动，绑定仍空；随后新引擎仅一条绑定，实际DecodedVideo38/DisplayedPictures7，恢复后第2张原图有视频素材内容。
这组测试退出0、原件锁/SHA保持；6张原图已逐张查看，首次原播黑、恢复第1张灰、第2张真实内容，不能当完整首播/连续运动通过。
证据ZIP SHA `FF0BDF0740A627976545167C12159550C8B922B330B88D8F75E276F5399D8304`，本机`.validation/008-udp-inheritance-evidence-1791205100000/`。
三个自有任务确认Ready/instances0后精确移除，项目进程和第三组端口消失；包/媒体/日志保留。发布者结束为故障注入/精确夹具清理，不冒充正常服务停机。

## 判断与后续

上述退出对照证明原绑定寿命受自有发布者2影响；结合固定.NET10.0.11的[Process创建源码](https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs)（Windows CreateProcess启用句柄继承），支持“QA发布者继承旧原生UDP句柄”这一原因。不能由netstat的创建PID单独断言句柄仍只由Player持有。
固定VLC3.0.23的[UDP关闭源码](https://raw.githubusercontent.com/videolan/vlc/3.0.23/modules/access/udp.c)在访问器Close关闭其socket；源码不代替实际调用/资源退出证明。本轮不把3.0.21引用冒充当前运行版本，实际包使用3.0.23。

第四对照只将QA发布者隔离为独立shell启动/Hidden、自有FFmpeg原生-report留日志，按原生产同Host/同Media路径测试，不增加生产重建、延时、TS强制或软件解码。新增QA帧门禁须实际DecodedVideo>0且DisplayedPictures推进后才取图，不能拿Playing直接命名成运动图。
根`008-udp-isolated-1791205500000/run-fe34d74ef19d4920b382fce35b6d9e2f`，PID19640/start UTC13:09:37.3809472，UDP58511；首次OPEN报告Playing，但5秒内无新呈现帧，未进入重开阶段，任务2。没有截图被误称通过；旧错误`buffer deadlock prevented`保留。发布开始13:09:38.703、OPEN返回13:09:54.507，16秒间隔需进一步区分初始化与接入时序，不能据此直接叫GPU或长GOP根因。
证据ZIP SHA `F2B7FF36B58D17C74F44E2868791666F97E7BBEC2902021F0691BC6F93EF36C0`，本机`.validation/008-udp-isolated-evidence-1791205900000/`。该完成任务已移除，原视频/根/日志保留。
第五对照保持隔离发布者，仅先以non-autoplay完成初始化且无输入，再开始同参数发布。根`008-udp-warm-1791205900000/run-ada7a79a4e21435aaac3c936cb5007b6`，PID19384/start UTC13:23:41.8452100、UDP50506，任务0。
non-autoplay返回paused且没有UDP绑定；正常首播后呈现计数0→2/解码24及41→49/解码100。发布者消失后错误锁存，显式同Host/同Media重开，呈现0→5/解码30及47→54/解码110；重开前后均只有一条接收绑定，CLOSE完成actual源null/idle，端口立即消失。没有创建新Host/LibVLC、强制TS或禁GPU，也没有额外重发产品命令。
六张800×450原图已逐张查看：首播两图和恢复两图都显示素材人物/机械臂/仿真，姿态有变化；断流冻结图和关闭全黑图分别核对，没有新菜单、桌面边条或残留。素材自带字与应用画面不是项目控件。
证据ZIP SHA `E62A24994334FDEB1B34A1492147856B629CFF92583B4256F2DDBFA3AC4F624A`，本机`.validation/008-udp-warm-evidence-1791206300000/`；原件摘要/独占读保持，自有任务Ready/instances0/0后移除，项目进程/端口无残留，源码D4仍干净且未secondary同步。
日志仍含buffer deadlock prevented、TS discontinuity/PCR和SetThumbNailClip告警，不能把后续出画当全程GPU/媒体健康。单一暖初始化时序不是生产冷启动已修：第四组首次接入失败仍成立，尚须区分LibVLC冷初始化、WPF承载及发布/接入时序，不能直接加入生产预热或增大预算。
本轮QA每次Release构建0错误/0警告，生产DLL保持；未重跑旧1139后端/274前端或完整组回归。T073/T074仍未完成，UDP完整认证组、初始冷启动、重复/并行/长稳及音频在线字段等继续承接。

2026-10-07补充：后续已将冷构造分段定位到缺插件索引，T076正式发布生成索引及新进程默认冷UDP、完整认证组短测通过，原图归档复核完成，见[插件索引记录](vlc-plugin-cache-20261005.md)。不是生产预热或禁GPU；上述旧失败与暖初始化对照保留。重复/并行/混合长稳及动态控制台仍未完成。
旧[UDP失败记录](udp-reopen-diagnostic-20261004.md)原样保留，后续范围更正另写，不能用本轮夹具原因擦除旧告警、缺少真实首帧或所有UDP生产风险。
