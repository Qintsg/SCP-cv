# Tasks: 带凭据的直播源

**Input**: 本目录spec/plan/research/data-model/contracts。
**Tests**: 按宪章先红绿，虚构凭据与回环；不进行D4部署或实体控制。

## Phase 1: Setup

- [X] T001 核对工作区、宪章、忽略规则与副作用边界，生成本目录规范与设计：`specs/015-stream-credentials/`。

## Phase 2: Foundational

- [X] T002 补创建/编辑/编码/非法输入的红回归：`runtime-dotnet/tests/ScpCv.Infrastructure.Tests/StreamCredentialServiceTests.cs`。

## Phase 3: US1 认证直播（P1）

**独立验收**: 新增/修改/重读保留URI，真实回环认证成功，旧协议保持。

- [X] T003 [US1] 允许已支持认证协议，保留SRT/UDP/RTP规则和无效输入拒绝：`runtime-dotnet/src/ScpCv.Infrastructure/Media/MediaSourceService.Streams.cs`。
- [X] T004 [US1] 验证正常认证REST登记/修改到真实OPEN租约包含原凭据：`runtime-dotnet/tests/ScpCv.ControlHost.Tests/StreamCredentialEndpointTests.cs`。
- [X] T005 [US1] 用真实Player与回环401挑战验证凭据/失败和TCP，禁止实体连接：`runtime-dotnet/tests/ScpCv.Windows.Tests/RecordingRtspTransportServer.cs`及对应Physical测试。

## Phase 4: US2 凭据默认隐藏（P1）

**独立验收**: 默认隐藏/显式查看，摘要和自动名称/日志无秘密，原URI不被脱敏值覆盖。

- [X] T006 [US2] 补直播协议日志脱敏及安全自动名称红绿：`runtime-dotnet/src/ScpCv.Infrastructure/Diagnostics/LogRedaction.cs`、`MediaSourceService.Streams.cs`及对应测试。
- [X] T007 [US2] 直播地址密码输入、提示与纯展示脱敏接入：`frontend/src/features/sources/StreamAddressInput.vue`、`services/mediaAddress.ts`、新增/编辑抽屉、`PlaybackControl.vue`及`frontend/scripts/stream-credentials.test.mjs`。

## Phase 5: Polish

- [X] T008 完成前后端相关/完整回归、构建、真实只读浏览器与Spec Kit，记录执行边界：`specs/015-stream-credentials/verification.md`。
- [X] T009 同步OpenAPI3.0说明并Redocly校验及使用/维护/CHANGELOG：`docs/components/schemas/StreamSourceCreateRequest.yaml`等对应合同与文档。

## Dependencies & Execution Order

T001→T002红→T003→T004/T005；T006与T007分别完成后端/前端隐私，最后T008/T009。两个故事必须共同交付，不能只允许密码而泄漏自动名称。无额外代理工作；前后端验证可并行，改同一文件串行。

## Implementation Strategy

先最小创建/编辑红绿，再认证链路，再隐私和UI。只复用原源/URI/播放器，失败留证据，不把登记或模拟回环当现场画面。

## Verification

9/9完成；后端1633、前端1062、原生回环5、浏览器4及构建/规范门禁通过，范围与失败见[验证记录](verification.md)。仅本机，不推送/部署。错误认证重试可能保持loading，不承诺五秒内变error。
