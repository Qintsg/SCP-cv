# 验证记录

日期：2026-10-29。

## 原生窗口

`WindowForegroundTests`通过。使用本机真实PlayerWindow、PlayerRuntimeHost及项目PNG，执行20轮隐藏／显示。逐次读取WS_EX_TOPMOST、原生窗口顺序与矩形，并与另一个真实管理窗口比较前后位置；相同HWND、可见视觉树及图片播放状态保持。隐藏期间重新设置相同位置、换入另一来源后仍保持隐藏。测试窗口与线程正常关闭。

证据：`.validation/window-029/window-native-final.trx`，1通过、0失败、0跳过。

## HTTP、队列与观测

`WindowForegroundRealTests`两项通过，使用Hardware装配、真实Kestrel、独立SQLite、Named Pipe和两块WPF窗口。两块验证窗口由同一个测试进程的UI线程拥有，使用各自的RuntimeWorkerSession及生产PlayerRuntimeHost；未以模拟执行器替代窗口命令。

- 真实图片上传、两窗打开，20轮隐藏／显示均从HTTP提交并等待Worker实际观测。
- 202响应包含各自命令回执；未知状态409，缺少或错误布尔字段400，未登录401，缺少CSRF400。
- 旧真实窗口快照重放后最新隐藏状态保持；旧实例及旧所有者均被围栏拒绝。
- 两窗混合状态可恢复置顶；隐藏使用独立窗口快照，不改变媒体来源代次。
- 独立SQLite队列验证媒体OPEN保留窗口维护命令，较新维护请求只合并同类Pending命令。

证据：`.validation/window-029/window-web-native.trx`与`window-http-final.trx`，各2通过、0失败、0跳过。主机、管道、图片与测试窗口按用例退出。

## Web

真实Kestrel及WPF用例启动只读Edge验收，使用实际账号、Cookie和Vite代理。手机390×844、Pad800×1280、桌面1365×900分别读取维护菜单的生产Vue状态，两种实际窗口状态共6组。菜单正确提供隐藏窗口或显示窗口，均可操作，无横向溢出及页面异常。没有模拟鼠标、键盘或触摸。

证据：`.validation/window-029/web/隐藏窗口.json`、`显示窗口.json`及顶栏截图。原生API执行与网页只读渲染分别验证，未执行网页鼠标点击。

## 构建与规范

- 运行时Release整体构建通过，0警告、0错误；`build-029.log`。
- 前端类型及Web构建通过；`window-029/build-web.log`。没有执行客户端打包。
- Spec Kit校验通过；FR-001至FR-006分别由T002至T008及上述证据覆盖，需求、设计、任务复核无未完成项或宪章冲突。
- Redocly校验通过，0错误；4项既有告警来自一个WebSocket101接口及三个退役410接口无2XX响应，新增窗口维护接口无告警。

## 现场交付

后续已获部署及桌面入口优化授权，D4网页、播放运行时与数据库迁移已更新。现场隐藏／显示及原播放恢复通过，桌面快捷方式和重复启动验证通过，详见[部署记录](deployment.md)。本轮没有生成客户端安装包。
