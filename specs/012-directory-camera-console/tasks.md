# Tasks: 目录式现场控制台、清晰共享与摄像机音量

**Input**: `specs/012-directory-camera-console/{spec,plan,research,data-model}.md`与`contracts/api.md`

**Tests**: 每故事先写测试；自动化与实体确认分开记录。

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
- [ ] T019 [US3] 浏览器/磁盘完整流程及占用/失败保护：`specs/012-directory-camera-console/verification/directories.md`。

## Phase 6: US4 摄像机与音量（P1）

**独立验收**: 六路身份与只读保护；绑定明确才可运动/声音；未绑定零写出；实体停止和音量单独记录。

- [ ] T020 [US4] 六路实际播放地址、PTZ及话筒/音频来源绑定核对：`specs/012-directory-camera-console/verification/device-bindings.md`。
- [X] T021 [US4] 内置目录与摄像机同步/保护：`runtime-dotnet/src/ScpCv.Infrastructure/Devices/CameraCatalog.cs`。
- [ ] T022 [US4] 摄像机有界运动/位置和音频协议控制及读回：`runtime-dotnet/src/ScpCv.Infrastructure/Devices/VenueDeviceService.cs`、`VenueProtocol.cs`。
- [X] T023 [US4] 设备HTTP合同/权限/缺绑定拒绝：`runtime-dotnet/src/ScpCv.ControlHost/Endpoints/VenueDeviceEndpoints.cs`、`ScpCv.Contracts/Http/VenueDeviceDtos.cs`。
- [ ] T024 [US4] 摄像机与音量页面/来源小卡片及软件输出绑定：`frontend/src/features/devices/DevicesView.vue`、`frontend/src/layouts/AppNavigation.vue`及既有音频设置。
- [ ] T025 [US4] 摄像机/声音有界实测，未知项不勾完成：`specs/012-directory-camera-console/verification/devices.md`。
- [ ] T034 [US4] D4端点63/64独立窄带辨识，LibVLC两播放器/背景音频的显式绑定、缺失拒绝与重播观测：`ScpCv.Contracts/Runtime/AudioOutputBinding.cs`、`runtime-dotnet/shared/VlcAudioOutputBinding.cs`及Windows/Contracts回归；WebView2/LibreOffice仍属于T024，不能用VLC结果代替。

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

## 当前未完成边界

T019目录主流程及D4屏控根上传/实际图片播出/关闭已通过，占用/失败浏览器矩阵未完整实测。T020固定监控76及话筒接线已确认；T020/T022/T024/T025仍缺云台、话筒的控制设备族/协议及音频来源路由，仅屏幕音量与只读保护闭环。T034已完成D4端点63/64物理辨识、纯策略12项及两Worker编译；新LibVLC绑定实机播放/重播、缺失与退出尚未验收，不勾完成。T033标准入口/双输出已通过，但完整受管软件声音绑定未完成；这些任务不勾完成。T010新参数双路312.15秒及三轮交错关闭/重开/迟到旧ID停止通过，现场文字可读性、人工共享选择及更长混合稳定性仍未验，历史间歇失败保留，不勾完成。共34项，26项完成、8项未完成。

本轮发布：固定`b04f65b`完整门禁1477/458通过，签名推送两仓库并同步D4源码与012f包。D4有效Restricted策略拒绝普通启动脚本，未绕过；旧012e组和网页保持运行，76默认尚未同步，T033不因文件到位而勾完成。
