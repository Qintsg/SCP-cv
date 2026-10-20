# Implementation Plan: 紧凑仪表盘与品牌导航

**Branch**: `main` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)

## Summary

复用Vue／Ant控制台，移除指定重复说明及其布局占位，收紧导航与品牌区的纵向几何；保留32:9预览和矮屏滚动退路。仅编辑图像外部白底，主题感知的品牌链接返回仪表盘。澄清现有可选本机桥与免安装直播方案边界，不擅改预览架构。

## Technical Context

**Language/Version**: TypeScript、Vue 3，Node 24本地测试工具。
**Primary Dependencies**: 现有Ant Design Vue、Vue Router、Pinia；不增加播放器依赖。
**Storage**: 无业务数据变化；沿用主题偏好。
**Testing**: Node测试、HappyDOM真实Vue组件、只读无头Chromium截图及DOM几何。
**Target Platform**: 共享Web／Electron／Android控制台；本轮只验证本机浏览器与构建。
**Project Type**: 现有控制台有限样式与导航调整。
**Performance Goals**: 常规桌面无需滚动，保留字体和画面比例，不增加常驻进程。
**Constraints**: 仅本机、无computer use／浏览器输入模拟；无网络设备与写API调用。
**Scale/Scope**: 仪表盘、共享侧栏、品牌图像、预览设置文案和文档。

## Constitution Check

- I 安全：不改变墙面／电源／音量请求；品牌仅路由导航，错误依然可见。
- II 可追溯：017规范、设计、任务与验证记录相互引用。
- III 分层验证：样式不能仅靠构建宣布完成，需浅深主题与多尺寸截图／几何核验。
- IV 边界：不更改REST／SSE、.NET进程、预览桥或设备流协议。
- V 最小复杂度：复用组件，所有变更代码小于500行，不新增运行依赖。

设计前／后均通过，无例外。

## Project Structure

### Documentation (this feature)

```text
specs/017-dashboard-brand-polish/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/ui.md
├── quickstart.md
├── tasks.md
├── verification.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
frontend/src/features/dashboard/DashboardView.vue
frontend/src/features/runtime/wall/WallGridEditor.vue
frontend/src/features/settings/tabs/ClientPreviewSettings.vue
frontend/src/layouts/{AppShell,AppNavigation}.vue
frontend/src/styles/console.css
frontend/src/assets/sspu-ailab-transparent.png
frontend/scripts/dashboard-brand.test.mjs
```

**Structure Decision**: 保留既有分层，无新业务服务。临时截图、几何脚本位于忽略的`.validation/console017/`。

## Visual Direction

- 浅色：侧栏雾蓝`#e8eef7`、正文墨蓝`#213650`、辅助文字`#506480`；继续用品牌蓝`#1677ff`，选中蓝改为可读的`#1264d1`。
- 深色：侧栏蓝灰`#23364b`、文字`#edf3fb`、辅助文字`#c0cede`，反色Logo不加白色垫片。
- 字体沿用Segoe UI／微软雅黑，数据Consolas；不添加展示字体。
- 布局保持四窗→32:9墙面、右侧声音／电源；只减少无内容占位，不增加装饰或隐藏功能。
- 导航采用紧凑分组、系统区底部对齐；短屏减少上下留白，真正超出才滚动。控制台的重点仍是墙面图而非品牌装饰。

## Complexity Tracking

无治理例外，不新增数据库、运行服务或转换网关。
