# Tasks: LibreOffice完整接入

**Input**: spec/plan/research/data-model/contracts/quickstart；先保留549c57f保存点。

## Phase 1: Setup

- [X] T001 保存当前Git提交/标签与忽略实验代码归档，记录于 `research.md`，保留原件/数据库。
- [X] T002 在 `spec.md` 明确全部LibreOffice/退役PowerPoint及本机范围，requirements清单16/16。
- [X] T003 [P] 在 `.validation/libreoffice-integration-spike/` 证明.NET10旧CLI不兼容，并验证纯C#Net48官方绑定连接自有profile；失败日志保留。

## Phase 2: Foundational

- [ ] T004 在 `.validation/libreoffice-integration-spike/` 用纯C#正常Frame证明去菜单/导航、完整viewport、只读、动画和有界正常关闭。正常Frame显示和UI异步结束已有限实测；2026-10-02视频回退另有原生崩溃/卡住，异步UI跳页后原排版及适配各300轮正常关闭通过。双窗冷启动间歇超时及最终媒体/视觉仍待复核，旧失败/dump保留，不靠延长timeout/跳过end制造成功。
- [X] T005 在 `runtime-dotnet/tests/ScpCv.Integration.Tests/ShutdownPendingIntentTests.cs` 及独立回归先红，覆盖两窗旧Pending、音频、重复停机、迟到结果和最后观测。
- [X] T006 在 `runtime-dotnet/src/ScpCv.Infrastructure/Runtime/RuntimeAuthorityRepository.cs` 和partial修复确认停机后的Pending/Processing退役及投影，保留Actual/Observed，针对性19/19、加入排他后完整非Physical576/576；见 `verification.md`。
- [X] T007 在 `runtime-dotnet/src/ScpCv.ControlHost/Endpoints/SystemEndpoints.cs` 修复重复Stopped仍入队CLOSE及重启过早确认：先真实stop成功，再CompleteStop/BeginStart/start；失败不启动新组，合同4/4红→绿、相关15/15已通过。独立复核追加 `RuntimeSystemOperationGate.cs` 和 `SystemOperationConcurrencyTests.cs`，真实HTTP并发4/4红→绿，第二个启停请求在任何状态/外部操作前409拒绝，包含完成后再试，相关19/19、全576/576。原生stop超时后迟到操作仍需T008/T010单独身份围栏，不能据此宣称全部并发安全。
- [X] T008 在 `runtime-dotnet/src/ScpCv.Presentation.Protocol/` 建立Net10/Net48同源纯值协议、64KiB framing、快照后能力/身份门禁和完成指纹匹配；105/105纯回归、全非Physical681/681、真实Net48中文路径往返字节/SHA一致，无UNO/WPF/新包。不等于已接生产或真实OS/租约授权。

## Phase 3: User Story 1

- [ ] T009 [US1] 在 `runtime-dotnet/src/ScpCv.LibreOfficeHost/` 以官方CLI实现只读自有profile/引擎/文稿/窗口，声明依赖配置与发现，不触碰用户实例。
- [ ] T010 [US1] 按plan的2026-09-30调整，由Player/ControlHost按需拥有impress1/2及converter侧车，不新增Supervisor常驻空角色；OS父身份、启动门禁、双层独占job、结构化身份/协作退出及精确子进程证明，补异常父退出与组停机回归。
- [ ] T011 [US1] 在 `runtime-dotnet/src/ScpCv.PlayerWorker/Presentations/` 实现fenced adapter与Frame承载，补打开/失败留旧画面/关闭/重开/晚结果回归，拆分近500行宿主。
- [ ] T012 [US1] 在 `PresentationPlaybackSelector.cs`、OPEN参数、Snapshot/Progress及投影接入LibreOffice默认，PDF/页图显式模式，明确真实confirmed与desired。
- [ ] T013 [US1] 在导航/媒体合同与adapter中实现动画步/跳页及已验证能力；当前统一媒体暂停/恢复/定位capabilities=false、前端禁用、后端unsupported无副作用。mute/volume未验证也不启用，不伪造实际位置。

## Phase 4: User Story 2

- [ ] T014 [US2] 在 `ScpCv.ControlHost/Media/PptConversionHostedService.cs` 及Presentations/移除PowerPoint转换，LibreOffice独立converter导出有序PNG/匹配PDF与页数，不改原件。先解决/核验转换spike中oMath透明fallback黑块和严重重排，区分原件排版；PDF实含EmbeddedFile/Screen/Rendition不能直接标扁平静态，需剥离/禁用交互或明确page-render-only语义与体积预算。dirty标志只作内存诊断，RO/URL/实体SHA/锁/退出保护不放宽。
- [ ] T015 [US2] 在 `ScpCv.Infrastructure/Media/` 解耦原件/native可用与静态准备，保留摘要/修订关联、uncertain手动确认、活跃文稿关闭证明前禁止移动/删除。
- [ ] T016 [US2] 在上传/prepare/移动tests补原件、预览失败、转换晚到、中文文件夹与关闭在途，原件SHA和锁保持/释放真实。

## Phase 5: User Story 3

- [ ] T017 [US3] 在本机 `.validation/` 完成用户LibreOffice共存、两窗同/异源10轮切换关闭重开、100控制和精确PID/文件锁，真实像素/媒体与状态分开。
- [ ] T018 [US3] 在Host/adapter/pipe tests及本机进程注入验证崩溃/断线/超时/取消/晚到/关闭失效，禁止全局kill和盲重放。
- [ ] T019 [US3] 在本机至少60分钟双窗混合/替换观察资源/画面/媒体/退出，已知严重缺陷先修再同条件复跑，D4仍待授权。

## Phase 6: User Story 4

- [ ] T020 [US4] 在前后端/settings与OpenAPI退役PowerPoint/reset-ppt旧COM入口，新增presentation依赖/能力/显式模式，并清除运行时PowerPointHost启动和调用。
- [ ] T021 [US4] 在 `frontend/src/features/display/`、sources/settings抽独立presentation controls/capabilities，动态下一步/翻页/媒体与静态fallback不同，桌面/手机真实回归。
- [ ] T022 [US4] 更新 `docs/openapi.yaml`及schemas/paths，Redocly校验；settings/source/session/prepare状态与实际实现一致。

## Final

- [ ] T026 按2026-10-01确认增加默认关闭的可选文字适配，覆盖协议指纹/安全拒绝、动态与转换一致性、原件SHA和真实视觉复核。

- [ ] T023 更新 `README.md`、使用/维护/CHANGELOG/known-pitfalls及安装/运行脚本；治理约束同步作为独立constitution步骤，不改历史实测结论。
- [ ] T024 全.NET非Physical/相关Physical、pnpm测试/类型/Web构建、真实UI/Spec/Redocly，记录 `verification.md`，未执行原因明确。
- [ ] T025 收尾自有进程/窗口/profile监听，原件/数据库保留；按小块提交，仅用户另行要求时部署/推送。

## Dependencies & Parallel

保存点/规范→interop门禁与停机安全→协议/宿主→两窗播放/转换/控制台→稳定/完整验证。T005–T007已完成，T008可与T004关闭/显示诊断并行，无原生副作用；生产承载/默认路由仍须T004通过。统一媒体控制已获允许明确禁用，不豁免关闭/显示门禁。不同目录可并行；所有.NET构建由root协调，真实桌面操作错开。

## Implementation Strategy

先解决外部能力与生命周期，不删旧入口制造无法播出的中间版本。各门禁失败保持任务未完成，报告真实原因；不能把设置/DTO/禁用按钮当完整引擎接入完成。

## 2026-10-02补充：文字布局重开闭环

- [X] T027 [US4] 为Worker实际资源、fenced投影、HTTP/SessionSnapshot及前端重开补可空actual_fit_text垂直切片；Worker2条及HTTP8条红→绿、重开5条真实DOM红→绿，统一非Physical731/731、前端87/87。失败换源保留旧布局，关闭/非文稿清空，晚代次拒绝，重开沿实际资源。正向EF迁移不接用户库；不从desired或OPEN受理推断。该完成仅为布局观测闭环，不是原生满幅或T026视觉验收。

## 2026-10-03安全与发布收敛

- [X] T028 [US3] 关闭完成任务由Client/Surface共享并缓存真实终态，重复/并发/同步失败不得变成正常退出或重放shutdown；纯回归及真实自有子进程2红→5绿。没有解决原生退出根因，不替代T018。
- [X] T029 [US4] 修复发布全局RID与锁文件不匹配，保留LockedMode并独立选择x64 apphost；七条合同及全新包实际publish通过，五组件x64 PE及协议TFM隔离核对。发布ControlHost独立Simulation根真实启动/认证/SSE通过，未启动其它发布组件，不等于整个包原生可用。
- [X] T030 [US4] 当前连接主机端口摘要及API诊断地址复用实际请求解析，七个公开/真实DOM回归先红后绿；桌面/手机真实页面同为20881、无横向溢出，运行文案与DataRoot/appsettings配置一致。没有改变认证或连接安全策略。
- [X] T031 [US2] 转换profile从深层DataRoot暂存目录迁至既有短的独占引擎目录；暂停进程在放行前缓存精确OS句柄，Framework真实自有cmd退出码红→绿。五项路径/进程回归、同深度数据根两原件原/适配四次生产转换和移动锁/SHA验证通过。保留此前三个失败根与dump；不宣称已掌握全部C++启动异常或解决其它间歇风险。
- [X] T032 [US2] 显式PDF/页图选择的畸形metadata保持所请求模式的错误，不静默回退；数值/对象类型、修订和两布局边界37项回归，选择器17红/HTTP4红→全绿。原问题为PDF错误误报页图，不是未捕获500；不新增历史摘要格式约束。
- [X] T033 [US3] 公开客户端的deadline覆盖排队与IO；真实自有stdio侧车回归排队过期先红后绿，未下发取消不poison。半帧取消/迟到响应、错误scope/长度/截断、在途与排队并发关闭六项通过；测试失败清理有精确句柄和有限预算，fixture不进入发布包。不替代原生卡死、窗口/媒体验收。
- [X] T034 [US3] 本机自有空converter真实父死亡三次；先确认精确Framework/Office存活及自有监听，再结束父进程，两个精确句柄均在5秒内退出、端口消失。仅证明job兜底，不证明ParentProcessLifetime timer分支或持有文稿的正常关闭；T018仍未完成。
- [X] T035 [US3] 复现launcher退出后短活自有子成员（4096×16出现4次，含conhost）；Client在原有15秒总关闭预算内等完整job，不再即时误报。受限NUL继承/门禁子进程先红后绿；30秒长子成员仍在15秒报uncertain、精确兜底、重复失败不擦除。cmd句柄回归沿既有5秒真实成员退出预算，保留旧失败，不把launcher退出当全部退出或扩大为所有原生风险已修。
- [X] T036 [US3] 正式Framework父监测源码Compile Link至测试组件，三轮各4项真实父身份/Timer测试通过；无自有job与stdin EOF，错误PID/start/session无ready且退出2，父死亡正式timer日志及约0.5秒退出2。新增可重复的公开进程合同测试，不依赖忽略QA源码；不覆盖PID复用或文稿关闭。
- [X] T037 [US2] 只读包元数据从UNO应用分离并Net10/Net48同源；非法TargetMode和绝对内部URI先红后绿，16真ZIP边界通过。转换/动态候选前预检，只有纯包拒绝明确Failed；三类真实上传/持久作业/Converter在不存在宿主下拒绝、原件不变，未广泛吞原生不确定错误。两真实文稿原/fit四次转换、32PNG/四PDF与旧制品摘要完全一致，全非Physical804/804；旧版/加密/宏样例与原生门禁不据此验收。

## Phase 7: Convergence

- [X] T038 **CRITICAL** 保留已下发PresentationTransportException为uncertain/presentation_transport_uncertain；真实pipe→持久终态五项分类及丢ACK重放回归通过，包/普通IO拒绝仍failed，Actual/布局/cleanup不被清除。Dispatcher既有uncertain投影无需重复实现，不重放原生执行。见verification/safety-terminal-20261003.md。per FR-008、US3/AC3、Constitution I/IV
- [X] T039 **CRITICAL** 外层Host/App共享一次真实关闭终态，资源关闭成功前保留画面，失败退出码1；并发Dispose、在途新命令与迟到采样有界处理。拆分生命周期文件，隐藏STA+外部适配器六项及既有页码四项通过；不是实际App进程退出或UNO原生验收。见verification/safety-terminal-20261003.md。per FR-007/008、US3/AC3、Constitution I/IV、plan: lifecycle
- [X] T040 两预览GET明选fit布局、共用摘要/修订/连续页/受管理文件门禁；列表明传根不信历史row、缺页显式prepare真排新job。前端按actual源/布局/adapter/窗口/观测代次，旧响应及卸载不覆新预览，纯GET手动刷新不重置自动预算；PptFocus拆共用loader。49后端及22真实DOM/桌面390夹具通过，最终非Physical876/876、前端122/122；合同/文档同步。不冒称完整PNG解码、文件symlink、原生画面或D4验收。见verification/preview-layout-20261003.md。per FR-005/010/013、US2/AC1、T021/T026、plan: separate artifacts
- [X] T041 上传并打开复用选源的presentation_mode/fit_text决策，点击时快照，仅PPT原件带选项；已上传但OPEN失败保留反馈和已登记源，显式重试只OPEN不重复上传。六条真实DOM红绿、前端100/100与IAB桌面/390静态失败重试及动态fit接线通过；HTTP夹具不证明真实转换或原生出画。见verification/safety-terminal-20261003.md。per FR-005/013、T021/T026
- [X] T042 MediaSource schema与使用文档同步LibreOffice默认、原件可用条件、独立原/fit制品及历史powerpoint只读退役语义；真实认证上传/列表PPTX/PDF/图片4项先红后绿，Redocly2.55.0为0错误/3退役入口告警。显式静态OPEN门禁不冒称T040预览已修，历史QA不变。见verification/safety-terminal-20261003.md。per FR-003/005/012、US4/AC2、T022/T023
- [X] T043 在独立 `Contracts/Presentations/Dependencies/` 及设置端点实现只读固定文件、平台/Framework、PE/TFM/协议常量和五CLI实际引用绑定诊断，不只凭两文件存在；未确认配置明确不可用，不泄值或启动Office。69策略+2HTTP、27DOM及桌面/390夹具通过；最终非Physical947/947、前端149/149。真实探针16项通过且.Net10不加载UNO；不冒称完整CLR/native加载、签名真实性或出画。见verification/dependency-diagnostics-20261003.md。per US4/AC1、FR-012、plan: official interop dependencies
- [ ] T044 收敛 `ScpCv.LibreOfficeHost/Uno/PresentationDocument.cs` 所有公开导航入口的执行/完成语义，验证wire next/previous_slide与next/previous_effect的直接Controller路径、动画末步跨页/视频页往返/边界页，不只据页码Confirmed判断动画成功；Player的NEXT/PREV/GOTO已统一UI goto，不重复迁移。保留原生失败并有界清理，完成实际画面和关闭回归。per FR-006、US1/AC2、T013 (partial)
- [ ] T045 在T004承载讨论获人类确认后处理原生底部裁切，随后完成新版两窗同/异源10轮、100控制、用户实例共存、真实旧PPT/加密/宏拒绝与故障隔离，最终同条件60分钟混合/替换验证；缺样例如实登记，不用静态转换或回归数量替代出画。D4软件实测/相关环境安装现已授权，未证实的裁切仍不得叠加第四个同类猜测修补。per FR-004/007/008、US1/AC1、US3/AC1/AC3、SC-002/003/004/005、T004/T017-T019 (partial)
- [ ] T046 使用最终完整包在本机及D4明确两输出绑定及独立DataRoot，验证Player/Audio/LibreOffice/MediaMTX依赖、启动/失效/正常和强清退出，证明组件实际运行且只占两块指定输出；不把已验证ControlHost认证/SSE作为整个包验收，封装控制端文稿关键流另做真实回归。D4相关环境安装和软件测试按2026-10-03新授权执行；正式源码更新沿secondary推送/远端拉取，不覆盖未知运行数据。per FR-004/012、SC-001/004/005、T023/T024、plan: packaged runtime (partial)
- [X] T047 Broker停止登记并等待连接处理及所有并发dispatch的真实finally展开；Stop等待取消不删除在途任务，重复/并发Stop仍等待原事务。真实管道+独立SQLite四项先红后绿，相关18项及全非Physical820/820；不清全局连接池、不延时掩盖。见verification/safety-terminal-20261003.md。per FR-008、US3/AC3、Constitution I/IV
- [X] T048 公开COMMIT门禁复现重连提前welcome、断管Wake提前摘slot两红；精确连接一次退休、dispatch/发送全部归还后才释放gate并摘slot，Ready归属到connection，安装共用既有握手预算。三回归（正常/断管Wake交错/512积压）绿，不改认证或将Wake返回当送达/进程退出。并发三重重连/预算到期矩阵未由这三项验收，见verification/safety-terminal-20261003.md。per FR-008、US3/AC3、Constitution I/IV
- [X] T049 导航输入在副作用前拒绝：服务goto必需正int32，不让null默跳1；UI完整十进制文本不parseInt截断。两公开入口先红后绿，15HTTP/15DOM及桌面390夹具通过，first/last保留；最终串行项目非Physical962/962、前端164/164。首并行OOM/提交限制两失败保留，未改预算/断言；T044两次监听启动失败无导航实证，不据此验收原生或动画。见verification/navigation-input-20261003.md。per FR-006/008/012、US1/AC2、T044 (partial)
- [X] T050 仅全新自有profile预置官方MAR Enabled=false，强制开启覆盖启动前拒绝，两个纯文件入口先红后四绿；不改用户环境/默认profile/安装，不用诊断URL生产。更新join转储/官方符号+A-B-C实证、无URL新实例三短轮及四生产转换通过，非Physical966/966。冷启动放行后另复现wire视频上一页阻塞，整页复用既有UI goto后三新轮正常退出；动画/裁切/长稳仍不据此验收，旧失败/QA退出码误判保留。见verification/owned-update-20261003.md。per FR-004/007/008/012、T009/T010/T018、plan: owned profile update isolation
- [X] T051 T044诊断切片：默认关闭的官方listener+128有界纯值buffer，回调不UNO/IO/等锁，四纯回归容量红→绿；两ON/一OFF新实例各62返回/正常退出，第二效果返回1.45ms而主序结束约1055.6ms，异步跨页与撤销事件实证。非Physical970/970；不将操作窗口/事件数当因果完成，不改协议/能力，未有像素/媒体长稳门禁。见verification/effect-events-20261003.md。per FR-006/008/012、T044 (partial)、plan: diagnostic first
- [X] T052 未终结指令投影三类公开HTTP五红→绿，最早Pending/Processing在领取/报告/新入队保持；15保护含三终态/重复/替换/三目标隔离，两页8DOM通过。最终非Physical985/985、前端172/172；首丢ACK重连超时保留，复核绿不称根因解决。原生动画中关闭/换页与快速三次仍原页分别记录，两有限短轮正常退出，不算步序/视觉完成；仅本机、协议能力/D4不变。见verification/unfinished-command-20261003.md。per FR-006/008/012、T044 (partial)、plan: unfinished projection
- [ ] T053 按用户确认实现动画中“快速完成当前动画后下一步”。当前主序内存空尾的真实旧行为红→正式基础绿、诊断OFF三轮及无主序立即UI换页/视频页码边界/正常关闭通过，无盲双调用或固定等待。撤销残留报告已由原图/摘要/像素证实误读并撤回，ON/OFF各两轮像素门禁通过；单页动画末页真实黑屏而仍返回页码1，末页行为选择待回答。继续末页/初始就绪/复杂序列/快速多次与真实UI，不能据985非Physical或短跑勾完。见verification/rewind-boundary-20261003.md；仅本机。per FR-014、US1/AC5、T044 (partial)、plan: effect advancement
- [X] T054 动画组件卸载后的旧HTTP错误不污染后续页面；保持共享Pinia/通知的两DOM业务红→绿，scope守卫不改变受理/重放，相关10及全前端174、类型/Web构建绿，IAB真实回环旧错误忽略/当前错误保留通过。两个fresh正式冷开OPEN后立即首步像素2059/0、次步2059/1721及正常关闭通过，无点击前等待；只补有限样例，不据此完成T053/末页/长稳。见verification/cold-click-lifecycle-20261003.md。per FR-008/012/014、T021/T053 (partial)、plan: component lifetime
- [X] T055 两正式独占侧车并存，窗口1/2轮换活动端，各十轮100控制/20次异源关闭重开，保持端标题阶段；每轮页码/有限像素、中部差异0、摘要/关闭后副本独占读及最终四job正常退出。整窗/客户区差异另报并保留两个旧exit2，不称整图问题已修或满幅通过；用户实例共存/两窗交叉/媒体/长稳仍欠。见verification/dual-isolation-20261003.md；仅本机QA，不替代T045/T046或部署D4。per FR-007/008/014、US3/AC1、T017/T053 (partial)、plan: dual isolation
- [X] T056 公开Player OPEN真红复现旧关闭transport被候选清理IO覆盖；私有生命周期处理保留双原因/typed uncertain，成功清候选仍原异常，候选不发布旧观测保持。新7+旧6生命周期、真实Worker持久六分类及完整非Physical993/993通过，未改几何/顺序/预算或原生引擎。本轮无Office，不据受控adapter/隐藏树称旧物理画面保持或实际UNO退出已修。见verification/replacement-cleanup-20261003.md。per FR-008/012、US3/AC3、T011/T018/T038 (partial)、plan: replacement cleanup
- [X] T057 公开Player切入失败仍采样playing、候选清理异常/归属与空输出NEXT假成功真红→绿；切入后才发布，确认空输出明确failed/null/error，活页图保留，未确认候选隔离并纳入退出。新8项、持久空结果形态、前端按钮/专注键盘3DOM与IAB通过，最终非Physical1002/1002、前端177；内部None/HTTP空串不混，QA错断言/编译失败保持。本轮无Office/几何变更，不称原生门禁完成。见verification/activation-publication-20261003.md。per FR-005/008/012/013、US1/AC1、T011/T012 (partial)、plan: activation publication
- [X] T058 只读真实单DISPLAY1条件、正式Player小窗生产承载两轮（第二轮最新包侧车）、标题步序/原件/正常job退出通过；原35px黑带粗估后由原PNG量化撤回为2行，非满幅证明。SDK NuGet内层socks5h不支持定位，构建进程暂换socks5且保审计/锁后最新五x64包生成、16只读依赖通过并恢复环境。MediaMTX外配/两屏/全组未验收，不称整个目标完成，见verification/native-player-package-20261003.md。per FR-004/007/008/012、T011/T046 (partial)、plan: actual Player baseline
- [X] T059 正式Player/最新包LO样例视频后帧有人物/模型变化，播放中CLOSE及换源后CLOSE两组正常退出；精确job/监听、文件释放/摘要、关闭图360000像素黑分别证明。原WPF黑带粗估按原PNG撤回；初期黑、时钟/音频/全格式未测，统一控制/几何不改，D4未启动。见verification/native-video-close-20261003.md。per FR-006/007/008、SC-003/004、T013/T018 (partial)、plan: native video close
- [X] T060 两fresh正式Player小窗口各三轮/25明确控制，冷OPEN后两next2059/1721、两撤销0/0、三next随后观测页7，最后首效果后立即CLOSE/正常job退出/副本及原件保持。首QA即时页码断言失败保留，同1.5秒窗口时序实证异步6→7，不补发/延预算或称播放器红绿修复。独立画面复核与有限像素通过，不替代T053复杂/末页/UI或长稳，见verification/native-rapid-effects-20261003.md。per FR-014、US1/AC5、T053 (partial)、plan: native rapid sample
- [X] T061 两fresh普通只读Office与项目快速控制/视频换源关闭/父异常共存，另fresh明确CLOSE前媒体父异常；682次页/URL/模型摘要/精确进程监听窗口检查、原图和12副本锁/SHA通过，独立Office在项目退出后仍可用再自行正常关闭。前两次可能已进CLOSE的局限保留，强门禁不改产品预算；用户默认profile/未保存编辑/完整故障矩阵未验收，见verification/office-coexistence-20261003.md。per FR-007/008、US3/AC1、SC-004、T017/T018/T045 (partial)、plan: independent Office coexistence
- [X] T062 公开转换.ppsx与改名.ppt外链包绕过预检两业务红→绿；六OOXML声明扩展及ZIP内演示内容同一门禁，非OOXML旧格式保持原路。11公开转换/26包/3公开隐藏Player通过，完整非Physical1026/1026；四正常真实转换36PNG/PDF摘要与旧版相同，新五x64包含修复。错误采样夹具/分析器失败保留，宏禁执行不冒称全部宏/加密预检拒绝或实机，见verification/package-content-gate-20261003.md。per FR-004/007/012、T014/T015/T037/T045 (partial)、plan: content-aware package gate
- [X] T063 本轮自有真实PowerPoint97与加密ODP/OOXML夹具、正确密钥/无密钥对照、Player整页出画/动态拒绝留旧/退出、旧PPT9页静态发布及两加密静态明确拒绝完成，锁/SHA保持；历史QA失败保留，旧uncertain不改。该有限夹具不是全部宏/历史PPT/媒体/版式验收，T045仍欠，见verification/load-rejection-20261003.md及legacy-encrypted-formats历史记录。per FR-004/007/012、US1/AC4、T014/T017/T045 (partial)、plan: legacy encrypted fixtures
- [X] T064 公开ODP invalid_source_path红→绿，其余变体8红→绿，既有10种非PDF文稿格式与协议接线对齐，18相关/最终1044非Physical通过；宏/外链/本地路径/身份/期限不放宽。真实旧PPT及加密动态候选留旧复核、新64fbc8…Net10/48包完成；初错误被清理覆盖不当密码加载证据，转换uncertain另欠T065，见verification/legacy-encrypted-formats-20261003.md。per FR-004/005/012、T013/T014/T063 (partial)、plan: accepted source paths
- [X] T065 真实公开Converter Physical红exit2→绿Failed/load_rejected及已有输出负例2/2、4纯输出guard；加载Abort且无模型/自有组件/输出才typed拒绝，正常EOF完整job退出仍必需。三源真实流程exit0/新旧正常10制品完全一致、全非Physical1048通过，旧uncertain不重试/改终态；创建候选/转换双异常诊断另由T066完成，完整范围保留，见verification/load-rejection-20261003.md。per FR-004/008/012、US1/AC4、T014/T018/T063 (partial)、plan: known load rejection
- [X] T066 公开Converter4项/实际Player adapter2项受控stdio红→绿，原动作异常或Failed响应与后退出错误同时保留，正常退出仍原异常/结果；typed uncertain、退出预算/重放/原件保持。全非Physical1054、另真实2Physical通过，三源真转换及正式Player留旧/正常CLOSE复核，新430158…本机包；夹具缺摘要/构建错误不计业务红，完整范围不删除，见verification/creation-cleanup-diagnostic-20261003.md。per FR-008/012、T018/T065 (diagnostic follow-up)、plan: operation cleanup diagnostic
- [X] T067 公开Player→正式adapter CLOSE双失败业务红→绿，五关闭保护含真实自有父窗销毁后的三原因；重复CLOSE/Dispose原失败保持，单失败原类型/栈及正常空actual不变。全非Physical1059、两真实视频关闭/换源正常退出与原件/锁复核，新981767…本机包；DPI144截图测量边界记录不冒称满幅，完整T018/双输出/长稳不缩减，见verification/published-close-diagnostic-20261003.md。per FR-007/008/012、US3/AC3、T018/T028/T039 (close diagnostic follow-up)、plan: published surface cleanup diagnostic
- [ ] T068 仅QA验证同一自有Player小窗的截图DPI虚拟化：当前96 DPI三轮九图同SHA/物理矩形、六上下文恢复/四声明拒绝及正式PPT三轮25控制/十物理图通过；编码负例误判保持，前端本轮177/类型/Web通过。144 DPI差异尚未复现，任务保持未完成，不改生产几何/系统缩放/共享助手或旧图，完整viewport/两输出/长稳范围不缩减，见verification/capture-dpi-measurement-20261003.md。per FR-004/012、SC-005、T004/T045 (measurement follow-up)、plan: capture DPI evidence
- [ ] T069 按2026-10-03明确要求实现末页动画耗尽后的无播放动作与前端“当前已是最后一页”反馈；仍有动画不能禁用，倒退/跳页/重开/换源不误用旧完成事实。先复现带动画末页黑屏/同页观测的实际红，补原生/公开Player/持久结果/两页面DOM及D4实机回归，原件/超时/进程归属保持。per FR-014/015、US1/AC6、T044/T053 (terminal behavior)、plan: terminal hold
  2026-10-05切片：启动后/已重显示后改模型两独立D4实验正常0；正式预绑定原末组/自有空尾、实际终点模型保持、不可变listener轮与有界纯值事实已接。12纯保护三红绿/全1135；同新版父测试旧候选图相同却extra返回ok业务红→当前3轮30noop/保持/撤销/关闭绿。自定义重复页明确限制但负例/隐藏/完整模式/零时长/重开两窗等仍欠，不勾；见verification/animated-terminal-evidence-20261005.md。
  后续同版矩阵：普通七页、[1,6]多页GOTO/PREV/NEXT/重开、隐藏尾6/7与手动7、零叶子完整图/30noop、PAR重复新轮3ok后noop及保持、重复页拒绝留旧图均正常退出；深度诊断关闭。重复组首QA误把动画中图当完成而失败，原图定位后仅改判据，不禁重复。1135非Physical及28图复核有限通过；自动/复杂/并行/完整持久反馈等仍欠不勾，见verification/terminal-matrix-20261005.md。
  最后正式EXE404B7D8B…同版普通文稿实播也正常0/两种边界/第7页/退出通过，不是完整运行组或全部模式，八完成任务精确移除/服务保持停止，源码未secondary。
  2026-10-04补充：公开Player→正式adapter noop真红绿并验证倒退恢复ok，HTTP精确回执/持久查询5保护、两页面44DOM与桌面/390夹具通过；全非Physical1081、前端211。原生宿主耗尽事实/保护及Player整页路由未接完，仍不勾选，见verification/terminal-command-feedback-20261004.md。
  后续：整页桥接已真红绿，正式Host无主序真实终点返回noop；D4两个指定输出顺序小窗保持/倒退/清黑与退出通过，全非Physical1082。旧本机启动超时/山谷假绿证据保留，有主序/隐藏重复页/并行长稳仍未完成，见verification/native-static-terminal-d4-20261004.md。
  独立受控重显示QA原样D4三轮事件/图像通过，但zero容器裁掉内容、repeat2第三next失去完成/后清理重复CLI listener失败；保留证据，未授予noop。zero-leaves及动作原错日志下一轮实测，见verification/terminal-replay-candidate-20261004.md。
  合法zero-leaves已在D4事件/完整文字/清黑退出通过；repeat2原错为observe URP断开、同PID/start WER证实Office异常退出。仅QA stderr取证仍无栈，root原因与安全保护未修，不勾完成，见verification/terminal-native-fault-diagnostic-20261004.md。
  后续D4两份真实AV转储及官方私有符号定位到SequentialTimeContainer::resolveChild；自然End对照仍同位置失败，不能仅撤销End.NONE结案。仅QA诊断/烟测修正，正式策略未改；完整容器/下标与自然作者样例仍欠，见verification/terminal-repeat-native-stack-20261004.md。
- [ ] T070 按最新授权检查D4 SSH/交互会话/显示拓扑/当前服务与仓库，安装缺失的免费运行环境并核版本/摘要；使用独立验证根/明确两大屏输出进行当前LibreOffice包实测，正式源码更新沿secondary推送/拉取，保留原件/数据库/旧失败，不更改电源/未知墙面映射/音量。per FR-004/007/012、SC-001/002/004/005、T045/T046 (D4 software gate)、plan: renewed D4 scope
  当前v3独立QA在DISPLAY2/3小窗各一次真实通过，源码b2f09c5未同步、完整组与并行/100控制/60分钟未验收；两完成任务精确移除，相关服务保持停止，原件/包/数据库/证据均保留。

## Phase 8: Convergence

2026-10-04新取舍：稳定运行优先，允许少量明示PPT限制/轻微差异；不要求把罕见PPT特性全部补齐后才检查其它源。历史任务与失败不重写，原件保护、实际末页、正常退出和真实性门禁仍保留。以下为FR-016/017的新增收敛工作，按T071风险边界识别→T072/T073可独立推进→T074完整组验收执行，不以任务安排冒称已实现。

- [ ] T071 **HIGH** 在 `runtime-dotnet/src/ScpCv.LibreOfficeHost/Uno/` 及 `ScpCv.Infrastructure/Presentations/PresentationPlaybackSelector.cs` 建立少量可证明的PPT限制边界：优先识别已复现危险的主序repeat/重启组合，在动作前安全拒绝或采用经验证的受限行为；明确原因/影响/手动静态回退，不静默删动画、不泛拒全部PPT、不改原件或把不确定降为普通失败。补公开打开/保留旧输出、普通动画与关闭回归，以及D4相关样例实证。须依据现有转储和模型证据选实际边界，不预设所有repeat都危险。per FR-016、US5/AC1、Constitution I/IV、T044/T053/T069 (partial)
  2026-10-05切片：按真实堆/固定源码只拦非空MAIN_SEQUENCE SEQ的double>=2或无限重复，7次数保护/公开Player错误detail两红绿，1114非Physical；D4内存注入两拒绝、旧原PNG保持、ordinary/PAR重复4步/正常关闭与SHA锁有限通过。首两QA误判保留，旧动态/另一窗/作者样例等仍欠不勾完，见verification/repeat-safety-and-source-picker-20261005.md。
- [ ] T072 **HIGH** 在 `frontend/src/features/display/PlaybackControl.vue`、`SourcePicker.vue`、`sources/`、共享状态/能力模块及相应DOM测试完成多源前端优化：先复现期望/实际源不一致时的身份、缩略图/“当前输出”、按钮能力和反馈；保持旧实际输出但显式表示新请求待处理或失败，不以desired或参考图冒充出画。逐项核加载/空/离线/重连/旧响应及桌面/390布局，按现有设计系统改善信息与可操作性，不先做无验收依据的大换肤；超过500行先拆模块。per FR-017、US5/AC2/AC4、Constitution III、T021/T040/T057 (partial)
  首切片六类非PPT参考源真红→绿，空输出/旧字段/缺元数据/离线与跨类型适配器保护，共12新DOM、相关42/42、全前端223/223及类型/Web构建通过；IAB模拟页面桌面/390核对。按钮/标题完整一致性、SourcePicker及D4实播仍未覆盖，不勾完成；首QA高占用与矛盾适配器失败保留，见verification/multi-source-output-reference-20261004.md。
  第二切片显示页请求PPT/实际VLC按钮与专注页方向键POST两业务红→绿；共用实际适配器类型保护，空输出/不对应类型禁用相关操作但保留关闭/重开恢复。第一相关103/103、浏览器桌面390布局和错误列表通过；完整能力/标题及SourcePicker未完成，见verification/multi-source-control-guard-20261004.md。
  收尾完整前端226/226、0跳过及类型/Web构建通过，主包1105.19kB告警保留；不把后台旧1092称本轮执行或勾T072。
  第三切片真实音量两红/SEEK一红→共用目标代次/isActive绿，6新增DOM、相关25及全前端232/232，类型/Web与浏览器延迟响应复核通过；仅丢未发缓冲，不取消已受理HTTP。后台/系统音量与独立音频滑块另欠，见verification/playback-slider-fence-20261004.md。
  音频观测切片两DOM红→统一REST/SSE时间围栏与刷新symbol寿命绿；13新DOM、全前端245/类型/Web、桌面390延迟响应复核通过。相同时间保兼容、不称全序，后台离线/通知/滑块仍欠，见verification/background-audio-observation-20261004.md。
  2026-10-05源列表切片：desired新视频/actual旧图误标真红→复用实际ID/适配器/库类型绿，9新DOM/相关15、全前端254/类型/Web及390失败流/1440离线通过；未知输出不借请求，离线最后报告。仅模拟HTTP状态/布局，不当流出帧或全能力验收，见verification/repeat-safety-and-source-picker-20261005.md。
  2026-10-05音频通知切片：loading/OPEN却提示已播放、切主机旧请求错误污染两DOM真红→请求提示/连接代次绿；4新增及相关26、全前端267/类型/Web、桌面390模拟反馈通过。全测发现选源上传音频旧文案键另补断言真红→同提示绿，最终相关41/类型/Web再过，不冒称最终全套已重跑；在线能力/卸载与并发寿命完整检查仍欠，不勾T072，见verification/audio-request-feedback-20261005.md。
  2026-10-05其它入口请求提示：普通选源/上传打开/媒体库打开及背景音乐四DOM loading通知真红→仅文案绿；全前端271/类型/Web与桌面/390模拟页面通过。未改变实际状态、接口或489行SourcesView，不把提示当完成或D4出画；长稳任务独立仍在运行，不勾T072，见verification/open-request-feedback-20261005.md。
  2026-10-05真实完整Web/ControlHost联调：认证连接/SSE首快照200、中文目录/图片201/RTSP201/手机编辑400保留→200恢复/移动200及实体SHA通过，390/1440离线明确禁用、原图检查。初次未配置默认8000拒绝3条与故意400日志保留，不称0控制台错误；本机Host正常0，D4长稳未干扰，实际Worker/SSE动态和全入口仍欠，不勾T072，见verification/full-console-api-20261005.md。
  2026-10-05页面寿命：音频页卸载后旧播放/刷新503污染共享通知、旧播放finally提前解除后续等待三公开DOM红→页面active/操作symbol owner绿，相关29及最终274全前端/类型/Web通过。真实1440/390模拟浏览器核旧成功/失败不提示、当前错误保留/并发等待true→false，既有QA configLoader runner失败不吞；最终收尾见verification/audio-page-lifetime-20261005.md。音频在线能力、UDP及D4动态控制台仍欠，不勾T072。
- [ ] T073 **HIGH** 在 `runtime-dotnet/tests/ScpCv.Windows.Tests/`、`ScpCv.Integration.Tests/`、`frontend/scripts/` 与 `specs/008-libreoffice-playback/verification/` 建立当前版本其它既有源矩阵：图片、PDF/页图、视频、网页、RTSP/SRT/自定义直播、独立背景音频的登记/编辑/打开、适用控制、失败/恢复、切换/关闭及另一窗保持。优先使用Resources与自有本地网页/推流夹具，不覆盖媒体库或访问未经指定的外部流；本机/DOM/API/D4真实画面、时钟和退出分层记录，缺样例或条件单列未测，不用在线或HTTP受理代替出画。per FR-017、SC-006、US5/AC3、Constitution I/III、T017/T018/T024/T070 (partial)
  首实机切片：PDF缓存CurrentPage公开Physical红→绿2/2；全非Physical1082/1082。D4两指定输出顺序各图/PDF/视频/网页/关闭真出画，另两次定位/运动帧/时钟补测，四正常退出/文件释放通过。即时seek旧帧、VLC告警/审计/QA自身失败均保留；直播/背景音频、登记API及并行长稳未覆盖，不勾完成，见verification/native-media-d4-20261004.md。
  后续直播：SRT实际计数冻结而Playing两红，正式仅直播5秒无输入/解码/呈现推进锁存错误，暂停/停止/缓冲和非直播保护；D4 SRT错误/显式恢复/关闭、RTSP后续帧及退出有限通过，完整1092非Physical绿。UDP两恢复仍loading，首RTSP防火墙遮挡视觉否定、告警/失败保留；背景音频/API/并行长稳仍欠，见verification/native-stream-d4-20261004.md。
  UDP分层诊断：独立接收器首发/同接收器重启/迟加入三组TS及FFmpeg原图通过；实际Player默认、强制TS、CLOSE新Player及软件解码仍重开loading/time0。旧累计统计/Opening零值不当新输入有无证明；仅QA未改正式策略，五任务退出后精确移除，仍未修复，见verification/udp-reopen-diagnostic-20261004.md。
  2026-10-05UDP诊断：全新Host/LibVLC仍失败；真实端口观测发现释放后旧绑定存活、重开两绑定，只结束自有publisher2后旧绑定消失，新引擎收到Decoded38/Displayed7及真实内容帧。隔离发布者+non-autoplay初始化后，正式同Host/同Media首播/断流/显式重开、两组真实运动图、关闭null/全黑/端口消失有限通过。冷首次接入5秒无帧失败仍成立，未改生产或宣称全部UDP通过；五完成任务已移除，证据见verification/udp-engine-and-handle-inheritance-20261005.md。
  音频切片：实际worker非autoplay换源借旧Playing红→Stop旧输入后绿1Physical；全非Physical1092重新绿。D4 MP3/WAV/OGG时钟/解码、暂停/恢复/定位后推进/STOP和换源正常0、SHA/锁保持，自有任务已退出移除；静音不证明实体声音。源码列表循环/Finished差距待公开/原生复现修复，API/完整组未验收，见verification/native-audio-d4-20261004.md。
  列表切片：公开末曲不循环回首红→带事件STOP；原生短夹具吞Finished红→始终通知绿，Physical3/非Physical1101。D4正式Worker/管道/独立SQLite/公开服务完成1→2停止、循环1→2→1与协作退出；5.5秒夹具和未ready两QA误判保留，不延生产预算。HTTP/声音/全组仍欠，见verification/audio-list-loop-20261004.md。
  观察时间：实际加入曲目未推进快照时间公开红→同事务NextTimestamp绿，重复加入幂等；相关11/全非Physical1103及D4同版时间/管道/退出通过。首字符串比较编译失败保留不当业务红；周期音频进度当前缺sample接线待实证，见verification/background-audio-observation-20261004.md。
  周期进度：D4旧18无控制样本全0、单PAUSE真实3956ms红；正式只读idle sampler/成功源代次/失败OPEN失效及自然Ended不抢Finished后，最终同条件0→3053ms、PAUSE4150ms绿。新增4保护/相关14/非Physical1107，最终DLL进度与列表退出正常0；首QA参数/分析器失败留档。HTTP/声音/完整竞态/长稳未验收，见verification/audio-idle-progress-20261004.md。
  2026-10-05定位切片：文档声明seek而HTTP400真红→目标必填int64/原源代次保持/不假报位置绿，HTTP11/公开租约1/非Physical1123；进度入口与旧音量跨主机POST两DOM红绿，9新增/相关22/全前端263及类型/Web/桌面390通过。D4正式路由/Auth/Broker/真实Worker/SSE定位12500/回1000/停机有限通过，首头注释前包与最终DLL复测分开；音频在线/不可定位源/登记及完整组仍欠，见verification/audio-seek-http-20261005.md。
  2026-10-05完整接口切片：本机/D4实际发布ControlHost在新根认证上传图/PDF/视频、中文目录移动/改名、下载SHA/锁及三类直播/网页登记编辑通过。D4非法control next暴露同步校验异常漏HTTP边界，公开红→400/原会话保持绿；最终1137非Physical通过。只读状态不当像素/实际流播放，见verification/full-api-group-20261005.md。
  2026-10-05正式MediaMTX组：三格式音频真实上传/时钟/暂停恢复/seek/STOP、RTSP/SRT断流error/显式重开出内容/关闭和另一窗整图保持通过，Host/task0、17原图全SHA核对并复核。首次playing黑/灰不叫首帧，恢复连续非空运动仍欠；SRT拒绝/硬解告警及夹具别名冲突/直播Time0误判保留。MediaMTX纳入正式Supervisor但未取得其正常协作退出码，UDP/混合长稳/真实控制台仍欠，不勾T073/T074，见verification/full-group-live-audio-20261005.md。
- [ ] T074 **HIGH** 复用 `runtime-dotnet/scripts/publish-runtime.ps1` 与T045/T046/T070最终同版包门禁，在D4明确DISPLAY2/3及独立DataRoot完成两窗混合源10轮切换、100控制与至少60分钟观察，核其它源/已允许PPT限制的状态、真实显示/媒体、原件/文件锁及正常退出；控制桌面/电源/未知墙面/音量不据软件测试扩权。完整组、共享控制台真实桌面/手机、全套相关回归和文档均须收敛，正式源码仍按secondary推送/远端拉取。旧T004/T009-T026/T044-T046/T053/T068-T070缺口逐项带证据复核，不借小限制许可批量勾完或删除；轻微可接受差异登记后不再追求逐像素保真。per FR-016/017、SC-002/004/005/006、Constitution II/III、T019/T024/T025 (partial)
  新依赖风险：SQLitePCLRaw.lib.e_sqlite3 2.1.11的NU1903/GHSA-2m69-gcr7-jv3q尚未修复；须核免费许可/兼容替代、原生版本、锁/构建/持久及D4回归，不能屏蔽审计或据内部QA宣布安全。纳入最终收敛，不缩小目标。
  后续更正：生产HEAD/锁已pin2.1.13，旧告警是外部QA漏继承CPM而解析2.1.11/实际3.49.1；同级props继承后实际3.53.3/DLL与生产同SHA，恢复build无警告且审计不屏蔽。新维护floor回归及全非Physical1102、D4同版列表/退出通过，本分项差距已对齐；最终完整发布包的实际DLL仍须检查，不勾T074，见verification/sqlite-qa-alignment-20261004.md。
  2026-10-05按正式脚本重新发布008-full-runtime-1791146354218，正式Host404B7D与上轮EXE一致、SQLite原生B7385D与生产同版；四NET10/独立NET48组件具备，未部署/启动整组，MediaMTX外配与完整门禁仍欠。D4只读console1在线/服务及项目进程0/Git干净；不当源码同步或T074完成，见verification/audio-request-feedback-20261005.md。
  2026-10-05正式App/Broker/Supervisor两输出首次三轮状态矩阵通过：窗口2图片保持、窗口1PDF2/视频时钟/暂停/关闭，真实runtime-processes.json与协作停机；最终新完整包Contracts63304E/Control5C1610在D4复跑正常0。全套发现reader断开释放与Flush竞态ObjectDisposed漏幂等重连，新增4096轮真红→传输IOException绿/相关12，1137最终全套。测试误路由及900ms同采样误判保留；无像素/MediaMTX/实际PPT/SSE长稳，不勾T074，五完成任务精确移除/服务及进程0，见verification/full-api-group-20261005.md。
  2026-10-05正式组满幅：两Player精确身份/DWM1920×1080/九点无遮挡26PNG，三轮PDF两页/视频片头/网页、窗2整图SHA保持及关闭全黑；最终Host18296/task0正常退出，三完成任务精确移除。MainWindowHandle0与QA网页停止995仅修测试、不改播放器。后续10轮/100条实际PDF控制及3613.7秒文件/网页观察完成，240原图全SHA核对、89张另一窗保持；同阶段资源有限稳定、正常停机/原件锁释放。该自有任务已退出并精确移除，PPT/直播/音频长稳仍欠，不勾T074；见verification/group-visual-soak-20261005.md。

- [X] T075 **HIGH** 修复D4实际98MB TestPPT上传被Kestrel默认30000000字节请求限制截断的入口。仅上传端点采用明确有限1GiB总请求/表单上限，超限413 upload_too_large，畸形multipart明确400且不登记源；普通JSON等请求不放宽，身份/CSRF/原件目录/流式摘要保持。先补超限/非法表单公开HTTP红绿，再同实际大文稿在完整Kestrel/D4上传与两窗播放/转换/正常退出验证；文档/OpenAPI同步，不把测试夹具环境覆盖当生产修复。per FR-003/012/017、T016/T073/T074、Constitution I/III
  两公开HTTP红绿及最终1139非Physical/Release通过；D4实际98756629字节上传、原件SHA/7PNG及PDF/独占读、两窗原排版第6页/动画/第7页精确持久noop、16原图独立复核和正常退出0通过。声明长度测试不是实际1GB/chunked传输，代理/IIS限制与VLC硬解警告保留；不勾整体T074，见verification/upload-limit-and-full-ppt-group-20261005.md。

- [X] T076 **HIGH** 补齐当前发布包LibVLC插件索引：D4默认冷构造两次13.74/13.79秒、缺plugins.dat/436模块扫描；相同包仅生成官方索引后，新进程默认构造23ms/OPEN182ms、冷UDP首帧及同Host重开通过。先以公开发布/工具入口覆盖只生成当前包、禁止额外插件路径/用户配置/既有索引覆盖、失败不标发布成功，随后接发布末端并核DLL不变、索引/引擎/插件时间戳匹配。保持五运行组件、不启动服务/播放或全局写，缓存不作为可信代码与出画证明，不禁正常扫描兜底；同新正式包本机/D4多类VLC与正常退出、文档更新后才完成，不以QA缓存拷贝冒称生产已修。per FR-012/016/017、T073/T074、Constitution I/III
  2026-10-07收尾：正式发布、1140非Physical/3CLI保护及本机音频、D4文件视频/三格式音频/三直播短测正常退出证据齐备；36原图全SHA匹配/19种代表图、冷UDP六图逐张复核，自有任务注册精确移除。GPU/SRT告警、恢复首图灰/黑、首轮遮挡原因及MediaMTX正常退出码缺口保留，不据此勾T073/T074。见verification/vlc-plugin-cache-20261005.md。
