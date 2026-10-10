# T013原生专用音频桥（部分，2026-10-11）

## 实现与验收边界

- 新增`SCREEN_SHARE_AUDIO`，只在媒体锁内对同share/window/原OPEN代次控制实际接收页；严格参数、最多三秒及租约截止，不推进代次，不借用VLC。普通共享音量入口仍拒绝。
- 激活和控制均核严格原生回执；无live音轨强制静音。音频控制不增加帧或playing，未切入/已结束拒绝；动作开始后旧回读无效，错误不假报旧属性仍已确认。
- 内部IPC新增可空音量/静音观测。认证HTTP入口、公共投影、网页专用控件及真实内容音频尚未完成；T013保持未勾选，当前默认激活仍静音。

## 自动化证据

- 正式Host新命令最初四项实际红，原脚本八项中两项红，分别复现未接线、音轨已结束仍假报支持及预备资源开放音频控制。修复后相关Windows/Host寿命回归103/103，接收脚本8/8；新Windows32项不包含真实出声。
- 前端接收脚本/发布者/动态输出/导航四文件共36/36、0跳过；Windows非Physical全项目197/197、ControlHost非Physical全项目378/378，均0跳过并取得退出0。TRX保留`.validation/qa-screen-share-010/t013-audio/`，此前红文件不覆盖。未重跑其他.NET项目、全前端、typecheck/build:web或Redocly；公共API未改，完整门禁仍留T018。
- 接口修改后遗漏释放测试替身的ApplyAudioAsync导致CS0535；同步该接缝而非添加默认假实现。三秒寿命方法触发CA1068，按CancellationToken最后参数修正，未关闭分析器。

## 真实本机接收证据

- `.validation/qa-screen-share-010/t013-audio-runs/b37fb1231848473aa665eb170cfe59a3/evidence.json`：37.66秒，退出0，PAIR_HOST_EXIT0，stderr空，页面/控制台错误0。两个独立Chromium真实getDisplayMedia采集自有DOM运动标签页，正式Panel/Publisher/RTC/receiver与两个正式Host；账号/信令/命令分发是内存夹具，不当正式ControlHost/队列/Worker或D4。
- 两套原生OPEN回读100/静音，真实采集和接收都无音轨。窗口1依次29/解除静音请求、80/静音、0/解除静音请求、100/解除静音请求，窗口2为35/解除静音请求；五次控制实际completed，原代次/分享保持。每次另从真实Core查询video.volume/muted/paused/live音轨数，核浮点回读匹配、muted=true、paused=false、live_audio=0；不是仅核返回状态。
- 每次控制核另一窗分享/代次/音量100/静音保持，真实帧至少再推进30，运动条像素摘要改变；保存七张自有原生原图。主代理查看两目标代表图，编号与动态画面正确。
- 显式停止后两个轨道/peer结束，两目标0WebView、分享closed及原生Dispose回执到达、音量/静音字段null。原生应用正常退出，存活时持有原浏览器对象56752/55096，结束后WaitForSingleObject均0；不以PID不存在代替死亡证明。
- 首次QA缺Playwright包，未启动原生；使用已有uv离线Playwright缓存后实测通过。只用自有标签页自动选择，不计普通系统选择器许可；未捕获用户桌面。Vite已主动停止，后续不沿用旧执行句柄。

## 后续

继续T013认证/CSRF/原Worker事务门禁、公共实际回读、前端专用控件及真实内容声音；T014正常可信HTTPS、T015真实许可/响应式与键盘、T016两正式D4 Worker连续五分钟/十轮/断线、T017/T018文档与全量门禁仍未完成。不要从本次无音轨属性验证推断有声音、D4出画或目标已完成。
