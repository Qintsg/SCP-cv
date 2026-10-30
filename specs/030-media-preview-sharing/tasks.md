# Tasks: 媒体预览、网页刷新与持续共享

依据：[spec.md](spec.md)、[plan.md](plan.md)。源码及客户端发布范围见031，D4部署排除。

## Phase 1：准备

- [x] T001 在specs/030-media-preview-sharing/完成需求、研究、数据模型和合同。

## Phase 2：基础检查

- [x] T002 检查docs/known-pitfalls.md、正式WebView链路与共享/预览资源归属。

## Phase 3：US1 网页刷新

- [x] T003 [US1] 在RuntimeStateService、PlayerRuntimeHost.Web与PlaybackControl实现REFRESH及来源门禁。
- [x] T004 [US1] 更新docs/components/schemas/PlaybackAction.yaml及控制合同。
- [x] T005 [US1] 在runtime-dotnet/tests/ScpCv.Windows.Tests/用真实WebView与长缓存HTTP验证刷新和失败。

## Phase 4：US2 持续共享

- [x] T006 [US2] 在frontend/src/features/screen-share/实现应用共享所有者与紧凑停止入口。
- [x] T007 [US2] 在rtcPeer.ts处理暂时连接中断并核对心跳寿命。
- [x] T008 [US2] 在frontend/scripts/添加真实WebRTC与组件切换验证。

## Phase 5：US3 全类型预览

- [x] T009 [US3] 在frontend/src/features/sources/实现全类型详情与文稿分页预览。
- [x] T010 [US3] 在SourcesView及useSourceActions加入名称和菜单入口，在ClientStreamPreview支持手机明确观看。
- [x] T011 [US3] 在MediaSourceService增加音频文件预览并更新OpenAPI。
- [x] T012 [US3] 在frontend/scripts/用真实媒体、流和只读浏览器验证预览及释放。

## Phase 6：交付检查

- [x] T013 在specs/030-media-preview-sharing/verification.md记录本机验证，更新使用文档及已知坑。
- [x] T014 执行类型、构建、Spec Kit、Redocly与相关实际回归，核对git diff。

## Dependencies

T001→T002→US1/US2/US3→T013→T014。独立研究可并行；共享App接线及预览入口顺序编辑。每条用户路径独立验证，全部完成后交付工作区。
