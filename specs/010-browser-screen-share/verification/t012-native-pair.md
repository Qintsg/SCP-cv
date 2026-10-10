# T012：本机双接收资源十轮交错（2026-10-11）

从69f958a干净工作区继续；Spec Kit前置及只读requirements门禁16/16通过，extensions不存在。本轮只完成T012，不连接D4，不打开用户媒体或下发墙面/电源。

## 实际执行与层级

- ignored QA的PairProbe最终Release构建退出0、0警告0错误，保留项目分析器。两个正式PlayerRuntimeHost在同一自有WPF应用中持续复用；两个独立Chromium进程真实getDisplayMedia采集唯一自有DOM运动标签页。正式Panel/Publisher/RTC、独占InPrivate WebView2及正式receiver HTML/JS/CSP实际执行，仅账号/信令/命令队列为内存夹具，不持久化cap/SDP。
- `uv run --offline --no-project --with playwright python .validation/qa-screen-share-010/t012_pair_browser.py`实际退出0。完整证据在`.validation/qa-screen-share-010/t012-pair-runs/cfbd8b81b5c6499e9df7fa32a2c9d73b/evidence.json`，终端输出`t012-pair-browser-http11.log`；303.09秒、十轮全部完成、30次独立采集、220份原生动作结果。
- 两目标20次初始OPEN从原生授权返回到Host激活后帧1328–2787ms。稍晚截图时间、连接成功或信令状态不当首帧；重新共享仍须正式Host的后帧门禁。
- 前端相关三文件28/28、0失败/取消/跳过，实际退出0，`t012-related-frontend.log`。这是子集，不把前轮1353.NET/348前端或build:web算本轮完整运行；生产代码和API未改变，不重跑Redocly，最终T018仍须功能完整后执行。

## 十轮分别核验

- 两路正确目标/中心像素，实际帧继续增加至少30，底部运动区域摘要变化；不只比较帧号或全图摘要。交替先停止窗口1/2，另一窗保持原分享身份且继续运动。
- 重新共享后旧条件CLOSE返回`share_no_longer_current`；通过真实原生WebMessage通道投递旧身份closed消息后，新分享继续运动。旧Core实际调用抛Disposed/InvalidOperation/COM类错误，不能再使用，不以stopped布尔代替对象退役。
- 普通图片替换后0原生WebView、原分享/OPEN代次Dispose回执到达；正式发布者观察结束并释放轨道/peer。另一窗停止及迟到旧CLOSE不改变图片的source_id/source_generation和实际像素摘要。
- 每轮末两窗均0WebView/null分享；信令夹具只能收到真实原生Dispose回执后标closed，不从队列、轨道ended或普通换源推断原生死亡。

## 最终释放及截图

- 两面板各15条视频轨道全部ended、各15个peer的connection/signaling全部closed；30分享全部closed、两个Host合计30份原分享释放回执。
- 原生应用正常退出0、stderr为空。30个浏览器在存活时持有的原Windows对象全部`WaitForSingleObject=WAIT_OBJECT_0`，保留十秒等待预算；不将PID不存在、访问失败或后来同PID对象当证据。正式页面pageerror/console error为0。
- 主代理查看第一轮及第十轮两目标各两张原生运动图，共8张；另查看浅深主题接收态/关闭态4张正式面板图。目标标识、运动块变化、仅视频提示和关闭后可重新选择均与实际状态相符。截图只包含自有夹具，不截用户桌面。

## 保留的失败与修正

- 初始QA构建变量重名CS0136保留，改夹具变量而非关闭分析器。Pillow离线缓存缺失后使用.NET原生像素读取，不增加依赖或改变仓库源。
- HTTP/1.0阶段出现窗口2未接收或采样中断；分目标记录定位QA运输的ConnectionAborted/ResponseEnded。不启动播放器的独立800请求实验同样退出1，只改夹具`protocol_version`为HTTP/1.1后800请求退出0，随后真实十轮通过。没有添加请求重试、放宽媒体/退出预算或修改生产HTTP，不宣称已证明/修复Player缺陷。
- 第一轮失败漏取已退出原生进程的stdout/stderr；补QA无条件收尾取证，旧缺证据不倒补。四个失败目录`99d972c523c043579dea62fe986a45d6`、`7161293976bd480b860a827ac39a5b34`、`6c70ee7afd364c7fbe3c82f3d1149e87`、`ef033d7b288943398c829d2215e0e6a8`及原构建/运输日志保留。

## 未被替代的门禁

同进程两个Host不等于两个正式Worker，不覆盖正式ControlHost/Named Pipe身份或D4跨机媒体路由。303秒交错总时长不等于两个分享连续五分钟，SC-003仍留T016。正常选择器许可/取消、内容音频、可信HTTPS、断线实机及最终完整回归继续由T013–T018完成；不据此交付半成品010或改写D4稳定009的状态。
