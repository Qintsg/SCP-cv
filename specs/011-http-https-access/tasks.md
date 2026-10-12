# Tasks: 网页双协议入口与共享安全跳转

**Input**: `specs/011-http-https-access/`

**Prerequisites**: spec/plan/research/data-model/contracts/quickstart与16/16规范清单。

**Organization**: 用户故事分阶段，先红后绿，测试并行不代表授权多个agent。

## Phase 1: Setup

- [x] T001 检查工作区、宪章、忽略及证书公开参数，记录到 specs/011-http-https-access/research.md

## Phase 2: Foundational

- [x] T002 明确双入口/发现/会话/寿命合同及验证边界，生成 specs/011-http-https-access/contracts/access.yaml 和 quickstart.md

## Phase 3: User Story 1 - 默认HTTP (Priority: P1)

**Goal**: 普通网页允许两协议，裸地址默认HTTP，明确HTTPS不降级。

**Independent Test**: 地址用例、HTTP普通认证/写操作、双监听与错误配置拒绝。

- [x] T003 [US1] 先更新地址与双入口测试 frontend/scripts/client-connection.test.mjs、dual-protocol.test.mjs，确认新要求红
- [x] T004 [US1] 实现 frontend/config/dualProtocol.ts、vite.config.ts 双监听、每请求代理、发现及清理；https.ts接用户.crt/.key且不放宽内容门禁
- [x] T005 [US1] 更新 frontend/src/platform/connection.ts、features/settings/ServerConnectionView.vue 和中文提示；封装端范围明确

## Phase 4: User Story 2 - 共享确认 (Priority: P1)

**Goal**: HTTP只提示/导航，HTTPS再次主动点击才采集。

**Independent Test**: 窗口1/2取消、确认、缺入口、重复点击、迟到结果，零采集/共享写。

- [x] T006 [US2] 新增 frontend/scripts/https-upgrade.test.mjs，扩展 screen-sharing-panel.test.mjs 先红覆盖交互/寿命
- [x] T007 [US2] 实现 frontend/src/features/screen-share/httpsUpgrade.ts、ScreenSharePanel.vue、useScreenShare.ts 与 locales/zh-CN/screenShare.ts；保留真实采集门禁

## Phase 5: User Story 3 - 会话隔离 (Priority: P2)

**Goal**: HTTP/HTTPS会话与CSRF不覆盖，注销只清当前协议。

**Independent Test**: 三轮往返及并发认证/写请求/状态和独立注销。

- [x] T008 [US3] 新增 runtime-dotnet/tests/ScpCv.ControlHost.Tests/ProtocolAuthTests.cs 先红检查cookie/CSRF往返与跨协议伪令牌拒绝
- [x] T009 [US3] 实现 runtime-dotnet/src/ScpCv.Infrastructure/Auth/ProtocolCookieManager.cs，更新 AuthServiceCollectionExtensions.cs 和 frontend/src/services 的CSRF fallback；保持HTTPS保护
- [x] T010 [US3] 验证双代理并发、SSE、认证及TLS，记录 specs/011-http-https-access/verification/local.md

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T011 真实浏览器验证两窗确认、取消/键盘、浅深/窄屏及导航不自动采集，记录 specs/011-http-https-access/verification/local.md
- [x] T012 更新 docs/screen-sharing.md、使用文档.md、维护文档.md、known-pitfalls.md、README.md、docs/CHANGELOG.md 与010访问说明
- [x] T013 前端全测试/类型/构建、.NET相关及全非Physical、Redocly合同、SpecKit、diff/编码/无密钥检查；关闭本机自有QA，更新 specs/011-http-https-access/tasks.md 和 verification/local.md

## Dependencies & Execution Order

T001→T002→US1(T003–005)→US2(T006–007)→US3(T008–010)→T011–013。US2纯地址合同可用入口替身独立测；US3正式认证不需要采集/Player。US1完整普通认证验收依赖US3后共同复核。

## Parallel Examples

- US1：地址与服务配置测试不同文件可同时跑。
- US2：纯跳转规则与DOM两窗测试可同时跑。
- US3：纯Cookie/认证测试和前端token回归可同时跑。

## Implementation Strategy

先完成HTTP页面与两端口（MVP），再提示导航，再认证隔离；每阶段保留失败证据，最终统一回归。实际D4部署/系统根导入不在本轮执行范围。无扩展hooks。
