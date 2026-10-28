# Implementation Plan: 滑动液态玻璃底栏

**Branch**: `main` | **Date**: 2026-10-28 | **Spec**: [spec.md](spec.md)

## Summary

共享导航组件使用一个玻璃镜片与Motion弹簧；Pointer Events负责捕获手势，路由在松手后提交。视觉更新直接写入组件局部CSS属性，不触发业务状态更新。

## Technical Context

**Language/Version**: TypeScript 6、Vue 3.5。
**Primary Dependencies**: Motion 14.7.1、simple-liquid-glass 5.4.0，均MIT。
**Storage**: 仅组件内存状态。
**Testing**: 真实浏览器运动控制与只读页面检查、类型与双模式构建、Android签名检查。
**Target Platform**: Android arm64-v8a与移动网页，包含手机和Pad。
**Project Type**: 共享Vue控制客户端。
**Performance Goals**: 视觉更新按显示刷新率运行，空闲后无底栏动画调度。
**Constraints**: 不在拖动期间触发路由或媒体订阅变更；不操作D4。
**Scale/Scope**: 五个导航入口、两种主题、手机和Pad横竖屏。

## Constitution Check

- 客户端只修改本地导航；REST、SSE及硬件合同保持。
- 规范、设计和任务先于实现；依赖许可及真实验证单独记录。
- 共享组件与运动控制各自小于500行；预览策略继续使用现有布局判断。
- 设计完成复查：满足上述要求。

## Project Structure

- `frontend/src/layouts/glass/`：运动控制、底栏Vue组件及材质CSS。
- `frontend/src/layouts/AppNavigation.vue`、`AppShell.vue`：共享入口与路由接线。
- `frontend/src/styles/console.css`：底栏位置、安全区与Pad尺寸。
- `frontend/scripts/verify-glass-motion.mjs`：真实浏览器运动验证入口。
- `frontend/android/app/build.gradle`：安装包版本。
- `specs/028-liquid-glass-navigation/`：规范、调研、合同、任务与验证记录。

## Dependencies

研究、组件接口和依赖确认后完成运动控制，再接入页面、验证及打包。已有本机调研包含引擎源码与卸载路径检查；外部调研代理仅执行只读检查。
