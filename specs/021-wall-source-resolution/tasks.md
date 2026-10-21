# Tasks: 输入分辨率与墙面裁剪修复

**Input**: 本目录spec.md、plan.md、research.md、data-model.md、contracts/resolution.md。

## Phase 1: Setup

- [x] T001 在specs/021-wall-source-resolution/spec.md记录根因边界和验收。
- [x] T002 在specs/021-wall-source-resolution/research.md保留31路调查和D4切4K前后证据。

## Phase 2: Foundational

- [x] T003 在tools/input-resolution/protocol.test.cjs先覆盖固定查询/半包粘包/错误身份/预算/超时/清理。
- [x] T004 实现tools/input-resolution/protocol.cjs及probe.cjs，只查固定31路并输出JSON。

## Phase 3: US1 D4正确裁剪（P1，MVP）

独立验收：63–66的4K及显式1080全屏/左右/重复源/自定义片段覆盖完整、不越界，节点目的矩形不跟改。

- [x] T005 [US1] 在runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallSourceResolutionTests.cs添加4K及别名冲突回归。
- [x] T006 [US1] 在runtime-dotnet/src/ScpCv.Infrastructure/Devices/SiteInputCatalog.cs统一同IP解析、拒绝冲突。
- [x] T007 [US1] 在runtime-dotnet/tests/ScpCv.ControlHost.Tests/VideoWallRegistrationTests.cs验证装配4K/覆盖配置，保留旧D2黄金包。

## Phase 4: US2 其它输入独立尺寸（P1）

独立验收：31路证据可追溯，84未知拒发，混合4K/1080帧和电视源尺寸/指纹一致。

- [x] T008 [US2] 在runtime-dotnet/tests/ScpCv.Infrastructure.Tests/SiteInputCatalogTests.cs先覆盖实测分组、84未知及配置覆盖。
- [x] T009 [US2] 在runtime-dotnet/src/ScpCv.Infrastructure/Devices/SiteInputCatalog.cs替换其它设备统一4K默认。
- [x] T010 [US2] 在runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallSourceResolutionTests.cs验证混合尺寸/电视/指纹及未知首包前拒发。

## Phase 5: Polish

- [x] T011 更新docs/维护文档.md、docs/CHANGELOG.md、docs/known-pitfalls.md及specs/021-wall-source-resolution/verification.md。
- [x] T012 在specs/021-wall-source-resolution/verification.md记录本机回归/规范校验和未发送实体帧的边界。

## Dependencies & Execution Order

T001→T002→T003→T004；T005→T006→T007；T008→T009→T010；全部后T011→T012。同文件顺序编辑，工具和CPU测试可并行运行，无需并行编辑。

## Implementation Strategy

MVP先保留D4实测4K并校验别名，再纠正其它设备尺寸。先失败回归、后最小修复。无hooks；不自动推送、部署或发送实体帧。

## Phase 6: 用户后续授权推送与部署

- [ ] T013 在specs/021-wall-source-resolution/deployment.md记录双仓推送、D4干净快进及部署前状态。
- [ ] T014 在specs/021-wall-source-resolution/deployment.md记录新包摘要、协作停机与独占冷备、交互任务及桌面入口切换。
- [ ] T015 在specs/021-wall-source-resolution/deployment.md记录双窗/数据/4K拓扑/正常HTTPS与重复启动验证，并将最终文档同步两个仓库及D4。
