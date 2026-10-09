# 背景音频定位、真实HTTP/SSE与前端操作域（2026-10-05）

## 真实断链与修复

T072/T073检查发现既有OpenAPI、前端API和AudioWorker均有seek/position_ms，但ControlHost忽略目标、
BackgroundAudioService拒绝seek。通过正式认证的公开HTTP红例返回400，不能用原生执行器单测宣称接口已可用。
修复HTTP的非负int64必填验证与公开SeekAsync持久意图；源/代次保持，不重开，目标不写成已观察位置。
无源或非法目标明确400，不把缺失/字符串/小数/null/负数/越界int64默默转0。

HTTP相关11/11（既有3+新增8）；公开服务→实际SQLite→公开Claim租约另1/1，
目标3000000000ms不截成int32，排队SEEK合并保持源代次；此大目标只验证编码，不当长音频实播。
整套非Physical1123/1123、0跳过：41/310/127/277/241/127，六独立TRX保留于`.validation/008-audio-http/full/`。
Release solution及QA均0警告/0错误；QA首NETSDK1083 RID失败按既有Windows QA配置修正，未放宽分析器或审计。
OpenAPI3增加seek条件必填/int64与受理说明；Redocly CLI通过，三条历史410退役入口无2xx告警保留。

## 前端

公开页面原无进度操作，键盘定位DOM红→复用既有节流器绿，不重开曲目。
只有明确源、有限正时长及playing/paused时可定位；待机/加载/停止/错误/未知时长/空源不发SEEK。
定位未发缓冲按源/列表项/主机隔离，换曲丢旧值、新输入可用，不把普通时钟变化当换域。
另公开DOM复现Alpha音量在途缓冲自动发到Beta；音量仅按主机域隔离、无快照时禁用后绿。
音量是全局设置，不按每首曲目换域，正常列表推进不据此丢用户音量意图。
首键盘QA未focus/未让渲染完成，以及误把snapshot第二参数的更新时间当位置，各失败保留，不当生产bug。
keydown与keyup可能各提交同一定位值，沿既有节流/队列合并语义，不宣称每次键盘恰好一个HTTP；未做全局去重改造。
新增9项与既有13共22/22，完整前端263/263、0跳过及类型/Web构建通过。
主包1106.89kB/gzip315.50kB告警保留，不调高阈值。

手机端保留实际时间/总时长，进度轨道44px操作高度；首只改高度导致视觉偏上，
依Naive真实rail/handles结构调整padding，浏览器重新核对中央对齐，不以构建绿代替布局。
自有20893模拟HTTP页面的390键盘SEEK、在途换曲/释放旧响应、1440停止禁用通过。
新图/进度2s保持，写请求4不再增加；停止aria-disabled=true，两个时间可见，
documentWidth380/1430小于390/1440，Errors0/Warnings0。
原图`.validation/008-audio-seek-browser/mobile-final.png`与`desktop-stopped.png`已只读核对。
模拟HTTP与D4真实链路分别取证，未把浏览器页面称为已连接实机。
只关闭自有tab并恢复viewport，精确PID71532/start UTC17:32:44.407819停止，20893无监听；其它页面不动。
站点操作无新增可复用路由/认证规则，本轮未改站点笔记。

## D4 HTTP→实际Worker→SSE

只读确认admin console1在线、项目服务和旧自有音频QA停止。
自有Kestrel回环随机端口、正式Auth/BackgroundAudioEndpoints/SseEventEndpoints、真实Broker/命令/SQLite及AudioWorker。
独立DataRoot/随机自有账号；不保存或打印密码，不装配墙面/系统音量/设备电源入口。
不是完整ControlHost启动程序、Supervisor或全组；夹具通过独立库登记，不冒称上传/登记API完成。
15秒240078字节PCM副本静音，SHA `4960D23F140D050130314604ACB06EC92862BB3090532C50C3129A836999064E`保持。

首同条件根`D:\SCP-cv\.validation\008-d4-audio-http-1791134059952`：
parent9704/start UTC17:18:43.2352421，worker11272/start17:18:51.7068448，session1，回环53482。
真实POST PAUSE后1951ms；POST SEEK12500的受理仍1951/Pending SEEK，
后续GET在paused/pending空为12500，真实SSE亦12500；PLAY实际推进12540→13555，
再次PAUSE、SEEK1000及STOP完成，23 SSE观察、8命令均Completed，SEEK代次仍1。
Worker正常0，最终17:19:15.1679982通过，副本独占读/SHA及协作停机正常。
证据ZIP `C07A796590B8C6EEAD91AD42CE0326DA17E872A455DA5A7CF729397C3CF377DF`本远核SHA后保留`.validation/008-audio-http/d4/`。
首本机只读核库猜错文件名，用默认sqlite3意外创建了0字节db.sqlite3；留为QA误操作证据，不碰远端/原control.db。
随后按实际control.db及`-readonly`读取8条命令；不会把那个空文件当运行组库缺失或伪造业务记录。

正式补文件头后Infrastructure DLL摘要改变，虽然行为未改，也不把首包称最终同版。
最终Infrastructure DLL `191B9A985EE4E3DDCE2D40C9DD1E366C5A0FD113178B019452B20BAC7FFC9343`，
ControlHost `309073BCBB6EC2649EDF4E6801553C02DB8491037DC40B83DD56B2646699F00A`，
AudioWorker `81B81CC635833F2C693690EAED5C3481CA210B8757681B7D2872A135057E4971`，
QA Probe `44972547176CEEAF85B074D98B728310A3BFF42EAB744AE59BDE25D76EAF702A`。
最终DLL复测新根`D:\SCP-cv\.validation\008-d4-audio-http-final-1791135783186`：
parent11208/start UTC17:44:29.1684268，worker9196/start17:44:35.7340736，session1/回环54010；
受理SEEK仍1901/Pending SEEK，随后实际12500，回1000与停止、22 SSE观察及8 Completed命令通过。
Worker/task/parent均正常0，结束17:44:57.7672717Z，副本独占读/SHA保持。
最终证据ZIP `2D333D40D3F0EBDB96F48E902AD69DA29F969362070F4B3DB45D63E1218251B3`
本远核SHA后保留`.validation/008-audio-http/d4-final/`，首包证据不替换。

## 保留范围

启动仍正式30秒、状态/时钟观察15秒、协作退出10秒，不用超时结束当正常退出。
首及最终任务分别Ready/instances0后精确移除，最终OwnProcesses0/ProjectServices0、远端git clean；
仅删除自有任务登记，包/库/副本/失败证据保留。
不改变D4显示/控制桌面/墙面/电源/系统音量/防火墙/执行策略/业务库，未secondary同步源码。
静音不证明实体声音；音频worker独立在线字段、失联按钮、同源重开代次/范围外或不可定位源、
登记API/多源故障矩阵仍待核。PPT动画末页、UDP恢复、正式同版包两窗10轮/100控制/60分钟仍欠，
不勾T072/T073/T074完成。“说人话”仅用于文档和汇报，不掩盖失败/未测。

Spec Kit、git diff --check和本轮改动代码/新增证据的严格UTF-8/LF检查通过；
正式服务437行、页面309行、样式435行，未继续向超过500行文件堆积。
