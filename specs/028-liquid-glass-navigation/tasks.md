# Tasks: 滑动液态玻璃底栏

**Input**: [spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[contracts/navigation.md](contracts/navigation.md)

## Phase 1: Setup

- [x] T001 完成规范、Android参考实现与依赖研究，记录于specs/028-liquid-glass-navigation/。
- [x] T002 在frontend/package.json引入精确版本开源依赖并记录许可。

## Phase 2: User Story 1

- [x] T003 [US1] 在frontend/src/layouts/glass/navigationMotion.ts实现连续位置、形变、取消、吸附与资源释放。
- [x] T004 [US1] 在frontend/src/layouts/glass/LiquidBottomNavigation.vue接入指针、键盘、单次导航和实际路由同步。
- [x] T005 [US1] 更新frontend/src/layouts/AppNavigation.vue与AppShell.vue导航接线。

## Phase 3: User Story 2

- [x] T006 [US2] 在frontend/src/layouts/glass/liquidNavigation.css和frontend/src/styles/console.css实现镜片、深浅色、手机Pad、安全区与减弱效果偏好。
- [x] T007 [US2] 运行frontend/scripts/verify-glass-motion.mjs及只读真实页面检查，记录specs/028-liquid-glass-navigation/verification.md。

## Phase 4: Delivery

- [x] T008 更新docs/使用文档.md和docs/CHANGELOG.md，完成类型、Web及App构建和Spec Kit校验。
- [x] T009 更新frontend/android/app/build.gradle，生成并核验签名arm64-v8a APK，将手机与Pad文件放到桌面，推送两个仓库。

依赖顺序：T001→T002→T003→T004→T005→T006→T007→T008→T009。
