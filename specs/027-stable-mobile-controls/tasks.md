# Tasks: 稳定播放控制与手机工作台

**Input**: 本目录spec.md、plan.md和research.md。

## Phase 1: 调查

- [x] T001 检查滑块、状态、文稿、导航、预览和共享链路，完成研究及设计。

## Phase 2: 稳定控制

- [x] T002 [US1] 修复`frontend/src/composables/useThrottledSlider.ts`的同次收尾重复提交，验证真实文件提交。
- [x] T003 [US1] 调整`DisplayControlView.vue`、共享状态及音频页面的频繁反馈位置，避免布局位移。
- [x] T004 [US2] 统一文稿两按钮与键盘导航，更新`PlaybackControl.vue`和`PptFocusView.vue`布局。

## Phase 3: 手机

- [x] T005 [US3] 实现共享手机策略，调整AppShell及玻璃悬浮底栏和安全区。
- [x] T006 [US3] 停用手机实时预览与缩略图，调整对应父容器和专注页远控布局。

## Phase 4: 屏幕共享

- [x] T007 [US4] 后端发布可配置的视频策略，前端应用更高上限并处理尺寸改变。
- [x] T008 [US4] 精简共享面板，固定状态位置，更新OpenAPI和使用说明。

## Phase 5: 验证

- [x] T009 执行真实滑块提交、共享策略及WebRTC验证。
- [x] T010 执行手机/桌面只读页面检查、前后端构建及合同校验，更新验证记录并提交。
