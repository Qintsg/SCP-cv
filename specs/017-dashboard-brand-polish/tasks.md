# Tasks: 紧凑仪表盘与品牌导航

**Input**: [plan.md](plan.md)、[spec.md](spec.md)、[research.md](research.md)、[contracts/ui.md](contracts/ui.md)
**Tests**: 规范要求自动回归与只读浏览器验收；不使用computer use。

## Phase 1: Setup

- [x] T001 检查工作区与治理文件，在specs/017-dashboard-brand-polish/完成规范和16项需求质量检查。

## Phase 2: Foundational

- [x] T002 复现自定义滚动并记录至specs/017-dashboard-brand-polish/research.md，明确只读夹具和无现场副作用边界。

## Phase 3: US1 一屏概览（P1，MVP）

独立验收：常规桌面浅深主题，单／双／自定义无多余滚动；框选及错误保留。

- [x] T003 [US1] 在frontend/scripts/wall-workspace-dom.test.mjs加入默认hint不出现且无空footer的回归，保留选择／重叠反馈测试。
- [x] T004 [US1] 调整frontend/src/features/dashboard/DashboardView.vue与frontend/src/features/runtime/wall/WallGridEditor.vue，删除无内容占位。
- [x] T005 [US1] 在.validation/console017/只读验证常规桌面／矮屏／手机，必要时按证据收紧仪表盘留白。

## Phase 4: US2 品牌导航（P2）

独立验收：两种主题、完整／折叠菜单可辨认且品牌链接仅导航。

- [x] T006 [P] [US2] 使用内置图像编辑生成frontend/src/assets/sspu-ailab-transparent.png；保留外部透明、内部白与原图。
- [x] T007 [US2] 在frontend/scripts/dashboard-brand.test.mjs覆盖品牌导航、主题和无业务调用合同。
- [x] T008 [US2] 修改frontend/src/layouts/AppShell.vue、AppNavigation.vue及frontend/src/styles/console.css，接入主题色、紧凑侧栏和品牌链接。
- [x] T009 [US2] 在.validation/console017/验证正常高度无菜单滚动、超矮可滚动、折叠及主题Logo对比。

## Phase 5: US3 预览说明（P2）

独立验收：说明清楚区分实体播放与可选本机预览；新服务端预览另立018。

- [x] T010 [P] [US3] 更新docs/console-workspace-016.md，记录用户最新要求改为服务端免安装FLV预览，解释当前实现与后续018边界。

## Phase 6: Polish

- [x] T011 运行相关及完整前端测试、类型与构建，更新specs/017-dashboard-brand-polish/verification.md、design-qa.md与docs/CHANGELOG.md。
- [x] T012 运行Spec Kit校验并检查diff与任务一致性，在specs/017-dashboard-brand-polish/verification.md记录结果；只本地提交不推送。

## Dependencies & Execution Order

T001→T002→US1；US2和US3可在基础后独立推进，最终统一验收。T003／T007先于对应实现运行，T011依赖故事完成。

## Parallel Examples

US1几何取证与US2图像编辑可独立进行；US3文档不与代码冲突。仅工具任务并发，不默认分派实现agent。

## Implementation Strategy

先完成删除冗余行的MVP，再主题／导航，最后完整视觉与测试；不改变安全门禁，不部署。
