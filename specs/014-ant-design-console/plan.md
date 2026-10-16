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

## T019修改密码后的CSRF同步

密码POST已成功是不可回退事实：Auth先复核原归属、递增认证修订并清掉旧CSRF缓存，再通过既有公开CSRF读取同步目标主机令牌。正常返回同步结果；同步失败但归属仍有效时不抛成密码失败，账户表单清敏感字段，显示持久的“密码已修改、会话未同步”警告，暂禁再次改密并提供仅重读CSRF的显式重试。重试和初次同步都复核认证/主机；离页或换账号不串反馈，旧响应不写新缓存。保持Cookie按请求协议隔离和跨源缓存优先，不改API合同、不盲目重放密码或其它写操作。

真实重试暴露通知宿主离场竞态：Ant4.2.6的旧placement结束回调捕获空列表，会删掉刚进入同placement的新提示。宿主在onAllRemoved按仍有效的Toast store重建当前项，不复活旧项或清其它业务通知；补真实DOM过渡交错与浏览器失败→恢复回归。

## T020退出完成归属

退出会主动清当前身份，不能在完成后用开始时的账号对象判定自身过期。Auth退出返回一次性完成凭据：记录服务端退出是否确认、原失败说明以及清本地身份后捕获的认证/主机寿命函数；旧响应仍抛明确归属失效，不清新身份。组件另外冻结原页面/路由及主机环境，仅环境和完成凭据都有效才反馈/跳转。正常HTTP200提示退出；当前网络/服务端失败只表示页面状态已清，持久警告服务器登录可能仍有效并进入登录页，不声称已安全退出、不重放。当前401仍由main原处理接管。页面/身份改变解除旧忙态，但旧finally不能解除新退出的忙态。

## T021主机切换完整寿命

- Coordinator每次切换同步递增并冻结自己的代次、关闭旧连接，所有await及可重入回调前后复核；不得把最后读到的全局代次作为旧请求结果。失效请求明确拒绝，当前原错误保留。
- 清原生会话/保存配置等不可撤销的异步阶段串行执行，但新意图仍立即使旧代次失效。新阶段等已启动的旧阶段实际结束后才继续，不能靠观察超时假称清理完成；未开始的旧阶段跳过，最终有效保存不会被更早写入反盖。直接restore也不能提前解除仍在途的原生清理门禁。
- 暂存切换数量控制`isSwitching`；普通API/上传及新认证在切换未结束时明确拒绝，不发往尚未提交的旧地址。只有Runtime持有的本次清会话检查器允许发送旧主机logout，不授予其它业务写入例外。
- Runtime把旧logout放入受管切换阶段，复核原认证及调用页面，避免等待期间的新登录被随后清除；真实代次在开始时投影。清本地store后改用完成后的认证凭据，配置提交时立即投影实际主机；保存失败保留已发生状态，不静默回退或报成功。
- 连接页冻结目标、页面/路由和本地意图，只有本次切换完成凭据有效才探测；探测也接同一调用归属，离页不续发CSRF后的状态查询或导航。测试使用实际模块/内存网络、受控Electron桥与生产浏览器，不发设备命令。
- 调用链复核覆盖`stores/index.ts`启动/全量读取和`platform/lifecycle.ts`原生恢复：读取只向原身份反馈，失效后不建SSE；inactive/卸载使旧恢复失效，后来的active可开始新探测，旧finally不清新在途状态。保持默认探测布尔合同，原生框架回调只在内存驱动验证，不当作实机验收。

## T022登录意图与完成

登录页同步防重，提交时冻结用户名/密码/redirect、页面完整路由、本地意图及真实/投影主机与地址。离开守卫、主机变化、同页路由变化及卸载清旧密码并失效旧意图，旧finally不解锁新请求。复用AuthenticationOperationOptions的canContinue/onCommitted，Auth.login在CSRF后、密码提交前和身份提交前复核，保持原AuthUser返回值；自身成功后由完成凭据接管，不将正常提交误判过期。

HTTP层也要消费同一归属：CSRF/登录/状态的调用检查器在派发、解析/401处理及CSRF缓存赋值前复核，不能只在Auth await后过滤旧错误，否则离页旧401仍会先走全局跳转。requestJson使用命名归属选项，原切换例外仍仅限当前固定logout；普通当前401和原错误说明、CSRF原回退策略不变，不盲目重放。当前失败保留当前表单的显式重试，成功/取消/换主机/离页清敏感输入，错误与原生输入建立可访问关联。
