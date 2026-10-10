# Verification: 网页屏幕共享

本文件只登记已执行证据；技术门禁不是正式两窗功能或D4出画验收。

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

## 仍未实现/未验证

此前基础阶段全非Physical1231/1231的独立证据保留于`.validation/qa-screen-share-010/retained-full-test-results/`与`full-test-retained.log`；当时未改API/前端而未跑的验证现已在上述后端接入阶段执行。旧失败、覆盖TRX及调试输出不删除，最终T018仍待完整功能接线后重验。

T010及之后未完成：后台异常清理及原生停止确认、内容音频、正常HTTPS、真实选择器取消、正式双窗5分钟/十轮、D4跨机及最终文档/全套回归。T017/T018虽然已有阶段工作，仍不提前勾选。

上述本机prototype不能勾选FR-001至FR-011或SC-001至SC-005全部完成，不把以前D4部署/媒体测试作为本功能证据。
