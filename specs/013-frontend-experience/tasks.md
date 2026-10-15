# Tasks: 现场控制台前端体验优化

**Input**: `specs/013-frontend-experience/{spec,plan,research,data-model,quickstart}.md`与`contracts/ui.md`
**Tests**: 先覆盖新增交互；保留既有回归，浏览器和组件分开验收。

## Phase 1: Setup

- [X] T001 核对工作区、宪章、现有主要页面与前端副作用边界：`AGENTS.md`、`STYLE.md`、`frontend/src/features/`。
- [X] T002 整理规范、视觉方向、数据与交互约定：`specs/013-frontend-experience/`。

## Phase 2: Foundational

- [X] T003 新增公共页头/空状态组件及回归：`frontend/src/design-system/FPageHeader.vue`、`FEmptyState.vue`、`frontend/scripts/frontend-experience.test.mjs`。
- [X] T004 公共响应式内容/长文本/触控/焦点样式：`frontend/src/styles/experience.css`、`frontend/src/main.ts`。

## Phase 3: US1 常用操作

**独立验收**: 所有导航入口可达，长页切换回顶部，键盘主内容定位，两窗卡不发控制命令。

- [X] T005 [US1] 导航滚动/焦点与顶栏信息层级：`frontend/src/layouts/AppShell.vue`、`AppShell.css`、`AppTopBar.vue`。
- [X] T006 [US1] 首页紧凑页头与两窗口真实状态入口：`frontend/src/features/dashboard/DashboardView.vue`、`DashboardWindows.vue`。

## Phase 4: US2 目录操作

**独立验收**: 无匹配与空目录不同，清除筛选不动目录，多选范围明确，长路径可用。

- [X] T007 [US2] 媒体目录工具栏、空结果和选择反馈：`frontend/src/features/sources/SourcesView.vue`、`SourcesView.css`。
- [X] T008 [US2] 预设页头/空结果/目录与操作区一致：`frontend/src/features/scenarios/ScenariosView.vue`、`ScenarioDirectoryTools.vue`。

## Phase 5: US3 操作反馈与其余页面

**独立验收**: 原权限、确认与请求阶段保持，主要操作名和提示具体，未新增隐式写出。

- [X] T009 [US3] 两窗口与PPT/共享页面信息层级和反馈：`frontend/src/features/display/DisplayControlView.vue`、`frontend/src/features/pptFocus/`、`frontend/src/features/screen-share/`。
- [X] T010 [US3] 背景音乐、摄像机与音量操作区及说明：`frontend/src/features/backgroundAudio/`、`frontend/src/features/devices/`。
- [X] T011 [US3] 设置、连接和登录的标题/表单/无效入口：`frontend/src/features/settings/`、`frontend/src/features/auth/LoginView.vue`。
- [X] T012 [US3] 用“说人话”整理界面文案并核事实/阶段不变：`frontend/src/locales/zh-CN/`及本轮组件可见文字。

## Phase 6: US4 响应式与视觉

**独立验收**: 五种宽度/双主题无页面级溢出，手机44px，键盘/减少动效可用。

- [ ] T013 [US4] 全主要页面真实浏览器截图和关键交互：`specs/013-frontend-experience/verification.md`、忽略的`.validation/frontend-experience-013/`。
  - 已完成五宽度/双主题主要页面与目录、抽屉、取消确认、导航交互；真实200%缩放和系统减少动效偏好尚未验收，不勾全。
- [X] T014 [US4] 依据截图修正溢出、触控、焦点和布局：本轮`frontend/src/`组件及样式。

## Phase 7: Polish

- [X] T015 全量前端测试/类型/构建、Spec Kit与差异检查：`frontend/scripts/`及`specs/013-frontend-experience/verification.md`。
- [X] T016 同步使用文档、变更和任务真实完成状态：`docs/使用文档.md`、`docs/CHANGELOG.md`、本文件。

## Dependencies & Execution Order

T001–T002→T003–T004→US1→US2→US3→US4→Polish。同一文件串行；页头与纯回归可独立准备，不自动委派额外代理。先公共结构再各页，截图后修正，最后全量门禁。不以本机仿真替代012实体任务；初轮仅本地提交，后续部署按下述追加授权执行。

## Phase 8: 用户追加部署授权（2026-10-15）

- [ ] T017 推送GitHub/GitLab并在D4干净工作区快进拉取；固定已测网页产物，备份并部署，核对HTTP/HTTPS、资产摘要、原进程及两窗状态不变：`specs/013-frontend-experience/deployment.md`与忽略的`.validation/deploy-013/`。不重启播放组、不发墙面/电源/声音/媒体指令；T013未验项不据部署完成。
