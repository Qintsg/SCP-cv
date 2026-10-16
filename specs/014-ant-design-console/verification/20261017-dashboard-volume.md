# T024 首页系统音量输入归属

日期：2026-10-17。T024完成；T025首页电源/静音与设备结果尚未完成。仅本机，不推送、不连接D4、不改变实体电源、墙面或音频输出。

## 红回归与根因

真实Dashboard/Ant滑杆经键盘发第一笔49并缓冲48，挂起首笔HTTP；切主机实际代次/投影、换账号/同ID重登、会话探测或改密后再交付旧200/503，仍会发送第二笔旧音量。旧API虽拒绝旧响应，Slider的finally仍读取当前Runtime续发缓冲；不能用API围栏或Slider自身已有scope选项来代替首页实际接线。

20项持久测试初轮7绿13红。已有卸载/离页、正常连续调节、原失败说明与当前切换门禁为正对照，不宣称这些旧分支全部坏了。最初夹具缺history、缺两条输出路由已修正；对应异常/警告不作为产品红证据，清理后仍7绿13红。

## 修复

- Dashboard通过useRequestContext驱动滑杆scope，真实主机、投影或认证寿命变化即清缓冲；未认证/切换中不接新输入。原组件的串行、最终值与卸载释放不变，不新增队列、不撤销已受理HTTP。
- Auth私有修订改为ref，保持纯读取器和Store公开合同；改密未替换用户对象也可同步失效普通控制。共用请求归属默认复核完整认证寿命。
- 账户表单明确由既有Auth动作围栏/完成凭据消费自身改密、退出，只在该调用保留账号对象/主机/页面边界。首版严格围栏曾使9项旧账户测试失败，因自身轮换被误认外来变更；没有改弱断言，修正后59项针对性、全量及真实改密/退出通过。

## 完整门禁

| 检查 | 当前结果 |
| --- | --- |
| 新增真实Dashboard/Ant/API回归 | 20/20；主机/会话/私有修订、在途恢复、旧finally及正常最终值 |
| 完整前端 | 795/795，0失败/跳过/取消，无Vue/Ant警告或结束后异步异常 |
| Web/App完整类型与构建 | exit0；既有Vite扩展名提示仍保留 |
| SFC、Spec Kit、diff | 54个无失败，规范与差异通过 |
| 生产网页账户与连接 | 8登录、5认证、3改密、7退出、6切换通过；预期注入错误单列 |
| 原生产网页流程/页面 | 10组、9内存写；56页面组合，0未知错误/警告/横向溢出/外网 |

## 实际.NET ControlHost + Electron

不是上轮Node认证夹具：SDK10.0.401从当前源码无恢复构建ControlHost，0警告/错误；以独立数据库/媒体根及Simulation启动实际Kestrel，未启动Supervisor、Player、AudioWorker、LibreOffice或流探测。正式Electron44.2.0 main/preload和本次app://生产构建使用独立profile/session，只允许本地资产及127.0.0.1:18884。

正常TLS直接使用用户已有SSPU108证书与已有根信任，curl健康接口200、verify=0；没有关证书验证或增删CA。测试账号仅属于这份独立数据库，随机密码只留辅助进程内存/子环境，未输出、未写源码/凭据文件。后端沿用默认Data Protection仓库；前后均1文件、最后修改时间2026-09-22 07:59:39，无密钥修改命令。

1. UI明确保存主机并登录，真实Cookie/CORS/CSRF与受保护读取、SSE成功。登录时sessionid为Secure/HttpOnly/SameSite=None，防伪Cookie为Secure/SameSite=None；只记录属性，日志不记录Cookie值。
2. 键盘两次调节，原生公开ARIA显示98；QA只挂起第一笔音量请求，未模拟后端响应。向既有平台通道发受控active通知，走正式恢复/认证链；原生滑杆回到远端100。释放原请求后实际200，旧缓冲不补发；新键盘输入99才出现第二笔200。
3. 本轮仅两笔真实音量PATCH，进入SimulationSystemAudioController，不触碰声卡。随后实际修改测试账号密码、CSRF同步、退出以及新密码再登录成功：2次登录、1次改密、1次退出POST全部200，无设备或布局写请求。
4. 调用既有clearSession IPC并重载，精确回到登录页；会话Cookie消失，匿名CSRF读取会新建一个csrftoken，不能误断言所有Cookie都必须为0。原始全零断言误判已纠正，不改产品匿名防伪行为。
5. 最终0renderer错误/警告、0不允许的出网；测试窗口按自身关闭路径结束，报告有before-quit。后端仅精确终止本轮进程，不据此宣称其正常进程组停机已验。最终OS核实本轮进程与18884/18886/18887监听均已退出。

辅助工具首轮未保留CDP选择、Electron未消费stdin消息，未能填密码；该轮已确认所有旧进程退出，修正为明确CDP端口与独立控制文件后新开一轮。不是因观察超时重启仍活着的产品，也未用该轮作产品失败。受控active通知证明正式适配/恢复接线，不是Windows真实焦点或最小化恢复验收。

## 证据与剩余边界

忽略目录`.validation/ant-console-014/`中dashboard-volume-red-clean/green、dashboard-targeted-final、dashboard-all-final、dashboard-build-web/app-final及dashboard-各browser-final日志。真实集成成功目录为`native-aspnet-20261017/run-obaOir/`，含native-report、assertions、后端日志与dashboard-resume/password-updated/login-new-password/login-after-clear截图；失败辅助轮run-EGco7r保留，不覆盖。

Kestrel的PEM配置参考[微软官方文档](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/fundamentals/servers/kestrel/endpoints.md)，实际结果以上述本机观察为准。

没有证明D4、实体声音/显示、原生文件对话框、系统焦点、Android、安装签名或完整运行组；012现场任务及暂停摄像机仍保留。T025不能由本次795绿提前勾选，长期目标未完成。
