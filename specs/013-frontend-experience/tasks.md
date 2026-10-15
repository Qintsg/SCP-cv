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

- [X] T017 推送GitHub/GitLab并在D4干净工作区快进拉取；固定已测网页产物，备份并部署，核对HTTP/HTTPS、资产摘要、原进程及两窗状态不变：`specs/013-frontend-experience/deployment.md`与忽略的`.validation/deploy-013/`。不重启播放组、不发墙面/电源/声音/媒体指令；T013未验项不据部署完成。

## Phase 9: Convergence

2026-10-15接续检查FR-004至FR-008、US2/US3/US4相关验收、当前状态捕获及五项宪章原则。发现7项可实现缺口（3项contradicts、4项partial；1项CRITICAL、3项HIGH、3项MEDIUM），按下列任务收敛。012未勾项主要是现场验收，用户暂挂的摄像机机位不在本轮恢复；T013人工偏好/缩放仍单独验证。

- [X] T018 修复显示器绑定在途重复提交和可变目标导致的错误成功提示；冻结请求窗口/显示器，提交期间明确等待，卸载/换主机/换账号后抑制迟到提示；用延迟响应组件测试覆盖，涉及`frontend/src/features/settings/tabs/DisplaySettingsTab.vue`及`frontend/scripts/settings-experience.test.mjs`，追溯FR-005/006、US3/AC1–2、Constitution I（contradicts，CRITICAL）。
- [X] T019 使媒体/预设选择范围随目录和筛选结果保持可见，确认框固定本次对象/目标并列出路径，处理确认期间改选和忙时切目录；覆盖批量逐项失败与零意外请求，涉及`frontend/src/features/sources/useSourceSelection.ts`、`ScenarioDirectoryTools.vue`及相关目录回归，追溯FR-004/006、US2/AC2（contradicts，HIGH）。
- [X] T020 原子提交预设列表与目录的同次刷新，保留旧快照及持久错误/重试入口，不把失败显示为空目录；覆盖部分读取失败与迟到响应，涉及`frontend/src/stores/scenarios.ts`、`ScenariosView.vue`及组件回归，追溯FR-006、Edge Cases网络失败（partial，HIGH）。
- [X] T021 为预设预览提供原生键盘入口，关闭后返回触发位置，不让预览触发激活；涉及`ScenariosView.vue`、`ScenarioPreviewDrawer.vue`及DOM回归，追溯FR-007、US4/AC2（partial，MEDIUM）。
- [X] T022 “从当前状态生成”依据在线实际源/实际播放状态生成草稿，拒绝暂态共享，对未知/未确认窗口说明保持原状；不得保存失败换源的请求源；覆盖实际/请求不一致、离线、暂停/空闲及无HTTP副作用，涉及`frontend/src/features/scenarios/scenarioCapture.ts`、`ScenariosView.vue`及回归；同时在`stores/runtime.ts`切主机时清除旧系统音量可信标记，追溯FR-005/006、US3/AC2（contradicts，HIGH）。
- [X] T023 清除PPT专注页旧五/六列和低高度隐藏名称规则，与现有两列动画/翻页布局统一；补页图/LibreOffice在768×560、840及1024宽度浏览器检查，涉及`PptFocusResponsive.css`、`PptFocusView.vue`，追溯FR-007/008、US4/AC1–2（partial，MEDIUM）。
- [X] T024 为账户密码表单提供输入名称、错误关联和回车提交，保留原验证/安全流程；使用隔离组件验证，不修改真实账号口令，涉及`frontend/src/features/settings/tabs/AccountSettingsTab.vue`及设置DOM回归，追溯FR-007、US4/AC2（partial，MEDIUM）。

本阶段验证见[接续验证](followup-verification.md)：新增51项、全量535/535通过，真实浏览器关键流程与PPT两模式12组尺寸通过。T013保留真实缩放/系统偏好未验项。
