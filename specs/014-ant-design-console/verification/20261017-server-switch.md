# T021 主机切换完整寿命

日期：2026-10-17。仅本机实际coordinator、API/Auth/Runtime、Ant连接页、启动和原生恢复代码；HTTP、原生桥及SSE均在隔离内存边界验证，不连接D4或发设备指令。

## 原始失败

先挂起旧切换的清会话，完成新切换，再释放旧请求：原实现最终地址是`older.invalid`，调用顺序`latest-clear→older-clear→older-save`，旧请求还返回新代次2。独立实验与12项核心红回归保留；不是据状态文件猜测完成。Runtime另在旧logout之后才递增真实代次。

实际Runtime/API/原生桥初轮13项4绿9红，连接页正确夹具9项3绿6红；启动/原生恢复10项1绿9红。后续增加入口已失效零动作、清会话例外越权、实际原生阶段反序及分层状态正对照。

## 实现

- Coordinator同步失效旧代次并返回本次冻结值，每个await与可重入回调前后复核。已启动的原生清理/保存串行实际结束，未开始的旧阶段跳过；不以观察超时伪造完成。restore也不能提前解除仍在途的清理门禁。
- `isSwitching`覆盖整个实际清理期。共享JSON/XHR业务入口和新认证明确拒绝，不排队重放；仅固定POST logout可由当前受管检查器越过门禁。该清会话401不触发连接页全局退出，普通当前401及其原说明仍有效，不是全局忽略401。
- Runtime将旧logout放入受管阶段，开始即投影真实代次，等待后复核原身份/页面。自己清身份后换用完成凭据，避免吞正常结果或清新账号。原生保存失败保留已经提交的新地址与实际投影，不假报回退或成功。
- 连接页冻结目标和意图，切换凭据有效才探测，探测完成凭据有效才导航。当前前置校验/保存失败不能误写成ControlHost离线，保留原已知连接状态；只有实际探测失败才显示不可达。
- 启动全量读取过期后不发旧警告或建SSE。原生inactive/卸载使旧恢复失效，重复active复用当前请求，后续active能开新探测，旧finally不清新在途记录；默认探测布尔合同保留。当前部分读取失败仍有警告，不因围栏静默吞错。

## 真实后退与路由守卫

Node真实路由守卫和生产浏览器均重现：连接页CSRF挂起时后退，全局初始化守卫会复用旧页面探测，页面因此仍卡住。地址栏popstate先变化不能证明SPA已离开。现在用`onBeforeRouteLeave`先失效旧意图，全局守卫可初始化新页面；旧探测晚到不能覆盖新初始化或续发状态查询。测试必须在释放旧门闩前看到登录表单，而非只核URL。

## 最终门禁

| 检查 | 实际结果 |
| --- | --- |
| 新持久回归 | 51项：coordinator13、Runtime/API/原生桥16、连接页12、启动/原生恢复10 |
| 全量前端 | 715/715，0失败、0跳过、0取消，无Vue/Ant警告或结束后异步异常 |
| Web/App | 各完整vue-tsc及生产构建exit 0 |
| Electron主进程 | TypeScript编译exit 0，不等于真实壳运行 |
| SFC/规范/差异 | 54个SFC、Spec Kit全目录及`git diff --check`通过 |
| 生产SPA连接 | 6组通过：正常、当前503后显式重试、logout/CSRF/status挂起后真实后退、旧切换在途后退再选新目标；未知错误/警告和横向溢出0 |
| 原有流程 | 5段认证、3组改密、7组退出、10组操作9笔内存写通过；56组页面0未知错误/警告/横向溢出/外网 |

浏览器使用1440/390/360px及明暗；两目标都是127.0.0.1虚构端口，全部API由浏览器内存响应，并明确返回跨源预检/凭据许可，没有关闭CORS/TLS策略。最新目标用例只向最新目标发CSRF/status，旧目标未获后续探测。离页用例允许新路由自己的初始化，不把它误算为旧探测续读；释放旧响应后请求总数不再增加。主动注入的一次503网络错误独立记录。

## 夹具误判

测试初版缺`history`全局、地址输入文案及“登录此主机”选择器与实际不符，忙碌按钮可访问名还带loading图标；按真实DOM修正，不改业务断言。错误状态响应须提供`detail`，不能把未登录成功体配503后又要求原错误说明。原始失败全部保留；有效红回归与真实后退红记录和这些夹具失败分别识别。

本机忽略目录`.validation/ant-console-014/`保存`switch-race-repro.log`、`switch-core-red.log`、`switch-runtime-red.log`、`switch-view-red-corrected.log`、`switch-resume-red-corrected.log`、`switch-leave-guard-red.log`、`switch-browser-leave-real-red.log`、`switch-layer-label-red.log`、`switch-all-verified.log`、`switch-build-web/app-complete.log`、`switch-electron-main.log`、`switch-browser-final-complete.log`与各`switch-*-regression.log`、`switch-functional.log`、`switch-visual.log`；浏览器截图/逐请求JSON在`switch-browser/final-complete-*/`。

## 边界与剩余工作

本轮不验证真实Electron/Android系统回调、Cookie清理/磁盘写入、跨标签或整页重载竞态、真实HTTPS与现场服务器、出画或声音。已开始的原生/HTTP副作用不能由客户端围栏撤销；原生阶段未真正结束时保持门禁，不据超时自动启动替代阶段。未推送或部署D4，012实体/人工待验及用户暂停的摄像机仍保留。T022登录页意图保护尚未实现，整体不标完成。
