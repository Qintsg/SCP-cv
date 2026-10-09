# Implementation Plan: LibreOffice完整接入

2026-10-05 T073冷接入分段：同版默认LibVLC构造实测13.74秒，静音约0.32ms，完整OPEN14.10秒；
原生日志确认缺plugins.dat且枚举436模块。先在新独占QA包用官方reset-plugins-cache生成索引，禁止额外VLC_PLUGIN_PATH、忽略用户配置、核插件DLL不变，
再在新进程无non-autoplay预热下复测同流/实际帧；未证实A/B前不将插件扫描当完整根因或接生产预热。缓存必须匹配固定引擎/插件及时间戳，不禁正常扫描兜底。
后续A/B已确认默认新进程构造23.18ms/OPEN181.63ms、冷UDP实际帧/重开/关闭通过；T076只为当前最终发布包补官方索引。
生成动作与播放进程隔离，仅显式发布阶段执行、忽略用户配置并拒绝额外VLC_PLUGIN_PATH；核插件DLL集合/内容/时间戳保持与生成物存在，不创建业务库/窗口/媒体，也不将缓存当签名可信性或完整媒体就绪。

2026-10-05 T073 UDP诊断：先按同流地址/同发布时序完整Dispose旧PlayerRuntimeHost（含LibVLC），
在原自有窗口创建新Host及显式new LibVLC；与先前只新MediaPlayer对照区分。统计/实际状态/原样物理图/退出分开取证，
默认解复用及GPU不改、15秒观察不延，未取得同条件证据前不接生产重建策略。测试只占DISPLAY2/3且新验证根，不冒称完整组。
后续单变量退出实证QA发布者延长旧UDP绑定寿命；只改QA发布者隔离，non-autoplay暖初始化时序下实际同Host重开/帧推进/关闭通过。
冷首次接入无帧仍欠，不能因此接生产暖初始化、循环重建或批量勾T073，见udp-engine-and-handle-inheritance记录。

2026-10-05 T072音频页面寿命：实际Vue/Pinia/HTTP分别复现卸载后的播放错误、刷新错误及旧finally提前解除新等待。
只在页面内保存active标记与每次操作的symbol owner，反馈继续核主机连接代次；共享store和已提交后端请求保持，不取消/重放。
公开DOM与真实桌面/390模拟浏览器分层验证，不把它扩成播放器在线能力或D4动态SSE完成。

2026-10-05正式组直播/音频：Supervisor__MediaMtxPath沿既有生产入口登记MediaMTX，QA只拥有回环发布者；
复制现有配置到新根并覆盖自有端口，不新增同义兜底路径。三格式音频按实际时钟、直播按实际状态/原图分别验收；
RTSP Time0不是文件视频时钟，首次playing黑/灰不当首帧。有限恢复/退出已完成，默认GPU警告和混合长稳继续承接，见full-group-live-audio记录。

2026-10-05 T075实机缺陷：正式Kestrel实际拒绝98MB文稿于30000000字节默认上限，未进入转换。
仅上传入口在读取前设置有限1GiB总请求/Multipart门禁；已声明超限在读前413，实际解析超限同样明确，
畸形表单400且无媒体登记。保留普通请求默认大小、认证/CSRF与现有磁盘暂存，禁止全局无限上限或QA环境放宽假绿。
本切片两公开HTTP红绿、1139非Physical及D4实际大文稿上传/7页制品/两窗动态短测/精确末页回执/正常退出完成；
文件与网页长稳另已完成3613.7秒。声明超限、实际大文稿和有限原生短测分别取证，不扩为chunked/所有文稿/完整媒体长稳，见upload-limit-and-full-ppt-group记录。

2026-10-05共享控制台完整API联调：使用正式Web构建和完整发布ControlHost的Simulation新根，
明确连接实际回环地址后登录，浏览器操作文件夹/上传/直播登记/编辑拒绝恢复/移动，核真实HTTP响应、
可见结果及实体文件SHA。浏览器读取认证SSE首快照后取消自有读取，不以链路在线冒充Worker出画；
与D4同版长稳任务独立，见full-console-api记录。

2026-10-05 T072余下请求提示：只修既有四个入口文案，不新增完成查询或改变成功状态；
源选择、上传打开、媒体库打开/音频播放均以公开DOM外部HTTPloading响应复现旧假完成通知。
实际输出标记/原生结果仍沿现有观测，模拟浏览器反馈与D4长稳独立取证。

2026-10-05正式组画面门禁：按运行组PID/start/session读取两Player实际顶层窗口，
以PerMonitorV2线程和DWM物理矩形核DISPLAY2/3满幅、九点遮挡后原样截图，不访问控制桌面。
三轮图/PDF/视频/网页切换与另一窗整图保持、关闭黑屏和正常退出分开验证。
长稳沿同一正式组新增10轮/100控制后至少60分钟，真实采样与45秒间隔、原图/资源记录不等同完成；
活动测试保持明确身份与进程句柄，不因一次观察超时重启，详见group-visual-soak记录。

2026-10-05 T073/T074转完整发布ControlHost外部HTTP与正式两Player/Supervisor组。
QA仅用新DataRoot和随机内存认证、指定DISPLAY2/3，先登记/上传/移动，再以真实管道观测确认三轮混合源及退出。
非法control动作同步校验必须在HTTP异常边界内；管道对端断开与发送Flush竞态按既有IOException重连路径处理，
保留消息身份及停止闩锁，不新增盲重放。状态取证不代替实际像素/SSE/MediaMTX/长稳，见本轮full-api-group记录。

2026-10-05 T072音频反馈沿既有页面动作/通知与主机连接代次修正，不新增播放合同或完成查询。
HTTP播放受理提示“已提交播放请求”，原生状态仍用后续观测；迟到响应只在原主机发布通知。
公开Vue/Pinia/HTTP回归与真实桌面/390模拟页面分别验证，不当作D4出声或整组验收。

2026-10-05 T069矩阵补测：自有QA只在内存构造唯一/多页/隐藏尾页/最后PAR重复/零叶子时长场景，
其它末页逻辑仍链接正式源码；父测试分别核完整普通页、两种下一步边界、GOTO后新动画、相邻页和重开。
关闭可选深度诊断以验证正式轻量listener不依赖诊断；原件、预算、窗口1/2落位和正常退出不放宽，
任何失败按实际层次记录，不直接缩成限制或以同页码代替原图。

2026-10-05 T069启动后绑定实验：先取得真实controller/list，在自有模型移除已追加空尾并设End.NONE，
再强制同页UI重显示以引擎实际transition/main begin取证。单页首实验已保持/撤销/退出，但不授予noop；
加强对照先完成一次受控重显示后再改模型，排除首次导入之前修改的假绿。若证明可用，
候选应按真实列表的最后项/最后可见项绑定，不按文件页数猜；完成事实须匹配当前播放轮及最后组，
倒退/跳页/晚回调/丢事件不沿用旧事实，原件/重复风险保护和正常退出不放宽。完整模式/模型重复页
仍须实证，不因单页试验勾T069。后续已接正式owner绑定/listener/纯事实，并以旧候选返回ok的真红对照
取得单页30noop绿；不冒称完整模式/发布组。详情见[本轮记录](verification/animated-terminal-evidence-20261005.md)。

2026-10-05 T072/T073音频定位沿既有HTTP/SEEK/Worker语义补链，不另建播放通道。
服务只入队目标，验证必填非负int64与当前源；控制源/代次不变、位置留实际投影。
进度复用节流器源/列表项/主机域，音量只按主机域；手机显示时钟/44px操作区。
独立回环Kestrel只装配正式Auth/音频/SSE路径和实际Broker/Worker，非完整主机装配证明；
本机/DOM/D4与同版包证据分开，见[定位记录](verification/audio-seek-http-20261005.md)。

2026-10-04 T073直播修复：SRT断流实证四原生计数与时钟冻结、Playing仍真。每个声明为直播的VLC尝试
持有独立纯进度窗口，owner在快照中读取自有Media统计；Playing下四计数5秒无变化锁存既有错误，
paused/stopped/buffering及非直播不误套。计数/监测不会调用Stop/重连，显式重开创建新尝试，错误不擦除。
只在自有D4回环夹具验证，UDP恢复和首帧/完整API/长稳未因SRT有限通过而完成，
见[直播取证](verification/native-stream-d4-20261004.md)。

2026-10-04 T073首实机切片：复用正式Player小窗做非PPT多源，PDF相邻缓存命中在同一门禁更新CurrentPage。
公开WinPDF Physical与D4两指定输出分层验证，不启动Office；VLC时钟/暂停、异步seek旧帧与实际运动图单列。
只上传自有QA包，不改正式源码同步流程或业务库，不把有限静态网页/视频等价于直播/背景音频/长稳，
见[PDF与非PPT实机记录](verification/native-media-d4-20261004.md)。

2026-10-04范围补充：按FR-016/017稳定优先与多源前端收敛，不以罕见PPT兼容性阻挡其它源检查。
T072首切片新增纯前端playbackOutput，按actual源及适配器/列表类型选择参考；期望源仍用于请求信息，
不能补齐未知实际输出，离线参考标最后报告。文稿布局加载器和原生控制语义不变，
完整按钮/跨类型身份与D4多源矩阵仍待后续，见[本轮参考修复](verification/multi-source-output-reference-20261004.md)。

**Branch**: main（保存点549c57f） | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

## Summary

2026-10-04 T073周期音频进度：执行器保存成功命令的纯源/代次，OPEN前失效旧采样，
通过既有RuntimeWorkerSession空闲sampleState链路只读playing/paused/error时钟；
自然Ended映射stopped留Finished推进，不改变列表规则或补控制。
最终D4同条件进度/列表通过，HTTP及完整故障/长稳仍欠，见[周期进度记录](verification/audio-idle-progress-20261004.md)。

2026-10-04 T072/T073音频观测：REST/SSE共用服务端时间围栏，刷新完成用实例WeakMap symbol隔离，
主机代次沿既有API保护，不重放控制。音频实际列表加入同事务推进快照时间，重复操作不改。
相同历史时间保兼容、不假称全序；周期进度与离线能力另待证据，见[本轮记录](verification/background-audio-observation-20261004.md)。

2026-10-04 T073列表：Worker仅报带源/代次的Finished，控制端同事务选下一曲或末尾STOP；
STOP带事件、保留末曲/代次，手动导航不套自动终点规则。
启动ready按正式30秒门禁与播放15秒分别取证，不因首QA未ready延生产预算。
D4真实管道有限通过，SQLite高危依赖仍须免费合法/兼容方案修复，见[列表记录](verification/audio-list-loop-20261004.md)。

后续查证更正：生产已pin2.1.13，旧QA漏CPM导致实际3.49.1，不升级生产来修QA。
QA同级props早期继承集中表后对齐3.53.3，实际DLL/SHA、原生floor回归、D4列表再证；
最终完整包仍需复核，不把同版声明代替执行，见[依赖对齐记录](verification/sqlite-qa-alignment-20261004.md)。

2026-10-04 T072滑块及T073音频：复用现有节流作用域，按请求/实际身份与代次失效未发缓冲；
普通心跳不改变作用域，不取消已发HTTP。Audio OPEN先Stop旧输入再换Media/身份，
避免non-autoplay新源借旧Playing；控制预算/播放列表策略不变。
公开DOM/实际Physical及D4三格式有限检查分层，列表循环/Finished差距另需门禁，
见[滑块记录](verification/playback-slider-fence-20261004.md)和[音频记录](verification/native-audio-d4-20261004.md)。

替换PowerPoint播放/转换/设置，使用已安装LibreOffice及官方UNO绑定，不fork其源码。PlayerWorker仍唯一拥有Display命令和WPF窗口；每窗独立C#互操作侧车/profile拥有原生文稿，另设转换实例。先验证关键能力，再接入生产；不将原型或投影受理当播放完成。

## Technical Context

**Language/Version**: 主运行时C#/.NET10/WPF；官方旧CLI UNO使用独立.NET Framework4.8 C#侧车；前端Vue3/TypeScript。

**Primary Dependencies**: 已安装LibreOffice26.8.0.3的官方CLI/GAC程序集、URE/offapi类型配置；框架引用/协议序列化按仓库集中依赖管理；无生产Python。纯值协议使用net48/net10.0双TFM及内置DataContract JSON，不引入第三方JSON包；Framework targeting pack须存在，缺失时明确配置失败。

**Storage**: 原件/准备产物仍由ControlHost管理；每个引擎独占profile，身份含group/instance；SQLite只由ControlHost写。

**Testing**: 非Physical policy/合同/进程归属回归、真实本机与已授权D4只读文稿、多开切换/关闭/媒体时钟/图像；D4仅已核验的两块大屏输出，控制桌面保持。

**Target Platform**: Windows x64交互桌面；侧车依赖.NET Framework4.8及正确UNO运行时类型注册。

**Project Type**: 多进程桌面播放主机与共享Web控制台。

**Performance Goals**: 有界启动/打开/关闭与超时证据；10轮双窗20次原件切换、100次控制；至少60分钟稳定性留在最终门禁，不以短跑替代。

**Constraints**: pnpm/CERNET、零付费、不修改LibreOffice、只读/禁宏/禁外链、两窗、不控制用户Office、每文件<500行。

**Scale/Scope**: 两播放实例+单转换实例；PDF/页图显式静态模式；旧PowerPoint入口退役，历史证据不改写。

**2026-10-03新授权**：用户明确末页无剩余动画时下一步实际无操作并前端提示；D4窗口1/2、D:/SCP-cv项目软件测试及相关环境安装重新开放，尽量不再提问。下文各历史切片的“D4暂停”仅描述当时执行范围，现行范围以spec和T069/T070为准；物理电源/未知墙面映射/系统音量不随软件授权扩展。

**2026-09-30生命周期调整**：impress-1/2不作为Supervisor空闲常驻成员，而由对应Player按文稿创建独占侧车；
转换由ControlHost准备作业创建，不依赖大屏运行组已Armed。两窗身份沿用真实Display租约的group/owner；
converter的group/owner指ControlHost进程寿命（OS启动ticks），由持久job/source修订及摘要授权，不与墙面播放组混用。
stdio父归属由OS父PID/start/session核验；启动门禁先入父job，再允许侧车启动自有Office job。
Supervisor仍只管理两Player、Audio、MediaMTX；正常Player退出必须等侧车/文稿/引擎完成，强清不冒充正常证明。
PNG与raster-only PDF同一次转换、同源发布，整个制品摘要含PNG序列与PDF；无媒体附件的PDF只作静态图片页播放。

**2026-10-03转换路径调整**：播放与转换profile统一放在OS临时目录的`SCP-cv/presentations/<engine-instance>/`，
不嵌套在媒体或制品暂存目录下。实例身份仍独占、目录必须新建，原件/制品/数据库仍留DataRoot。
profile根使用120字符保守工程预算，异常深TMP/TEMP在启动前明确拒绝；不是LibreOffice通用路径上限。
本机154字符根在扩展初始化阶段原生fatal退出，88字符短根及原深数据根复跑通过；未取得完整C++异常文本，
不把该单类启动路径修正扩大为所有间歇启动风险已修复。暂停创建进程须先缓存精确OS句柄再放行，Framework退出码才可读。

**2026-10-03包预检调整**：ZIP/XML元数据读取抽为Net10/Net48同源纯文件模块，与UNO文字提示应用分开。
转换在作业/摘要校验后、引擎启动前预检；动态候选在viewport/侧车创建前预检，失败留旧。
明确非Internal模式和绝对内部引用拒绝，页引用不接受query/fragment；默认布局提示规则不变。
只有纯预检拒绝可明确Failed，不能把已启动原生后的导出/关闭异常或旧暂存输出改成安全重试。

## Constitution Check

- 安全/原件保护/失败真实/明确归属/代次围栏继续适用。
- 用户最新选择取代旧“单PowerPoint COM”技术约束，已写spec，治理同步单独任务，不覆盖原则或旧006结果。
- 官方CLI引用RealProxy，.NET10直接加载已失败；纯C#Net48连接成功（显式uno.ini/URE_MORE_TYPES），不能塞进主进程或复制Python原型当生产。
- 原生对象仅留侧车，WPF对象仅留Player；跨进程传递带身份/代次的纯值及验证过的OS窗口句柄。
- 未通过去UI/有界正常关闭门禁前，不宣称完整路由可交付。用户接受统一媒体控制不支持；当前capabilities默认关闭暂停/恢复/定位，前后端拒绝且不触发原生动作。无源代码fork例外。

## Project Structure

```text
specs/008-libreoffice-playback/
  spec.md, plan.md, tasks.md, research.md, data-model.md, quickstart.md
  contracts/, checklists/, verification.md
runtime-dotnet/src/
  ScpCv.LibreOfficeHost/          # 独立.NET48官方CLI宿主
  ScpCv.Presentation.Protocol/   # 纯值兼容协议，不引用UNO/WPF
  ScpCv.PlayerWorker/Presentations/
  ScpCv.ControlHost/Presentations/
  ScpCv.Infrastructure/Presentations/
frontend/src/features/settings/ & display/ & sources/
```

## Phase 0 Research Decisions

- 以纯C#Frame正常承载为基础，不使用已崩溃的Hidden+外部ParentWindow组合。
- 原件可播放与静态产物准备分开；native失败明确error，静态仅人工选择。
- 先修停机Pending旧命令与过早CompleteStop，再启动新宿主，防止跨组误操作。
- IPC需父command/claim、group/owner、engine/document/source epochs、deadline及parentPID/start/session/DPI，晚结果不能更新新画面。
- 统一媒体暂停/恢复/定位当前明确不支持，不以stopSound/静音假修。未来若启用须实际冻结/恢复时钟；当前关闭仍须停止媒体并证明退出。
- 在关闭/显示门禁诊断期间可并行完成T008纯值协议及无原生副作用回归；T009生产承载和默认路由仍须门禁通过。

## Complexity Tracking

文字适配采用每次打开/明确准备的fit_text布尔选项，默认false；由只读UNO模型TextFitToSize的AUTOFIT实现，绝不保存原件。
纯值请求指纹覆盖该选项；不能携带到无关控制动作。静态制品记录text_layout，原排版与适配制品不混用。

Net48侧车是官方旧混合CLI/Remoting兼容所必需，不替换主.NET10，不新增Python业务服务。持有独立引擎的profile/进程是双窗和用户文稿隔离所必需，不复用单全局Office槽位。

## 2026-10-02文字布局实际观测补充

会话增加可空 `actual_fit_text`，只由Worker成功发布的当前文稿资源提供，不从desired、最后OPEN请求或静态准备偏好推断。
失败换源保留旧资源的布局；关闭确认及非文稿资源清空；代次过期的报告不得覆盖当前布局。
ControlHost沿用既有fenced状态投影，将实际布局持久化到PlaybackSession并通过REST/SSE报告；旧记录为null表示未观测。
前端显式重开沿用该实际布局，不将重开动作静默改成默认原排版；布局未知时保持既有默认false并明确不宣称已确认适配。

## T043：运行依赖只读诊断补充

实现采用Contracts独立Dependencies模块的快照读取与纯判定，设置端点通过小接口取得单次报告；设置页检查不得创建profile、启动侧车/Office或加载UNO到.NET10。

- **运行与构建分开**：运行侧检查Windows x64、Framework Full注册表Release至少528040（接受4.8.1）、可读的Framework64 CLR、x64/net48宿主及配套Net48协议程序集。SDK、开发编译器和Framework targeting pack只属构建条件；上文targeting pack要求指构建机。发布预检不能直接用于运行就绪，只复用其官方安装文件/CLI清单与平台判据。[Framework版本检测](https://learn.microsoft.com/dotnet/framework/install/how-to-determine-which-versions-are-installed)、[Developer Pack与Runtime区别](https://learn.microsoft.com/en-us/dotnet/framework/install/guide-for-developers)。
- **官方安装判据**：检查program下soffice.exe、soffice.bin、uno.ini、cli_uno.dll、types.rdb、services.rdb及types/offapi.rdb可读且非空；四个GAC_MSIL程序集cli_basetypes/cli_ure/cli_uretypes/cli_oootypes与GAC_64的cli_cppuhelper必须有匹配宿主引用的身份和兼容绑定。普通MSIL的PE可以是I386，不能误当32位原生；混合CLI与原生桥需x64。CLI程序集版本不同于LibreOffice产品版本，不按首个同名DLL或产品版本数字猜兼容。[官方CLI绑定说明](https://wiki.documentfoundation.org/Documentation/DevGuide/Professional_UNO#CLI_Bindings)、[官方绑定版本策略](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/cli_ure/readme.txt)。
- **只读元数据接缝**：将纯判定与文件/注册表/PE及程序集元数据读取适配分开，以可注入快照测试缺失、损坏、不兼容和读取失败；不修改真实安装或注册表，不通过启动宿主来探测。AssemblyName.GetAssemblyName只读manifest，不将程序集加入当前domain；不得改成Assembly.Load UNO。返回组件原因和公开配置项名，不泄漏配置值；静态检查通过仅表示可尝试，不能标成UNO连接、文稿或出画就绪。[Microsoft元数据读取说明](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assemblyname.getassemblyname?view=net-10.0)。
- **最小测试先行**：首条真实设置HTTP红例为host和uno.ini存在但缺soffice.exe，应返回available=false及明确组件原因，且无Office/profile副作用。之后逐项覆盖必需文件与五个CLI缺失、错误架构/损坏元数据/绑定不匹配、Framework缺失与528039/528040/533320边界、非法或不可读配置（可诊断且不泄值/500），最后以“运行依赖完整但无SDK/targeting pack仍可用”保护运行/构建区分。HTTP、纯策略、只读适配与设置页反馈分别验证，不把假文件矩阵冒充真实UNO互操作。

当前实现固定必需事实，PEReader只读入口/架构/TFM及ProtocolVersion=2；五CLI按宿主和选中程序集传递引用匹配，而非取首个同名DLL。有限GAC枚举采集manifest关联的publisher配置，app后按major/minor族处理publisher；Framework64 machine配置有CLI覆盖或未知全局规则时保持未确认。DTD、codeBase/probing、错误XML及尚未支持的appliesTo/processorArchitecture限定拒绝静态确认。不实现完整CLR/native loader或加密签名/关联文件hash验真。前端严格采用成功合同，失败/切主机/卸载使旧响应失效，手动重复检查不重复在途GET。验证与剩余边界见[依赖诊断记录](verification/dependency-diagnostics-20261003.md)。

## T044/T049：导航调查与输入门禁

T049仅完成导航输入垂直切片：前端完整十进制正int32，服务goto目标缺失或非法在Enqueue前拒绝；first/last派生和既有超总页数处理不变。15HTTP/15DOM分别证明拒绝不排意图与既有通知，不让默认第一页替代未指定目标。
T049调查时wire相邻页/动画步直接Controller路径与Player UI goto不同；T050已把相邻整页复用UI goto并有限实测。T044仍未完成：效果步仍是独立路径，页码观测不是效果结束；官方listener自动动画、同页撤销重播无OperationId，不能以计数制造操作完成。
本轮两个新实例在Office监听前30秒超时，尚未执行导航；client保持uncertain及原有job清理。未改预算、复用profile或将强清记正常关闭。不能把后续测试的OS内存不足或profile更新日志猜成该启动失败根因。

## T050：仅自有实例的更新隔离

2026-10-03离线转储结合官方GUID符号把等待定位到Desktop::doShutdown的更新线程join，URP尚等初始化放行；只更改自有子环境更新查询目标后，原预算内完成真实open/控制/正常关闭。该对照地址不进入正式代码，也不据此称全部启动/动画已修。
按官方Update.xcs在OfficeProcess创建的全新profile/user/registrymodifications.xcu预置MAR Enabled=false，之后由LibreOffice正常完成初始化；不写初始化完成/迁移状态、不改ProgramFiles或用户默认profile，不复用已有根。独立纯文件模块先测配置和已存在根拒绝，再接正式创建路径。
继承的LIBO_UPDATER_TEST_ENABLE会强制覆盖，存在时在创建/启动前明确拒绝，而不清用户环境变量或打印其值。此项仅关闭自有播放/转换实例的MAR，不构成整个LibreOffice网络沙箱。
正式验证必须取消诊断URL覆盖，原30秒预算、多轮fresh实例及正常job退出/原件摘要证据保持；符号文件内部Age与PE差异、首失败/dump及裁切门禁单独保留。

## T044：动画诊断先行

默认关闭的本机诊断可注册官方XSlideShowListener；回调只写有界纯值记录，不读节点属性、不重入UNO、不做IO或等待锁。记录序号/本机单调时间/事件种类及当前操作观测窗口，窗口标签不代表因果或完成。
由侧车主调用线程在观测时排空并写stderr，溢出/争用明确统计丢失，不以空缓冲推断没有动画。打开/关闭绑定到该文稿listener，不使用用户实例或全局listener。
先按只读PPTX声明选择点击动画样例，再实测正向/撤销/跨页及实际页码。事件开始/结束可来自自动动画和撤销重播，不能用单个事件或同页码宣称整次效果已完成；协议与正式能力暂不改为假成功。

## T052：未终结指令投影

动画步审查实证既有兼容pending投影漏Processing：领取后、Worker采样及新HTTP入队均可过早隐藏执行中命令。
维持durable队列和fencing不变，在同一事务投影按TargetSequence最早Pending/Processing；考虑tracked终态及尚未保存新命令，不能由SQL旧状态复活Completed/Superseded。
认证HTTP与独立SQLite验证Display/Audio，终态后转下一/空；前端仅补真实DOM网络防重、错误释放和REST/SSE迟到保护，不据此定义动画播放中再次推进策略。
快速原生调用次数不等于点击步数，关闭/换页成功与连续效果推进缺口分开记录，不靠延长预算或固定等待将T044标完成。

## T053：动画中再次推进的已确认行为

用户2026-10-03确认“快速完成当前动画后下一步，类似普通PPT放映”，不是等待自然结束或仅把本次点击用于跳过当前效果。
先追踪官方gotoNextEffect和UI点击路径的实际队列/skip语义，验证原生接口是否足够；不预先叠加两次调用、固定delay或从页码/事件数猜正在播放。
本机受控点击动画分别验证空闲推进、播放中再点、快速多次受理、跨页与关闭；需要可归属的执行/完成证据，原生视觉门禁不免除。
当前采用仅内存主序零时长自动空尾，由原生skip本身继续next，不补发第二调用；交互序列和现有原子效果不改，转换不应用空尾。
绑定真实模型页身份；无主序页复用UI goto，回退到有动画页沿原生效果重播。默认关闭的节点目录诊断只owner读属性、回调用CLI本地OID匹配，不把标签当完成。
正式基础推进/无动画立即下一页/短循环及视觉多阶段有限通过；初次撤销残留报告经原图/摘要/像素核对确认为误读，不据此修播放器。单页自定义放映另实证末动画完成再next进入黑屏而页码仍1，末页产品选择待回答。见[本轮快进与视觉记录](verification/effect-advance-20261003.md)及[撤销与末页记录](verification/rewind-boundary-20261003.md)，T053仍未完成。

## T054：动画页面请求生命周期

保持应用Pinia/通知store，只替换路由组件，公开DOM复现卸载后的旧HTTP错误污染后续页面；独立测试仍采用新store隔离，不能借通知宿主一起消失假绿。
仅组件scope存活时发布catch通知，不撤销已受理命令、不盲重放或吞当前页错误。两业务红绿及真实回环浏览器确认，与T053原生完成/末页策略分开。
补两个fresh正式样例OPEN后立即首点击的像素门禁，之前不加Observe/截图/sleep，之后有限采样只判本次图像，不扩大为通用就绪协议。见[本轮记录](verification/cold-click-lifecycle-20261003.md)。

## T055：两独占实例的快速控制与切源隔离

本机公开正式Client/Host分别绑定两个自有Form、profile、scope及独立原件副本；A进行连续推进/撤销/跨页与TestPPT→AllinOne→TestPPT关闭重开，B停在TestPPT第六页标题阶段。
逐轮核真实页码、A两次推进/撤销后的像素、B原样截图中部内容差异、两原件及副本摘要、关闭后独占读与最终job退出；观察命令不计入100控制。快速点击之间不采样或等待自然动画。
首整窗PNG摘要门禁被活动标题及近边缘像素差异触发；图像中部差异为0，原标题/字幕像素不变。保留原失败和全区域差异诊断，不把收窄的中部判据称整客户区/满幅通过，也不据此改正式几何。用户实例共存、两窗交叉切换、媒体时钟、完整Player包及60分钟仍由T045/T046验收。
当前两个角色各十轮100控制/20源切换、有限像素/原件/退出通过，详见[两实例隔离记录](verification/dual-isolation-20261003.md)；不是恢复D4或最终原生验收。

## T056：切源退役失败与候选清理失败

真实Player OPEN先取得候选，再关闭旧LibreOffice资源；旧关闭失败时必须清候选，但第二个清理异常不能覆盖第一个已下发的不确定结果。
通过未Show的自有STA窗口和既有外部presentation adapter seam先复现；两清理都失败时以PresentationTransportException携带AggregateException两原因，仍由既有Worker保留uncertain，不重放。候选清理成功时原异常类型/实例保持，正常切入及旧实际身份/布局保护不变。
仅针对已开始外部退役/候选清理的双失败，不把纯包/文件预检的一般IO异常都升级不确定；不修改Frame、打开顺序或原生预算。此门禁不替代完整Player包和真实Office退出。
当前一条公开业务红→绿，七新/六旧生命周期与真实管道持久六分类保护、完整非Physical993/993通过；旧引用/实际身份保留不等于旧物理画面仍在，见[切源双清理记录](verification/replacement-cleanup-20261003.md)。

## T057：窗口切入与实际状态发布

公开Player已复现Activate抛Win32异常后，后续采样仍把候选source202/generation2/playing发布；原因是_current/源/状态在切入之前提交。
准备和旧资源退役顺序保持，候选Activate成功后才发布；失败必须清候选。旧LibreOffice已确认关闭且候选也确认释放时，没有可继续播出的源，返回failed/presentation_activation_failed及明确source_id=null/error，而不是伪保留旧播放或宣布候选playing。
若旧资源尚存活，候选失败清理成功后保留旧源/布局；清理不确定则保持typed uncertain、保留候选归属并拒绝普通控制/采样假成功，退出仍检查所有自有资源，不自动重放。
真实Worker/管道/独立SQLite核对空输出结果清实际源/布局但desired不改；这一层不以异常形态注入冒称完整Player连接Broker。无Frame/尺寸、统一媒体能力或动画末页改变。
确认无当前资源的公开翻页明确拒绝；前端actual_source_id=null禁导航/动画与专注键盘，恢复后可用，不改变缺字段兼容。内部空模式显式none以清投影，HTTP保持既有空串；证据与有限范围见[切入发布记录](verification/activation-publication-20261003.md)。

## T058：实际Player承载基线与最新包

先只读Screen.AllScreens枚举活动输出，当前仅主DISPLAY1/2560×1440，不足以证明两个非控制桌面输出；不启动全屏组或以Simulation拓扑伪造物理绑定。
正式PlayerWindow/PlayerRuntimeHost在自有800×450小窗、现有PresentationViewport路径，只读TestPPT副本，默认原版第六页；不用PresentationFactory接缝，不改Frame/几何。公开OPEN/效果/Observe/CLOSE和正常Dispose，图片与页码证据分开。
小窗仅本机承载诊断，不能替代正式App启动/真实Worker管道、两屏全组或60分钟。旧三几何失败继续等待人类架构讨论，不为基线叠第四个修补。
发布采用既有脚本到全新GUID根，保留锁模式/TFM隔离、旧包/失败/数据库；最新打包与运行依赖就绪不等于全组已启动。
两有限基线与新包/审计协议定位完成，原WPF黑带35px粗估已由原PNG量化更正为2行，不作满幅证明；本机单输出、MediaMTX外配及完整组/长稳仍欠，见[本轮基线与包记录](verification/native-player-package-20261003.md)。

## T059：真实视频页与关闭

沿T058正式Player生产候选容器，不改几何；只读TestPPT第3页，公开NEXT_EFFECT触发原声明媒体，捕获触发前及多时刻原样窗口图。
截图只作有限解码/显示证据，不能以SHA变化/页码或HTTP受理声称媒体时钟/全部视频稳定。真实播放期间CLOSE后捕获空窗，再核正常job退出/Office监听消失、原件释放与摘要。
失败保持外部有界清理与原记录，不改原30秒请求/15秒关闭/5秒换页预算，不使用统一暂停/定位/音量接口假证明能力，不恢复D4。
两组视频后帧变化、播放中关闭及换源后关闭有限通过，精确自有job退出/文件释放及原图全黑单独核；仍无媒体时钟/音频或全格式证明，详见[实际视频记录](verification/native-video-close-20261003.md)。

## T060：实际Player快速动画样例

用户最新重申快进后推进；既有T053内存空尾保持，不重复实现或以双调用补偿。实际Player生产候选路径/最新侧车在自有小窗复核连续明确请求，中间不插入Observe、截图或delay；冷开两次推进后核标题/字幕，再两次撤销核空白、三次推进核真实第七页。
三轮后的首效果立即CLOSE，核空输出/精确正常退出/副本与原件；有限采样只为本次图像，不定义通用动画完成协议。复杂序列和动画末页选择仍归T053/T044，不改几何、不恢复D4。
当前两fresh各三轮/25控制有限通过；三next返回后首样本6、第二样本约285–295ms为7，不能把首同页观测当丢点击或把首次见7时间当GPU完成。原首即时断言exit2保留，QA仅同1.5秒窗口采时序，详见[快速动画记录](verification/native-rapid-effects-20261003.md)。

## T061：独立Office共存

启动仅本机QA observer，自己的全新profile和Office job，与任何Display租约/项目侧车job无关；打开AllinOne副本普通只读窗口第2页，不以第二播放器替代用户式Office。
observer保持其UNO模型/当前页/源URL、模型文字摘要、监听者精确句柄及普通窗口，在项目快速控制/视频换源/正常退出及精确项目父异常退出后逐阶段读取，最后才能由observer自己结束。
每次项目OPEN对应新Office/profile，原生预算不变；记录项目与独立实例归属不混用。只结束自己的精确进程，外部看门狗保留失败，不能把强清称正常。真实用户默认profile/未保存编辑/其它Office版本不在此有限夹具范围。
两普通实例完整共存及另fresh CLOSE前故障门禁有限通过，682健康检查/原图/12副本锁和摘要保持；首两次末帧后注入可能已进入CLOSE，只保留该较窄证据。当前强门禁仅QA stdin等待、不延产品预算，异常退出仍不写成正常关闭，详见[独立Office共存记录](verification/office-coexistence-20261003.md)。

## T062：包内容与文件名的安全门禁

已接受源扩展不止.pptx，共享预检只凭该后缀早退可能让同一OOXML包改名绕过既有外链门禁；先以公开上传/Claim/LibreOfficeSlideConverter和不存在Host路径证明是否确实越过预检，不让测试启动Office或访问外链。
保持模块现有小接口与纯文件职责；先一条.ppsx红例，再按真实ZIP内容和OOXML声明扩展决定是否需要相同包检查。非OOXML旧PPT/ODP不假装已预检，不为修安全缺口新增宏执行/解密或改变播放预算。危险文件留原，转换失败结果与动态候选拒绝分别核，复杂/宏/加密实机仍单列。
当前两公开入口业务红后，11转换/26包/3Player、完整非Physical1026通过；原/fit正常转换36制品与旧同布局完整摘要一致，最新200bc0…本机包更新Net10/48共享门禁。旧9ce543…包仅历史，不称最新；禁止宏执行不等于按扩展拒绝所有含宏文稿，实机样例仍欠，详见[内容门禁记录](verification/package-content-gate-20261003.md)。

## T063：真实旧二进制与加密格式

资源目录只有两份PPTX；在自有fresh Net48官方UNO实例将AllinOne只读模型storeToURL到本次全新.ppt/.odp/.pptx验证副本，Overwrite=false且不store/storeAs原件。MS PowerPoint97导出后必须实际重新加载9页，不能只核OLE头；加密副本密码仅保留在测试进程内，正确密码重开和无密码终止对照均需通过才称有效夹具。
随后用当前正式Player小窗与生产转换公开路径检验格式、正常关闭/锁/SHA、无密码拒绝与精确退出。加密拒绝分纯预检与实际原生handler，不吞原生不确定或把job强清称正常；不新增产品密码输入/解密能力、不触用户Office/D4。生成的兼容夹具不能替代跨作者历史PPT、宏或全部版式媒体验收。

## T064：媒体登记与文稿协议格式接线

T063实际ODP候选尚未下发：媒体库支持ODP/PPS/POT/OOXML变体，协议只允许ppt/pptx，SendAsync的快照/策略先拒绝invalid_source_path；侧车已被创建，随后退出失败掩原原因。该失败不作为密码拒绝或原生加载死锁证据。
先公开PresentationRequestPolicy ODP红例，对齐所有既有非PDF文稿扩展；仅扩大已支持的文稿文件名集合，不放宽本地路径/只读宏外链/身份/期限/页范围，不新支持密码。协议规范字节与v2指纹不变，两运行时同版重建。原清理诊断覆盖问题单列，不能把它静默降failed或以延长预算修。
18相关红绿/最终1044非Physical、旧PPT正常出画/静态发布及加密动态拒绝留旧通过，最新64fbc8…包双TFM更新；T063的加密ODP静态仍uncertain，不能整体完成，见[真实格式记录](verification/legacy-encrypted-formats-20261003.md)。

## T065：转换的已知加载拒绝与诊断

真实加密ODP动态OPEN在handler终止后正常候选退出并留旧画面，但converter的SlideExporter在加载null时抛普通InvalidOperation，Program只捕获open而非export_slides，导致侧车故障退出/自有job兜底；公开作业按uncertain保存。旧运行不重试/改终态。
下一切片须区分只加载拒绝与可能已导出的不确定；只有无加载模型/无页图、终止原因已知、正常退出可确认才可明确失败，不广泛吞原生异常、擦导出部分输出或用强清作正常证明。候选/转换的后清理异常覆盖原错误另按双原因保留处理；当前只完成诊断，不先猜补丁。
实现门禁先用仓库Physical真实公开Converter测试复现，夹具由显式环境变量指向本轮自有有效加密ODP，不携带密码、不读取用户配置、常规回归排除。加载handler逐实例记录是否实际选择Abort；仅加载阶段没有返回模型且自有Desktop无components、输出为空，才发typed加载拒绝。宿主返回已盖章Failed而保持可正常EOF/shutdown退出，父Converter仍必须await真实Dispose；退出失败继续typed uncertain，不据失败响应跳过进程退出证明。
该已知加载分支Physical真红→绿、已有输出负例仍uncertain/证据保持、四纯文件guard通过，真实三源新根流程全部exit0；创建候选/转换原错误被后Dispose覆盖的诊断另拆T066，完整需求不删除。T065只按已知加载安全行为验收，T066仍未完成。
当前T063的有限夹具全流程已随加载修复完成，T065有限X；1048非Physical、2Physical与当前baf5c4…包完成，历史uncertain/错误记录保留，后续T066/宏历史媒体/双输出长稳仍欠。详见[加载终止记录](verification/load-rejection-20261003.md)。

## T066：原始动作与后清理错误诊断

两实际失败的主要来源可被Dispose替换，现有typed uncertain安全分类不改但诊断丢失。单独公开stdio/生产Facade回归先复现，再保留原始动作与精确关闭错误的Aggregate；成功关闭仍原异常/正常失败结果，不重放已下发动作、延退出预算或跳过job证明。不得仅检查异常类或内部helper宣称完整Player/Converter路径修复。
首先外部TestHost回匹配制品观测但不生成文件，EOF退出2；公开Converter须同时保持FileNotFound及退出transport。夹具只在测试输出，不进入五组件包；随后Player采用自己创建的隐藏HWND/实际生产adapter、外部failed-open+exit2，补同一双原因与成功清理保护，不显示用户桌面或启动Office。
本切片Converter4/Player2红绿、全1054非Physical及另2真实Physical通过；正常三源转换和实际Player拒绝留旧/关闭复核、新430158…包已完成。Aggregate是本地诊断，不新增HTTP字段或声称堆栈全持久化；复杂动画/末页、全viewport/双输出/长稳仍欠，详见[创建与转换双原因记录](verification/creation-cleanup-diagnostic-20261003.md)。

## T067：已发布文稿关闭的双原因诊断

LibreOfficeSurface正常文稿关闭仍以嵌套finally执行client退出与viewport释放；原shutdown异常可能被后退出错误替换，后退出又可能被DestroyWindow错误替换。先公开Player OPEN成功后CLOSE，用受控stdio返回shutdown Failed及EOF exit2复现，不能拿T066候选创建测试替代。
仅在原模块内部保留各阶段失败并继续自有清理，单失败保留原类型/栈，多个失败typed transport uncertain并保Aggregate；共享PresentationCloseCompletion继续记住首次关闭终态，重复CLOSE/Host Dispose不重放。后端公开合同不新增字段/错误码，不改正常关闭顺序、预算/原生承载。真实播放关闭及切源另复核，不能把假侧车Open观测称出画。
当前业务红绿、五公开关闭保护及1059非Physical通过；两实际视频关闭/换源正常退出，三副本释放/原件保持，新981767…包完成。144 DPI截图533×300的助手测量边界单列，不借此验收物理viewport或双输出/长稳，见[已发布文稿关闭记录](verification/published-close-diagnostic-20261003.md)。

## T068：截图DPI的受控因果对照

T067静态PNG尺寸可能来自截图调用方DPI虚拟化，不能拿它叠生产几何补丁。仅QA自有无边框PlayerWindow显示已有静态制品；DWM可见物理矩形作独立基准，同一窗口/内容/落位分别由默认助手、明确Unaware及PerMonitorV2线程捕获。只改变截图线程上下文，finally恢复，复用原技能助手不改共享文件。
窗口句柄须匹配本轮精确PID/start，输出仅新临时图目录；先证明默认图与物理矩形是否不同，再核明确aware输出与基准一致，多轮正常关窗。实际文稿复测使用同一QA包装，不改正式播放器/Office的DPI或预算，不把像素尺寸一致当全部几何/混合DPI/两屏验收。
当前只能实证96 DPI：三轮九图同SHA、四坏声明拒绝、六上下文恢复及实际Player三轮/十图正常退出；前端177/类型/Web本轮通过。历史144 DPI缩图未重现，T068继续未完成，生产代码/981767包保持，详见[本轮测量记录](verification/capture-dpi-measurement-20261003.md)。

## T069：动画耗尽的末页保持与反馈

用户已选择不进入黑屏/结束/清空。必须区分整页NEXT与效果NEXT_EFFECT，实际放映序可能是隐藏页/自定义放映；仅current==total不能判断动画耗尽。先沿官方固定版本控制器/事件/模型调查真实耗尽事实与快速最后效果的行为，用既有真实单页动画放映复现，再最小实现有界保护。
无操作结果必须贯通宿主→实际Player→持久命令→两控制页面提示，不用新期望覆盖实际画面；倒退、重开、跳页和新文稿使旧耗尽事实失效。保持原件只读、指纹围栏、原预算和非末页快进语义，不以固定等待/盲双调用/调用次数猜完成。
合同采用v3的独立noop/presentation_at_last_slide与已确认当前源/页观测，新增规范tag并拒绝v1/v2混用；只允许next_effect/next_slide采用，不授权关闭或产物。真实终端按native nextIndex<0或>=count及当前模型页/occurrence判定，不能current==total。纯协议红绿及Contracts303通过，Net48同版已编译；Player效果步桥接已完成有限红绿，原生保护与完整整页路由仍未完成。
异步控制反馈必须精确关联提交的command_id：POST只证明入队，前端等待该条持久终态才显示边界；旧source/generation、路由卸载或不同窗口不得污染提示。优先利用既有command_records，不新增命令数据库或以全局最新终态/只看页码替代因果关联。正常命令/失败/uncertain保持既有安全语义，查询只读，不盲重放。
实际采用POST目标会话submitted_command_id与认证只读命令查询，普通GET/SSE不沿用旧回执；前端抽共用反馈模块，按值身份+客户端后续操作+主机连接代次围栏，后台查询不锁快速动画点击，35秒读取安排范围/读取失败只提示，单次沿既有10秒HTTP预算。HTTP5/相关20、两页面44DOM及浏览器夹具通过，不冒称真实宿主已回noop，见[反馈记录](verification/terminal-command-feedback-20261004.md)。
官方调查证实主序结束后注册下一次点击为slide-end，现有自动tail可让最后真实子项skip穿透；单靠listener检查还存在初始零时长漏监听和检查/调用竞态。优先验证仅末序End=EventTrigger.NONE且取消tail的内存保护候选，依据最后真实直属子项观察耗尽，保留原子效果和原件；repeat/隐藏/重复自定义页/倒退须单列门禁。候选未实测前不接默认生产路由，详见研究补充。
当前唯一自定义动画页真实旧行为red、End.NONE/no-tail候选两组各三轮保持/撤销/正常关闭green；第二组含末组运行中立即next。第一组完整视觉复核通过，但尚未限定真实终端或接生产noop/前端，不能勾T069，详见[候选与D4门禁](verification/terminal-hold-candidate-20261003.md)。
2026-10-04后续：正式Host无主序/真实无后继先于resize和控制返回noop；Player NEXT/PREV走宿主相邻页并传边界码，外部stdio整页NEXT真红绿、Windows113通过。D4 DISPLAY2/3分别小窗真出画/保持/倒退/清黑与正常退出通过；有主序、并行/满幅/长稳不据此验收。监听快照/repeat/重复页方案须依[新研究](research-terminal-lifecycle.md)实测，详见[本轮验证](verification/native-static-terminal-d4-20261004.md)。
受控重显示独立QA原样三轮在D4捕获初始transition/main begin，图像保持/撤销/关闭通过；zero容器夹具只有事件但裁掉内容，repeat2第三next EOF及CLI listener重复注册/后清理未确认失败。均不接正式策略或授予noop；新zero-leaves夹具与QA原错日志已编译待下一轮，见[重显示候选记录](verification/terminal-replay-candidate-20261004.md)。
后续合法zero-leaves在D4完整显示/事件/退出通过；原错日志捕获observe URP断开，同实例WER确认Office异常退出，后CLI栈不当根因。仅QA stderr句柄白名单取证仍无故障栈，不改生产或预算；下一步取得精确自有实例原生栈及对照，不因零时长绿绕过repeat，见[诊断记录](verification/terminal-native-fault-diagnostic-20261004.md)。
下一轮已取得两份自有Office AV转储，官方私有符号定位顺序容器子项结束路径；自然End单变量对照同位置失败。源码推导的重复后下标越界仍须对象证据，不能仅取消End.NONE或取消repeat改掉原行为来验收。诊断参数/活动日志/转储路径烟测与Office故障分别记录，正式策略未替换，见[原生栈及对照](verification/terminal-repeat-native-stack-20261004.md)。
后续堆转储已实证当前主序children.size=2、mnFinishedChildren=2、mnLeftIterations=1，发布二进制无边界检查地读end槽。下一独立QA假设：把该样例的重复时序移到单子项ParallelTimeContainer、保留内部原序及叶子，避免SequentialTimeContainer自身重复；不是取消重复或修改引擎源码。须实际证明重复轮次/点击顺序/撤销/画面/退出，任何语义偏差都不接正式策略，复杂RepeatDuration/无限重复/媒体/末页仍保留完整门禁。
包装候选已在D4失败：放映过早结束/第三步没有当前放映，不能因未见旧AV而验收，已从活动QA入口撤回且包/源码/日志保留。下一夹具只测试末个PAR动画组RepeatCount=2，属于不同层级的能力门禁，不替代仍失败的主序重复要求。

## T070：D4软件测试重新授权

2026-10-05 T071依据主序重复AV堆和固定源码，仅非空MAIN_SEQUENCE SEQ的UNO double>=2/INDEFINITE
在空尾/放映前拒绝，不扩大其它重复特性；无原件写/自动降级。Player保留有界ErrorDetail，清理失败仍uncertain。
T072源选择列表复用既有实际源解析，与请求标签分开；不增加新store或用desired补未知actual。
公开回归、D4内存模型拒绝与旧PNG、浏览器多源有限结果见[本轮记录](verification/repeat-safety-and-source-picker-20261005.md)，
作者样例/旧动态/另一窗/有动画末页及完整组长稳保留未完成。

T072控制入口切片沿既有playbackCapabilities共用实际适配器/类型对应，拒绝明确空输出和不对应操作；
两页面公开DOM分别复现按钮错误可用/方向键真实POST后最小修复，保持恢复入口和普通动画规则。
这不是同VLC多类细项或不同文稿模式的完整能力设计，见[控制类型保护](verification/multi-source-control-guard-20261004.md)。

其它源并行收敛的UDP分层检查已证明独立收包/解码三种重启时序正常，但项目Player仍失败；
QA强制TS及软件解码不接正式默认。同LibVLC内新MediaPlayer不是新引擎隔离，后续必须区分，
不能据累计统计或Opening零值判网络收包，也不因此停下前端/音频工作，见[UDP诊断](verification/udp-reopen-diagnostic-20261004.md)。

先只读核SSH身份/交互桌面/明确两输出/旧进程与D:/SCP-cv状态，再核.NET/Framework/LibreOffice等当前依赖；只安装相关免费环境，不重启Windows或影响未知用户应用。测试先使用新独立验证根和当前同版包，代码更新按既有secondary推送/远端拉取流程；不把上传测试包冒称源码已同步。
用户已有媒体/库/失败证据保留，只控制项目自有精确进程；未掌握的墙面映射继续禁止下发。D4真实两输出、完整组/媒体/长稳的证据不能由本机短窗/静态就绪代替。
当前D4管理员console1在线、SDK10.0.400/运行10.0.11/Framework528040已具备，项目进程与监听停；CERNET固定MSI经官方SHA、TDF签名、产品身份核验后静默安装26.8.0.3成功，不重启/不接管MSO关联。脚本策略拒绝及MSI正斜线路径失败保持，不改变执行策略；源码仍b2f09c5清洁、尚未推送/拉取或启动测试组。
session1自有一次只读显示器任务/result0与SQLite只读核对：现window1/2绑定DISPLAY2/3，DISPLAY1为1920×1200控制输出、历史3/4为DISPLAY4/5。primary是DISPLAY2，不能按primary猜控制桌面。只删除本轮唯一已完成任务、结果/工具保留；尚无当前包/两窗出画证明。
2026-10-04当前同版v3独立QA包已传新根，不是源码同步或完整发布组。两个已完成任务各在对应大屏800×450区域正常播出/末页保持/清黑，移除本轮两任务并保留包/证据；服务仍停止，源码仍b2f09c5清洁。不能称两窗同时、满幅或60分钟完成，T070保留其完整范围。
