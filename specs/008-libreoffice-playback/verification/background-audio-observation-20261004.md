# 背景音频观测与迟到响应（2026-10-04）

## 修复与证据

T072/T073当前切片。真实Vue/Naive/Pinia/API页面复现：音量REST挂起后收到新曲目2，
旧曲目1响应晚到使标题与位置倒退；并发刷新中旧请求先结束又把新读取loading清false。
两DOM业务红保留，分别在audio-observation-red.log、audio-loading-red.log。

backgroundAudio store的REST和SSE统一走applyRemoteSnapshot，按服务端updated_at比较。
有明确当前时间时，较旧或不可解析新时间不覆盖；第一次旧格式仍初始化，
相同时间允许列表/元信息兼容更新。只隔离观测，不撤销HTTP、不额外发送/重放控制。
refresh的finally由store实例WeakMap唯一symbol拥有，不用会在$reset后重名的数字代次；
API现有主机generation仍负责拒绝Alpha旧响应，旧finally不能解除Beta新读取。

新增13DOM覆盖音量/刷新/加入/源与列表项播放/删除/清空/控制/静音/循环、
旧SSE、无效时间、新观测与同时间列表更新、并发读取、公开切主机并清store。
相关13/13、全前端245/245零跳过、类型/Web通过；1105.88kB/gzip315.24kB分块告警保留。
没有新增依赖或改包源，pnpm执行；日志audio-observation-related/full/build保留在008-playback-output。

后端另复现实际加入曲目不推进快照时间，先误把DTO字符串用>比较导致编译失败，
按InvariantCulture解析后得到有效业务红TRX。AddToPlaylist仅实际新行时同事务推进NextTimestamp，
重复加入不改时间、不产生播放指令。相关持久11/11、完整非Physical1103/1103零跳过，
计数41/303/127/269/240/123；不把编译失败当业务红或把列表登记当播放完成。
证据`.validation/008-audio-observation/audio-list-observation-{red,green}.trx`及full-nonphysical。

## 实际浏览器

自有IAB20889、真实音频页面/共享主题，所有HTTP模拟，不连接D4、不产生声音。
挂起静音PATCH→推送曲目2/时间2→释放旧响应，当前标题/列表标记仍曲目2、时间00:02，
原写请求保持1，按钮等待解除；浏览器warn/error0。
390×844实测scrollWidth380，1440×900为1430，无横向溢出。
主agent核原PNG：手机播放器/源/列表纵向堆叠、桌面双列，当前曲目与控件无重叠。
原图`.validation/008-audio-browser/stale-response-{mobile,desktop}.png`。
该临时页不在127.0.0.1生产控制台笔记的适用标题范围，无通用新规律，不改站点笔记。
视口恢复、自有页关闭；Vite PID2928/start UTC14:34:39.997391精确退出，20889监听0。
QA配置的esbuild解析告警保留，不冒称生产构建失败或安装依赖掩盖它。

## D4公开服务/真实管道复核

新根`D:\SCP-cv\.validation\008-d4-audio-observation-1791125113166`，
补丁SHA `B32797374FD47E4DCF61CD11BDC746A45473642C709DD0E359D79D6CBB433540`，
Infrastructure DLL `75116B57FE04D98F5FD7B1807DCD7147D5D3216F5A6210F0D5342E8C2F20C099`核验后执行。
parent4196/start UTC14:46:22.684213、worker9704/start14:46:28.2937001，ready14754ms。
实际SQLite3.53.3/DLL B7385D72…1111FB2E，与生产/上轮对齐基线一致。
服务快照before22:46:26.8427679+08→added22:46:28.0755216+08，duplicate保持added。
不循环1→2停止、循环1→2→1、显式STOP与正常协作退出0再次通过，副本SHA/锁释放。
没有把这次管道/服务证据当浏览器真实后端HTTP、现场声音或整个组验收。

证据ZIP `835F353195F618D69F6AD173F2DEFE66B9E9A4F29BA9D98556E388B2584FFC63`
本远核SHA并原样解压`.validation/008-audio-observation/d4-audio-observation-1791125113166/`。
任务Ready/instances0后精确移除，OwnProcesses0/ProjectServices0/OwnTasks0。
业务库/媒体、控制桌面/显示绑定、系统音量、墙面/电源、防火墙/执行策略未动；
D4源码未secondary同步，独立QA上传不是正式部署。旧失败及前轮证据不删除。

## 边界与下一步

相同时间的历史快照无法仅凭时间区分先后，保留兼容而不宣称全序；
不新增客户端序号冒充服务端观察代次。媒体库元信息修订不是此播放状态时间。
当前背景音频离线控制、旧页面错误通知、滑块缓冲和实际周期进度还需检查。
代码检查发现AudioWorker目前只用execute的RunAsync重载，没有周期sampleState，
进度仅随控制结果上报；尚未在公开链路复现冻结，下一轮取证，不把此猜测当已修。
PPT危险特性/动画末页、UDP恢复、API/完整组和混合100控制/60分钟继续未完成。
Spec/diff、UTF-8/LF通过；store90行、服务424行，不勾完整T072/T073/T074。
