# T033本机真人点击与网页声音验证（2026-10-18）

## 结论与边界

本机普通网页真实用户点击后的媒体播放、明确非默认音频端点、保温切换与关闭验证通过。不是脚本模拟手势，不使用Computer Use；用户回复“已点击”，实际输入事件、媒体时间和原生会话互相印证。

仅验证本机Windows 11/正式PlayerRuntimeHost与私有WebView2，不连接或修改D4。D4原10项现场/人工任务保持未完成，T033不勾；不能把本机零PCM声音会话当现场扬声器可听、屏幕共享人工许可/画质或D4双窗实际输入验收。

## 方法

沿用T046已验证的无包独立上下文、自有私有引擎、独立profile及环回零PCM WAV页面，直接引用当前正式Player工程。仅改忽略目录QA夹具，新增“本人点击一次后自动验证”模式；本轮没有生产源代码、HTTP合同或前端行为变更。

程序正常OPEN网页，原自动播放策略不放宽；蓝色播放按钮必须由用户本人点击。只有可信输入对应的play已完成、真实媒体时间超过0.2秒且自有WASAPI会话在明确端点活动后，才调用正式Player接口进行两轮图片/保温网页切换及CLOSE。不模拟按钮点击，不给用户页面注入CDP手势，不修改原生产零音暖机行为。

启动只确认新PID，不把broker退出0称作测试完成；8分钟无人输入则失败收尾。本轮原PID8000及其创建时刻/映像已在等待点击前复核存活，ready之后才向用户说明操作，未重复启动。

## 实际结果

2026-10-18本机11:56:32.835记录input事件、isTrusted=true；11:56:32.872对应input_play_resolved、trusted=true。此前自动播放仍为NotAllowedError，保留原失败，没有改成自动播放成功。

| 阶段 | 网页媒体时间（秒） | Core静音 | 自有WASAPI活动 |
| --- | ---: | --- | --- |
| 本人点击后 | 0.365640 | false | true |
| 第一次切图片 | 0.729755 | true | false |
| 第一次返回网页 | 0.890735 | false | true |
| 第二次切图片 | 0.999941 | true | false |
| 第二次返回网页 | 1.107407 | false | true |

媒体paused=false、readyState=4。实际声音进程46704的活动会话始终仅在目标端点`{0.0.0.00000000}.{29d162af-bc2a-466d-afcb-23a261fd0a98}`，系统Console/Multimedia默认仍是886端点；不以同名设备、配置值或用户激活布尔值替代实际端点证据。WAV全零，Peak=0符合夹具设计，不作为可听证明。

两轮切换后正式CLOSE完成，最终source_id=null、playback_state=idle、视图0、会话观察为空；completed/trusted/progressConfirmed/boundActiveObserved/genuineGesturePassed均true，failures为空。6个已捕获原浏览器句柄全部实际退出0；原8000已不存活，未另行保留它的OS退出码，不把PID消失写成根进程退出0。

从无包宿主读取的三个默认角色、两个活动端点总音量/静音前后逐值相同。独立后续Codex上下文读回仍为886/886/29d，未再污染已恢复的应用偏好。OS核本轮CLI/精确私有映像无残留，35268无监听；没有强杀无关应用。

## 验证及原始证据

本轮夹具与正式Player引用编译实际0警告0错误。上一提交T046的1615项非Physical与完整Framework编译是已完成基线，本轮生产源码未改，不重复声称已重跑全量前后端或Redocly；提交前另核Spec Kit全目录和diff。

忽略根`.validation/web-gesture-012-20261018/`：`human-launch-*.json`记录PID8000与原启动ticks639278925563179655；本轮唯一运行目录`runs/20261018-035556-58e441cfcc494e0ab51b39717ec214cd/`含ready/current/result及独立profile，环回端口35268。结果mode=`human-once-then-api-matrix`，与上轮`binding-api-matrix-only`或初轮失败手势记录分别保留。

当前012仍46项、36完成、10未完成；本机先行证据使T033的下一步更明确，但没有以它替换D4的要求。
