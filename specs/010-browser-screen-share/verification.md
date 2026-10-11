# Verification: 网页屏幕共享

功能已实现并部署D4；最终后端1437/1437、前端430/430通过。D4正式双Worker跨机、连续双共享312秒、十轮32次共享、有声双路360秒及异常退出后主动恢复通过，见[D4记录](verification/t016-d4.md)和[最终门禁](verification/t018-final.md)。以下阶段记录保留当时的范围，不把早期技术门禁当正式验收。

T013本机渲染信号与T016正式D4属性/RTP分别验证，现场扬声器可听未验，D4独立meter无有效采样。见[原生](verification/t013-native-audio.md)、[HTTP](verification/t013-audio-http.md)和[网页/有声链路](verification/t013-audio-ui.md)。不把音轨存在、命令受理或属性匹配称作现场听音验收。

## 2026-10-10 / 本机T001、T002

- Spec Kit全目录校验通过；需求质量清单16/16。忽略目录与现有登录、普通OPEN、Web缓存、IPC源代次及物理副作用路径已核对。
- 独立`.validation/qa-screen-share-010/native/Gate.csproj`使用现有WebView2 1.0.3537.50，Release构建0警告0错误。不修改正式Player或D4服务。
- 实际`getDisplayMedia`采集独占标题的自有动态标签页，不用`canvas.captureStream`，不采集用户桌面。Chromium153新headless模式；QA自动选择仅此自有标签页，不计作系统选择器人工验收，不用于生产许可逻辑。
- 真WebRTC至独立原生WPF WebView2；实际`requestVideoFrameCallback`累计1/30/60/90帧，mediaTime持续增加；原生`CapturePreviewAsync`的两张截图分别可辨识动态帧号、不同色块和移动位置，主代理已查看首次成功原图。
- 完全独占服务器五轮均通过，授权到首帧分别4718/2548/3069/3309/2953ms；最终数据保存于`.validation/qa-screen-share-010/runs/`每轮独立目录及`gate-evidence.json`指针。不是跨机时间或正式Worker指标。主代理已查看第五轮原生截图，帧号302→426、条块位置继续变化；第五轮两截图背景恰好同色，不把它称作两张颜色变化证明。
- 网页停止后视频轨道`ended`、发布peer`closed`；接收peer`closed`、video.srcObject清空；原生进程正常退出0。浏览器关闭由finally拥有。
- 原始失败保留在工具输出：默认headless shell采集NotSupportedError；改用官方支持的完整Chromium新headless后真实采集成功。QA异步`wait_for_function`把Promise当真值，最小`async()=>false`实证，改为异步更新状态+同步判据，不降低帧门禁。
- Windows `with_server.py`经shell/uv多层启动留下自有server子进程，旧信令造成复测污染；原生CIM命令行确认后只结束本轮明确server进程，改为QA直接拥有ThreadingHTTPServer/关闭并禁止地址复用，启动时核空统计。接收页no-store与每轮独立profile/URI，避免旧代码缓存。
- 最终五轮WebView浏览器PID45984/49700/37636/38548/54140均已不存在；原生Gate及自有QA server/browser进程0，原生netstat确认5207无非零PID的LISTENING。TIME_WAIT不当作活动接收。

## 2026-10-10 / T003会话基础

- 新增Registry、独立Entry和快照/错误/私有grant模型，尚未向Program装配，不开放半成品HTTP接口。
- 先写17项测试，缺少实现时构建红；实现后重新构建并实际执行`ScreenShareRegistryTests`，17/17、0跳过、0失败。TRX：`.validation/qa-screen-share-010/test-results/registry.trx`。
- 核两窗各一预约、3个非法目标、其他账号不能读/协商/续租/停、预约无接收授权、公开快照和grant格式化不泄漏token、cap/window/generation围栏、SDP类型/UTF8大小/重复协商、心跳不算帧、帧证据、20秒协商硬限/30秒发布者寿命、立即撤销但实际关闭前不释放槽、重复停止/旧身份、无OPEN取消无需关窗及终态有界。
- 本层通过不证明HTTP鉴权/代理来源/真实Worker/数据库条件CLOSE；T004及后续分别验。

## 2026-10-10 / T004、T005部分：真实SQLite运行意图

- 先写`ScreenShareRuntimeTests`，缺少运行合同类型时构建红；新增`ScreenShareRuntimeModels.cs`与`RuntimeStateService.ScreenShare.cs`后重新构建，真实SQLite集成5/5通过、0跳过。TRX：`.validation/qa-screen-share-010/test-results/runtime.trx`。
- OPEN在同一事务核当前运行组/Worker instance/owner、5秒传输/Player心跳及已选目标，再分配源代次，调用快速内存绑定；source_id=0、MediaSourceId=null、不新增媒体记录，受理保持loading，不持久SDP/token/receiver URI。
- 绑定回调在提交和Wake前执行，失败能回滚OPEN及旧pending supersede；后续Service必须在提交/唤醒异常路径撤销cap，不能只靠本层回滚假设内存自动撤销。
- 条件CLOSE在同一事务按当前DesiredGeneration及真正OPEN审计中的share_id检查，不误用最早pending的CommandArgsJson；不匹配抛错回滚整事务，包括Repository提前supersede的新普通源OPEN。测试证明迟到结束不关新图、不取消其pending。
- 匹配停止返回Queued及新CLOSE代次，命令仍pending；NoLongerDesired不代表实际原生释放，后续清理服务还须观察真实源或释放证据。未提前释放Registry窗口槽。
- T004的HTTP鉴权/CSRF/端点用例、T005的已认证lease注入尚未实现，因此这两个任务均保持未勾选。本轮没有开放半成品HTTP接口，也没有更新D4。

## 2026-10-10 / T004、T005、T006后端接入

- 工作区原102bc23保存点干净后开始；按Spec Kit前置/16项质量门禁推进，本轮不更改D4源码、服务、墙面、电源或媒体。
- `ScreenShareEndpointTests`先真实红6/6（未接线返回404），后接Program/Service。Registry新增原Worker instance/owner/group绑定；发布者登录/CSRF、离线/非法目标、预约无命令、重复取消、两目标、offer受理、无永久源、公开/持久数据不含cap、原Worker更换、越权及64KiB回归通过。
- 四种错误window_id JSON类型用例真实红，根因TryGetInt32对错误ValueKind抛异常；补类型门禁后4/4。不把未处理异常改成假成功或默认为窗口1。
- `ScreenShareLeaseTests`先缺实现红；真实SQLite领取后只给原Worker匹配lease注入URI fragment授权，DB仍无token/URI。错误实例领取被fenced；已认证替换Worker及未装配授权均fail-closed，已领取无效OPEN记Failed，不留下Processing阻塞。与运行事务5项合计Integration共享9/9。
- 接收HTML/JS嵌入发布包，CSP精确脚本/样式哈希，cap不放查询串而用Authorization header；loopback和Origin/窗口/代次/schema/阶段同时核。8项接收HTTP测试含正向读取、无cap/查询cap/其他窗口/外部来源拒绝、一次answer、有界请求体、激活门禁、重复帧及撤销后不能读/答；模拟HTTP帧不是原生出画。
- Hardware与Simulation真实Program容器分别解析共享服务/授权/dispatcher；不启动Player。测试注入Wake失败，证明提交后的OPEN确实仍在库，但内存cap已撤销且状态stopping，不冒称事务回滚或原生释放。合计ControlHost共享专项47/47、0失败/跳过，TRX在`.validation/qa-screen-share-010/backend-assembly/`。
- 接收脚本VM先红1/5，ICE协商停止后自己的等待/监听残留；接统一取消信号后5/5，核预备帧不授予实际状态、激活后回调才报、cap不进URL/原生消息、停止释放及两资源隔离。这是模拟边界，不替代真实浏览器/原生资源门禁。前端全套实际298/298，0失败/跳过，原始输出`receiver-all-frontend.log`。
- OpenAPI按共享目录拆分并登记全部新路由。首次全量回归发现旧覆盖器仅支持paths目录及无fragment引用；修后保留严格全量路由/方法相等，新增模块只选当前Path Item、缺片段/越界拒绝。私有404另核已登记路由、share_receiver_denied及no-store，不改变生产授权。合同覆盖子集3/3；首次失败TRX在`backend-wired-full-results/`保留。
- 最终Release全解决方案构建0警告0错误；全非Physical实际1267/1267、0失败/跳过，6个独立TRX逐项核total=executed=passed，套件41/171/333/271/324/127，证据`backend-final-full-results/`与`backend-final-full.log`。
- Redocly校验退出0，新增共享模块无告警；仍有4项既有告警（三条PowerPoint退役端点无2xx及一条背景音频oneOf组合），未伪造2xx或夹带修改。CERNET跳转节点下载失败后仅本轮进程临时使用先前获准的官方源，仓库.npmrc保持CERNET；仍用pnpm，不生成或提交Node锁。
- 前端typecheck与build:web均退出0；Vite保留既有大块体积告警/插件耗时提示，未关闭阈值来掩盖。未改正式UI，因此本阶段没有新增真实页面视觉验收，T008/T015仍未完成。
- API、使用/维护/CHANGELOG、共享配置与已知坑79同步。本阶段HTTP和脚本已有实现，但正式Player/前端入口仍未接线，不向D4发布这个阶段包。

## 2026-10-11 / T007正式Player专用资源

- 按T007补`ScreenSharePlaybackTests`和`PlayerRuntimeHostScreenShareTests`，先缺少实现构建红，再实现独占InPrivate接收资源。新增状态23项、Host13项，涵盖实际预备/激活后帧、严格来源/schema/身份、旧回调/旧条件CLOSE隔离、准备/切入失败保留旧图、过期租约、取消释放及不支持控制拒绝。Host替身只替换外部原生资源，不计作真实媒体证明。
- `PlayerRuntimeHost.cs`原约450行，不是超过500行；任务文字已纠正。普通Web方法等价拆到独立partial，共享资源独立目录且不入普通网页保温池。共享快照source_id/position/duration为null，share_id/实际帧/实际音轨不含接收cap；HTTP/SSE接线仍留T009。
- 两个边界实际红后修复：`CancelAfter(0)`异步触发使已过期租约可能启动；已完成首帧Task不能证明当前资源仍活着。改为过期同步取消，并在等待返回后核当前取消/存活状态；红TRX保留于`native-deadline-red/`与`native-ended-wait-red/`。
- 最终Release全解决方案构建0警告0错误；全非Physical实际1303/1303、0失败/跳过。`t007-final-full-results/`六个独立TRX核total=executed=passed：Domain41、Infrastructure171、ControlHost333、Integration271、Contracts324、Windows163。前一轮1302及原始红/编译失败不删除，不重复计作本轮最终结果。
- 独立QA调用正式`PlayerRuntimeHost`和实际专用WebView2，同时使用正式receiver HTML/JS/CSP；HTTP夹具仅替换本机信令服务，不是正式ControlHost/Worker。真实getDisplayMedia自动选择唯一自有动态标签页，无canvas.captureStream、麦克风或用户桌面。正常原生选择器仍留T015，D4跨机留T016。
- 最新两轮证据分别在`host-runs/ffc6cbdf3ad943a186bd26c51b577c94/`（窗口1）及`3686da0ef9f9423293cbc9c43ebae5f1/`（窗口2）。授权到Host OPEN实际后帧2939/3054ms，原生帧2→62；profile实际InPrivate=true。主代理已查看最新两目标原生截图及此前各两张运动截图。本机依次创建Host，不冒称同时两Worker、非目标保持、五分钟或十轮通过。
- 两轮停止均核发布轨道ended/peer closed、原生CLOSE idle/null、WebView数量0、进度采样null、Host进程正常退出0；Windows进程句柄确认浏览器PID16824/52284均实际退出。JS closed只是附加报告，不单独作为原生释放证据。
- 真实错误cap首次文档返回404，正式Host候选失败并释放；旧图片source101/gen1及原Surface保留，0原生WebView，Host退出0。证据`host-runs/df4325eb90db4edbbb404f377276a8d4/failure-evidence.json`，不以导航成功冒充出画。
- 本阶段未更改前端UI或HTTP API结构，不将前轮298项、typecheck/build或Redocly算作新运行；最终T018仍须功能完整后重跑。文档/合同/已知坑80同步，本机QA资源已退出；本阶段没有更新D4包、打开原始媒体、下发墙面或电源。

## 2026-10-11 / T008网页原生采集入口

- `ScreenSharePublisher`独占一次尝试/轨道/peer/计时器，原点击栈同步getDisplayMedia；许可前零API/OPEN。两个目标独立，用户内容音频默认关闭，has_audio按实际轨道；不请求麦克风、不造媒体库源。
- 前端接正式共享HTTP，复用cookie/CSRF/no-store及主机代次，调用方abort不被超时controller覆盖；offer和连接不冒充实际出画。关闭优先释放本地，仅向原身份发一次DELETE，stopping保持待确认，只读GET查询、不自动重采或重试。
- 先缺实现红；迟到预约未撤销、结构化占用码被泛化两项实际红后修。迟到有效分配仅撤销原ID、不offer；公开code/status由固定白名单翻译，任意share_自由文本不进入页面。生命周期更全面异常边界仍留T010。
- 相关正式模块/DOM/API边界24项通过（控制器14、面板4、API6）；API新测试第一次伪document不含createElement导致Vue初始化失败，改完整Happy DOM，仅修自有夹具，不算生产缺陷或功能红绿。
- 最终全前端实际322/322、0失败/跳过，`t008-final-all-frontend.log`，执行235583ms；此前同数首次完整85秒结果独立保留，不冒称页面操作耗时。最终build:web内含vue-tsc类型检查并退出0，`t008-final-build-web.log`；Vite保留既有>500kB大入口/插件计时告警，未改变阈值。未改后端/HTTP结构，不重跑或计入前轮1303及Redocly；最终T018仍待完整功能后执行。
- `.validation/qa-screen-share-010/t008-panel-browser.py`实际挂正式Panel/composable/RTC/API模块，只有发布者账号夹具和HTTP信令替身；真实完整Chromium原生getDisplayMedia自动选择唯一自有动态标签页，非canvas.captureStream，非正常系统选择器验收。没有receiver、answer、实际接收帧或D4，不能计SC-001/002/003。
- 本机首次六轮通过后，自审补查发现按钮获得焦点但没有独立轮廓；两次红的JSON和截图保留。Naive默认仅填色/内边线不足清晰键盘标识，按主题令牌补:focus-visible。最终五轮独立目录：`e6e364500250408b971dd17a73fa4a6a`、`392b004246604c41a878c359d56dfcab`、`66ba5c7a0bd9494cbcde2c0a3fbaea1c`、`bbb7ccbed9a34529a26d4069bc567c22`、`d1853249912e47a3a2d9e336d9e9f509`，均在`t008-browser-runs/`；每轮两次真实采集/RTC offer、浅深1440/390无横溢、按钮/复选框44px、四场景可见2px实线焦点及offline入口拒绝、pageerror/console error为0。
- 每轮两个页面同时持有自己的真实video live轨道/peer；先停目标1，目标2仍live及have-local-offer；最终两轨道ended、两peer实际connection/signaling closed。替身DELETE各一次，返回stopping时禁止新共享，手动GET closed才重新允许；这证明前端资源释放/状态边界，不证明实际原生Player释放。主代理查看首轮浅/深/停止及最终浅深键盘原图。
- 在线uv请求CERNET PyPI页面超时，实际进程终止后用已有uv离线缓存运行Playwright，不更改镜像/安装依赖。pnpm11.24对旧manage_package_manager_versions=false忽略并试图下载项目11.22；按当前本地CLI实现用仅本命令`--pm-on-fail=ignore`避免下载，不修改仓库或全局配置、不改依赖。新增代码均不足500行，文档/合同/坑81同步；本阶段没有部署010、操作墙面/电源或原媒体。

## 2026-10-11 / T009临时HTTP/SSE输出与控制

- 新增两窗口有界`ScreenSharePlaybackProjection`，仅内存记录非秘密分享ID/原Worker/代次/实际帧与音轨。OPEN事务绑定；状态报告通过持久身份/代次门禁并提交后才补观测，单调会话版本防异步倒序。DTO同时核持久请求/观测代次，不造MediaSource、不持久接收URI/cap、重启不恢复。HTTP与SSE共用明确`share_id/actual_share_id/actual_frames/actual_audio`，普通源空/空/0/空，共享文件时钟为null。
- 正式认证HTTP/SSE最初七项全红，接线后七项通过；新增纯值乱序、帧单调、Worker替换、两窗隔离、重启及未提交代次七项，合计14/14、0失败/跳过，`t009-tests/t009-memory-projection.trx`和`t009-memory-projection.log`。纯值未提交代次用例不是实际提交失败实验；真实事务回滚仍由既有T005集成用例覆盖。
- 控制条正确显示null媒体库ID共享，不借普通文件缩略图、不自动重开；同分享/代次、在线及正帧才显示已收到画面。停止仅DELETE原分享ID并GET刷新；意图清除但实际共享仍在时禁用重复窗口关闭、显示停止待确认，不沿用历史playing成功色。共享音量/静音仍明确禁用，实际音轨存在不宣称已听到音频，专用桥留T013。
- 两逻辑窗口在single/double/custom都可达，选择路由零HTTP写操作。真实Naive页签通过tabProps补role/选择/面板关系、方向键/Home/End与焦点；窄屏使用可见原生单选组，真实方向键核对目标，标签及页签至少44px，焦点为主题令牌2px实线。停止标签、禁用ARIA、手机页签先红后修；截图自审发现重复限制说明，新增精确次数断言真实红2≠1后删去重复段落。
- 最终全前端334/334、0失败/取消/跳过，实际退出0；`t009-final-caption-all-frontend.log`，94475ms。最后build:web包含vue-tsc并实际退出0，`t009-final-caption-build-web.log`，保留既有>500kB入口告警；此前331/334及原始红分别保留，不算最后代码的最终结果。新增外壳DOM测试加载真实Fluent客户端ESM，不改变生产依赖或模拟组件。
- 真实Chromium渲染正式外壳/显示页/Pinia/Naive组件，以HTTP替身及实际store观察入口核公开状态，不采集或传输媒体。1440/390×浅深×single/double/custom共12场景，核零帧/离线/正帧/停止待确认、无缩略图、禁用能力、导航零写入、键盘原ID停止一次、无横溢及pageerror/console error为0。系统采集选择器、正式Player和D4均不在本轮浏览器证据范围。
- 完整键盘验证连续五轮每轮12/12：`29cdcfb2ea384cde8e7b391f8a89ecf5`、`3504f7272601471e91f0e760f0f676fe`、`37cf071388c94ef791ff1be10b238701`、`76d615f99a2349ba86bf14017b8f21cd`、`1a7a52fe67f946c592f2f50f6cabcc6a`，均在`t009-browser-runs/`。去重文案后最后一轮`6e740b3920ab49458feb2f9ab6bf1672`再次12/12，主代理已查看最终手机浅色与桌面深色原图，以及此前手机深色停止/浅色键盘焦点和桌面浅深原图。早期Provider缺失、ARIA、页签及隐藏单选输入的失败原件全部保留；隐藏输入导致check拒绝不等同证明第三方全部键盘逻辑损坏。
- 最初全量后端失败两项：旧正常会话合同字段集合未包含新增四字段，以及Contracts子进程清理超时。合同保持严格集合相等并补普通源默认值断言，不放宽校验；子进程用例单独重跑1/1及串行全量Contracts324通过，没有改生产或测试时限，不能宣称其根因已修。首次固定TRX覆盖也保留，不将单个被覆盖TRX当全套证明。
- 最后重新构建、串行全非Physical.NET实际1317/1317、0失败/跳过，命令退出0，`t009-final-full-dotnet.log`和`t009-final-full-results/`六个独立TRX：Contracts324、ControlHost347、Domain41、Infrastructure171、Integration271、Windows163，逐项核total=executed=passed。此前T007的1303不是本轮计数。
- Redocly通过、退出0，`t009-redocly-direct.log`，仍为四项既有PowerPoint退役/背景音频组合告警。第一次根目录pnpm exec触发依赖检查并因非TTY中止，没有成功验证；后直接运行既有缓存Redocly CLI，不安装依赖、不删除node_modules、不更改CERNET配置或生成Node锁。最终Spec Kit全目录通过，`t009-final-spec-kit.log`；diff校验通过，所有本轮代码不足500行，extensions.yml不存在。
- 本机自有Vite5210已关闭（中断退出1不是测试失败），CIM核匹配本轮浏览器QA/该Vite进程0，TCP5210监听0；浏览器finally关闭独占上下文。没有推送/部署010、更换D4源或发送墙面/电源。最新D4旧稳定部署只读复核不计本功能验收。

## 2026-10-11 / T010页面寿命与并发停止回归

- 从签名保存点5e2de85继续；先写重复停止/失败待确认/旧代次/主机及登录寿命回归，再修改缺陷。没有新增依赖、Node锁、镜像配置或任何D4媒体/墙面/电源副作用。
- 发布者先红16/18：旧主机请求被拒绝后清理被live条件绕过、迟到停止查询失败覆盖已确认closed。修复只释放仍属于自己的尝试，catch更新须同尝试/当前上下文/仍stopping；原始红与绿分别在`t010-publisher-lifecycle-red.log`、`t010-publisher-lifecycle-green.log`。
- 面板主机切换用例先红6/7，普通类generation字段不会触发Vue订阅。协调器新增同步代次订阅，在等待清会话之前即失效；面板结束后取消订阅并dispose，原面板禁用自动重采，重新打开页才能开始新许可。订阅用例先红6/7、后绿，日志`t010-connection-lifecycle-red.log`、`t010-panel-lifecycle-red.log`保留。
- 相关前端32/32、0失败/取消/跳过，实际退出0，`t010-frontend-lifecycle-green.log`；18发布者、7面板、7协调器。停止失败先核轨道/peer只释放一次，再核只读查询及重复停止不重发DELETE；已确认关闭保留原失败事实，不假报请求成功。
- 真实Program/SQLite Host3/3、Integration共享运行事务7/7，0失败/跳过，实际退出0，`t010-results/`两独立TRX。12个同时停止只撤销原目标授权并排一次CLOSE，另一窗有效；唤醒异常发生于提交之后，仍stopping/observed未推进，重复请求不重新副作用。普通OPEN与旧共享停止并发及迟到停止不取消新pending，两个目标分别核独立；没有Player或原生资源，不将排队受理计作实际释放。
- `.validation/qa-screen-share-010/t010-panel-lifecycle-browser.py`挂正式Panel/composable/RTC/HTTP客户端；保留真实完整Chromium原生getDisplayMedia及实际RTCPeerConnection，仅自动选择唯一自有运动标签页。HTTP信令/账号隔离夹具，未接answer/receiver/正式Worker/D4；不计正常系统选择器或实际接收帧。
- 五轮×两目标×主机切换/账号退出/pagehide信号/实际Vue卸载，共40次实际采集，结果`PASS`，40自有轨道ended、40peer的connection/signaling均closed，页面错误0。主机/账号失效不发旧DELETE；同上下文pagehide/卸载各一次；无二次采集。pagehide是分发真实事件类型的结束信号，不冒称实际导航或操作系统关窗。完整证据`t010-browser-runs/fe9dacf07cd745cd9eb44743f35bef5c/result.json`，主代理查看第五轮桌面浅色主机失效与390深色账号退出原图。
- 本阶段D4只读检查再次实际退出0，稳定源码fd64de6干净，两正式Player在线、HTTP200、新009组件/样式及63/64/IP81配置均在；没有部署010，也不能将这个检查计作本功能跨机出画。
- 最终重新构建、串行全非Physical.NET1322/1322、0失败/跳过，命令实际退出0，`t010-full-dotnet.log`与`t010-full-results/`六独立TRX逐项核total=executed=passed：Contracts324、ControlHost350、Domain41、Infrastructure171、Integration273、Windows163。全前端342/342、0失败/取消/跳过，实际退出0，`t010-full-frontend.log`，172438ms。
- 最后`build:web`含vue-tsc通过并实际退出0，`t010-build-web.log`；保留既有入口>500kB告警，未更改阈值。Spec Kit全目录通过，`t010-spec-kit.log`；diff校验通过，本阶段代码均不足500行。未更改API/IPC合同，未将上一阶段Redocly结果计作新运行，最终T018仍须功能完整后重验。
- 本机QA浏览器在finally关闭；自有Vite5210精确中断（退出1为主动停止，不是测试失败），实际匹配进程0与TCP5210监听0，不结束其他浏览器/Node或D4服务。

## 2026-10-11 / T011后台回收、释放证明与预案

- 从签名保存点53bae03继续，requirements只读门禁16/16。`ScreenShareCleanupService`在两模式真实注册，独立一秒周期；不依赖发布者GET，过期/换源/组失效先撤销cap，再按原分享/OPEN代次条件CLOSE。无OPEN审计的回滚预约只在同Service门禁内确认无原生接收可能；已提交OPEN仍须实际释放证明。后端周期/数据库验证不当作实际Player。
- 原生完整Dispose后才生成两字段`ScreenShareReleaseEvidenceDto`，最多32条去重历史；空闲/后来图片重复采样。`RuntimeProjectionPublisher`提交原Worker身份后采信释放，旧源状态围栏不妨碍原资源释放、也不允许覆盖新源。实际释放未到或失败保持stopping，JS closed、轨道ended、offline、换组、新Worker和取消均不算原生死亡。
- 原Player退出观察至多两条，先核持久instance/owner和已验证OS登记PID/start/session/role。ControlledExitObserver只替代OS等待，真实Program/SQLite核换组/换Worker不提前closed、五种登记不匹配不观察、取消后迟到confirmed不采信；没有实际杀进程或宣称这八项是Windows退出实验。正常宿主停止取消并等待自有观察，Simulation缺OS观察器不造证明。
- 重启边界以真实DB加全新Registry测试：当前动态OPEN审计只排一次原条件CLOSE，不重建cap/SDP或恢复旧分享；后来普通OPEN保持。预案捕获在同写事务检查请求审计/实际适配器，失败不新增或覆盖；set零/负ID不保存，unset激活不重播/终止共享。七项预案及六项正式Vue DOM先红后绿；后端测试曾错误断言error字段，改核实际detail及code，原失败TRX保留。
- 最终新增31项.NET：后台11（含真实一秒HostedService无需GET/手工cycle）、原进程观察8、预案7、重启2、正式STA Host释放2、IPC序列化合同1。Windows生命周期相关48/48通过；两个旧空闲null采样断言严格升级为idle及原分享两字段回执，过期零原生创建仍保持null，未删除门禁。Contracts新增用例最初被CA1861阻断，改静态字段而非关闭分析器。
- 最后串行全非Physical.NET实际1353/1353、0失败/跳过，六个独立TRX在`t011-full-results/`：Contracts325、ControlHost378、Domain41、Infrastructure171、Integration273、Windows165。每项核total=executed=passed，六项目命令均实际退出0；完整Release解决方案另构建退出0、0警告0错误，`t011-solution-build.log`。并行早期构建DLL锁失败原件保留，未计作通过。
- 全前端348/348、0失败/取消/跳过，实际退出0，`t011-frontend-full.log`，130683ms；新预案DOM6/6同时覆盖两窗×请求ID/实际ID/适配器。最后`build:web`内含vue-tsc，退出0，`t011-web-build.log`；保留既有入口>500kB告警。
- 真实Chromium渲染正式AppShell/ScenariosView/Pinia/Naive Provider，仅HTTP公开快照替身、不采集或控制D4。1440/390×浅深共4场景，24次键盘Enter明确拒绝捕获、4次手动新建可用，只有读取sessions、零写请求、无横溢、pageerror/console error为0。首次旧通知离场DOM并存严格检查失败，等待其真实退出，未用first掩盖。截图自审发现手机提示仍在入场过渡，追加实际祖先opacity及视口边界等待，最终28检查再次通过，证据`t011-browser-runs/c76f407bd329489fb0350bd31f2f402a/`；主代理已查看最后手机浅深拒绝/新建原图及桌面深色原图。
- 重新编译自有HostProbe，调用正式PlayerRuntimeHost、独占WebView2和正式receiver HTML/JS/CSP；仅loopback信令夹具，真实getDisplayMedia自动选择唯一自有运动标签页，非canvas.captureStream。两目标**分别**授权到激活后帧2665ms/2410ms，帧2→62，实际InPrivate；不是同时正式Worker、正常系统选择器、5分钟或十轮。最终证据`host-runs/7222a2f241ad47068469912b1937446b/`、`8d2600b0a83947609893b7e45e5068e0/`，主代理查看两目标原生运动截图。
- 两次停止均核自有轨道ended/peer closed、原生CLOSE idle/null、实际WebView0、空闲采样重复原分享/OPEN代次回执、Host退出0；原浏览器PID37392/13184由Windows句柄WaitForSingleObject确认WAIT_OBJECT_0，保持10秒预算。真实首次文档HTTP授权拒绝释放失败候选、回执对应失败原身份，旧图片101/gen1和原Surface保持，0WebView及Host退出0；证据`host-runs/4512df61151b4b1cb948f2263cb64d25/`。
- 第一次本机原生运行在浏览器PID十秒退出等待失败，日志`t011-native-host.log`及`host-runs/e1e4a1060fc94a89954d452e6bfe3346/`保留；因缺返回码/PID落盘，不能确定当时原生退出根因。随后仅补自有QA明确Win32参数/返回码并在断言前落盘，最终两句柄成功，`t011-native-host-observed.log`；未放宽时间、不强杀其他浏览器、不宣称修复原生泄漏。
- 文档、预案REST错误码、私有IPC合同及坑84同步；最终Redocly直接用已存在的CLI校验退出0，`t011-redocly-final.log`，本次实际三项既有退役PowerPoint端点无2xx告警保留，不沿用以前四项计数。第一次误读旧C盘shim导致模块不存在，未执行校验，`t011-redocly.log`保留；按实际Get-Command定位E盘缓存后运行，不安装依赖、不改CERNET、不生成Node锁。Spec Kit全目录与diff检查通过，全部本轮代码不足500行；extensions.yml不存在。
- 本机自有Vite5210精确中断并确认退出（主动退出1不是测试失败），所有本轮QA/Host/共享WebView匹配进程0、TCP5208/5210监听0；浏览器finally关闭独占上下文，不结束其他Node/浏览器。D4仍稳定009，本阶段没有010部署、打开原媒体或下发墙面/电源。T011完成不代表十轮/跨机及最终010验收。

## 2026-10-11 / T012本机双资源十轮交错

正式面板/采集/RTC与两个持续复用正式Host完成十轮、30次共享，核动态像素、交错停止/换源、旧回调隔离、全部轨道/peer/控制器及原浏览器OS对象释放。实际退出0、无页面异常；两目标20次初始OPEN首帧1328–2787ms。HTTP内存夹具不等于正式ControlHost/双Worker或D4，完整原始证据、保留失败和边界见[专项记录](verification/t012-native-pair.md)。

## 2026-10-11 / T015完整页面与原生权限路径

正式Web产物、完整SPA和真实登录/可信LAN HTTPS完成44权限/采集后失败场景及五轮关键清理路径；真实API503后21条采集轨道全部ended。原生拒绝使用Chromium官方QA参数，公开在线/状态替身单列，不冒称人工系统选择器或Player出画。另完成4公开状态、键盘停止及手机真实滚动/五点无遮挡检查；发现并修复网页默认8000回退与手机停止32px问题，最终44px。最后全前端430/430、build:web含vue-tsc通过，保留既有体积告警。原始失败/分层证据和清理见[完整记录](verification/t015-browser-ui.md)。

## 最终需求映射与验证边界

| 需求 | 已执行证据 |
| --- | --- |
| FR-001、FR-003、SC-001 | T016真实getDisplayMedia/RTC→正式双Worker后帧；独立DISPLAY2/3运动原图；最大首帧上界4.769秒 |
| FR-002、SC-004 | T015完整SPA/可信HTTPS、44权限及采集后失败场景；认证/CSRF/争抢/越权自动化回归，不用API替身冒充出画 |
| FR-004、FR-005、SC-002 | T016十轮32次授权/停止、两目标普通换源/旧DELETE隔离、发布浏览器退出6.025秒；T019原Player死亡保守5.332秒、明确重启后新身份共享/释放 |
| FR-006、FR-010 | Program/SQLite/IPC/Receiver回归：内存cap、身份/代次/原进程围栏、原子条件CLOSE及预案拒绝；无新增媒体记录，不改墙面/电源/控制桌面 |
| FR-007 | T013本机实际内容音轨与独立渲染信号；T016两路音频RTP和12组正式音频命令/实际属性/后帧；无音轨仍可播视频、默认静音、不请求麦克风 |
| FR-008、FR-009 | 同身份已提交后帧授予出画；HTTP/SSE/音频回读及倒序回归；T014/T016正常TLS、安全Cookie/CSRF、私钥403与不信任上游拒绝 |
| FR-011、SC-003 | T016连续双路312.060秒和有声360.485秒；独立原生像素及原浏览器OS退出；T018最终六套1437后端、430前端及各专项 |
| SC-005 | T009/T015浅深桌面/390px、方向键/页签/停止、44px/可见焦点/真实滚动无遮挡，无横溢；手机不伪装支持采集 |

历史的1231、1427等阶段计数、失败TRX和调试原件保留，不累加或冒充最终1437结果。T019真实Program/SQLite先红后绿，既有Receiver夹具时钟不稳定单独复现并只修测试时钟；详见[最终门禁](verification/t018-final.md)。

正常系统选择器的人工操作、D4扬声器现场听音和其它访问设备的组织证书部署未验；不是以关闭安全策略代替验证。原生采集只自动选择唯一自有动态标签页，不采集私密桌面，不修改系统音量、墙面或电源。浏览器/系统提供的内容音频能力不同，无麦克风替代。功能面向同一局域网，不声称互联网/NAT/TURN已经验证。

当前D4包为`deploy-20261011-4f76a10`，使用原DataRoot；正式两窗最终恢复在线idle、源/共享身份均null。其它设备必须正常信任有效HTTPS证书，本轮临时CA按精确指纹撤销后不保证该QA地址在未配证书的浏览器中直接可信；详情见[HTTPS记录](verification/t014-https.md)。

以上需求完成判断来自相应分层证据，未以prototype、旧009健康检查或硬件可听推测替代010正式验收。

## 2026-10-11 部署复核与独立复测

用户再次要求部署并测试后，只读核对D4源码HEAD `9d0ef169ff4c802e3bfc065848fb354bf73a3309`、ControlHost/Infrastructure摘要和正式Web入口，均与现行版本相符。继续复用`deploy-20261011-4f76a10`及原DataRoot，没有重复换包、重启服务或覆盖媒体。以下数字只属于本次新执行，不累加上轮完整回归。

- 后端共享专项实际138/138，0失败/跳过，原进程退出0；独立TRX的total=executed=passed。前端八个共享专项实际90/90，0失败/取消/跳过/todo，退出0。未重新执行完整1437后端/430前端、类型或Web构建；生产代码和API未改。
- 普通登录用户会话中，严格HTTPS、正式SPA登录及真实getDisplayMedia连接D4两独立Worker，连续双路313.024秒、218次后帧观察通过。十轮32次共享全部通过；最大首帧上界4456.1ms，32次停止/轨道和peer释放的最大耗时4.722秒，页面异常及清理异常均为0。保留全部原GUID/代次、交替停止、新身份、迟到原DELETE和非目标后帧证据。
- 独立D4采样只捕获原两GUID对应的DISPLAY2/3全屏项目窗口，每窗5张，共10张原图；实际接收帧1562→6902、1412→6632，五次摘要各不相同。主代理查看两窗第一/第五原图，帧号和运动块位置变化、目标与画面对应正确；四张传输摘要匹配原件。原接收浏览器OS对象PID16036/22380均确证Exited=true，不以接口closed代替退出。
- 两窗各一次普通PNG换源、原共享确证关闭、旧DELETE不关闭PNG、命令实际completed及另一窗继续接收通过。关闭独立窗口1发布浏览器后5.733秒内回收，窗口2继续接收并正常释放；本轮唯一随机命名的临时图片源经归属核对后正常API删除200，页面异常及清理异常为0。

本机正常证书安装与原命令上下文的读回不同，实际同账号/SID普通登录任务确认精确CurrentUser根存在；在该上下文运行的普通Chromium严格校验通过。原命令上下文的ERR_CERT_AUTHORITY_INVALID失败保留，不归因于用户未安装或D4服务损坏，不改TLS保护或来源策略，见[坑94](../../docs/known-pitfalls/screen-share.md)。测试始终只选择自有运动标签页，没有请求麦克风、捕获控制桌面、下发墙面/电源或修改系统音量；本次无声复测不补充扬声器可听或人工系统选择器结论。

原始材料在ignored `.validation/qa-screen-share-010/redeploy-20261011/`；稳定性运行`3a977a89ba844b54aeca5cd131ec1505`，换源/失联运行`6f940a0aca1649df872da897c9e5a93b`。截图保存在两端自有TEMP目录，不提交媒体、登录口令、证书或日志。正式服务保持运行，临时QA任务及测试信任的最终清理单独核验，不由上述通过计数推定。
