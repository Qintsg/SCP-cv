# T019 改密后的会话同步

日期：2026-10-17。仅本机前端、实际Auth/API模块和生产SPA；认证、Cookie及失败响应在内存HTTP边界模拟，不改真实账号或D4。

## 原因与红回归

后端成功改密会重签会话并`csrf.IssueToken`轮换Cookie；前端此前仍优先取内存旧CSRF，之后的写入会被拒绝。真实模块先红：期望`after-password`，实际`before-password`。生产SPA也先红：改密POST成功，随后的退出POST携带旧令牌，受控校验返回400；不是仅看HTTP200或成功Toast。

新增扩展回归初轮52项40绿12红；旧实现没有同步步骤。随后修正测试CSRF成功响应缺少JSON Content-Type的夹具错误，保留正式解析/错误检查，不修改生产解析器掩盖它。

## 行为

- 密码POST成功后先检查原归属，再使旧请求失效、清旧令牌，读取目标主机的新CSRF。目标HTTP/HTTPS Cookie选择、跨源缓存优先和凭据跟随不变，没有修改REST合同。
- 改密与令牌同步是两个结果：仅同步失败时仍清空密码，持久提示“密码已修改，但会话尚未同步”，禁止再次提交密码并提供“重新同步会话”。按钮只GET令牌，不重放密码或其它写入。
- 重试失败保留已改密事实；恢复或归属失效仅关闭本表单拥有的旧同步通知，不清其它业务错误。离页、换主机/账号不发布旧反馈，旧同步响应不覆盖新令牌。
- 手机说明与按钮纵向排布，避免窄列；360/390px实测按钮至少44px且无横向溢出。

## 通知离场竞态

真实浏览器第一次失败→重试成功时，旧错误已移除、状态已恢复，但新成功提示消失。Ant4.2.6 `vc-notification/HookNotification`的旧`onAfterLeave`捕获空placement，迟到后删除新placement。真实DOM控制旧离场节点transitionend也先红，不能只验证Toast store。

FToastHost在其`onAllRemoved`回调按仍有效的业务store重建当前项，不复活已删除/过期项。固定版本实际支持此回调但公开类型漏列，使用显式交叉类型扩展，无any、无关闭类型检查；组件回归能在未来升级时暴露失效，不修改node_modules。

## 最终门禁

| 层级 | 实际结果 |
| --- | --- |
| 完整前端 | 644/644，0失败、0跳过、0取消；无Vue/Ant警告或结束后异步异常 |
| Web/App | 各完整vue-tsc及生产构建exit 0 |
| SFC | 54/54解析及组件接线通过 |
| 改密生产SPA | 1440明色正常同步、360明色/390暗色同步失败→键盘重试失败→再次重试成功，3/3通过；每例恰好一次密码POST、一次成功退出，令牌匹配 |
| 旧认证竞态 | 5段真实SPA流程再次通过，当前401/错误密码正常；注入3个预期401单列 |
| 原关键操作 | 10组流程、9笔内存写，0未知错误/警告 |
| 视觉矩阵 | 56组页面，0未知错误/警告/横向溢出/外网；改密警告另验手机明暗与44px |

手机两例各注入2次明确CSRF读取503，4条网络错误独立记录，不算未知异常。改密之后不刷新整页，避免重新初始化拿令牌掩盖旧缓存；所有写入仅在浏览器内存，测试账号/密码均为虚构数据。

新增持久回归共16项：认证API9项、设置表单6项、通知过渡1项；628→644。覆盖真实密码失败保留原错误/令牌、HTTP/HTTPS跨源缓存、改密重签使旧401失效、同步失败及显式重读、主机/账号/离页归属、键盘防重、字段清空和其它错误通知保留。

本机忽略证据：`.validation/ant-console-014/password-csrf-red.log`、`password-browser-red.log`、`password-notification-red.log`、`password-notice-transition-red/green.log`、`password-all-layout-final.log`、`password-build-web-complete.log`、`password-build-app-layout-final.log`、`password-browser-layout-final.log`及独立`password-browser/final-layout-*/`截图/结果。56组页面及10组流程日志分别为`password-visual-layout-final.log`、`password-functional-complete.log`。

## 边界

未运行.NET全套、原生Electron/Android认证、D4实际网络/认证Cookie时序、实体出画或声音。本轮保持用户原账号、媒体、数据库、证书和设备状态；不把内存Cookie校验称为真实后端认证集成。T018关于无法撤销已受理服务器动作/网络层Cookie的限制继续有效，012暂停/现场待验事项不关闭。

收尾按完整命令与创建时间核对后仅停止本轮预览PID36176；OS实际查询18793–18797均无监听，未依赖helper的“已停止”文字。浏览器在finally关闭；原系统动画设置未改动。T018本地保存点为`14dbd02`，本阶段独立提交；两远端及D4均未改动。
