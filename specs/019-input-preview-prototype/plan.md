# Implementation Plan: 输入与预览原型

**Branch**: `main` | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

## Summary

在现有wall功能旁放显式prototype目录，用独立Vue入口和回环Vite启动器隔离生产鉴权／API。Ant选择器提供A二级菜单、B分类面板、C侧边抽屉；内存目录与布局状态跨变体保留。Canvas.captureStream生成样片，一个IP一个生产者，多个video消费同一MediaStream，稳定块ID与CSS几何脱钩。不是正式FLV池，不连接现场，不改生产播放组件。

## Technical Context

**Language/Version**: Vue3、TypeScript、Node24。
**Primary Dependencies**: 现有Ant Design Vue、Vite、Vue Router，无新增依赖。
**Storage**: 内存，URL仅保存variant，无业务持久化。
**Testing**: 类型／生产构建；只读无头浏览器截图和媒体检查，不模拟鼠标键盘。
**Target Platform**: 支持Canvas捕获流的本机Chromium，手机响应式布局。
**Project Type**: 可丢弃UI原型，确认后重写正式实现。
**Performance Goals**: 同IP一个生产者；纯几何变化无重建，不虚构网络首帧目标。
**Constraints**: 仅回环监听，无API代理，禁止访问现场；生产构建不导入原型。
**Scale/Scope**: 24工作站、监控76、五摄像机84–88、现场81与四窗别名。

## Constitution Check

安全：无设备／数据写入，不加载真实鉴权和API。
追溯：019规范／设计／任务／验证齐全，设备表为事实来源。
验证：媒体／布局证据与模拟状态分离，不声称现场性能。
最小复杂度：无新依赖、常驻后端；单文件低于500行，入口可整体删除。

设计前后均通过。prototype技能偏好嵌入原页，但该页会初始化真实鉴权和SSE；独立入口可保证只看demo，同时保留控制台导航和密度语境。

## Project Structure

```text
frontend/src/features/runtime/wall/prototype/
  index.html main.ts DemoApp.vue InputPicker.vue PreviewTile.vue
  catalog.ts demoStreams.ts prototype.css NOTES.md
frontend/scripts/serve-input-prototype.mjs
specs/019-input-preview-prototype/
  spec.md plan.md research.md data-model.md quickstart.md tasks.md verification.md
  checklists/requirements.md contracts/ui.md
```

## Visual Direction

沿已有雾蓝侧栏／Ant蓝，不做营销hero。色板：画布#f3f6fb、面板#fff、墨色#182338、操作#1677ff、边界#e2e8f0、演示提示#b66b13。文字用雅黑／Segoe UI，IP与媒体计数用Consolas。签名是持续运行的32:9信号画布：调整几何时时间继续；计数是机制观测，不是装饰KPI。

结构：四输出小预览→左布局与选源／右媒体目录；三选源变体只换展开结构，布局不跳动。审视后去掉冗余Toast和每卡技术说明，演示边界集中在顶栏和页脚。

## Implementation Strategy

先目录／样片，再三个选择器和稳定几何，最后实际渲染。生产首GOP发送、FLV客户端解码共享池、真实可达探测、组播协议不在本轮实现。
