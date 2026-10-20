# Tasks: 4K与目录化工作区

**Input**: 本目录spec/plan/research/data-model/contracts。

## Phase 1: Setup

- [x] T001 建立`specs/016-console-wall-workspace/`规范、研究与设计并核对宪章、忽略规则和不部署授权。

## Phase 2: Foundation

- [ ] T002 在`frontend/src/features/runtime/wall/`与`ScpCv.Domain/Model/VideoWallLayout.cs`建立矩形合同，先补纯模型回归。
- [ ] T003 在`frontend/src/features/runtime/wall/layoutCode.ts`及Domain规则实现一致无库短码与恶意输入测试。

## Phase 3: US1 大屏编排

**Goal**: 50格编辑、短码、32:9预览、一次应用。**Independent Test**: 纯模型/真实组件/后端预检无实体测试。

- [ ] T004 [US1] 扩展`WallLayoutPolicy.cs`/`VideoWallLayoutPlanner.cs`/DTO/Endpoints，保留修订及音量同步、未知余数和3/4拒发。
- [ ] T005 [US1] 在`frontend/src/features/runtime/wall/`实现框选、选择来源、换源、删除、键盘与重叠防护。
- [ ] T006 [US1] 重构`VideoWallMappingPanel.vue`单/双/自定义、代码输入、去布局选项并保留范围围栏。
- [ ] T007 [US1] 补`frontend/scripts/`及`runtime-dotnet/tests/`50格、代码、包级与生命周期回归。

## Phase 4: US2 本机预览与4K

**Goal**: 用户本机拉流，4K源配置，预留窗口。**Independent Test**: loopback媒体、释放/失败和配置回归。

- [ ] T008 [US2] 更新`VideoWallProtocolOptions.cs`/`appsettings.json`仅63/64为4K，核对PDF输出分辨率适配。
- [ ] T009 [US2] 在`tools/client-preview/`实现受保护可选本机FFmpeg桥、帧/会话/超时/资源清理测试。
- [ ] T010 [US2] 在`frontend/src/features/preview/`实现本机拉流与内联状态，chn0/1、隐藏卸载释放，不回退D4。
- [ ] T011 [US2] 更新`DashboardWindows.vue`和`router/index.ts`、`layouts/navItems.ts`四窗卡片/3/4预留页及回归。

## Phase 5: US3 目录和预案

**Goal**: 文件同表、编辑与YAML、预案高级码。**Independent Test**: 目录/批量及保存重读完整性。

- [ ] T012 [US3] 重构`SourcesView.vue`文件夹同表、独立状态列、删路径/副标题，保留保护目录和操作。
- [ ] T013 [US3] 重构`ScenariosView.vue`/`ScenarioDirectoryTools.vue`为同类列表，明确编辑、导航、批量操作。
- [ ] T014 [US3] 扩展`ScenarioEditDrawer.vue`/`scenarioModel.ts`目录联想、预设和独立高级布局编辑/代码。
- [ ] T015 [US3] 扩展`runtime-dotnet/src/ScpCv.Infrastructure/Scenarios/`与合同支持预案代码预检/激活及原子YAML文件保存。
- [ ] T016 [US3] 修`ScenarioAdvancedSettings.vue`机位0保持、音量0有效，删冗余说明并补往返回归。

## Phase 6: US4 页面一致性

**Goal**: 图文对齐、紧凑背景音乐、设置和Logo。**Independent Test**: 内存组件及只读浏览器检查。

- [ ] T017 [US4] 在`frontend/src/styles/`修Ant图标/文字/开关/进度对齐，不覆盖组件固有尺寸。
- [ ] T018 [US4] 优化`BackgroundAudioView.vue`/CSS桌面播放台与内部滚动，保留手机可达性。
- [ ] T019 [US4] 重构`RuntimeSettingsTab.vue`维护动作及安全确认，`DevSettingsTab.vue`诊断分区，删除设置副标题。
- [ ] T020 [US4] 在`frontend/src/assets/`使用指定Logo，更新`AppShell.vue`和导航布局、对齐电源动作、设置下沉。

## Phase 7: Verification

- [ ] T021 更新`docs/openapi.yaml`及schemas、`docs/使用文档.md`、`docs/维护文档.md`、`docs/CHANGELOG.md`与相关安全坑文档。
- [ ] T022 运行`frontend/scripts/`相关与完整测试、类型检查/三端构建、.NET非Physical、Spec Kit及Redocly；记录`verification.md`。
- [ ] T023 只读无头浏览器按附件对照，保存`design-qa.md`，修P0/P1/P2，清理精确自有测试进程；不推送/部署。

## Dependencies & Execution Order

T001→T002→T003→US1；US2可在模型稳定后独立推进；US3高级布局依赖US1；US4可与非重叠文件测试并行；最后T021–T023。

## Parallel Examples

US1可并行运行纯短码测试与只读DTO检查；US2可独立测试本机桥；US3可并行文件持久化与列表用例；US4可并行页面截图与样式检查。仅研究阶段已派只读研究代理，具体实现由主代理顺序完成。

## Implementation Strategy

先交互模型和安全预检，再逐页接线；每段先验证再推进，不将模拟成功当现场确认。本轮不执行任何部署。
