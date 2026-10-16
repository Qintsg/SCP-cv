# Implementation Plan: Ant Design 现场控制台重构

**Branch**: `main` | **Date**: 2026-10-16 | **Spec**: [spec.md](spec.md)

## Summary

用真实Ant Design Vue组件替换Naive UI，保留Pinia/API和现场协议边界。按现场操作任务重排页面，不引入完整后台模板。布局只暴露一个应用按钮，内部复用版本预检再发送，不改变设备端语义。

## Technical Context

**Language/Version**: TypeScript 6、Vue 3.5。
**Primary Dependencies**: Ant Design Vue 4.2.6、Pinia、Vue Router、vue-i18n、Vite；移除Naive UI及其专用主题。
**Storage**: 保留服务端SQLite、客户端主机/主题偏好，无新增持久实体。
**Testing**: Node test、happy-dom真实组件、本机Playwright Chromium、vue-tsc、Web/App构建、Spec Kit校验。
**Target Platform**: 浏览器及共享Electron/Capacitor前端；本轮不改变原生壳。
**Project Type**: 现场设备控制Web应用。
**Performance Goals**: 不新增常驻轮询、不默认打开摄像机直播；布局点击不重复发送。
**Constraints**: pnpm、淘宝镜像（2026-10-16用户指定南京/清华/阿里云淘宝可选，南大失效后采用淘宝）、精确版本；UTF-8/LF；单文件默认500行；本机假API隔离实体副作用。
**Scale/Scope**: 54个Vue组件、主路由/设置页、共用通知/对话框和设计令牌。

## Constitution Check

- 运行安全：选择不发包；单次参数冻结；busy覆盖预检和发送；主机/账户/挂载寿命围栏；后端expected_revision拒绝并发旧稿。
- 规范追踪：本规范取代009/013冲突的草稿与视觉约定，保留历史验收事实。
- 分层验证：保留纯业务测试，组件测试迁移为Ant真实DOM；本机浏览器检查主流程与布局。
- 合同边界：不改REST/SSE、队列、窗口数、云台/音量/电源帧或软件声音绑定。
- 最小复杂度：不引入React/Vben工程或N组件兼容套壳；业务ActionMenu集中菜单动作派发。
- 初次及设计后复核通过；不需要治理例外。

## Project Structure

```text
specs/014-ant-design-console/
  spec.md plan.md research.md data-model.md quickstart.md tasks.md
  checklists/requirements.md contracts/ui.md verification.md
frontend/src/
  App.vue main.ts
  design-system/              # Ant主题、业务菜单、Toast/Dialog及图标
  layouts/                    # 分组导航、轻顶栏、响应式外壳
  composables/                # 确认与请求归属、滑杆收尾
  platform/lifecycleBack.ts   # 原生返回键识别真实Ant弹层
  features/                   # 各任务工作区与既有业务逻辑
  styles/                     # 共用控制台语义令牌及移动端覆盖
frontend/scripts/             # 真实组件与迁移合同回归
docs/                         # 使用、维护与变更记录
```

**Structure Decision**: 沿用已有功能目录；改动超500行的组件先拆分。小范围UI语义抽取不建立全套兼容控件。

## Execution

1. 记录官方研究与UI合同；需求清单16/16通过。
2. 安装精确Ant依赖，移除Naive Provider/主题，建立真实Ant反馈与业务菜单。
3. 迁移各页事件/属性/slot，先验证表单、滑块、菜单及弹层，避免视觉正确但交互失效。
4. 重组工作台、目录、显控、摄像机与设置布局；应用墙面保持一次点击。
5. 完整前端测试、类型、双构建、Electron薄壳编译及本机浏览器；更新文档并记录原生壳/现场边界。

## T018认证响应边界

认证与主机是两个独立寿命：纯服务模块读取当前认证对象/ID和认证操作修订，API在解析JSON/处理401及XHR进度/完成前同时复核主机与认证。Auth Store保持原公开字段、动作及`$reset`，内部使用Pinia setup作用域绑定身份读取器，避免API反向依赖Store；初始化共用一项在途探测，登录/退出/探测只允许原操作继续或提交，过期结果不得覆盖新身份。当前401仍退出，CSRF仍按原Cookie/内存Token路径，不重放请求、不改后端会话或设备。先验证已有旧401红回归，再验证JSON/XHR、同ID认证、主机切换和认证操作交错，最后全量与浏览器。
