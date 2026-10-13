# LibreOffice文稿运行指南

此页是008接入后的文稿行为说明；与旧PowerPoint/实验性入口文档冲突时，以此页和OpenAPI为准。
历史QA记录保留原结论，不表示新引擎已在D4验收。

## 使用方式

- PPT/PPTX默认“LibreOffice动态放映”，保留引擎支持的动画和内嵌多媒体；仅占窗口1/2绑定的大屏。
- 已登记的PPS/POT/ODP及OOXML放映/模板/宏扩展采用相同原生协议路径；宏执行仍禁止，不增加密码输入。格式允许不代表全部历史样例兼容。新PPT97二进制已有限验证；加密动态拒绝留旧与静态加载/退出分类分开，历史失败见[真实格式记录](../specs/008-libreoffice-playback/verification/legacy-encrypted-formats-20261003.md)，当前加载修复见下项。
- 已知加载交互终止（例如无密码）在无模型/组件/输出并正常引擎退出后报告准备失败；原件保留。加密ODP静态加载这一分支已修，不代表导出/退出不确定都可重试，旧uncertain仍保留，见[加载终止记录](../specs/008-libreoffice-playback/verification/load-rejection-20261003.md)。
- “上一页/下一页/跳页”与“上一步动画/下一步动画”不同。动画步可能推进当前页效果，也可能到相邻页。
- 无主序且当前放映列表没有后继时，下一页/下一步返回末页无操作，控制端依据本次持久命令提示并保持画面；D4两个绑定输出的小窗样例已验证。带动画的末页耗尽保护仍未接完，不凭页码提前禁用，不把该有限验证称最终现场版，见[原生末页记录](../specs/008-libreoffice-playback/verification/native-static-terminal-d4-20261004.md)。
- 动画播放中再点下一步采用“快速完成当前动画后推进下一步”，不是等自然结束或仅跳过当前效果。正式Player两fresh/六轮样例已有限验证；复杂/末页和通用完成反馈仍未验收，见[快速动画记录](../specs/008-libreoffice-playback/verification/native-rapid-effects-20261003.md)。
- 跳页只接受完整十进制正整数，不将`2a`或`1.8`截成页码。API的goto必须提供正整数int32目标；无效值在入队前400拒绝，不默跳第一页。first/last沿已知页数派生，HTTP受理和页码相同都不证明动画结束。
- 页图、静态PDF必须手动选择，缺失或版本不匹配会明确拒绝，不在动态失败后自动宣称播出成功。
- 文字适配默认关闭。在源选择的“演示文稿”分类中开启后，只在内存中让文字适应原框。
  可用于窄标题换行/裁切，但会改变字号/换行，不能保证所有版式完全保真；原件绝不保存或重写。
- 媒体库编辑文稿可明确准备文字适配版静态制品；它们使用独立缓存，不覆盖默认原排版页图。
  在源选择中勾选文字适配后选择静态模式，必须已有对应适配版，不能混用默认版。
- 内嵌媒体按原文稿规则播放。统一暂停/恢复/定位、窗口音量/静音尚不支持，前端不提供假能力，
  后端与播放器在动作前拒绝。系统音量是独立的全机功能，不等同某份文稿的媒体暂停。

PDF原源继续由PDF适配器逐页渲染。新PPT派生PDF从PNG生成，只含静态图片，不含媒体附件、脚本或活动注释；
这不是可编辑PDF，不保留动画、媒体、可搜索文本或超链接。

## 原件与缓存

原件路径仍按用户目录保存，例如`media/a.pptx`、`media/PPT文件/b.pptx`；不恢复日期目录。
每次转换用独立空暂存目录，制品含连续`page-0001.png`等和`presentation.pdf`。
原件摘要/修订、job和布局类型共同关联缓存。转换摘要覆盖PNG序列和PDF，发布前再次核原件摘要。

播放/转换profile统一使用`TMP/SCP-cv/presentations/<引擎GUID>/`，不随用户媒体目录深度增长，也不复用旧实例。
这是Office临时配置/扩展缓存，不改变原件目录；开发期保留以便诊断。根路径超过120字符会在启动前明确拒绝，
请将TMP/TEMP设为短的本机目录；120是保守工程预算，不是所有LibreOffice文件的通用上限。

仅本次自有profile预置官方MAR自动更新关闭，避免独立播放/转换实例在更新查询的收尾join中阻塞。
不修改安装或用户LibreOffice更新策略，也不伪造初始化完成。继承`LIBO_UPDATER_TEST_ENABLE`强制开启覆盖时，
启动前明确拒绝；请从项目启动环境移除该测试覆盖，不使用假更新地址作为运行依赖。此项不等于完整网络沙箱。

实际源尚未确认关闭时，即使投影为“空闲/错误”，也阻止原件或所在目录的移动/删除。
`uncertain`不等同未执行，后台和显式准备都不能盲重放；先核自有实例、暂存区及文件占用。

## 安装与打包

已验证开发机为Windows x64、LibreOffice26.8.0.3，官方CLI/GAC组件及offapi类型库完整。
主服务使用.NET10；官方旧混合CLI绑定使用Framework4.8侧车，不在.NET10进程直接加载UNO代理。
构建机需Framework4.8 targeting pack；运行机需Framework4.8运行时与LibreOffice官方.NET互操作组件。

本地路径覆盖仅用环境变量：

```powershell
$env:SCP_CV_LIBREOFFICE_PROGRAM = 'C:\Program Files\LibreOffice\program'
$env:SCP_CV_LIBREOFFICE_HOST = '<Framework宿主绝对路径>\ScpCv.LibreOfficeHost.exe'
```

发布时将Framework宿主、Net48协议及全部托管依赖放到运行根的`libreoffice/`子目录，
不要把Net48协议DLL覆盖到.NET10运行根。发现顺序为显式环境变量、独立子目录、直达路径和已知开发输出。

2026-10-13工作区加入声音绑定：该子目录还需包含NAudio及其对应Framework依赖，不能只复制exe/协议两文件。可信`SCP_CV_AUDIO_OUTPUT_DEVICE_ID`与`SCP_CV_PRIVATE_AUDIO_ENGINE_ROOT`明确配置时，两个Player选择各自私有Office；converter仍沿原安装路径，不参与声音暖机。侧车启动前核端点和私有映像路径，用户原件加载前核真实声音流，缺失不回退默认设备。已停止的零音暖机资源统一留到独占shutdown，关闭中丢失UNO回复必须用原始进程正常退出0、job空与监听关闭共同证明。D4正式客户端/宿主双文稿有限重复通过，常驻未更新，见[接续验证](../specs/012-directory-camera-console/verification/20261013-venue-followup.md)。
设置页“静态检查通过”来自同次只读检查：必需文件非空可读、Windows/x64、Framework Full/64位CLR、
宿主与Net48协议映像/协议版本，以及五类官方CLI的身份、架构和引用绑定；不要求SDK或targeting pack。
逐项原因与建议不返回配置值或磁盘路径。缺失、不兼容或绑定配置尚未确认时明确报告，不启动Office探测。
静态检查不等于完整CLR/原生DLL加载、UNO连接、具体文稿兼容或真实出画，也不验证安装文件的加密签名。
读取失败/无效响应或切换播放主机时会清除旧检查，保持“待检查”，不能把旧主机通过结果用于新主机。

仓库提供只生成本机包的入口，不包含部署或启动步骤：

```powershell
powershell -File runtime-dotnet/scripts/publish-runtime.ps1 -CheckDependencies
powershell -File runtime-dotnet/scripts/publish-runtime.ps1 -OutputDirectory runtime-dotnet/artifacts/runtime-new
```

`-CheckDependencies`只检查SDK入口、Framework4.8 targeting pack、LibreOffice文件及全部五类官方CLI绑定。
输出目录必须是新目录；四个.NET10程序平铺在发布根，LibreOfficeHost及Net48协议仅在`libreoffice/`。
各组件先独立发布，同路径依赖只有SHA256相同才复用，不允许后发布的组件悄悄覆盖前一个。
失败保留本轮部分包并返回错误，不把它标成可交付；本轮GUID临时构建目录会精确清理。
发布沿项目自身RID与锁，x64可执行宿主单独选择；不要附加全局`-r win-x64`改变共享库锁定还原图。
本机已实际生成包并验证ControlHost的Simulation启动、认证与SSE；其它发布组件及完整原生包仍待验证。
2026-10-03最新包已生成并核五组件x64/TFM隔离、16只读依赖；正式Player小窗口调用最新包侧车的原版两步及正常退出通过，图片仍有底部黑带。该测试不是整个发布App/两输出运行组；本机当前仅一个主输出，完整包/满幅/长稳未验收。
MediaMTX程序及配置不随五组件发布；使用推拉流时，需按既有启动参数明确提供`-MediaMtxPath`及其配置，不将未配置的可选进程称已启动。

该包采用框架依赖式发布。运行机仍需.NET10 Desktop Runtime与ASP.NET Core Runtime、Framework4.8、LibreOffice x64及官方.NET互操作组件，
并按其它媒体需求安装WebView2运行时；构建机的GAC/LibreOffice文件不会被复制到包中。
前端、MediaMTX及配置另行准备；DataRoot、媒体、凭据、日志、数据库不会混入发布包。
使用包内`run-headless.ps1`时明确传`-RuntimeRoot`发布根、`-ControlHostPath`和`-SupervisorExecutable`；
仍需独立DataRoot/认证配置与已保存的两块大屏绑定，打包完成本身不授权启动D4。

## 生命周期与故障

两Player仍是Display命令唯一所有者；每份候选原件使用独立侧车/profile，候选未成功前不覆盖旧输出。
侧车核OS实际父PID/start/session，先加入父进程独占job，再放行Office启动；Office暂停创建并加入独占job。
只连接属于自有job的回环监听，不复用用户LibreOffice或旧profile。

正常关闭顺序为Office主线程异步结束放映、读回结束、关闭文稿、terminate、自有进程和监听退出。
直接跨线程结束曾暴露窗口销毁锁等待，不能以延长超时或跳过结束调用掩盖。
wire相邻整页与Player共同走UI异步goto并核实际页码，首末页钳制到有效范围；效果步是另一条路径，不能由页码相同推断完成。
job成员计数可能因已终止进程的句柄引用暂留；必须核真实退出，不能只看计数或只看launcher退出。
宿主退出后，自有console helper等子成员可能短暂仍活；客户端在同一15秒预算内等待完整job。
预算没有重新计时或扩大，超时仍为不确定并仅清自有job；不能把兜底后的消失改成正常退出成功。

已开始传输的超时、取消或断线将通信隔离，不能把下一条响应当旧命令完成。兜底只清自有job/进程树，
强清会记录“不确定/正常退出未确认”，不返回虚假成功；禁止按soffice/Office名称全局结束。
请求deadline也覆盖前一条命令的排队等待；未下发的过期/取消不等于原生执行不确定，也不能污染仍健康的在途操作。
关闭在途、失败及取消终态会被记住；重复关闭不能擦除首次失败、重复shutdown或充当新退出证明。
正常文稿关闭的shutdown、自有进程退出和容器释放按原顺序全部尝试；单失败保留原原因，多阶段失败保Aggregate且仍不确定，不能只按最后一个Win32/IO错误判安全失败。此保护也覆盖重复CLOSE和Host退出，见[正常文稿关闭诊断](../specs/008-libreoffice-playback/verification/published-close-diagnostic-20261003.md)。
这一门禁也覆盖外层Player/App退出：资源真正关闭前不清画面，关闭失败退出码为1。
已经下发的文稿传输故障在持久命令中保持uncertain，最后实际源与布局不会因此被假清空；
纯包提前拒绝仍是failed。断管或兜底结束不是正常关闭证明，不授权自动盲重放。
候选OPEN或生产转换失败后，若自有退出也失败，保留动作及清理两原因并仍视为uncertain；不能只根据最后一个IO错误称安全失败。正常退出保持原异常或Failed响应，不重放。Aggregate是本地诊断链，并非新增API堆栈字段，见[创建与转换诊断记录](../specs/008-libreoffice-playback/verification/creation-cleanup-diagnostic-20261003.md)。
后台只读观测独立于媒体时钟，LibreOffice时间/总时长不可得时为null。

会话的`actual_fit_text`只表示已发布实际资源的布局选择：true为适配、false为原排版、null为未知/非文稿。
“重新打开当前画面源”沿用实际源与布局，失败换源不会把新期望布局套到旧画面，明确实际源为空时禁用。
控制页监看与专注页页图也沿实际源、布局和已观测代次读取，不把失败的期望新源当当前文稿。
原排版与文字适配预览不借图；未知实际布局显示未确认，预览未准备不等于动态原件不能播放。
“刷新预览”只重读当前布局的静态资源，不发送原生OPEN或换页，也不扩大专注页八次自动补齐预算。
列表默认缩略图只来自有效原排版制品，历史页资源中保存的外链或错源地址不会作为缩略图返回。
两预览GET采用单值`fit_text=true/false`（缺省false），非法/重复值400；所选制品缺失或过期时资源图片为空、PNG404。
检查包含摘要/修订、布局声明、连续页数、受管理目录/重解析点及文件长度/PNG签名，不承诺完整解码或校验后的原子文件快照。
丢页后的明确prepare会重新排队；运行中或结果不确定的旧作业仍禁止盲重试。
本机深度诊断可设`SCP_CV_PRESENTATION_DIAGNOSTICS=1`记录Frame尺寸和原生调用边界；默认不做额外几何采样。
该开关也注册仅自有文稿的官方动画listener，回调只写128条有界纯值缓冲，主线程排空后记录丢失及单调时序。
事件窗口标签不是命令因果/完成，自动动画与撤销重播可有同类事件；返回成功、页码相同或固定采样等待都不证明动画视觉已结束。
会话pending包含排队及执行中的最早命令，但不报告动画阶段；成功返回次数不等于效果步数。主序内存空尾已使基础快进后推进通过；撤销残留旧报告已证实图片误读并撤回，复杂序列及动画末页黑屏仍未验收。
`SCP_CV_PRESENTATION_NODE_DIAGNOSTICS=1`在既有诊断开启时额外关联只读动画模型标签；默认关闭，不启动第二引擎、不保存原件，也不作为生产完成状态。
仅诊断开启时增加同步listener移除/日志开销，可能影响时序；末批并非所有晚callback最终账，不能靠它宣称无丢事件或无原生风险。

旧`/api/settings/powerpoint/`与`/api/playback/reset-ppt/`返回410；旧Office管道请求明确退役，零转发。
历史PowerPointHost策略类只供审计回归，其程序入口不再运行COM。

## 验证边界

2026-10-05后续D4默认轻量listener路径已有限核对普通七页、多页跳页/重开、隐藏尾/手动隐藏末页、零叶子时长和原末PAR重复。
重复组未耗尽时仍能继续动画，不按两次点击后的图片判断完成；已确认耗尽后才比较保持图。
重复页序拒绝的旧输出保持实机负例也通过，完整并行/自动序列/长稳仍欠，见[多模式记录](../specs/008-libreoffice-playback/verification/terminal-matrix-20261005.md)。

2026-10-05动画末页的原生事实模块已接入：实际无后继且本轮原末组已开始/结束时返回noop，
不resize/goto/继续效果；倒退/跳页换轮、丢事件撤销旧证明。D4单页三轮30noop/保持/撤销/正常关闭通过，
完整文稿/隐藏与重复/自动序列、两窗及最终组尚欠，不能称末页全范围已完成，见[本轮记录](../specs/008-libreoffice-playback/verification/animated-terminal-evidence-20261005.md)。
自定义动态放映重复引用同一页目前明确拒绝，以免书签导航选错occurrence或共享保持属性；
可使用不重复页序，或手动静态页图/PDF（无动画/媒体，按标准文稿页序）。这不是一概禁用叶子/并行重复动画。

2026-10-05起，非空主动画SEQ的RepeatCount>=2或无限重复在原生放映启动前明确拒绝，
因为当前固定版本已复现第二轮越界崩溃。普通动画与叶子/并行组重复不被一概禁用，原件保留，
不静默删动画或自动降级；可在选源面板手动选择静态逐页图片/PDF（无动画/媒体）。
具体原因随失败返回，候选清理失败仍不确定，见[安全限制实测](../specs/008-libreoffice-playback/verification/repeat-safety-and-source-picker-20261005.md)。

2026-10-03用户已明确末页且动画耗尽后下一步保持最后画面、仅提示“当前已是最后一页”，并重新授权D4窗口1/2软件测试及相关环境安装。当前v3 noop协议、Player/精确持久命令反馈及无主序原生终点已部分接通；有主序耗尽保护仍未完成，不能称该规则完整交付，旧v2包不可与源码v3混用。
D4已安装匹配本机的LibreOffice26.8.0.3，交互清单和只读库确认窗口1/2绑定DISPLAY2/3，控制用DISPLAY1非primary；尚未更新当前应用/启动新运行组。历史“D4暂停/未访问”段落保留为原轮结论，现行范围和剩余门禁见[末页候选与D4记录](../specs/008-libreoffice-playback/verification/terminal-hold-candidate-20261003.md)。

本机已验证Frame承载、正常关闭、真实转换、两窗同/异源开关、损坏候选留旧画面、桌面/手机设置与静态选择。
具体计数与失败证据见[008验证记录](../specs/008-libreoffice-playback/verification.md)。
不把页码/“受理成功”当视频解码或动画画面完成；字体、外链/加密/旧PPT和所有媒体行为需按样本验证。
样例视频已在正式Player小窗取得后期帧变化、播放中关闭/换源与正常退出的有限证据，媒体时钟和音频仍未测。WPF图的35px底部黑带粗估已按原PNG更正为仅2行全黑；这不是几何修复或完整显示验收，见[视频与原图核对记录](../specs/008-libreoffice-playback/verification/native-video-close-20261003.md)。
独立普通只读Office文稿已有本机共存/项目精确父异常的有限证据，项目退出后仍保持第2页、内容和窗口，随后由测试归属者关闭；不触碰真实用户默认profile或未保存编辑，也不能宣称所有Office版本稳定，见[共存记录](../specs/008-libreoffice-playback/verification/office-coexistence-20261003.md)。
当前发现TestPPT原生页面底部裁切，及间歇启动/退出确认失败；修复门禁未全部通过，不能将本版称为最终可交付播放器。
2026-10-03真实生产转换在中文目录移动、原/适配双制品、PNG/PDF关联及关闭后文件锁释放上通过；
静态制品没有原生窗底部黑条。默认AllinOne第5/6页仍有旧LibreOffice窄标题重排，不承诺原排版完全保真。
显式适配版PNG/PDF已消除上述两处标题问题，但部分正文换行改变；默认关闭的选择保持不变。
当前拒绝OOXML外部关系，禁止宏执行和自动更新外链；自动加载的交互handler只选择终止，不批准修复、密码或保存。禁止宏执行不等于按.pptm后缀拒绝所有文稿，真实宏/加密样例仍待测。
共享纯包预检在引擎启动/候选创建前完成；六OOXML声明扩展及可解析ZIP内演示包内容用同一门禁，不靠改名绕过。非法关系模式、绝对内部引用、异常页引用和XML DTD拒绝；非OOXML旧格式保持既有路由，不假装经过OOXML检查。见[内容门禁与新包记录](../specs/008-libreoffice-playback/verification/package-content-gate-20261003.md)。
这类已知拒绝记为失败并保留原件，不能与原生执行后的不确定混为一谈；旧输出或原生退出未确认仍禁止盲重试。
D4已按新授权进行独立QA软件实测，应用源码尚未同步、完整运行组仍停止；电源、未知墙面映射和系统音量未操作。重复主序在末页保持候选与自然End对照均有原生访问冲突，两份转储及官方符号结果见[故障栈记录](../specs/008-libreoffice-playback/verification/terminal-repeat-native-stack-20261004.md)，取得故障栈不等于修复或完成长稳。
