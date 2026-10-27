# Tasks: 控制台与实现简化

**Input**: 本目录 spec.md、plan.md、research.md 和 contracts/behavior.md。

## Phase 1: 调查

- [x] T001 检查 `frontend/src/`、`runtime-dotnet/src/`、`docs/` 的表达、调用链与现有工作区状态。

## Phase 2: 用户操作与表达

- [x] T002 [US1] 更新 `docs/使用文档.md`、`docs/video-wall-mapping.md`、`docs/web-access.md` 和 `docs/libreoffice-playback.md` 中的操作说明与过期状态。
- [x] T003 [US1] 清理 `frontend/src/locales/zh-CN/` 和相关组件的重复提示，并验证实际页面。

## Phase 3: 受支持的操作

- [x] T004 [US2] 修复 `frontend/src/features/runtime/wall/layoutModel.ts` 与 `WallLayoutComposer.vue` 的布局恢复与错误呈现。
- [x] T005 [US2] 修复 `frontend/src/stores/auth.ts` 与路由和登录页的认证探测失败反馈及重试。
- [x] T006 [US2] 检查 `runtime-dotnet/src/ScpCv.Infrastructure/Media/PresentationPreviewArtifact.cs` 的重复文件读取和完整制品合同，记录性能问题及限制。
- [x] T007 [US2] 核对 `runtime-dotnet/src/` 中无调用的退役分支和它们的测试、协议合同，清理有明确证据的冗余。

## Phase 4: 验证

- [x] T008 运行相关前后端检查、真实服务验证，并记录检查范围及结果。
- [x] T009 运行 Spec Kit 校验和差异检查，更新本目录的验证记录。
