# Implementation Plan: 网页侧栏与客户端玻璃导航

**Branch**: `main` | **Date**: 2026-10-30 | **Spec**: [spec.md](spec.md)

## Summary

导航形态按构建目标区分：普通Web与Electron使用侧栏，Android的手机/Pad布局使用底栏。触摸命中按当前真实几何计算，镜片和交互层分别保持正确位置；沿用Motion和simple-liquid-glass改善材质与运动。

## Technical Context

**Language/Version**: Vue3、TypeScript、CSS、Java/Android。

**Primary Dependencies**: Ant Design Vue、Motion14.7.1、simple-liquid-glass5.4.0、Electron44.2、Capacitor8。

**Storage**: 不新增持久数据。

**Testing**: 真实Vue/浏览器几何、生产运动接口、构建与包内容/签名校验。

**Target Platform**: Web、Windows x64、Android arm64-v8a。

**Project Type**: 共享控制台与客户端制品。

**Performance Goals**: 手势局部绘制，空闲停止动画，导航只执行一次。

**Constraints**: 保留030改动；不使用computer use，不部署D4。

**Scale/Scope**: 五个底栏入口，手机/Pad横竖屏及Web侧栏。

## Constitution Check

- [x] 运行安全：只修改控制客户端，不操作现场设备。
- [x] 规范可追溯：本目录记录需求、设计、任务与验证。
- [x] 分层验证：真实几何与运动、包内容和签名分别检查；手指操作边界明确。
- [x] 进程边界：复用平台接口、路由与Store，不增加直接设备访问。
- [x] 复杂度：复用已有玻璃组件与运动模块，不新增渲染框架。

## Project Structure

- `frontend/src/composables/useControlLayout.ts`：导航平台条件。
- `frontend/src/layouts/glass/`：当前几何、交互命中与玻璃材质。
- `frontend/scripts/verify-navigation-031.mjs`：实际页面几何和视觉检查。
- `frontend/android/app/build.gradle`及`frontend/package.json`：1.0.3/code4版本。
- `specs/031-navigation-glass-client/`：验证及制品记录。

## Implementation

先限定Web/Electron导航，再修正固定宽度旋转时失效的缓存位置。生产命中函数接受真实DOM测量，视觉变形只作用于光学层；监听视口/方向变化取消旧手势。验证深浅色、横竖屏、偏好和030回归后提交推送；从固定实现提交构建并更新三个桌面制品，保留既有签名及公开CA。

030进入客户端打包后，Electron资源协议为PDF.js的.mjs工作线程返回JavaScript类型；仅允许HTTPS及本机回环内嵌预览，保留被嵌入网页自身CSP和X-Frame-Options，继续隔离原生桥及顶层导航。

## Complexity Tracking

无宪章例外。
