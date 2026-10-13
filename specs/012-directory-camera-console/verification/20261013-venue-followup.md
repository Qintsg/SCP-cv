# 选源、控制快照与手机操作台接续

本轮未提交、推送或部署；D4正式012g及原网页服务不变。代码层的协议恢复、实际端点和实体动作分别验收，不宣称39项任务已全部完成。

## 实际参数与代码

- 63/81现场Audio均为`["12"]`，00550880没有匹配HDMI/LINE标签，输入类型保持-1；00535D00映射为分类1。当前SendType=1、155输出通道1、组播224.1.1.63/81和5000端口均有独立资料。
- 用原675640在隔离CPU生成两条23字节610D：63为`FBFC610D0001000BE001013F13880780043801023DFDFE`，81为`FBFC610D0001000BE001015113880780043801024FFDFE`。校验只累加[2:15]。`VenueAudioSourceProtocol`和真实TCP传输已实现，两条原生黄金、非法源拒绝及一次写出/EOF共8项通过；回环不是实体选源验收。HTTP来源仍在首包前拒绝，不能绕过完整软件输出门禁。
- IP81本轮RTSP读取H264 1920×1080/AAC，修正历史4K默认值；63/64/81默认均1080P，仍可明确独立覆盖尺寸。新增1080默认/4K覆盖回归先红后绿，12组合完整裁剪及旧协议黄金保持。
- 私有引擎根的驱动器相对路径、根相对路径和正斜杠UNC会被误当绝对目录访问。回归3/14失败，修复后14/14通过；首个文件系统访问前限定本机盘符绝对路径。
- 头戴/手持/电脑滑杆部分失败、取消后已写出但未记忆、旧悬挂混合请求零写出，新增三项公开服务/HTTP回归通过。没有向现场话筒写新音量。

## 会话与缓存不是执行证明

原C1D0/C770/D250/CE40构造器独立截获登记`FE01000000020A01FC`、注销`FE01000000020A00FC`、资料查询`FE020000000400000000FC`和保活`020F00090D0A`。85登记/保活返回合法`41030043`。登记后各一次150ms左/右脉冲、独立RTSP前后截图仍未确认转动；所有脉冲均发送释放，不保存/调用现场已有机位。不把登记遗漏认定为根因，也未据此替换生产传输。

FE02响应是`tbl_tp(type,joinnum,data)`状态快照，不是串口脚本或完整中控工程。85为Join33/34缓存0；10的模拟量Join1/2/3缓存12/10/9。该快照支持通道/档位证据，不证明实际DSP增益或现场可听。初次85只捕获7686字节为不完整响应，后续完整8203字节才反序列化；中控旧SQLite页数字段0按受限声明长度读取。所有原始响应保存在忽略目录，不输出秘密或执行其内容。

## LibreOffice边界

- r13安装阶段登记后新实例仍未通过。r14诊断在声音身份创建前调用Read会自身抛E_INVALIDARG，不能归为播放崩溃；r15修正诊断后真实记录到第二实例策略为63、活动WASAPI却在默认端点，峰值约0.00497。逻辑策略不代表实际出声设备。
- r16每实例独立零音登记，零音不循环并自然结束后才结束暖机放映。实际PID11044/20908的声音会话均仅在63端点，音频阶段通过；随后正常原生退出超时，任务退出1并由自有job兜底。`office-audio-result.json`单独的passed不能代表本轮完整通过。
- 未将不稳定LibreOffice登记/关闭流程接入生产；未知来源能力继续false，不改系统默认声卡、OS总音量、原安装或用户PPT。

## 浏览器与前端

独立本机Simulation数据库及回环18780/18781服务，真实浏览器登录、SSE、六摄像机/唯一手控块、短点击停止、音量25应用后刷新保留、六位置调用、保存确认取消、固定监控无转动和来源禁用均核对。没有通过页面访问实体控制。

手机390/360原按钮32px不合格，方向键已有48px。修复后现场按钮、数字字段及浮层确认按钮44px，方向/停止48px；数字输入移除微型加减按钮，键盘和滑块保留。真实DOM新增回归先失败（10个小按钮）后5/5通过。360/390的scrollWidth为350/380，无横向溢出；浅/深主题及1440桌面截图核对，页面控制台错误0。截图在`.validation/venue-console-20261013/`；不是D4出画或机械验证。账户正常注销，临时视口恢复，测试页关闭。

## 验证与清理

类型检查和隔离Web构建通过，保留已有Vite扩展名与大包告警。一次全量.NET构建被本轮仍运行的测试Host锁DLL阻止，属于QA生命周期问题；精确结束自有测试Host后重新完整验证，不能计入第一次失败为通过。

清理锁DLL的自有测试Host后，常规后端1532/1532通过：41 Domain、337 Contracts、206 Infrastructure、453 Host、281 Integration、214 Windows，排除Physical，六份独立TRX核对计数。前端468/468通过；最终类型/构建及API/Spec门禁单独核验，不把先前的TypeScript错误或锁DLL失败消除为历史成功。

最终类型检查及设备/HTTPS针对性9项通过，连接页示例同步80/443裸地址；隔离Web构建通过。Spec Kit通过，Redocly零错误、三条既有退役PowerPoint入口2XX告警，diff/改动代码文件500行门禁通过；新增文件也保持各自小模块边界。规范后钩子未配置。

本机测试Host/preview已关闭，浏览器正常注销并恢复临时视口；D4 r13–r16任务仅在Ready后移除，证据与引擎副本保留，没有自有原生探针残留。正式Host11720、Supervisor9772、Player23000/20448、Audio2120、MediaMTX24492、Node23616均保持Session1，两正式任务Running。T035–T039仍未完成，原D4运行包/数据/证书/墙面/电源不变。

## 本轮接续：身份确认、精确退出与预设编辑

用户明确确认“挂着的话筒”即原头戴1/2分组，对应5.10中控Join1。规范增加一条Clarifications；US4/AC4、FR-011、边界与声音实体同步，质量检查16/16保持通过，无新增待用户回答项。headset_volume为唯一保存/响应与sent_fields字段，hanging_volume兼容旧单值；两个同值仅写一次，不同值首包前拒绝。读取155不抹掉旧话筒值；旧预设单值可以激活，新保存规范化。持久别名冲突或中控未装配也在直接/预设副作用前拒绝，不擅自选择历史值。后端初轮6红1绿，修复针对性16通过；后续持久冲突与缺装配两项补充通过，没有向实体话筒改音量。

前端只保留“悬挂（头戴）话筒”一张卡与一个高级编辑项；输入/清空同时清除旧别名，历史冲突显示两值。真实整数控件因precision约束在失焦时提交，测试补Tab/blur，不为测试改控件语义。手机高级抽屉另有18px微型按钮与36px数字输入，去掉微型按钮，360/390实测九个高级输入、摘要开关与编辑保存/取消44px，抽屉scrollWidth=clientWidth（360时350、390时381）。上下标签单列宽度铺满，沿用既有设计令牌。

真实浏览器再发现编辑已保存预设时structuredClone响应式advanced抛DataCloneError，赋值中断，名称与高级字段空白。按合同独立复制camera_positions/audio两个纯值映射，旧别名/规范字段/冲突三种响应式输入先3红后3绿，并确认修改草稿不修改保存对象。浏览器完整载入名称与65%，修改75%保存/重开一致，清空保存/重开显示保持，最后恢复65%；本机独立Simulation库，未连接实体控制。修复后的新测试页错误/警告0，正常注销，临时视口恢复，自有页关闭。截图`.validation/venue-alias-console-20261013/preset-alias-final.png`及`mobile-alias-edit-final.png`；原错误与中间截图保留。

LibreOffice r17/r18对齐产品文稿关闭后，r18精确原生句柄证明桥异常对应19564已正常退出0、25040仍在，不能将失去最后RPC回复等同崩溃。正式OfficeProcess启动时捕获原生映像句柄并校验私有路径/会话；ConfirmStopped同时要求launcher/native退出码0、job空、监听关闭，异常/仍存活不通过。NormalOfficeShutdown只在完整OS证明满足后接受DisposedException，并本地退休死桥引用；八项纯退出判据通过，Framework宿主Release编译零警告错误。

r19双实例19392/19952实际只在63，正常退出0、job释放通过。r20使用加强后的正式OfficeProcess连续两轮：23636/19980与20872/17144实际WASAPI均仅63，四原生退出0、两个job与监听均释放，每轮exit-confirmed文件存在，任务最终0。r20初次启动因测试脚本缺BOM导致PS5解析失败、无原生启动；补UTF-8 BOM后先核parser_errors=0再执行，不能计初次为成功。声音暖机仍是独立自有内存文稿，尚未接正式两Player/真实文稿/共享完整链路，不解除source门禁。

新增只创建新目录的私有引擎初始化脚本：拒绝相对/UNC/网络盘、链接、已有目标和原安装子目录，核官方签名、磁盘空间及每文件源前/后与副本SHA一致，最后写就绪标记。D4实际初始化`D:/SCP-cv/runtime-dotnet/private-audio-engines-20261013`成功，双套共3221373764字节，WebView每套916文件、LibreOffice每套13768文件，清单SHA256 `7F7E559CCDD416E8B2F15560F30FD5C99B15E03B2C3B2A2EAFBB37CB11E97F1E`。原安装/系统音频/正式环境配置不改，没有激活该副本。

D4 r17–r20四个精确QA任务在Ready后移除，自有探针/Office残留0，证据与副本保留。原七常驻进程PID及Session1不变，两正式任务仍Running；未提交推送或部署。T036身份与软件兼容已完成部分，实际DSP/机械/完整输出/来源路由仍未验，39项任务仍27完成12未完。

本轮最终常规后端1548/1548通过、0跳过：41 Domain、345 Contracts、206 Infrastructure、461 Host、281 Integration、214 Windows，六份TRX在`.validation/venue-completion-20261013/alias-final-results-v2/`。前端474项全过、十一项设备/响应式编辑针对性通过；类型检查和最终隔离Web构建通过。Spec Kit及Redocly通过，Redocly保留三个既有退役入口2XX告警；diff与本轮代码文件500行门禁通过。18782/18783本机临时监听0，测试数据库/截图/失败证据保留。

## 正式LibreOffice声音接线接续

Player的LibreOfficeSurface在候选容器创建前按窗口1/2选择私有Office；Framework侧车原生启动前核唯一Render端点与精确私有路径，converter不参与声音绑定。共享纯端点选择源码链接到Framework，不跨框架加载.NET10程序集或UNO代理。新增NAudio依赖保留在libreoffice子目录。两处空值判断适配Framework缺失的注解，显式守卫后编译零警告错误，不关闭分析器。

用户原件前两份自有内存文稿使用独立URI播放有限三秒零音：登记真实soffice.bin、应用绑定、第二流核WASAPI仅指定端点；均自然结束并异步结束放映。观测/控制核实际会话，错路或端点缺失只静音本进程会话并报错，不改系统默认/主音量。私有Office路径的无配置/窗口范围/缺文件回归10项先写至缺实现编译失败，接入后与既有14项共24通过；端点/正常退出纯策略20项通过，缓存网页声音退休两项通过。

r21正式NET10客户端到新Framework宿主的原件OPEN失败，最终清理错误覆盖主因。r22清理前记录表明用户文稿已加载，关闭暖机XCloseable引发URP断桥，原native20600退出0、launcher3160仍收尾；不是native崩溃。改为已停止的暖机资源留至整个实例shutdown统一清理，不在用户放映期关闭Frame。r23两文稿打开与两个完整shutdown已通过，但夹具错误要求无主序动画的NEXT_EFFECT停在第4页，实际正常进入第5页；修夹具直接goto媒体页，不改产品翻页逻辑。

r24正式客户端/宿主两轮双文稿：四次OPEN均succeeded、实际7页；窗口1/2媒体页的实际活动声音只在63，关闭A后B仍有非零声音，四次正常shutdown、client完整job退出0及原生进程原句柄退出0通过。r25重复同链并核声音会话volume=0.005、mute=false，16条观测（4打开、12实际声音）及两轮完整退出通过。原件SHA256始终`0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`，系统默认仍非63/64的原端点；未改OS主音量、原安装或现场DSP。meter峰值不是会话增益或现场可听证明。临时窗离屏，不占控制桌面或增加大屏输出。

r26正常Framework启动门禁独立验证：缺Render端点、误指向原安装两项都明确拒绝，侧车退出2属于预期拒绝；client有界等待完整job释放，两次未创建原生profile，原件未变，夹具任务最终0。绑定代码已进工作区，但正式环境未设置且常驻未换包；实际两Player/网页共享完整组与现场来源切换仍未通过，不解除source能力。

全量非Physical1558/1558、0跳过：41 Domain、345 Contracts、206 Infrastructure、461 Host、281 Integration、224 Windows；六份TRX在`.validation/venue-completion-20261013/office-final-results/`。Framework独立构建零错误警告，新增锁定还原通过；Spec Kit、Redocly及diff通过，API保留三条既有退役2XX告警。前端本轮未改，沿用上一轮474项及真实浏览器证据，不把它当本轮新前端检查。

本轮发现两个正式任务包装器Ready、退出0xC000013A，但七个原PID进程仍为Session1，80/443/18444仍正常监听；不能凭任务状态重复启动。未重启包装器或正式服务，后续部署必须核实际旧组。r21–r26为本轮精确QA任务，证据和私有副本保留；任务勾选仍39/27/12，T038已完成更多模块/正式侧车链路验证，但整组与部署、云台机械、DSP与来源仍未验收。

## 实际WPF播放器双窗验证

r27通过公开PlayerRuntimeHost/PlayerWindow接口执行，未替换PresentationFactory或实际引擎。初次验证在缺私有目录的预期拒绝上停止：Windows的File.GetAttributes抛FileNotFoundException，夹具仅接DirectoryNotFoundException/InvalidOperationException，任务退出1。补夹具异常分支后生成独立r28包；未为测试修改产品的拒绝语义。测试工程位于治理props目录之外，需明确继承产品的WinUI/CoreMrt/PRI排除配置，否则依赖传递触发无效win10 RID；修正仅限忽略的夹具工程，最终构建零警告错误。

r28包SHA256为`F2DB3858589A69B1F7BD697F15E56D3EDE7B5808CE8667AE59CE6EC0390AD2E9`，D4核对后以会话1离屏窗口运行。两轮实际双窗口：四次原件打开均completed、7页、current_slide=1，SampleProgress观测媒体页为4；12个活动WASAPI样本均仅63号RenderID、mute=false、会话volume=0.005且peak>0。窗口1切图片后另一窗口仍有活动非零声音；四个原生句柄退出码0，侧车日志四次native_exit_confirmed和job active=0。缺私有引擎明确拒绝且旧图片视觉对象保持。原件SHA与系统默认RenderID和此前一致，没有修改主音量、原文稿、原安装或DSP。result.json为passed=true，任务最终结果0。

夹具证据保留于`D:/SCP-cv/.validation/venue-player-audio-r28/`，本机归档目录`.validation/venue-completion-20261013/player-audio-r28-evidence/`。r27/r28两个精确任务仅在Ready且原生/侧车/探针残留0后移除，未删证据。正式七个原PID保持Session1，80/443/18444监听与归属不变，未重复启动Ready的正式包装器。

此结果验证实际Player模块、候选拒绝、文稿/图片切换、双窗互不干扰及声音/退出，不等同Supervisor IPC完整运行组、网页/共享完整链路、实体来源选择或现场听感验收。T038/其余12项仍不勾完成。本次未改产品代码，不重复宣称此前1558/474门禁为新的执行结果。

## 2026-10-14：实际网页矩阵与完整包

实际公开PlayerRuntimeHost/PlayerWindow执行双窗网页，使用自有回环页面/0.005幅度技术音轨，没有替换产品工厂、采集桌面或麦克风。r29两窗内容、WASAPI、热切及13个原生句柄退出0均通过，但夹具HTTP循环捕获Dispatcher后在同一线程同步等待清理，成功文件写在清理之前；探针最终120秒超时退出1，不计完整成功。改为无UI依赖的ConfigureAwait(false)循环、异步幂等清理，并把成功文件移至清理之后。r30最终任务0、passed=true、探针/浏览器/28129监听残留0；两份原生PNG核对有真实文字/运动图形而非黑屏，非用户控制台布局或实体墙面视觉证明。

可信启动配置SCP_CV_WEBVIEW_USER_DATA_ROOT接入实际App、普通网页与共享，按两个窗口/用途隔离目录。默认保留旧路径；显式错误路径拒绝，显式配置被SDK覆盖到其他目录则在加载内容前拒绝，InPrivate与授权/首帧门禁不变。14个纯回归先缺实现失败后通过，不修改环境/资料；完整Windows初轮236项，补SDK覆盖守卫及依赖回归后239项。r37不使用SDK目录覆盖，直接用产品配置和新WASAPI依赖重做同矩阵，实际资料目录为本轮evidence/profiles/player-1、player-2，双窗实际声音仅63、切图片/关闭无音、另一窗继续出声、保温重开对象保持；13个原生退出码均0，任务0及passed=true。r29/r30/r37精确任务均在Ready/终态与无原生残留后移除，原证据保留；r30归档已在本机核摘要和截图，r37远端归档SHA256为`249E69C184F595451B70B9E4B0AF2608056105F8108C8FAE8E71E50FBCB13096`。

完整发布r31先被NU1900阻断；官方源与漏洞索引经正常TLS的curl/PowerShell HTTP客户端均200，但NuGet客户端仍失败。原socks5h代理出现明确不支持scheme；仅在子进程改同端点的socks5或HTTP后仍失败，socks5有TLS连接被关闭，未绕过TLS、改OS代理或仓库配置。保留诊断，停止重复猜测；在线漏洞审计仍未完成。使用已缓存包、contentHash和locked restore做隔离构建，不将其称在线审计通过。

r32隔离合包又检出NAudio.dll冲突：Host采用net6.0资产SHA`31D89AB89171978F0BA2581A85F2FB437057702C1C46CC05ECADDD898FE60BB7`，Player采用net6.0-windows7.0资产SHA`C001E49D31497F608B14066B0F10304C0CCB7E3B9DF9D5FEC5CF3196F92961B8`。Player仅使用CoreAudioApi，改直接依赖NAudio.Wasapi2.2.1及Core，中央版本/锁文件同步；实际编译deps图先红后绿，不放宽合并函数。Framework仍用完整NAudio依赖且仅在libreoffice子目录。r38五组件、TFM隔离及VLC插件缓存实际生成成功，967文件290459237字节，tar SHA256`C562AC49C6016B99C1AF041E7035A34BEE30EA84A6B863B6BD30BE178DAAB89C`；是隔离验证包，不是正式发布/部署成功。

最终六份TRX核1573/1573、0跳过：41 Domain、345 Contracts、206 Infrastructure、461 Host、281 Integration、239 Windows，位于`.validation/venue-completion-20261013/package-final-tests/`。缓存锁定还原及Spec Kit/diff通过；本轮前端未改，未重新执行474项或完整控制台浏览器。正式七个原PID、会话1与80/443/18444保持；源码仍未提交推送、常驻未换包。T038实际网页模块验证进展不代替屏幕共享/IPC整组、物理来源或听感，任务保持39/27/12。

r38已上传并安全解包至D4的`D:/SCP-cv/.validation/venue-full-runtime-r38`，归档路径白名单、967文件/290459237字节及四个主要组件SHA核对通过，activated=false；没有启动新组或覆盖正式包/用户数据。r37归档已下载本机核摘要、passed及13个唯一原生退出0。本轮改动代码文件均不超过500行，无Spec Kit扩展后钩子。

## 2026-10-14：真实共享/背景声音整组与Supervisor停机

r39/r40以独立Hardware ControlHost、实际Supervisor/两个Player/Audio及独立数据库执行，不替换管道、receiver、工厂或WASAPI。发布者只发送自有canvas1080P/低幅合成轨，没有采集桌面/麦克风；不替代浏览器手动许可或现场墙面清晰度。真实REST Cookie/CSRF→持久队列→身份租约/IPC→私有原生WebView首帧及HTTP观测链路均执行。两共享与背景声音同时仅63；A的37%峰值约0.00186、100%约0.00513，A静音峰值0而B继续；切图片、重新分享及迟到旧ID停止不影响新源。受理和实际状态分别等待，不记录SDP/cap。

两轮播放矩阵通过但整轮退出失败，不计成功。r40原句柄证据明确：两Player/Audio及21个接收浏览器均退出0，Supervisor27500退出1，触发日志为audio退出故障。Broker只通知Worker，监督者从未获得停止意图；正常成员退出被误认运行故障，不是共享/音轨崩溃。

修复使用当前认证Supervisor连接的Owner/原Group/一次nonce准备：先立本地围栏并确认，Broker最多三秒等待原连接的shutdown_prepared，再通知各Worker。错误nonce/缺确认不放行；无准备的退出0和所有非零退出仍失败，最终核原成员退出码。真实管道准备回归先超时红后绿；补缺确认零字节通知和Office旧通知兼容。BeginDrain实际递增epoch，最初新增测试错误期待不变而红；修正测试与心跳等待边界为恰下一代draining/stopped，不能据此接受成员退出。13纯围栏及4真实管道/心跳用例通过。内部合同与方案同步，无HTTP接口变更。

r42为中间隔离包，未部署。最终r43完整五组件包生成并安全解包D4，tar SHA256`80EEE47891DE8C207B218E2EE21126FAD3066694BE64A576C2C4570984EC9BE2`，Host SHA256`94E475BA0C4121EC23053B356C4E293E914F80C5B0A2835EBF5482EB85A7546A`。r41探针包SHA`C6FE479BE6E9A2C9A1323DE269E8C6350AD8B8A13D137242B3088F2E5C9AF57D`，使用同一矩阵与产品资料根配置；20条阶段观测、passed=true、任务最终0。原句柄证明Supervisor29832/Player27916、27980/Audio30700及22个接收浏览器进程全部退出0。独立Host在成员协作退出后被精确结束，此动作不称Host自然退出。MediaMTX没有参与该独立测试组（共享为直接WebRTC），原正式MediaMTX保持；不把本矩阵称所有媒体长稳验收。

后续Integration283/283、Windows252/252、0跳过通过，TRX在`.validation/venue-completion-20261013/shutdown-final-tests/`；Host全回归另行核最终结果，不混合为本轮六套全量。Spec Kit/diff及改动代码500行门禁通过。在线NuGet审计仍未完成，隔离包使用缓存锁定依赖；不改正式源/系统代理/TLS/主音量。

测试前后正式七原PID/会话1和80/443/18444不变，原两窗经正常认证GET确认idle、DISPLAY2/3；QA只用这两块输出、不改变墙面/设备电源/话筒。r39/r40/r41仅在Ready及自有进程、28131/28133监听残留0后移除任务，所有失败/数据/资料目录保留。工作区未提交推送或换正式常驻，来源API/页面仍禁用；LibreOffice实际IPC声音、来源路由、实体/DSP和正式部署继续验证，任务39/27/12保持。

r41归档SHA256`91875ADE5425135C87510BF550F668CC191103483C5122AFE32C985538894BE5`已下载并在本机`.validation/venue-completion-20261013/full-share-r41-evidence/`核对：20阶段、26个原句柄全退出0。最终Host461/461通过；本轮三套相关全回归为Host461、Integration283、Windows252，其余三套沿用上一轮41/345/206，不把相加1588称本轮六套全执行。测试后钩子未配置，Spec Kit/diff和代码500行门禁通过。

## 2026-10-14：双PPT完整IPC、来源卡片与生产就绪门禁

r44使用r43完整产品包与独立Hardware数据库，真实Cookie/CSRF REST→队列→认证IPC→两个Player→Framework/私有Office。只读原`TestPPT.pptx`7页文稿，SHA256`0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`前后不变；两轮双窗打开，第四页实际WAV、A换自有图片时B继续声音、关闭和完整组停止。自有声音会话限0.005并在无声页恢复原值，不写系统默认或主音量。11阶段passed=true、任务最终0；原句柄16个唯一进程全退出0，原生/job/Office关闭另核日志，不把精确结束测试Host称自然退出。系统默认完整端点ID前后保持。任务仅在Ready及自有进程/回环监听无残留后移除，数据/失败历史保留。归档SHA256`9E7FE1A2337B67CEA5E684CD1294CF1F3AE8C7E078D3E46A696C1EAFA9212A65`下载本机核对通过。

结合实际VLC20项、普通网页r30/r37及真实共享/背景r41，T038实现验收完成并勾选，39项中28完成、11未完成。T033/T034正式常驻激活仍单列，不能称已部署、所有媒体长稳或现场扬声器可听；Mechanical/DSP/来源实体效果保留未确认。

T037新增来源传输独立能力及实时软件门禁，不再一概禁止IP81。GET按source_ip81/source_scp_cv区分；SCP-cv需可信63总线输出、唯一活动RenderID、两窗口私有网页/Office文件及已知Windows声音策略ABI只读预检。直接发送、重发与预设统一门禁，不接受请求里的就绪布尔绕过；预设完整预检先于墙面/机位，首页墙面已发后的音频失败保留阶段事实。按来源→155→滑杆发送，来源保存/回执仅为请求。故障测试发现155的新结果对象会丢掉已发送source，先红后修；后续话筒失败仍返回source/sent_fields/failed_field，保存已发送来源/155，不假存失败滑杆、不重发或回滚。23项相关测试及完整Host466通过。

前端按既有设计tokens拆出小来源卡片，二选一草稿加显式应用，最后请求与实体读回分开。软件未就绪仍可选择IP81，SCP-cv禁用及原因明确；来源应用不重置未应用话筒草稿，预设摘要不再把所有非空来源一概称未绑定。本机r45独立Simulation真实登录/REST/SSE页面只选择零PUT，应用一个`{"source":"ip81"}`且sent_fields只含source，最后请求更新为IP81，手持草稿72保留。390px实际单选标签/按钮44px，页面无横向溢出，截图核对及控制台0错误。Naive原生radio input不可见，普通可见label操作通过，未强制点击隐藏输入。QA页面与两个精确PID/命令/创建时间匹配的独立服务关闭，资料不删除，不连接D4或发实体包。

本轮完整非Physical六套1593/1593、0跳过：Contracts345、Host466、Domain41、Infrastructure206、Integration283、Windows252，TRX在`.validation/venue-completion-20261013/source-all-tests/`；前端476/476、类型检查和pnpm网页构建通过。Redocly合同有效，四个既有警告（退役入口无2XX及旧背景控制oneOf）保留，不新增假2XX来消警；Spec Kit通过。在线NuGet审计仍未完成，使用缓存locked依赖，不改仓库源/系统代理/TLS。pnpm本机入口初始化慢，最终pnpm类型/构建完成；全前端测试直接执行同一Node脚本，不用npm。

完整五组件r46包生成967文件，tar SHA256`D344219569DA898FCF1285E8FA83F73D0A76790C76C1444FE046FD279938892D`，Host SHA256`3E44E2B90218C2A5E859F2E91AB3B261A7E0001088E54FDBCB01D37D446A0398`；D4安全解包/摘要验证，activated=false。r47在已登录会话1用这个真实Hardware Host执行四次认证只读GET：缺输出、64错输出、缺引擎都禁SCP-cv，正确63/私有引擎启用；四次IP81均可发送，拒绝原因非空。不启动Supervisor/Worker、不调用选源/音量/云台/电源API；每个自有只读Host精确结束，无自有组件残留，任务Ready最终0后移除。归档SHA256`962675223358D86DD54A3F75D067B6D5AAF34D4978AEC971E6A92F5D532DF0D5`。正式七原PID及会话1不变，未替换常驻或激活新环境；实体来源路由及云台/话筒实际效果继续单列。

本轮实现拆为两个已核GPG有效的签名提交：`f3fe1d4`运行时声音/原生与监督者停机，`7b50437`现场协议、来源与前端/合同/证据。D4新增私有引擎根属于生成资产，补充精确仓库忽略约定；不删除已验证副本/摘要、不提交第三方数GB二进制。源码快进与常驻换包为不同阶段，后者仍须协作停机/冷备份/同步新Host和Supervisor后激活，禁止把已上传隔离包当作已上线。
