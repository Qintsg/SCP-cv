# Tasks: 目录式现场控制台、清晰共享与摄像机音量

**Input**: `specs/012-directory-camera-console/{spec,plan,research,data-model}.md`与`contracts/api.md`

**Tests**: 每故事先写测试；自动化与实体确认分开记录。

**当前统计（2026-10-18）**：45项、35完成、10未完成；T044/T045本机软件归属修复及分层验收完成，原10项现场/人工缺口保持。当前正式运行012k/06d342e，D4网页仍为014/9594c19，此轮尚未发布；六轮18有效阶段累计3613.8秒混合长稳、原句柄退出、原媒体/配置及系统声音恢复门禁已通过，T041/T042/T043完成。旧失败保留；普通网页真手势、人工共享视觉及实体摄像机/DSP/音源不由前端结果代验。下文统计按日期保留，不能作为当前数量。

## Phase 1: Setup

- [X] T001 核对工作区、宪章、路径/实体副作用约束与忽略文件：`AGENTS.md`、`.specify/memory/constitution.md`、`docs/known-pitfalls.md`。
- [X] T002 并行只读研究与完成方案/数据/合同：`specs/012-directory-camera-console/`。

## Phase 2: Foundational

- [X] T003 确定目录投影和保护规则及设备证据门禁测试：`runtime-dotnet/tests/ScpCv.Infrastructure.Tests/MediaDirectoryTests.cs`、`VenueDeviceTests.cs`。
- [X] T004 新增必要可信内置标记/预设目录和高级字段及增量迁移：`runtime-dotnet/src/ScpCv.Domain/Model/Entities.cs`、`ScenarioFolder.cs`、`ScpCv.Infrastructure/Persistence/`。

## Phase 3: US1 正常拼接与清晰共享（P1）

**独立验收**: 12组合零越界、历史golden保持；捕获/发送真实指标及参数兼容测试，D4双路文字持续5分钟。

- [X] T005 [US1] 先覆盖1080P完整裁剪回归：`runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallLayoutPlannerTests.cs`。
- [X] T006 [US1] 修正63/64默认输入尺寸并保留81/历史独立配置：`runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/VideoWallProtocolOptions.cs`、`ScpCv.ControlHost/appsettings.json`。
- [X] T007 [P] [US1] 新增共享清晰度/码率/实际统计/取消测试：`frontend/scripts/screen-share-quality.test.mjs`。
- [X] T008 [US1] 无尺寸上限捕获、文字编码和协商后有界发送参数：`frontend/src/features/screen-share/shareQuality.ts`、`ScreenSharePublisher.ts`、`rtcPeer.ts`。
- [X] T009 [US1] 质量诊断显示及停止清理：`frontend/src/features/screen-share/ScreenSharePanel.vue`、`ScreenSharePublisher.ts`及后端接收统计。
- [ ] T010 [US1] D4双路5分钟清晰度/切换/关闭实测并记录：`specs/012-directory-camera-console/verification/share-quality.md`。

## Phase 4: US2 默认端口（P1）

**独立验收**: 80/443普通访问、HTTPS共享发现、Origin/Cookie/TLS与内网规则，无端口冲突误杀。

- [X] T011 [US2] 标准端口回归及部署配置说明：`frontend/scripts/https-upgrade.test.mjs`、`frontend/.env.example`、`docs/web-access.md`。
- [X] T012 [US2] D4入口、Origins、持久任务与限定内网防火墙改80/443：`.validation/deploy-012/`；结果记录`specs/012-directory-camera-console/verification/d4-deployment.md`。

## Phase 5: US3 媒体文件管理（P1）

**独立验收**: 根/两层目录、磁盘路径、上传目的地、筛选搜索、多选删移、小卡片与真实打开状态。

- [X] T013 [US3] 后端公开路径与可信内置保护覆盖全部媒体写入口：`ScpCv.Infrastructure/Media/MediaSourceService.Paths.cs`、`MediaSourceService*.cs`、`ScpCv.Contracts/Http/MediaDtos.cs`。
- [X] T014 [US3] 目录优先与搜索范围纯逻辑测试：`frontend/scripts/directory-browser.test.mjs`。
- [X] T015 [US3] 共用目录导航/路径模块：`frontend/src/features/directories/DirectoryNavigator.vue`、`directoryPath.ts`。
- [X] T016 [US3] 媒体库目录导航、类型筛选、批量逐项结果：`frontend/src/features/sources/SourcesView.vue`、`useSourceSelection.ts`、`frontend/src/stores/sources.ts`。
- [X] T017 [US3] 打开到窗口小卡片与受理/真实播出区分：`frontend/src/features/sources/SourceOpenCard.vue`。
- [X] T018 [US3] 屏控选源目录浏览并明确根上传：`frontend/src/features/display/SourcePicker.vue`。
- [X] T019 [US3] 浏览器/磁盘完整流程及占用/失败保护：`specs/012-directory-camera-console/verification/directories.md`。

## Phase 6: US4 摄像机与音量（P1）

**独立验收**: 六路身份与只读保护；绑定明确才可运动/声音；未绑定零写出；实体停止和音量单独记录。

- [ ] T020 [US4] 六路实际播放地址、PTZ及话筒/音频来源绑定核对：`specs/012-directory-camera-console/verification/device-bindings.md`。
- [X] T021 [US4] 内置目录与摄像机同步/保护：`runtime-dotnet/src/ScpCv.Infrastructure/Devices/CameraCatalog.cs`。
- [ ] T022 [US4] 摄像机有界运动/位置和音频协议控制及读回：`runtime-dotnet/src/ScpCv.Infrastructure/Devices/VenueDeviceService.cs`、`VenueProtocol.cs`。
- [X] T023 [US4] 设备HTTP合同/权限/缺绑定拒绝：`runtime-dotnet/src/ScpCv.ControlHost/Endpoints/VenueDeviceEndpoints.cs`、`ScpCv.Contracts/Http/VenueDeviceDtos.cs`。
- [ ] T024 [US4] 摄像机与音量页面/来源小卡片及软件输出绑定：`frontend/src/features/devices/DevicesView.vue`、`frontend/src/layouts/AppNavigation.vue`及既有音频设置。
- [ ] T025 [US4] 摄像机/声音有界实测，未知项不勾完成：`specs/012-directory-camera-console/verification/devices.md`。
- [X] T034 [US4] D4端点63/64独立窄带辨识，LibVLC两播放器/背景音频的显式绑定、缺失拒绝与重播观测：`ScpCv.Contracts/Runtime/AudioOutputBinding.cs`、`runtime-dotnet/shared/VlcAudioOutputBinding.cs`及Windows/Contracts回归；WebView2/LibreOffice仍属于T024，不能用VLC结果代替。

## Phase 7: US5 目录预设与高级项（P2）

**独立验收**: 目录CRUD/移动/批量，高级空项零调用；部分配置/失败不改未设置项。

- [X] T026 [US5] 目录/高级项回归：`runtime-dotnet/tests/ScpCv.Infrastructure.Tests/ScenarioDirectoryTests.cs`、`ScpCv.ControlHost.Tests/ScenarioDirectoryEndpointTests.cs`与`VenueAudioWallTests.cs`。
- [X] T027 [US5] 预设目录服务/HTTP与高级验证编排：`ScpCv.Infrastructure/Scenarios/ScenarioFolderService.cs`、`ScenarioService.Advanced.cs`、`ScpCv.ControlHost/Endpoints/ScenarioFolderEndpoints.cs`。
- [X] T028 [US5] 预设目录式页面与高级折叠配置：`frontend/src/features/scenarios/ScenariosView.vue`、`ScenarioAdvancedSettings.vue`、`scenarioModel.ts`、`frontend/src/stores/scenarios.ts`。

## Phase 8: US6 切墙保持声音（P1）

**独立验收**: 每次成功切墙后恰好一次声音配置；墙面失败零声音包，声音失败显示画面已发。

- [X] T029 [US6] 一次发送和阶段失败测试：`runtime-dotnet/tests/ScpCv.ControlHost.Tests/VenueAudioWallTests.cs`。
- [X] T030 [US6] 首页两种布局应用后重发已绑定屏幕音量一次：`runtime-dotnet/src/ScpCv.ControlHost/Endpoints/VideoWallLayoutEndpoints.cs`与`frontend/src/features/runtime/VideoWallMappingPanel.vue`；来源/话筒未绑定仍不下发。

## Phase 9: Polish

- [X] T031 同步OpenAPI3.0/Redocly、配置及使用文档和已知坑：`docs/openapi.yaml`、`docs/components/schemas/`、`README.md`、`docs/known-pitfalls.md`。
- [X] T032 全量前后端/类型构建/Spec Kit与真实浏览器检查：`specs/012-directory-camera-console/verification/test-results.md`。
- [ ] T033 D4部署新包并检查两显示器/受管音频及80/443入口：`specs/012-directory-camera-console/verification/d4-deployment.md`。

## Dependencies & Execution Order

T001–T002已完成。T003–T004先于目录/设备/预设；US1与US2不依赖新schema，可独立安全完成。US3依赖基础；US4核对先于实体启用；US5依赖目录/设备合同；US6依赖现场音频。测试先于对应实现，相同文件串行；T007可与T005验证并行。最后完整检查和部署。

## Implementation Strategy

先交正确裁剪/高清共享与标准入口，再目录、设备、预设和切墙声音。物理绑定无法确认时完成拒绝/诊断合同，但对应实测任务保持未完成，不用虚构绑定绕过安全。所有[P]是文件无冲突的机会，并非自动启动额外代理的授权。

## 此前34项阶段的未完成边界（当前见开头及接续）

T019目录主流程、D4屏控根上传/实际图片播出/关闭、播放占用/混合批次/真实文件锁保护及上传交互复核已完整通过。T020固定监控76及话筒接线已确认；T020/T022/T024/T025仍缺云台、话筒的控制设备族/协议及音频来源路由，仅屏幕音量与只读保护闭环。T034的LibVLC实际播放/重播、缺失拒绝与退出已在D4隔离验证，但常驻运行组绑定未启用，不勾完成。T033标准入口/双输出已通过，但完整受管软件声音绑定未完成；这些任务不勾完成。T010新参数双路312.15秒及三轮交错关闭/重开/迟到旧ID停止通过，现场文字可读性、人工共享选择及更长混合稳定性仍未验，历史间歇失败保留，不勾完成。共34项，27项完成、7项未完成。

2026-10-13接续补齐：T010发现并红绿修复命令局部取消导致整组退出，未代替现场清晰度/长稳。T019的D4播放占用、混合批次与真实文件锁失败保护通过，上传抽屉遮挡修复后的实际普通点击和手机44px触控复核通过，勾选完成。T034正式原生模块在D4会话1完成63/64各三轮及缺失/释放隔离实测（20项观测），但常驻运行组配置仍未启用，不勾完成。背景音频成功回执明确清空旧错误，不能凭新Worker在线清除。当前34/27/7，只有完整验收的任务才改勾选，证据见[本轮接续](verification/20261013-continuation.md)。

此前012f发布：固定`b04f65b`完整门禁1477/458通过并签名推送两仓库；D4最初Restricted策略拒绝启动，获得明确授权后仅调整CurrentUser RemoteSigned，协作停机/退出核验/冷备份后激活012f。两窗原生启动目标DISPLAY2/3及空闲CLOSE实际回执通过，HTTP80/HTTPS443正常，固定ID88同步76。当时可选软件音频绑定未启用，背景音频空列表仍有历史错误状态；当前012g已通过真实STOP清除该错误，详见接续部署记录。T033完整声音及T034常驻绑定仍未验，不勾完成。

## Phase 10: Convergence

本轮检查FR-010/011/012/014、US4验收2–6、SC-005/006及音频接线方案；对应五类高优先级partial缺口，未发现需要扩大范围的新增需求或宪章例外。下列是现有未完成任务的可执行细化，历史34/27/7为追加前统计，不代表新增任务已完成。

- [ ] T035 核对现场工程和原客户端云台参数选择，接通有界运动/停止与位置保存/调用；覆盖权限、失焦、并发和未知绑定零写出，记录实际设备证据，追溯FR-010、US4/AC2–3、T020/T022（partial）。
- [ ] T036 核对话筒所属设备/中控通道和音量编码，接入手持与悬挂分组，覆盖全字段预检、部分发送失败及未确认状态，追溯FR-011、US4/AC4、T022/T025（partial）。
- [ ] T037 核对155输出及IP81/63声音输入子项，接通二选一来源与首页/预设声音重发，保留发送与实际状态区别，追溯FR-012/014、SC-006、T024（partial）。
- [X] T038 为WebView2网页/共享、LibreOffice及背景音频补齐实际软件输出绑定与缺失拒绝；覆盖同名端点、两窗并存、切换和关闭，追溯FR-012、plan:软件输出接线、T024/T034（partial）。
- [ ] T039 更新摄像机/三种音量/来源前端和合同并在D4通过完整调用链验收，记录原件保持、两显示器、状态读回/不确定及失败清理边界，追溯FR-015、US4、SC-005、T025/T033（partial）。

接续实现与证据见[触控与声音记录](verification/20261013-venue-completion.md)：恢复General按钮与SQL Lock=19的三路滑杆量程；用户已确认旧悬挂即原头戴1/2，单值兼容、同值去重、冲突首包前拒绝及统一页面已接入；云台租约/停止、前端操作台、三路音量及网页声音绑定实现已完成部分验证。85机械动作、DSP实际增益、来源路由及LibreOffice稳定接线仍未闭环，T035–T039不勾完成。新增后当前共39项，27项完成、12项未完成；前文34/27/7是追加前历史统计。

继续阶段见[选源与手机操作台接续](verification/20261013-venue-followup.md)：T037原参数和TCP编码已恢复，完整来源能力仍禁用；T039真实仿真浏览器/手机44px、部分发送失败及响应式预设编辑回归补齐。T038早期r16只有音频通过而退出超时；后续正式源码接入私有Office/端点预检/有限零音验证，r24/r25正式客户端到宿主的双文稿两轮实际声音只63、交错关闭、完整job与原生退出0通过。实际两个Player/网页共享完整组及部署仍待验，不勾完成。IP81当前1080P默认与独立4K覆盖验证，不向墙面发包；中控快照只视作缓存，不把机械或DSP未确认项勾完成。任务数仍39/27/12。

T038接续r28已通过实际WPF PlayerRuntimeHost两轮双文稿、缺引擎失败留旧、文稿/图片切换后另一窗继续出声、真实WASAPI只63与四次原生正常退出。r27夹具漏接FileNotFoundException的失败另行保留；只修夹具，不放宽产品。Supervisor IPC完整组、网页/共享绑定与部署仍待验，任务勾选保持39/27/12，详见同一接续记录。

2026-10-14：T038实际网页矩阵r30/r37已通过双窗声音只63、失败留旧、热切/保温重开、切图片/关闭无音及13个原生退出0。新增可信资料根配置及SDK实际路径守卫；完整发布检出NAudio多TFM外壳冲突，改Player最小WASAPI依赖，r38完整五组件隔离包生成成功，后端1573项通过。在线NuGet审计失败另列，正式常驻未更新；屏幕共享/IPC整组、来源及实体效果仍待验，任务39/27/12不变。

T038接续r41已通过真实Hardware主机/认证REST/队列/IPC双共享与背景声音只63、音量/静音/换图/重开/旧ID围栏及完整自有原生退出0。r39/r40正常Worker退出误使Supervisor失败1已修为当前认证nonce停机准备，13纯围栏/4真实管道针对性和Integration283/Windows252通过，失败记录保留。LibreOffice实际IPC、来源及正式常驻绑定仍待验，不扩大当前证明为完整任务完成；39/27/12保持。

T038现已完成：r44通过实际Hardware认证REST/持久队列/IPC双PPT两轮打开、带声音页、A换图B继续、关闭及完整组退出，16个原进程退出0，原件/系统默认输出保持。结合VLC20项、网页r30/r37及共享/背景r41，所有受影响引擎的软件接线与生命周期已有对应实测；正式常驻激活仍由T033/T034追踪，不据此称已部署或扬声器已可听。当前39项、28完成、11未完成。T037新增独立IP81/实时SCP-cv门禁、部分失败保留source、预设首包前拒绝及显式来源卡片，相关Host466/前端476通过；D4就绪矩阵和实体来源效果继续单列。

## Phase 11: Convergence

2026-10-14本次检查T033涉及的FR-004/012/015、SC-003、双输出/可信声音/完整运行组方案与宪章I/III/IV。原未完成的实体/声音任务不重复追加；新增一个HIGH partial：正式012h含MediaMTX的停机实测，两个Player/Audio正常0，但MediaMTX被强制结束-1、Supervisor最终1，API200不是正常退出证据。状态文件已消失且原句柄全退出后通过正式API明确恢复；服务启动/页面可用不替代完整部署验收，T033保持未完成。

- [X] T040 补齐MediaMTX的定向协作退出，覆盖真实原进程身份、独立控制范围、其它进程不得收到控制信号、未知/失效身份拒绝及失败兜底仍非正常；在D4含MediaMTX完整组通过停机/恢复、原句柄与状态文件核验后重新固定提交发布，涉及`runtime-dotnet/src/ScpCv.Supervisor/Runtime/RuntimeLauncher.cs`、`ShutdownCoordinator.cs`、`Program.cs`及`runtime-dotnet/tests/ScpCv.Windows.Tests/`，追溯T033、plan:完整运行组/停机围栏、Constitution I/III/IV（partial，HIGH）。

追加后40项、28完成、12未完成；012h实际激活与失败证据见部署记录。不得通过接受所有非零退出或仅移除MediaMTX监控来消除该缺口。

T040于2026-10-14完成：真实MediaMTX先红-1后绿0，额外控制台成员/旧身份/错误身份/无创建证明/提前取消均拒绝；后端最终1601项通过。D4隔离完整组两轮10原句柄退出0；固定签名d67ce53重新发布012i、逐967文件核验、旧组退出后冷备，正式实际MediaMTX配置完整空闲组五原句柄全部0、状态文件删除、restart202恢复通过。原11媒体、DISPLAY2/3、Node23616/80/443/证书及系统声音基线保持。T033/T034的常驻实际声音及实体链路不据空闲停机勾完成。当前40项、29完成、11未完成，详见[012i证据](verification/20261014-mediamtx-stop.md)。

后续T034完成：常驻012i两个Player及Audio通过正常认证REST/队列/IPC三轮真实低增益播放，57次正峰值会话仅63，交错关闭/重开/背景持续通过；精确自有源96已删除、磁盘残留0、原静音/循环与音量恢复、原七进程身份及系统声音保持。结合此前63/64独立辨识和正式模块缺失拒绝，T034闭环；WebView/LibreOffice的常驻逐引擎播放及实体声音仍属于T033/T024，不用VLC结果代替。当前40/30/10，见[常驻声音记录](verification/20261014-formal-audio.md)。

后续常驻共享20阶段/21浏览器原退出0与双PPT两轮11阶段/12侧车Office原退出0均通过，原件与设置保持。普通网页自动播放被NotAllowedError拒绝，未出现实际音轨推进；两次失败均精确清理及完整组正常退出/明确恢复，不把清理当作播放通过。D4当前84–88的554/8889连接均未成功，固定76描述200；中控缓存12/10/9只读恢复、外框尾部仍待核，不当DSP反馈。T033等保持未完成，40/30/10，见[最新常驻与实体边界](verification/20261014-formal-engines.md)。

进一步接续：FE82原头/尾指令8项、夹具10项（真实TCP分段/粘连/拒绝）核定声明减一正文+4未解释字节+FC；不把尾字段推断为已验证CRC。三路独立Hardware正常REST实际MCU缓存一档下调及恢复六阶段通过，最终12/10/9，正式配置/组不变。18项相关Host回归再通过；真实DSP执行/增益尚未确认，T036及关联实体任务保持未勾，40/30/10，见[最新协议/接收证据](verification/20261014-touch-snapshot.md)。

T037后续只读：155管理80/控制4830可连接、61FA回复13字节状态2，不是来源。原Audio查询7037A0→65C5E0参数0及InputAudioRecall解析入口已核定，单独和同连接设备确认后两次610B查询均未取得来源；没有可靠恢复基线，不发选源包。已请求现场设备恢复和实际来源信息，T037保持未勾，见[来源只读记录](verification/20261014-audio-source-readonly.md)。

## Phase 12: Convergence

2026-10-14针对FR-002/012/015、SC-002、US1/AC3、共享生命周期/软件声音/正式运行组方案及五项宪章原则复核。已有实体和人工验收缺口继续由T020/T025/T033/T035–T039跟踪，不重复增加设备任务。另有一项HIGH partial：短时矩阵与五分钟共享不能代替已记载的更长混合稳定性；新增下列有界细化，不将其当人工捕获、文字可读性或全部设备验收。

- [X] T041 在D4正式双窗口通过至少60分钟共享/PPT/视频/固定监控/图片轮换及独立背景音频运行，按正常认证REST/IPC检查原运行组身份、实际共享帧推进、媒体状态、软件声音端点和自有原生退出，恢复原配置并精确清理自有测试源，记录失败及不能自动证明的视觉/人工共享边界；涉及`.validation/deploy-012i/MixedSoakProbe/`及`specs/012-directory-camera-console/verification/20261014-mixed-soak.md`，追溯T010、FR-002/012/015、SC-002、plan:分层实机验证（partial，HIGH）。

## Phase 13: Convergence

2026-10-14在T041接续中复核FR-012/015、窗口声音/实际状态方案及宪章I/III/IV。新增一项CRITICAL contradicts：普通与预设OPEN没有音量/静音字段；Player新建VLC不读取或记忆窗口配置，空闲SET_VOLUME/SET_MUTE不作用于下一资源，HTTP持久值0/true不能证明原生静音。补充根进程WASAPI观察器在第五轮VLC阶段未达到静音判据而退出1；此失败仍需交互会话原始量值核对，不按当前主探针状态通过声音验收。原实体缺口不重复追加。

- [X] T042 CRITICAL 修复VLC打开、同源重开、切源与自然循环的窗口音量/静音保持：从普通/预设OPEN传递当前窗口音频值，在新资源首次播放前安全应用且实际就绪后复核，空闲设置保留到下次打开，失败留旧、两窗隔离和未支持引擎限制不变；通过正常公开租约/真实Player接口的红绿回归及D4原始WASAPI观测，再重新验收T041，不修改系统主音量/默认输出。涉及`RuntimeStateService.Media.cs`、`ScenarioService.cs`、`PlayerRuntimeHost.Vlc.cs`及相关Contracts/Windows/Host测试和IPC文档，追溯FR-012/015、T033/T041、plan:软件声音与实际状态、Constitution I/III/IV（contradicts）。

T042本机1603非Physical/8真实全零PCM及D4固定3d9725c隔离双Player八阶段/36活动WASAPI样本通过，四原进程退出0、系统声音与正式七身份保持。修复已签名推送两仓库、D4源码同步，正式服务仍用d67ce53；待新包常驻激活与新的T041长稳，不提前勾选。初夹具路径规范化误拒及r69声音/清理失败全部保留，详见同一混合记录。

后续已激活正式012j/3d9725c并通过实际MediaMTX完整空闲组五原句柄0/恢复202，原数据冷备、网页/双输出/系统声音保持。新长稳r1首次共享专用音频409提前失败并恢复；夹具补已提交实际音频等待、根VLC及确认关闭共享追踪后，r2原PID28848确证存活并进入双路共享，尚无完整结果。T041/T042仍待完整长稳、恢复/原件/声音/退出门禁，42/30/12保持。

## Phase 14: Convergence

2026-10-14针对FR-009/012/015、US4/AC1、plan摄像机播放/软件声音与宪章I/III/IV复核。新增一个HIGH partial：固定76独立FFprobe取得483视频/251 AAC包；固定3d9725c真实Player默认RTSP方式15秒只解码8视频帧、音频0、WASAPI为空，而同源同端点TCP对照视频1566、音频870及435播放缓冲、真实静音会话仅63。不能将空会话当无音轨或放宽长稳判据。当前默认没有显式RTSP传输选择；其余机械/DSP/来源/人工缺口由原任务继续追踪。本阶段仅追加任务，不改规范/方案或应用代码；追加后43项、30完成、13未完成。

- [X] T043 HIGH 使窗口RTSP来源以已验证的可靠传输建立完整音视频，不影响文件/SRT/其他URI；先通过公开Player OPEN与真实RTSP SETUP请求的红绿回归，再在D4固定76验证解码推进、63音频/静音、重开/关闭及正式配置保持，重新验收T041；不靠空会话或仅Playing状态假报通过。涉及`PlayerRuntimeHost.Vlc.cs`、Windows真实协议夹具与012验证/使用文档，追溯FR-009/012/015、US4/AC1、plan摄像机播放/软件声音、Constitution I/III/IV（partial）。

012k/06d342e已正式激活，完整组停机五原句柄0/恢复202、原数据冷备/网页/双输出/系统声音通过。固定76正常REST/IPC三轮双窗口共19阶段及99活动WASAPI样本只63/零音量/静音、原配置恢复通过；短测不代替完整长稳。新012k-r1原PID23248确证存活进入双共享，六轮18阶段及最终恢复/退出/原件门禁未完成，T041/T042/T043仍未勾，43/30/13。旧r69、012j-r1/r2失败全部保留，见同一混合记录。

后续完整012k-r1通过：六轮18个唯一有效阶段各达到240/180/180秒，累计3613.797秒；235条观测、286个非空VLC根活动样本均63/Volume0/Mutedtrue。110个捕获原生句柄退出0，原主探针23248与旁观29632均0，独立complete收尾核原七正式身份、11源/原件/窗口/背景/系统声音保持，自有文件/原生/监听残留0。25文件证据下载解压/唯一条目/摘要及结果核对通过，T041/T042/T043勾选，43/33/10。人工视觉/共享许可、普通网页真手势和摄像机/DSP/音源不据此代验；旧失败保持失败。

T035后续：85正常REST左右各一次短时动作，停止请求524/507ms、软件意图idle及四张实时原帧的对应方向变化/静置稳定已确认。缓存快照需约3.8秒且当前表空，不把空表当0；原失败和未确认回执保留。未存储/调用现有机位，其他设备/方向/变焦与位置仍未验，不勾T035。收尾保留非本轮源115播放而不执行空闲恢复，见[有限机械证据](verification/20261014-ptz85-motion.md)，任务数量43/33/10不变。

T035再接续：85上下/变焦及86/88六方向共16次正常REST调用/独立短时停止完成，停止请求307–317ms，回执仍sent_unconfirmed、意图idle。22张原实时帧支持85/88与86多数方向的有限实际效果；86向下静态ROI仅-2像素，明确未确认，不归因或改绑定。三路静置未见持续全局漂移，位置存储/调用未执行；80文件精确白名单/摘要/回执核验通过。非本轮图片115、原进程/系统声音保持，见[三路矩阵](verification/20261014-ptz-matrix.md)。84/87、86向下及机位等仍待验，T035不勾、43/33/10不变。

T035最新补证：86向下仅改变取证为单RTSP连接12秒/60原帧，正常REST唯一300ms动作、303ms请求停止，未改协议或延长运动。固定墙面/门框一致上移并稳定，静态ROI第17–20帧0→-9/-32/-41/-42像素、20–60帧保持-42；本次实际效果补证通过，原独立截图未确认保留且根因不明。67唯一文件/60摘要核验、原115播放1107及窗口2/772/正式七身份/系统声音保持、QA残留0。当前三路六方向有限效果已有证据，84/87和机位等仍待验，T035不勾、43/33/10保持，详见同一矩阵接续。

当前接续：用户要求摄像机先不用管，未完成摄像机实测暂挂，不把暂挂当完成。音源切换待用户确认安全的最终恢复来源；DSP及人工网页/共享视觉仍缺证据。本轮Edge连接不可用、应用内浏览器打开D4失败，前端实测未进入控制台；独立只读诊断确认正式就绪Healthy、原七身份及监听保持，不当作UI通过。已同步设备/共享文档当前状态，旧失败及历史结论保留，详见[当前设备边界](verification/devices.md)，43/33/10不变。

## Phase 15: Convergence

2026-10-18继续核对FR-009–015、US4/AC1–6、US5高级项、US6及SC-005/006与现场设备/软件声音方案。原10项现场与人工缺口仍由原任务追踪，不重复增加或恢复暂挂摄像机实测。发现两个独立软件缺口：CameraControlPad只比较runtime投影与账号ID，未比较真实主机代次和完整认证寿命，300ms续租每次会捕获后来认证；位置保存Popconfirm也未冻结确认归属。DevicesView同样只比较ID/投影，旧finally会解忙态并向新环境续读，同ID认证变化未清旧设备/音量。两者均在本机实际组件和受控HTTP边界补红绿，不用实体动作来诊断。

- [x] T044 CRITICAL 为云台按住续租/独立停止与位置保存确认补齐真实主机、完整认证、摄像机和单次意图归属；同ID重登、会话修订或coordinator先失效而runtime未更新时，不把旧hold/stop/保存发给新环境；冻结确认的设备/位置并在变化时取消。当前松手/失焦/卸载仍独立发原有效范围的停止，不等待迟到start，不取消服务器看门狗；旧位置结果/finally不能污染新目标或解锁新操作。先补真实Ant/CameraMotionLease/API延迟HTTP和受控时钟红回归，再做隔离生产浏览器；涉及`frontend/src/features/devices/CameraControlPad.vue`、`motionLease.ts`与`frontend/scripts/`，追溯FR-010/011/015、US4/AC2–3、plan:云台短租约/完整归属、Constitution I/III（contradicts）。仅本机，不恢复实机运动或覆盖位置。
- [x] T045 HIGH 为现场音量/来源页面读取、显式应用及反馈补齐主机/完整认证/页面归属和真实请求占用：变化即清旧投影/草稿，旧结果不串反馈，旧finally不解新操作或自发向新环境续读；新上下文恢复由其自身读取负责，原写未终端不能误当已结束或排队重放。同域正常操作保留其它未应用草稿、部分发送阶段与原错误；先补真实DevicesView/Ant/API延迟HTTP回归及隔离浏览器。涉及`frontend/src/features/devices/DevicesView.vue`、`VenueAudioCard.vue`、`VenueSourceCard.vue`、`services/venueApi.ts`与`frontend/scripts/`，追溯FR-011/012/015、US4/AC4–6、plan:现场声音/明确副作用、Constitution I/III（partial）。不改帧/通道，不把本机绿当DSP或来源实体验收。

T044/T045完成70项新增回归（38云台、32现场页），完整1029/1029及Web/App/主进程编译、54SFC通过；真实生产SPA10场景连续5轮50次及原全部账户/连接/操作/56页面、16电源/系统音量场景通过。旧确认、真实占用、独立停止和当前失败均有正反对照，未操作实体或关闭原10项欠账。详见[完整红绿与边界](verification/20261018-venue-scope.md)。
