# Implementation Plan: 现场控制台前端体验优化

**Branch**: `main` | **Date**: 2026-10-15 | **Spec**: [spec.md](spec.md)

**Input**: `specs/013-frontend-experience/spec.md`

## Summary

统一共享Vue控制台的页面结构和反馈，保留现有Pinia/REST/SSE及设备动作。先完善通用页面标题、导航滚动/焦点与响应式样式，再优化首页两窗入口、目录工具栏/空结果、其余操作页及中文文案。只在本机隔离环境验证，不部署现场。

## Technical Context

**Language/Version**: TypeScript6、Vue3.5。
**Primary Dependencies**: 既有Naive UI、Vue Router、Pinia、Fluent图标与语义令牌；pnpm，不新增UI依赖。
**Storage**: 既有store与服务器数据；不增加后端字段或数据库迁移。
**Testing**: Node测试、happy-dom组件回归、vue-tsc、Vite网页构建；本机真实浏览器截图/交互。
**Target Platform**: 网页、共享Electron/Capacitor页面，360px起。
**Project Type**: 现有分进程控制台的前端改进。
**Performance Goals**: 不新增周期轮询，不在页面切换重复加载全库，主要反馈保持即时。
**Constraints**: 不自动发实体命令、不改权限/TLS/共享/播放生命周期；代码默认500行以内。
**Scale/Scope**: 主要导航页面、两窗口、目录和预设、设置与公共反馈。

## Constitution Check

| 原则 | 设计前/后结论 |
| --- | --- |
| I 运行安全 | PASS：只整理触发入口与展示，原防重、确认和阶段语义保持。 |
| II 规范驱动 | PASS：013规范、方案与任务先于实现；012现场缺口不转为本轮完成。 |
| III 分层验证 | PASS：纯逻辑、组件、构建与浏览器分开；无浏览器时不假报视觉通过。 |
| IV 合同边界 | PASS：前端只复用原REST/SSE，不接设备/本机原生对象。 |
| V 最小复杂度 | PASS：少量公共组件与语义样式，不引入框架或全局新状态层。 |

## Project Structure

```text
specs/013-frontend-experience/{spec,plan,research,data-model,quickstart,tasks}.md
specs/013-frontend-experience/contracts/ui.md
frontend/src/design-system/{FPageHeader,FEmptyState}.vue
frontend/src/layouts/{AppShell,AppTopBar,AppNavigation}.*
frontend/src/features/{dashboard,display,sources,scenarios,backgroundAudio,devices,settings,auth,pptFocus}/
frontend/src/styles/experience.css
frontend/src/locales/zh-CN/
frontend/scripts/frontend-experience.test.mjs
```

**Structure Decision**: 保留feature文件夹；通用视觉/语义放design-system，业务布局在feature，公共内容宽度/触控/可读性放experience.css，不在超长base.css堆积。

## 视觉方向

沿用清源青色操作色#478384、深青#356263、浅青#e9f0f0，以及现有中性前景/表面令牌，不新增裸色。标题使用现有MiSans/Segoe UI的600字重，正文常规，路径与数字用系统等宽字体。页面上部是紧凑标题与主动作，工具区紧跟内容；首页两张真实窗口状态卡是控制台的识别点。去除品牌重复头、无功能装饰和过大hero，状态始终用文字和色彩共同表达。

## Complexity Tracking

无宪章例外。已有接口不变，无需新增OpenAPI；UI交互约定见contracts/ui.md。
