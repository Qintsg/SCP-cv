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

## 仍未实现/未验证

本轮基础实现Release全解决方案构建0警告0错误；全非Physical实际1231/1231、0失败/跳过，六套件41/171/303/267/322/127，第二轮保留独立六TRX于`.validation/qa-screen-share-010/retained-full-test-results/`并核执行计数，完整输出`full-test-retained.log`。第一轮固定TRX文件名被覆盖的日志不删除，不把那个单文件当六套件证明。Spec Kit全目录及staged diff检查通过，新增代码均不足500行。未改API/前端，因此本阶段未跑Redocly和前端套件，最终T018仍待功能接线后执行。

T004及之后未完成：正式HTTP鉴权/来源/异常清理、动态OPEN与Worker信令注入、专用资源、前端界面及null源身份、内容音频、正常HTTPS、真实选择器取消、正式双窗5分钟/十轮、D4跨机、API文档及全套回归。

上述本机prototype不能勾选FR-001至FR-011或SC-001至SC-005全部完成，不把以前D4部署/媒体测试作为本功能证据。
