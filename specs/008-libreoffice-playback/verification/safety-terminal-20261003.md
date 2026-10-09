# 2026-10-03：终态、外层关闭、Broker展开与上传接线

范围仅本机；D4继续暂停，未部署、推送、开关电源或更改墙面/系统音量。
接入前保存点不变，原件、数据库、历史失败记录保留。

## 1. Worker不确定终态（T038）

通用执行catch原先把已经下发的原生超时/断管/关闭未确认归为failed。
现仅将类型明确的PresentationTransportException记为uncertain/presentation_transport_uncertain；
纯包拒绝及普通IO仍failed。运行态报告错误但不清最后Actual/actual_fit_text/cleanup_pending。
Dispatcher原有uncertain持久解析继续使用，不另加相互矛盾的分类。

真实独立SQLite与NamedPipe、公开RuntimeWorkerSession覆盖五类故障、持久结果和最后Actual。
丢结果ACK后的completed/uncertain均重发同MessageId及同结果指纹，执行只一次。
证据位于`.validation/008-worker-uncertain/`：uncertain-red.trx先红、uncertain-green.trx绿；
uncertain-classification-green.trx为13/13。uncertain-replay-green.trx曾有一项夹具数据库占用，
未删除该失败；根因和修复见第3节，相关回归最终18/18。

## 2. 外层Worker关闭（T039）

Host、App及退出协调共用已发布的一次关闭完成任务；首次失败/并发/重复Dispose不擦除终态。
外部资源关闭完成前保留_current及画面；失败不得清屏并正常退出。退出协调失败code=1。
同步/异步资源各自尝试清理，错误保存；关闭中的新命令立即拒绝，迟到后台采样返回null。
没有改动原本正确的单条CLOSE成功后再清源次序。

测试通过隐藏STA及外部演示引擎适配器调用真实公开Host生命周期，不显示窗口、不启动Office。
六项覆盖首次失败保留同画面/重复失败、并发成功和失败、退出码、关闭中命令、关闭后迟到采样。
`.validation/008-worker-lifecycle/`保留outer-close、app-close、inflight-command、queued-sample四个红记录；
outer-lifecycle-final-green.trx为10/10（六项新生命周期+四项既有页码）。
实际App进程Shutdown、UNO关闭和物理画面未由这些适配器回归验收。

## 3. Broker真正停止（T047）

连接和dispatch曾fire-and-forget，Stop仅等待accept主循环，数据库事务仍可在展开。
自有任务在执行前登记；连接finally先等全部dispatch，Stop再等全部连接与既有supervisor观察。
调用方取消只取消本次等待，不丢弃自有在途任务；不清全局SQLite池、不靠sleep/retry掩盖。

真实管道与独立SQLite SaveChanges拦截门禁覆盖state_report/handshake、正常/取消等待四组合。
观察取消已收到但事务未释放时Stop尚未完成，再释放并核重复/并发Stop真正完成。
`.validation/008-broker-drain/broker-drain-red.trx`先红；worker-drain-green.trx为18/18。
同角色重连直接Dispose旧连接及同instance旧finally影响新Ready仍是T048待验证切片，未泛称所有竞态解决。

## 4. 上传→OPEN接线（T041）

真实SourcePicker/Vue/Naive UI/Pinia/API模块的六条DOM回归先红后绿；
动态fit、静态未准备、PDF原源、图片、背景音频入口以及点击快照均覆盖。
上传已确认但OPEN失败后保留文件/参数/错误和已登记源；重试只OPEN，换新文件才再次上传。

IAB真实浏览器使用回环HTTP夹具（127.0.0.1:20882），无API代理、不登录、不保存字节、不启动Native。
自有sample.pptx只是测试字节，不是有效文稿，不能当格式/转换成功证据。
静态PDF+fit=true首次上传及重试：upload=1/open=2，两次OPEN参数完全相同；
随后改为LibreOffice+fit=true显式重试：upload仍1/open=3，仅模拟受理，界面清上传草稿。
桌面和390宽度错误提示、选项及重试按钮可读；body宽度/scrollWidth均390，业务元素无超界。
控制台error/warn为空。原documentElement clientWidth380/scrollWidth390是滚动条差异，未误报业务横溢出。

截图保留于`.validation/008-upload-browser/`：static-retry-desktop.png、static-retry-mobile.png、
dynamic-retry-desktop.png。自有tab关闭、viewport复原，按PID/start/父链确认后停止Vite67644，20882监听已撤除。
本轮为界面/请求接线，不是生产Host或LibreOffice出画证明。
浏览器经验回顾：本轮是一次性夹具，现有127.0.0.1站点笔记限定生产控制台；没有新增可泛化站点事实，保持该笔记不变。

## 5. 当前媒体源schema（T042）

旧schema缺libreoffice enum，且声称默认页图/实验PowerPoint，与真实上传响应不符。
新增真实Simulation认证→上传测试先红于Contains(libreoffice)，并保留schema-red.trx；
修文档后首条绿，列表PPTX/PDF/图片矩阵最终schema-matrix-green.trx为4/4。
样本只是自有文本字节登记，不启动Office，不证明格式可读或渲染。
schema区分原件可尝试、默认模式与实际观测，原/fit四制品及原版顶层准备字段分别说明；
powerpoint只作为历史只读值，不恢复实验入口。静态OPEN不借布局的描述限定为OPEN，不把T040预览当已修。

pnpm dlx沿CERNET获取新版时节点下载失败，没有修改.npmrc或依赖；
使用现有pnpm缓存的官方Redocly2.55.0执行lint，0错误/3个退役410入口无2XX告警。
完整schema语法/引用由Redocly验证，测试只核实际响应的关键枚举，不声称通用schema validator。

## 6. 同角色重连（T048有限回归）

旧连接局部dispatch drain曾被新hello中的直接old.Dispose绕过，发送gate也在有waiter时销毁；
Ready仅按可复用instance判断会让旧finally/Wake失败影响新连接。
公开真管道/独立SQLite测试在实际COMMIT后门禁保持旧state_report，独立authority提交证明新hello已登记，
没有以SQLite写锁造成的等待冒充连接退休门禁。修前welcome提前到达，reconnect-red.trx明确业务失败；
首次测试构建CA2016仅修显式CancellationToken.None，不当业务红。

同角色安装串行并在既有5秒握手预算内等旧连接真实退休；退休先停止接纳、取消自己的IO/排队发送，
等reader/全部dispatch/active sends后才销毁gate/CTS。发起退休与最终退休各有共享完成门禁，
防Cancel未发生却先Dispose CTS的并发；超时不发布新连接，旧清理继续。
Ready及其摘除改为确切connection归属，Wake/退出通知不按旧instance删新证据。
首条reconnect-green.trx为1/1，核原owner、新Ready及公开Wake健康。
固定512次公开Wake且不读旧管道，实证存在pending后才重连；所有旧Wake在5秒内结束，
新Ready/Wake正常，reconnect-send-queue-green.trx为1/1。Wake正常返回仅表示等待已结束，不保证通知送达。
收尾审查还发现失败Wake/退出通知提前摘slot可绕过旧dispatch门禁：
首条加disconnectAndWake后1绿1红（reconnect-wake-detach-red.trx）；修为仅请求退休，owner最后摘除，
reconnect-final-green.trx为3/3（两COMMIT交错+一发送队列）。
最后绿色重构将精确detach移至全部dispatch/发送/stream/gate/CTS收尾之后，失败仍保证摘失败slot并缓存异常。
T048完成这一竞态切片，不代表三重并发重连、握手预算到期或全部IPC异常已实测。

## 7. 统一回归与剩余门禁

`.validation/008-safety-convergence-full/`全非Physical820/820、零skip：
Domain41、Windows86、Contracts174、Infrastructure127、Integration209、ControlHost183。
加入schema四项及重连首条后，再次全非Physical825/825、零skip：41/86/178/127/210/183；
命令为`dotnet test runtime-dotnet/ScpCv.sln -c Release --no-restore --filter "Category!=Physical"`。
该轮输出未指定TRX logger，不把空results目录当已有TRX；首条红绿有独立TRX。
最终完整退休顺序修正后重编译，全非Physical827/827、零skip：41/86/178/127/212/183，
六份TRX保留于`.validation/008-safety-final-owner-retirement/`；此前825及首次827版本证据也保留。
前端100/100、零skip，类型检查和build:web通过；主包约1096.66kB既有分块告警保留。

原生底部裁切的三次失败承载实验仍需人类架构讨论，不叠加第四个猜测；
实际布局预览、依赖就绪、导航/动画完成、最终双窗长稳、真实旧PPT/加密/宏和完整发布组件验收仍未完成。
