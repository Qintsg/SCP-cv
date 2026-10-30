# Tasks: 网页侧栏与客户端玻璃导航

依据：[spec.md](spec.md)、[plan.md](plan.md)。

## Phase 1：准备

- [x] T001 在specs/031-navigation-glass-client/完成需求、研究、设计和合同。
- [x] T002 检查frontend/src/layouts/glass/及useControlLayout的实际坐标和平台条件。

## Phase 2：US1 网页导航

- [x] T003 [US1] 在frontend/src/composables/useControlLayout.ts区分Web、Electron及Android导航，核对AppShell、PptFocusView、ScreenShareTray。
- [x] T004 [US1] 在frontend/scripts/verify-navigation-031.mjs验证Web三档尺寸及App布局，无水平溢出。

## Phase 3：US2 Pad命中与玻璃

- [x] T005 [US2] 在frontend/src/layouts/glass/按实时几何命中并处理旋转、取消、视差及显示比例。
- [x] T006 [US2] 在liquidNavigation.css和LiquidBottomNavigation.vue完善材质、高光、折射及运动，保持系统偏好。
- [x] T007 [US2] 运行frontend/scripts/verify-glass-motion.mjs及真实几何/视觉回归，记录验证边界。

## Phase 4：US3 提交与制品

- [x] T008 [US3] 更新frontend/package.json、frontend/android/app/build.gradle到1.0.3/code4，更新文档并验证030。
- [x] T009 [US3] 将030及031实现、规范提交并推送两个现有远端，核对提交身份。
- [x] T010 [US3] 按固定提交构建Windows便携ZIP与Android签名包，更新桌面手机及Pad命名文件并核对内容、CA、签名。

## Phase 5：记录

- [x] T011 在specs/031-navigation-glass-client/verification.md及packages.md记录实际检查和制品。

## Dependencies

T001→T002→US1/US2→T008→T009→T010→T011。平台策略与玻璃模块可分别编辑，最终共用真实页面检查。每条路径完成后验证，全部功能完成再提交及打包。
