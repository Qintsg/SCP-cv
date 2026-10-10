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

## 仍未实现/未验证

此前基础阶段全非Physical1231/1231的独立证据保留于`.validation/qa-screen-share-010/retained-full-test-results/`与`full-test-retained.log`；当时未改API/前端而未跑的验证现已在上述后端接入阶段执行。旧失败、覆盖TRX及调试输出不删除，最终T018仍待完整功能接线后重验。

T007及之后未完成：正式Player专用资源/消息围栏/激活后帧、前端原生采集与null源身份、后台异常清理及原生停止确认、内容音频、正常HTTPS、真实选择器取消、正式双窗5分钟/十轮、D4跨机及最终文档/全套回归。T017/T018虽然已有阶段工作，仍不提前勾选。

上述本机prototype不能勾选FR-001至FR-011或SC-001至SC-005全部完成，不把以前D4部署/媒体测试作为本功能证据。
