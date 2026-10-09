# LibreOffice运行时已知坑

本文件承接[副作用路径与早期记录](../known-pitfalls.md)的§2。历史编号/证据保留，未解决项不改成完成。

### 坑 51 - 失败换源后期望源与实际源不同，重开丢布局（2026-10-03 本机修复）

- **症状/根因**：当前画面仍是源40适配版，新源41打开失败；“重开”却POST期望源41并默认fit=false，HTTP又未报告实际布局。
- **现状**：可空`actual_fit_text`跟随成功资源发布与fenced实际观测；失败留旧、关闭/非文稿清空、未知不推断。重开明确沿当前画面源/adapter/布局，actual=null禁用，字段完全缺失才兼容旧客户端。隐藏STA2/2与认证HTTP8/8红→绿，5个真实DOM红→绿；不能据此声称原生布局已满幅。

### 坑 52 - 两个TFM的同名协议DLL不能平铺覆盖（2026-10-03 打包接线）

- **症状/根因**：Net10父进程与Net48官方绑定侧车使用同名协议程序集；一个输出根盲覆盖会破坏运行时绑定，源码build绿不证明发布包能运行。
- **现状**：新包只用新目录、Net48隔离libreoffice/，同名Net10文件核SHA一致才复用；依赖不全明确失败，无部署/启动。六项合同绿，实际包运行待验证。摘要改内置流式SHA256，避免PS5错误继承PS7模块后缺Get-FileHash；未修改系统模块或配置。

### 坑 53 - 外层Frame满幅、视频已动仍可能裁切页面（2026-10-03 未解决）

- **症状/证据**：TestPPT原生窗底部黑条，原件/PNG/PDF背景全白；Frame/container/component读数800×450且视频真实变化仍不能排除内部放映裁切。
- **现状**：对照固定源码，内部ShowWindow尺寸依赖私有client-area缓存；仅是候选。doLayout、实际高度往返、可见且移出自有客户区的候选父窗三个单变量均未修复，实验已撤回，不以隐藏告警或扩大窗口盖住裁切。停止叠加同类修补，讨论承载架构后继续008 T004，最终60分钟尚未通过。

### 坑 54 - 首次关闭失败后，重复Dispose误成成功（2026-10-03 本机修复）

- **症状/根因**：Client和Surface先设disposed再退出；首次未取得正常退出证明抛错，第二次提前return，Host随后可清空实际源并Completed。
- **现状**：在外部关闭之前先发布同一完成Task，成功、失败、取消及在途均共享终态；同步抛错也缓存，绝不重放shutdown。纯及真实自有PowerShell子进程2红→5绿，非实体737/737。原生关闭失败根因仍未解决，不能把记住失败当原生退出成功。

### 坑 55 - publish全局RID改变引用图而与锁冲突（2026-10-03 本机修复）

- **症状**：发布脚本合同绿，真实publish第一步NU1004，ControlHost和共享库锁没有全局win-x64图。
- **现状**：沿项目既有RID，仅以DefaultAppHostRuntimeIdentifier选择x64宿主，保留LockedMode；7项合同及真实完整包绿，五EXE为AMD64/PE32+，Net10/Net48协议独立。ControlHost的Simulation新根启动和认证/SSE证明了该组件，不证明Player/Audio/LO/MediaMTX整体可用。

### 坑 56 - 实际请求沿当前主机，诊断仍显示构建目标（2026-10-03 本机修复）

- **症状**：当前主机20881已登录/SSE在线，设置品牌/开发摘要仍REST8000；原端点列表也未反映实际目标。
- **现状**：端口与诊断API地址沿实际连接解析器；7项公开/真实DOM红→绿，桌面1440/手机390实际摘要和六链接均20881且无横向溢出。未改主机信任/认证策略；生产dist不因preview环境变量而改变API基址，测试先通过/connect配置实际本机origin。

### 坑 57 - Framework的PID对象不等于可读终态的已持有句柄（2026-10-03 本机修复）

- **症状/根因**：启动器早退时新增ExitCode诊断再次抛“不是由此对象启动”；GetProcessById只有PID，HasExited临时开句柄不能满足Framework的ExitCode要求，原生失败被掩盖。
- **现状**：暂停创建、自有job归属后，在ResumeThread前缓存Process.Handle，退出后保留准确对象终态。真实Framework自有cmd exit7先失败后成功、正式进程回归通过；不是放宽ready门禁或把launcher退出当引擎正常退出。

### 坑 58 - 引擎profile根不长，内部扩展目录却可能过深（2026-10-03 本机限定修复）

- **症状/证据**：转换profile根154字符，初始化内部backenddb.xml已267，bundled预期更长；文稿未打开前fatal退出，dump为subcode7 abort，非此前MFPlay subcodeA。不能断言所有超过260的路径都失败，也未取得完整C++异常文本。
- **现状**：仅改变profile所在根即可ready及正常关闭，沿播放已有88字符独占临时目录；恢复原深数据根后9/7页原/适配四制品通过。profile根预算120、异常TMP/TEMP提前拒绝；原件/制品仍在DataRoot，原失败/库/dump保留。不扩大为所有间歇启动风险已解决。

### 坑 59 - 静态PDF畸形元数据被误报为页图未准备（2026-10-03 本机修复）

- **症状/根因**：PDF路径数值/对象异常进入共同异常兜底，反馈页图未准备；此前已捕获，实际不是HTTP500。
- **现状**：对象类型门禁及TryGet数值转换后，末尾错误沿用户所选PDF/页图；不静默改动态、不过度约束历史摘要。29选择器+8认证HTTP回归，红例17/4后全绿。

### 坑 60 - 请求deadline只算IO，不算发送锁排队（2026-10-03 本机修复）

- **症状/根因**：A在途时B的300ms deadline已到却仍等待；A返回后才给CancelAfter负预算，抛参数异常。仅caller token约束排队，绝对deadline漏入该阶段。
- **现状**：预算覆盖排队与IO，取得锁后再次核期限；未下发取消不poison，在途健康A和后续C仍可成功。真实stdio先红后绿；半帧取消/迟到/错误身份/截断/并发关闭共6项绿，不等于UNO原生生命周期全部通过。

### 坑 61 - 未列入解决方案的测试fixture混用构建配置（2026-10-03 本机修复）

- **症状/根因**：单项目Release6项绿，整解Release却构建未注册引用为Debug，复制目标仍取旧Release，新的接收标记不存在导致2失败。
- **现状**：fixture纳入tests及六配置映射，按实际TargetPath复制；默认测试不启动Office。旧失败保留，未增加等待预算或允许没有接收证据。

### 坑 62 - 宿主已退出不等于所有job子成员同一瞬间退出（2026-10-03 本机修复）

- **症状/证据**：旧cmd即时断言偶发失败。4096次16并发复现4次，launcher与存活成员PID不同；同句柄wait=258/exit=259/in_job=true，一例随后名称为conhost。不是靠job计数或假定PID复用得出的结论。
- **根因/现状**：Client只等待宿主退出后即检查成员并报错，没有使用剩余退出预算。改在同一15秒预算内轮询完整job；门禁子成员先红后绿，30秒负例15秒仍uncertain、精确兜底、重复失败保留。测试同时观察宿主退出0/子成员仍活/关闭尚未完成，再主动放行；没有扩大预算或改存活判定。
- **QA陷阱**：普通Process.Start夹具的继承句柄曾令WaitForExitAsync等待stdio EOF而假绿；仅继承NUL后才真正暴露宿主先退出，原误判记录保留。此类修复不代替持文稿/媒体退出和全部间歇风险验收。

### 坑 63 - 只拒External字面、只核URI Host不等于关系安全（2026-10-03 本机修复）

- **症状/根因**：TargetMode=true放行但LO按非Internal处理；https/ftp同host的绝对内部页被映成ZIP本地页。门禁解释与原输入语义不同，不等于已证明实际联网。
- **现状**：纯ZIP/XML同源预检拒非Internal或绝对关系，页引用另拒query/fragment；真实文件1/3红后16绿。已知包拒绝移至引擎/候选前，记Failed；不吞加载后/原生退出的不确定。三类真实上传/作业拒绝保留原件，正常两样例原/fit静态制品与旧完全一致；详见008包门禁记录。

### 坑 64 - Worker通用catch擦除原生不确定分类（2026-10-03 本机修复）

- **根因/检出**：已下发的typed transport故障被改为failed；真实管道→持久终态先红，五类分类及丢ACK重放覆盖。
- **现状**：PresentationTransportException保持uncertain，包/普通IO拒绝仍failed；Actual/布局/cleanup保留，不重放原生执行。

### 坑 65 - 外层退出绕过内层关闭终态（2026-10-03 本机修复）

- **根因/检出**：Host清屏或App正常退出早于真实资源关闭，重复失败被忽略；隐藏STA/外部适配器四类红例。
- **现状**：外层共享一次真实终态，关闭成功前保留画面，失败code=1；在途命令拒绝、迟到采样null。六新+四旧绿，不替代实际App/UNO退出。

### 坑 66 - Broker停止返回时fire-and-forget事务仍在展开（2026-10-03 本机修复）

- **根因/检出**：Stop只等accept循环；真实SQLite取消门禁显示dispatch仍借库，导致夹具清理busy。
- **现状**：执行前登记自有连接/dispatch，Stop等待全部finally；取消等待不丢任务，四组合绿。不清全局池、不延时掩盖；同角色重连另由T048验证。

### 坑 67 - 上传并打开漏模式，重试再次登记原件（2026-10-03 本机修复）

- **根因/检出**：上传路径未复用普通选源选项，OPEN失败再次上传；真实DOM先红，IAB账本核请求。
- **现状**：点击快照模式/适配，只PPT原件带选项；保留已登记源显式重试OPEN。浏览器upload1/open2→动态open3，HTTP模拟不当Native验收。

### 坑 68 - 新hello和发送失败都能绕过旧连接展开（2026-10-03 本机修复）

- **根因/检出**：重连直接Dispose旧sendGate；失败Wake提前摘slot；同instance旧finally可误删新Ready。真COMMIT后门禁两类红，不以SQLite写锁等待假绿。
- **现状**：精确连接一次退休，IO取消后等全部dispatch/发送再释放gate、摘slot；Ready按connection裁决，失败通知仅请求退休。两交错+512发送积压三项绿，通知返回不证明送达或进程退出；其它并发/握手超时边界不据此泛化。

### 坑 69 - 制品按布局发布，预览仍固定原版或信历史row（2026-10-03 本机修复）

- **根因/检出**：fit-only无持久页行，GET为空；PNG缺页/页数漂移仍放行，列表可回历史外链，repair却复用旧成功。五类公开HTTP先红，布局声明冲突另四红。
- **现状**：共用明确root/布局及完整页清单门禁，列表canonical图、丢页显式再准备；49项后端绿。目录junction实测、file-symlink缺权限未测；签名不当完整解码。详见008预览记录。

### 坑 70 - 实际画面与预览用不同源，测试又把波纹当轮询（2026-10-03 本机修复）

- **根因/检出**：组件按desired取图/标题，未知fit借默认图；同源重开和旧poll缺围栏。真实DOM及浏览器先红。
- **现状**：实际源/布局/adapter/窗口/观测代次共用loader；纯GET刷新不重开、不增8次预算，前端122绿。首121/122为Naive1000ms Wave误计，改公开GET判据，未改生产预算或忽略失败。

### 坑 71 - 两文件存在、配置可读不等于运行依赖就绪（2026-10-03 本机修复）

- **根因/检出**：仅host+uno.ini为真，缺soffice/CLI仍installed；TFM/入口/协议常量或配置限定被忽略同样假绿。公开HTTP与只读PE/XML红例逐项检出。
- **现状**：固定必需清单、平台/Framework/映像与五CLI引用绑定只读检查；不支持的绑定规则未确认，不执行Office。不能扩大为完整CLR/native加载、安装签名或出画证明，详见008依赖记录。

### 坑 72 - 诊断失败或换主机，仍展示旧的通过结果（2026-10-03 本机修复）

- **根因/检出**：请求失败未清快照、迟到响应未围栏，完整HTTP200但success=false也被采用；真实组件DOM先红。
- **现状**：新检查先清旧结果、严格合同与目标代次/卸载围栏，旧finally不解除新pending；27条DOM及桌面/390夹具验证，失败保持待检查，不自动重放GET。

### 坑 73 - 不完整跳页目标被默认第一页替代（2026-10-03 本机修复）

HTTP缺goto目标仍200，UI parseInt把2a/1.8截成2/1，两真实入口先红；服务入队前必需正int32、UI完整十进制文本，15HTTP/15DOM保护。first/last不误改，受理/同页码不当动画完成；原生启动失败与并行内存不足另保留，详见008导航记录。

**坑 74 - 原生更新join与测试启动器假绿**（2026-10-03）：私有符号转储定位更新join，假URL单变量改善后，仅新profile关闭MAR/拒强制覆盖；四纯回归、三新实例和四转换通过。放行后另有视频wire上一页阻塞，UI goto有限复测绿；QA只打印父exit仍返0已修并保留旧失败，不称动画/裁切/长稳完成。见008更新隔离记录。后续动画调查实际1.45ms返回、1055.6ms主序结束，首跨页即时断言失败保留；窗口/事件数/等待均不当视觉完成，见008事件诊断记录。

**坑 75 - 领取后“已受理”提前消失**（2026-10-03）：pending查询漏Processing，领取后空、状态报告跳后项、执行中新入队遮头三类公开HTTP先红。统一投影最早Pending/Processing并合并新实体/过滤tracked终态，15回归含重复结果、替换和三目标隔离通过；空值不是动画结束。快速三次效果仍停原页的缺口不随投影修复消失，见[执行中投影记录](../../specs/008-libreoffice-playback/verification/unfinished-command-20261003.md)。

**坑 76 - 图片误读也必须反查原始证据**（2026-10-03更正）：初次正式撤销图曾被误报副标题残留，重新逐张读图、区域像素与完整PNG摘要确认原图实际上只保留标题，旧报告撤回，不为它加重绘/延时补丁。原图未覆盖、时间与日志一致；ON/OFF四轮像素门禁通过。页码仍不等于画面：单页自定义放映另真实复现末页进入黑屏而仍报告页码1，T053/裁切/长稳继续未完成。见[撤销与末页记录](../../specs/008-libreoffice-playback/verification/rewind-boundary-20261003.md)。

**坑 77 - 一起卸载通知store让迟到错误测试假绿**（2026-10-03修复）：新建Pinia的首夹具误绿；保留应用共享store再卸载真实动画组件，两页面均出现旧HTTP错误，先红。组件scope退休后只忽略旧通知，不改命令受理/取消/重放；10相关、全前端174及真实回环旧错误忽略/当前错误保留通过。不是同实例切窗口/源或全部异常矩阵证明，见[生命周期记录](../../specs/008-libreoffice-playback/verification/cold-click-lifecycle-20261003.md)。

**坑 78 - 整窗截图差异不能直接判播出串扰**（2026-10-03有限门禁）：两个双窗首轮整PNG摘要失败，但B标题/字幕不变、中部像素差异0，标题活动态与近边缘微色差另存；给A发goto不能稳定归一，已撤回。整窗/客户区差异保留，最终中部内容门禁不是原整图门禁已修或满幅验收，不改正式几何。两角色各100控制/20源切换及正常job退出通过，详见[两实例记录](../../specs/008-libreoffice-playback/verification/dual-isolation-20261003.md)。

**坑 79 - 清理第二异常覆盖已下发的不确定结果**（2026-10-03修复）：旧文稿关闭transport异常后直接await候选清理，后者普通IO使throw不可达，Worker误降failed；公开Player真红复现。双失败保留Aggregate两原因并typed uncertain，候选清理成功原异常保持，未改全局普通IO分类；13生命周期/6持久分类与完整993通过。隐藏树/旧实际身份不是旧物理画面或UNO正常退出证明，见[切源双清理记录](../../specs/008-libreoffice-playback/verification/replacement-cleanup-20261003.md)。

**坑 80 - 窗口切入失败却提前发布playing**（2026-10-03修复）：_current/源/状态在Activate前提交，公开红例异常后采样仍source202/playing；候选清理失败又失去归属。改切入后发布、明确空输出或隔离未知候选，已确认无输出NEXT也不假completed；前端按钮/专注键盘真红保护。内部None用none而HTTP空串，QA错断言不改合同；1002后端/177前端与IAB有限通过，不称Office/满幅/长稳验收，见[切入记录](../../specs/008-libreoffice-playback/verification/activation-publication-20261003.md)。

**坑 81 - 普通HTTP成功不证明SDK NuGet审计可用**（2026-10-03诊断）：curl/HttpClient可读官方index，SDK NuGet仍NU1900，换audit端点/禁缓存/禁构建复用无改善；真实SDK内层NotSupportedException定位socks5h代理scheme。仅本轮构建进程暂用socks5、原地址/审计/TLS/锁不变后成功并恢复环境，不永久改源或禁告警。新包与正式Player短轮不是双输出/满幅验收，单主输出条件保持；原WPF黑带35px粗估后续撤回为2行，见[本轮记录](../../specs/008-libreoffice-playback/verification/native-player-package-20261003.md)。

**坑 82 - 工具中的黑带观感不能当原图像素尺寸**（2026-10-03更正）：T058四张WPF图及T059视频图被粗估35px/约一成，原PNG只读RGBA统计实际底部全宽纯黑仅2行，中部y420白；两关闭图才360000像素全黑。原图/摘要未改，撤回厚度判断而非修播放器，工具呈现原因仍未知。不用这个更正抹去旧WinForms或判满幅通过，见[视频与量化记录](../../specs/008-libreoffice-playback/verification/native-video-close-20261003.md)。

**坑 83 - 动画跨页的首采样不是同步完成回执**（2026-10-03有限调查）：实际Player三next返回后首页6，复用固定页Observe立即失败并关闭，没留下后续证据；同1.5秒QA窗口只采样、不补发，两fresh各三轮均下一样本页7（约0.29秒）。原exit2保留，未改生产合同/预算或称点击丢失已修，不从该时刻推GPU完成，见[快速动画记录](../../specs/008-libreoffice-playback/verification/native-rapid-effects-20261003.md)。

**坑 84 - 最后一帧之后注入不等于CLOSE之前**（2026-10-03验证门禁）：两个共存夹具在video-later-2写出后结束精确父，stderr却已有close_enter，原证据只能覆盖此较窄时序。仅QA增加明示CLOSE前有界stdin门禁，第三fresh视频真帧后注入、项目Office退出而普通文稿仍可用；不改产品预算或将异常job退出称正常关闭，也不以第二播放器充当用户式Office，见[共存记录](../../specs/008-libreoffice-playback/verification/office-coexistence-20261003.md)。

**坑 85 - 接受多种扩展却只对.pptx做包预检**（2026-10-03修复）：.ppsx和同包改名.ppt均公开转换越过外链预检到宿主启动，缺Host安全红保留；共享按可解析ZIP演示内容/六OOXML声明扩展检查后11转换/3Player绿，26包与完整1026通过。非OOXML旧格式保原路、宏禁执行不称全部宏/加密预检拒绝；原版/fit36制品完全一致，新200bc0包更新，旧9ce543只历史，见[内容门禁记录](../../specs/008-libreoffice-playback/verification/package-content-gate-20261003.md)。

**坑 86 - 媒体登记格式与wire白名单漏接、加载拒绝不等于退出证明**（2026-10-03部分修复）：ODP候选在下发前invalid_source_path，随后Dispose超时掩盖原错误，不能据只有launch日志当密码挂死；18公开红绿对齐10格式/1044回归及真实动态拒绝留旧通过。夹具null误判、自锁、stage越界单独保留。加密ODP静态加载null仍使export宿主exit2/作业uncertain，T065待修，不能借job兜底假正常；新64fbc8包更新格式，见[真实格式记录](../../specs/008-libreoffice-playback/verification/legacy-encrypted-formats-20261003.md)。

**坑 87 - 静态加载被终止却当导出原生故障退出**（2026-10-03有限修复）：实际加密ODP加载null抛普通异常、宿主只catch open，Converterexit2/uncertain真Physical红；加载owner只在Abort+无模型/组件/输出时typed拒绝，Host回Failed且父正常Dispose后公开failed。已有输出负例仍uncertain/证据保留，2Physical/4guard/全1048与三源真流程通过。旧uncertain不回写/重试，T066原动作被后清理覆盖另待修，见[加载终止记录](../../specs/008-libreoffice-playback/verification/load-rejection-20261003.md)。

**坑 88 - 创建候选与转换的Dispose擦除原动作诊断**（2026-10-03修复）：OPEN catch和Converter await using只剩后退出错误，公开stdio业务红成立；保留原动作异常或Failed响应Code/Detail及真实清理原因Aggregate，仍typed uncertain，正常退出原异常/结果不变。6项公开路径、1054非Physical/另2真实Physical及正常转换/Player留旧复核通过。首Player缺content_digest/CA2219分别为夹具/构建问题，不计业务红；本地异常链不冒称REST完整堆栈，原件/预算/重放保持，详见[创建与转换诊断](../../specs/008-libreoffice-playback/verification/creation-cleanup-diagnostic-20261003.md)。

**坑 89 - 候选创建修复不等于正常文稿关闭也已保原因**（2026-10-03修复）：Surface正常关闭嵌套finally仍把shutdown错误换成后exit2，公开CLOSE真红；三阶段全部尝试、单失败保原栈、多失败transport Aggregate，重复CLOSE/Host退出同失败不重放。5公开保护含自有父窗销毁三错误、1059非Physical及两真实视频关闭通过；不称旧失败物理画面保留或全故障矩阵验收，见[已发布文稿关闭记录](../../specs/008-libreoffice-playback/verification/published-close-diagnostic-20261003.md)。

**坑 90 - 截图助手未声明DPI感知，图像尺寸不能代替窗口物理尺寸**（2026-10-03测量边界）：同QA落位的Player两轮报告96/144 DPI，原PNG分别800×450/533×300；助手GetWindowRect/CopyFromScreen没有显式DPI设置。比例符合虚拟化缩放，但没有足够因果证明，不能把原图一行/两行黑边或工具显示尺寸当完整物理viewport/混合DPI通过。不改生产几何或共享截图技能脚本，完整显示门禁仍欠，见同上本轮记录。

**坑 91 - 高DPI原因不能由96 DPI对照结案，错误流编码也可误判QA**（2026-10-03部分验证）：新同窗默认/Unaware/PerMonitorV2三轮九图均800×450/DWM一致，是96 DPI路径而非历史144缩图复现。坏PID实际拒绝但CP936错误流被UTF8误解导致负例失败，仅新QA包装统一两流后四保护绿；旧失败保留，不称播放器bug。实际Player十图/三轮控制/正常退出与本轮前端177通过，生产代码/包保持，T068仍未完，见[截图测量记录](../../specs/008-libreoffice-playback/verification/capture-dpi-measurement-20261003.md)。

**坑 92 - 同页Confirmed掩盖动画末页进入结束屏**（2026-10-03候选验证）：实际自定义单页count1额外next后黑屏而仍1/1/playing真red；仅末序End.NONE/no-tail的隔离候选两组保持/撤销/关闭green，尚未按完整终端/occurrence及初始/重复序列接生产，不能据此叫末页功能已修。独立noop协议v3/303绿是另一层，不冒称原生或前端证明，详见[末页候选](../../specs/008-libreoffice-playback/verification/terminal-hold-candidate-20261003.md)。

**坑 93 - MSI原生解析与primary显示器不能凭惯例猜**（2026-10-03D4验证）：D:/安装路径被MSI服务误解析为/SCP-cv，哈希/签名正常仍exit1619；核日志后用实际D:\路径exit0，无重启，策略保持。D4primary是大屏DISPLAY2，控制输出DISPLAY1非primary；交互session1清单配SQLite-readonly核旧window1/2为DISPLAY2/3，不据primary排除正确大屏。仅精确删除本轮已完成清单任务，工具/结果/安装失败均保留，见同上记录。

**坑 94 - POST受理不能关联末页终态，同值快照也不能当换源**（2026-10-04有限修复）：入队不返回ID的认证HTTP真红；复用持久CommandId后按精确窗口查询，不读全局最新状态。首两DOM红进一步定位watch每次返回新数组导致同身份更新失效，改为按值身份后绿；后续操作/ABA/卸载隔离。44DOM/5HTTP保护、桌面/390模拟UI通过，原生末页保护不据此验收，见[反馈记录](../../specs/008-libreoffice-playback/verification/terminal-command-feedback-20261004.md)。

**坑 95 - 相同PNG可能只是同一桌面，整页GOTO也收不到边界回执**（2026-10-04有限修复）：本机无主序末页ok与公开整页NEXT两红成立；正式Host/相邻页桥接修正。随后本机四图均山谷且关闭不黑，不能称保持成功；不猜生产渲染，QA加可见/自有中心点/关闭黑图门禁后D4两输出独立原图真保持/清黑。旧启动失败/误捕保留、1082非Physical通过，有主序/重复页/并行/长稳仍欠，见[原生与D4记录](../../specs/008-libreoffice-playback/verification/native-static-terminal-d4-20261004.md)。

**坑 96 - 捕获end不保证内容有效，后清理栈也不等于动作原错**（2026-10-04候选失败）：零时长容器改造有完整回调却全白，不能按同SHA验收；repeat2第三next EOF，最终Close栈为CLI listener重复注册，未据后栈猜原动作原因。原样重显示有限通过不覆盖两个失败；仅QA新夹具/动作日志待下一轮，正式策略不改，见[重显示候选](../../specs/008-libreoffice-playback/verification/terminal-replay-candidate-20261004.md)。

**坑 97 - RPC断开与Windows故障模块都不是底层根因**（2026-10-04诊断）：新原错observe URP断开先于Close异常，同PID/start WER证实Office异常退出；仅KERNELBASE+0x43b19/代码0、stderr前缀警告不能定位原因，归档无可用dump不冒称已抓栈。合法zero-leaves三轮显示通过与repeat失败分开，生产策略/预算不改，见[原生故障诊断](../../specs/008-libreoffice-playback/verification/terminal-native-fault-diagnostic-20261004.md)。

**坑 98 - 调试器参数和默认异常停顿也会污染实机结论**（2026-10-04 QA修正）：CDB帮助漏写netsyms末尾s，实际参数错误；修后活动日志共享冲突，随后真正AV因默认break而停住，使关闭超时。分别实证并仅修QA参数/共享只读/自动AV取证后detach，不能把前三失败合称新的Office崩溃或正常退出。两实际转储离线私有符号定位顺序容器，自然End对照仍失败；源码越界推导未替代完整对象证据，正式策略保持，见[原生栈与对照](../../specs/008-libreoffice-playback/verification/terminal-repeat-native-stack-20261004.md)。

**坑 99 - 非PPT参考图也会把请求源冒充实际输出**（2026-10-04有限修复）：原PlaybackControl非文稿监看直接取source_id，六类真实DOM均显示失败/在途的新源41而非实际40；只按actual正整数ID及适配器对应类型取参考后绿。首次相关回归暴露VLC/PPT矛盾观测又借默认图，补类型对应检查，不放宽旧文稿未知布局保护。12新/42相关/223全前端及IAB桌面390通过；图片仍是参考，不是实体出帧，全部按钮/其它源D4门禁未据此完成，见[多源参考记录](../../specs/008-libreoffice-playback/verification/multi-source-output-reference-20261004.md)。

**坑 100 - PDF缓存内容正确但适配器当前页未更新**（2026-10-04修复）：相邻预渲染后RenderPageAsync(2)直接返回缓存、CurrentPage仍1，公开Physical红；缓存查询移入既有门禁并更新当前页，2Physical/1082非Physical通过。Host本来自己设_currentSlide，不泛称所有UI原错；D4两输出PDF1→2→1原图和退出另核，见[非PPT实机记录](../../specs/008-libreoffice-playback/verification/native-media-d4-20261004.md)。

**坑 101 - 媒体时钟到目标，不代表GPU画面已到目标**（2026-10-04取证）：D4视频seek后首样本30000但原图仍旧片头，后样本30584/31335与机械臂/仿真姿态变化才证实有限连续解码。首次playing截图全黑也不当成功出帧；保留所有旧帧，不加固定延迟生产补丁、不从QA800ms采样推通用完成时间。音频/直播和最终长稳仍欠，见同上记录。

**坑 102 - SRT重连中的原生Playing不代表还有输入**（2026-10-04有限修复）：D4两次发布者消失后12秒计数/时钟均冻结，VLC仍Playing；仅直播独立尝试5秒四计数无推进锁存错误，不影响暂停/缓冲/普通文件，不自动重放。SRT显式恢复与RTSP后续帧/关闭通过，1092回归绿；UDP同资源/新建都仍失败，不能缩小为全部直播已稳，见[直播记录](../../specs/008-libreoffice-playback/verification/native-stream-d4-20261004.md)。

**坑 103 - 只检查截图中心漏掉角落安全提示**（2026-10-04 QA修正）：RTSP task0但PNG右下防火墙窗口遮挡；撤回视觉验收，未允许访问或修改规则。九点根HWND检查和另一已授权大屏区域改善QA，仍不称逐像素遮挡证明；SSHsession0前台窗口0也不代表交互提示已消失，原图保留，见同上记录。

**坑 104 - 输出参考正确不代表控制类型正确**（2026-10-04有限修复）：请求PPT/实际VLC时显示页翻页可用、专注页方向键真实POST，两公开DOM红；共用实际ID/适配器对应检查后拒绝类型相关操作，恢复入口保留。相关103、完整前端226及桌面390通过，不把本切片当同VLC各媒体/不同文稿模式全部能力验收，见[控制保护](../../specs/008-libreoffice-playback/verification/multi-source-control-guard-20261004.md)。

**坑 105 - Media统计旧累计值与Opening零值不能代替收包证据**（2026-10-04诊断）：UDP重开Native Opening/time0，旧Media统计可能非零或全零；独立接收器三种发布时序都有有效TS/解码帧，项目Player仍失败。强制TS/新Player/软件解码QA均未修，不能凭单一统计把网络或硬件当确定根因，见[UDP诊断](../../specs/008-libreoffice-playback/verification/udp-reopen-diagnostic-20261004.md)。

**坑 106 - 按钮禁用不取消滑块已经缓存的旧值**（2026-10-04修复）：两窗音量未配置scope/isActive、SEEK只看请求源；真实DOM三红在失效/换源后仍发第二请求。复用节流器目标代次和实际类型保护后绿，新输入/心跳不误锁；6新DOM/232前端及浏览器仅1已发请求通过，不承诺撤销HTTP，见[滑块记录](../../specs/008-libreoffice-playback/verification/playback-slider-fence-20261004.md)。

**坑 107 - Media.Dispose没有停止仍在播放的旧音频输入**（2026-10-04有限修复）：公开worker MP3 Playing→WAV non-autoplay却报新身份Playing；OPEN先Stop旧输入再换Media/身份后Physical绿、1092回归绿。D4三格式静音控制/时钟/新解码/正常退出通过，不当实体声音或循环/Finished完整链路证明，见[音频记录](../../specs/008-libreoffice-playback/verification/native-audio-d4-20261004.md)。

**坑 108 - 列表循环不能由原生重播本曲代替**（2026-10-04修复）：Worker吞Finished、控制端无条件末曲回首，两有效红后修复，带事件STOP保持末曲/代次，单曲/多曲/迟到和手动回归、D4真实管道有限通过。5.5秒夹具误当5秒业务红、首未ready混播放预算两QA误判保留，不延生产预算，见[列表记录](../../specs/008-libreoffice-playback/verification/audio-list-loop-20261004.md)。

**坑 109 - ProjectReference不证明QA继承生产集中依赖**（2026-10-04 QA修复）：生产已pin2.1.13，但runtime-dotnet外QA解析2.1.11/实际3.49.1；后置csproj导入属性true仍告警、无Version引用NU1015。标准同级props早期继承后、删除多余显式引用，实际3.53.3/DLL与生产匹配，1102回归/D4同版列表绿；旧“生产未升级”范围判断更正，审计不关闭，见[依赖对齐](../../specs/008-libreoffice-playback/verification/sqlite-qa-alignment-20261004.md)。

**坑 110 - 音频快照也会被旧REST/SSE倒退，reset数字代次会重名**（2026-10-04有限修复）：真实页面新曲2被旧音量响应退为1，旧refresh解除新loading两红；统一时间围栏与WeakMap唯一symbol后绿、13DOM/245前端及浏览器通过。实际加入列表忘更新时间另公开红→同事务推进/D4复核；相同历史时间保兼容、周期进度和离线控制不据本切片完成，见[观测记录](../../specs/008-libreoffice-playback/verification/background-audio-observation-20261004.md)。

**坑 111 - 音频命令结果的时钟不能替代空闲进度，Ended采样会抢列表推进**（2026-10-04有限修复）：D4旧18样本位置0而PAUSE真实3956ms。复用空闲sampler后最终0→3053ms；成功身份、失败OPEN失效和只读控制保护通过。stopped留给Finished/显式STOP、单次状态读取避免跨末尾二读，最终DLL列表/正常退出绿；不当声音/所有竞态/长稳证据，见[周期记录](../../specs/008-libreoffice-playback/verification/audio-idle-progress-20261004.md)。

**坑 112 - 重复风险不能泛化，安全拒绝原因也不能丢**（2026-10-05有限保护）：固定版本主序第二轮已复现越界，PAR重复对照正常；只拦非空主序double>=2/无限，原件/普通效果保留。公开Player原先丢宿主detail，真实stdio红→保留绿；D4候选拒绝留旧PNG/正常关闭有限通过。QA IntPtr序列化、图片null时钟误判保留不算业务红绿，见[本轮记录](../../specs/008-libreoffice-playback/verification/repeat-safety-and-source-picker-20261005.md)。

**坑 113 - 源列表选中项不等于实际输出**（2026-10-05修复）：SourcePicker用请求source_id标当前项，失败切源误标新源；实际ID/适配器/库类型解析与请求标签分开，空actual不借用，离线标最后报告。9新DOM/254前端、桌面390通过；模拟HTTP不当媒体出帧，见[同轮记录](../../specs/008-libreoffice-playback/verification/repeat-safety-and-source-picker-20261005.md)。

**坑 114 - 音频seek声明/原生支持不证明HTTP已接通**（2026-10-05修复）：API和Worker有seek但HTTP忽略position、服务无分支，认证真实请求400红；严格int64→持久目标/代次→实际12500/SSE绿，不把受理位置当实播。1123非Physical及D4有限通过，范围外/不可定位源仍欠，见[定位记录](../../specs/008-libreoffice-playback/verification/audio-seek-http-20261005.md)。

**坑 115 - 全局音量也不能把旧主机缓冲发到新主机**（2026-10-05修复）：Alpha音量在途/缓冲，switchServer后Beta出现第二POST真红；音量按主机域/快照存在保护后绿，不按正常曲目切换丢全局设置。进度则按源/列表项/主机保护，相关22/前端263、390/桌面通过；未取消已发HTTP，见[同轮记录](../../specs/008-libreoffice-playback/verification/audio-seek-http-20261005.md)。

**坑 116 - 图相同不证明没有原生推进，未知模型0也不能当末组完成**（2026-10-05部分修复）：相同新版父测试旧保持候选图相同但extra仍ok业务红；正式实际终点/本轮原末组事实先于控制返回noop后D4单页三轮30次绿。纯值未知0/丢失红绿，旧轮/其它模型/重置保护共12；模板/隐藏/重复/双窗仍欠，见[本轮记录](../../specs/008-libreoffice-playback/verification/animated-terminal-evidence-20261005.md)。

**坑 117 - 重复组两次点击后的图片不一定已完成**（2026-10-05 QA纠正）：仍在第二轮字幕动画时图被叫complete，下一步结束动画后正常文字改变，旧比较失败。先核原图/ok状态，不改生产为禁用重复；改为正式首次noop后取耗尽图，再验证保持，新轮仍真实动画ok。多页/隐藏/重开/零叶子及PAR重复/拒绝矩阵有限通过，见[矩阵记录](../../specs/008-libreoffice-playback/verification/terminal-matrix-20261005.md)。

**坑 118 - 音频HTTP受理提示成播放成功，旧主机通知也会迟到**（2026-10-05修复）：真实页面在loading/OPEN时提示“已开始播放”，切主机后旧响应又显示操作失败；两项业务红分别改为请求提示和连接代次保护后绿。当前主机拒绝仍报错，源和列表项提示一致；4新增/26相关DOM及桌面390模拟页面通过，不冒称实际出声，见[反馈记录](../../specs/008-libreoffice-playback/verification/audio-request-feedback-20261005.md)。

**坑 119 - 传Task给错误包装时，同步校验已经在包装外执行**（2026-10-05修复）：D4非法control next返回500，服务同步PlaybackServiceException在MutateValueAsync调用前抛出；公开HTTP真红后在调用前建立同类错误边界，400/原会话保持通过。翻页本应走navigate，QA误路由不影响这个输入错误漏洞；见[完整接口记录](../../specs/008-libreoffice-playback/verification/full-api-group-20261005.md)。

**坑 120 - 对端断开与Flush竞态不是本地对象使用错误**（2026-10-05修复）：丢AudioFinished ACK集成回归偶发closed pipe ObjectDisposed，reader断开释放流与发送并发；4096轮真实对端断开复现。仅发送流的ObjectDisposed包装IOException，沿既有同消息/event身份重连，不吞停止闩锁或自动重放播放；相关12/最终1137及同版D4组通过，见[同轮记录](../../specs/008-libreoffice-playback/verification/full-api-group-20261005.md)。

**坑 121 - 不在任务栏不等于没有实际Player窗口**（2026-10-05 QA修正）：正式ShowInTaskbar=false的Player被Process.MainWindowHandle0误判，按精确PID/start/session枚举可见唯一顶层窗口后两屏满幅原图通过；QA HttpListener Stop与IsListening读取又产生995，停止前本地标记解决。不修改生产几何或吞业务错误，26图及完整协作退出证据见[group记录](../../specs/008-libreoffice-playback/verification/group-visual-soak-20261005.md)。

**坑 122 - 同一请求提示的其它入口仍可能冒称完成**（2026-10-05修复）：背景音频页已修，但选源/上传打开/媒体库打开及音频菜单四处仍提示已打开/已播放；四条公开DOM loading/OPEN真红后仅改请求文案。实际观测/调用链不变，桌面/390模拟页面核对；未按受理通知验收原生播放，见[反馈记录](../../specs/008-libreoffice-playback/verification/open-request-feedback-20261005.md)。

**坑 123 - 小文件上传绿，不代表实际Kestrel可接大文稿**（2026-10-05修复）：D4真实98756629字节PPT被默认30000000字节请求限制截断，未进入转换，不能误当UNO失败。仅上传端点在读取前设置有限1GiB请求/表单上限，已声明超限413、畸形multipart400且不登记源；普通JSON、认证和CSRF不放宽。两公开HTTP回归与1139非Physical通过，实际完整D4组大文稿上传/7页转换/两窗短测/正常退出及原件锁释放通过；声明长度测试不是实际传输1GB，chunked超限/代理自身上限不据此验收，见[大文稿记录](../../specs/008-libreoffice-playback/verification/upload-limit-and-full-ppt-group-20261005.md)。

**坑 124 - 文件视频的时钟判据不能套在直播上**（2026-10-05 QA纠正）：正式组RTSP实际源/playing/pending空但Time0，探针等position>3000导致两次误判。逐秒实际观测后，直播改分离状态、后续原图和断流error，不给生产造时钟或延长预算。当前RTSP/SRT后帧及显式恢复出内容/关闭有限通过；首次playing仍黑/灰、恢复连续运动和长稳未据此完成。MediaMTX首错为夹具新增all_others与原同义路径冲突，仅删冗余夹具项；旧失败与SRT/GPU告警保持，见[完整组记录](../../specs/008-libreoffice-playback/verification/full-group-live-audio-20261005.md)。

**坑 125 - 主机代次不能替代页面寿命，旧finally也不能释放新请求**（2026-10-05修复）：音频页卸载后旧播放/刷新503仍污染保留的共享通知，两条公开DOM红；连续两播放第一条完成又提前清busy，两实际请求红。页面active+每次symbol owner后三绿，相关29及真实1440/390模拟反馈/等待通过；共享store/已提交HTTP不撤销，当前错误仍显示。最终全套和夹具runner失败边界见[页面寿命记录](../../specs/008-libreoffice-playback/verification/audio-page-lifetime-20261005.md)，不冒称音频在线/完整竞态已验收。

**坑 126 - netstat创建PID不能证明句柄只留在该进程，新引擎也可能被夹具污染**（2026-10-05 QA诊断/有限修正）：UDP旧Host/LibVLC释放后仍一绑定，新引擎两绑定，只有精确结束旧接收器存活时创建的自有发布者后旧绑定消失。独立发布者隔离+non-autoplay初始化后，同正式Host/Media重开、Decoded/Displayed推进、六真实原图/关闭端口消失通过；不是生产新增重建修复。首冷接入仍Playing却无帧、GPU/TS告警保持，见[对照记录](../../specs/008-libreoffice-playback/verification/udp-engine-and-handle-inheritance-20261005.md)，不把旧根因判断或暖时序绿色当全部UDP验收。

**坑 127 - 只打包插件DLL会让每个新实例冷扫描全部模块**（2026-10-05发布修复）：同版D4默认LibVLC构造13.74/13.79秒，实际日志缺plugins.dat/436模块；官方生成索引且DLL不变后，默认新进程构造23ms/OPEN182ms、冷UDP实际帧/重开有限通过。发布末端仅当前包生成索引、忽略用户配置/拒绝额外路径和覆盖，1140非Physical/3真实CLI守卫与正式发布通过；缓存依赖插件时间戳，不禁扫描兜底、不当可信性或出画证明。正式组直播遮挡和GPU告警不吞，见[发布记录](../../specs/008-libreoffice-playback/verification/vlc-plugin-cache-20261005.md)。
