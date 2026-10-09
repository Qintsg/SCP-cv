# VLC插件索引与冷打开

日期：2026-10-05。T076发布修复；本记录只覆盖明确证据，不表示整体完成。

## 诊断与A/B

同版Player默认冷OPEN约14104.8ms，其中new LibVLC13740.0ms、静音0.3196ms；独立verbose实例构造13788.4ms。原生日志读取plugins.dat失败并枚举436模块，实际包缺索引。没有据此改GPU、延时或普通播放参数。
在D4新独占包中通过官方LibVLC的reset-plugins-cache生成265389字节索引，323个插件DLL集合/摘要不变；拒绝额外VLC_PLUGIN_PATH并ignore-config，不读取或写用户安装/配置。
相同包新交互进程无non-autoplay预热、默认引擎参数下，构造23.181ms/OPEN181.6284ms；UDP真实呈现计数推进、断流错误、同Host/Media显式重开、关闭null/端口消失通过。对应运行`008-udp-cache-1791208300000/run-741e8a96257045f6b04006c3ed0b5b62`，PID18308/UDP62132。原图仍需归档复核，不能只按事件报连续显示完成。
索引SHA `373DC49B5CF50B86BB7C37F1A0C0C90A2AEC2A164CB510E6371F95D23EB45EC6`。固定VLC3.0.23[模块加载源码](https://raw.githubusercontent.com/videolan/vlc/3.0.23/src/modules/bank.c)说明索引按插件mtime/size匹配，缺失/失效可正常扫描；[缓存源码](https://raw.githubusercontent.com/videolan/vlc/3.0.23/src/modules/cache.c)保存相对路径及插件元数据。索引不是签名真实性或媒体出画证明。

## 生产接线

复用AudioWorker增加仅供显式发布的`--generate-plugin-cache <当前发布根>`分支，位于普通播放适配器/运行组路径之前。主分支仍只管理原音频职责，发布模式不创建MediaPlayer、媒体、窗口、业务库或Named Pipe。
独立Publishing/VlcPluginCacheGenerator只允许自身包根；拒绝额外插件路径、重解析点、缺原生文件/插件、已有索引。官方生成前后核插件相对名、长度、UTC时间和内容SHA一致，输出非空索引摘要。没有自动覆盖或启动时修复用户目录。
发布脚本所有文件合并后才调用显式生成模式，输出目录仍必须全新，维持五运行组件/TFM隔离。生成超时/非零/无产物明确失败，保留部分包；失败回收仅精确自有生成进程，不按进程名处理其它应用。运行时不传禁扫描或禁硬解参数。

两条业务红：旧AudioWorker忽略发布参数误入常驻服务，5秒未退出；公开发布函数尚无生成入口/错误语义。修后CLI错误目录97ms拒绝，脚本相关8项通过。CLI已有索引不覆盖、额外搜索路径不泄值保护共3项通过；Physical标签保留，不把这些守卫称实际生成/媒体播放。
Release全解0警告/0错误；最终非Physical1140/1140、0跳过（Domain41/Contracts322/Infrastructure127/ControlHost281/Integration242/Windows127），TRX在`.validation/008-cache-regression-1791208800000/`。CLI三项另位于`.validation/008-cache-cli-final/`。前端本轮未改，不重报旧274项为本次执行。

## 新正式包

正式publish-runtime.ps1生成`.validation/008-cache-runtime-1791208600000`，索引265389字节/323DLL，与D4独立生成同SHA。没有增加生产Python或新商业依赖。
AudioWorker DLL SHA `A38183CA96DA5990DF5BC72948476777452211DE4D0709030FC6507BE18085AE`；Player仍`1A1AE7F7A6334C716C72B54B879B2A294AAA7F162953F5433E3B24E5B6B836CC`，播放策略未改。
正式运行包ZIP SHA `CFA00E5CE5991CC368FC8D483DD2D371F07607D5CBD79E40A0F6A74FD0DA407E`，D4新独立根`008-cache-full-group-1791209000000`。只是验证包，不是secondary源码同步。
本机复制同包+仅QA入口后，三格式音频实际解码/时钟/暂停恢复/seek/STOP/non-autoplay换源、文件释放及正常退出0通过；实例静音，不证明实体出声。根`.validation/008-cache-local-audio-1791209400000/run-47a7466a0697437a9bf47dab431eb490`，PID48872/start UTC14:58:03.6949152、与正式Audio/索引同SHA。

## D4正式组与剩余项

首正式组`api-run-053d173b6e9045e899b49c13d6277d5f`完成文件视频运动/暂停整图保持/关闭、三格式音频HTTP/真实Worker控制与另一窗保持；首次直播图因九点遮挡门禁拒绝，QA2、Host5336退出0。不能把它当直播视觉通过；保留默认GPU allocation/get_buffer/decode_slice/no frame和buffer deadlock告警。
SSH session0窗口列表不能证明交互桌面没有提示。后续QA只记录遮挡点、实际root/PID/窗口类，未关闭安全提示/其它应用或修改防火墙。首轮遮挡原因尚未确定，不能因复跑通过称根因消失。

## 2026-10-07补齐证据与清理

冷UDP六张原图已逐张复核：首播/重开各两图显示素材姿态变化，断流保持最后帧，关闭全黑。截图ZIP SHA `6880B841BE35E4CED7A8C992C865DE228A1D3B8134DA368B9782D3BF6167C493`，本机`.validation/008-cache-shots-1791383600000/1/`。上文“仍需归档”仅为当时状态。

同正式包复跑`api-run-4e6aa209caeb43f28f94fd4175cf6bed`通过文件视频运动/暂停保持/关闭、MP3/WAV/OGG真实时钟与控制，以及RTSP/SRT/UDP断流错误、显式重开后运动、关闭和另一窗保持。Host13444正常退出0，任务结果0；两Player18716/380、Audio18844、MediaMTX20328身份由日志关联。仅占DISPLAY2/3，未操作控制桌面、电源、墙面或系统音量。静音不证明实体出声，MediaMTX进程消失不等于已取得正常协作退出码。

36张原图全部SHA与事件匹配；按19种不同SHA逐种查看代表图，不称36张都独立目视。窗口2六张保持图与基线一致，七张关闭/首播黑场图单列。RTSP/UDP重开首图为灰场，后续第二/第三图才确认运动；playing不是首帧证明。默认GPU及SRT告警继续保留，不称日志清洁或混合长稳通过。证据根`.validation/008-cache-group-evidence-1791210000000/`，ZIP SHA `C5D29EC4EFC39AB727D85AA1FD8CDDBFD81231A25DD493A435608003EF10D372`。

D4只读检查admin/console1在线，未发现ScpCv项目运行进程；两个遗留任务均Ready且instances=0后精确删除。仅移除任务注册，包、原件、数据库及失败日志保留。诊断日志ZIP SHA `8988D65DAE718ACE52B1F68863317E9C593F947039BB16E978896383C0C814F6`；首次归档同名条目导致解压冲突，现按ZIP序号分别提取到`.validation/008-cache-diagnostic-entries-1791383500000/`，不覆盖旧日志。

最终只读复核ScpCv及项目路径下FFmpeg/MediaMTX进程合计0；本轮端口未见LISTEN或UDP绑定。56080/56081存在PID19400连接外部443的CLOSE_WAIT出站连接，不是本轮回环服务，不结束未知进程，也不把端口数字相同称服务残留。

T076有限发布修复完成；T072音频在线/D4动态控制台、T073其它源完整矩阵与T074混合长稳/封装端/正式源码同步等仍欠，整体任务不完成。
