# 正式运行组的MediaMTX、直播和背景音频

日期：2026-10-05。T073/T074有限实测；没有改生产播放策略，不标完整优化完成。

## 范围与身份

复用当前ControlHost8649CD/Contracts63304E/LibreOfficeHost404B7D完整包，新DataRoot与内存随机测试认证。
D4根`D:/SCP-cv/.validation/008-live-audio-group-1791198750000`，成功运行`runtime/api-run-96929aef298844be9d343ebea7035fd9`。
实际Host11448/start UTC2026-10-05T11:33:31.1263779Z/session1/HTTP52903。
状态文件实际登记Player1=18936/start11:33:37.8226935Z、Player2=5536/start11:33:37.8436059Z、Audio10364/start11:33:37.8551148Z、MediaMTX19392/start11:33:38.0257159Z。
MediaMTX由正式Supervisor启动/登记/停止，不由QA替代；二进制SHA `72A2B602BB28843E81ACFF2C97AD9524A448A0556DDAC7B4FFFD96060EBACC59`，原配置副本SHA `42BAD4D89CA3A3BFE9FC5D92997E364F7445BFF4B843321A6519E545196489F6`。
仅自有子环境覆盖回环RTSP52904、SRT59679、API52905，并关闭本轮无关协议。不改用户配置、墙面、电源、系统音量或防火墙。
Player仅DISPLAY2/3，两精确HWND/DWM1920×1080、PerMonitorV2与九点遮挡检查，不访问控制DISPLAY1。

## 三种音频的完整HTTP链路

真实上传MP3/WAV/OGG，分别source8/9/10；通过正式认证API、持久队列、Broker和AudioWorker执行，不直接写数据库。
仅测试实例静音，每条操作等待后续实际源、pending空、状态与时钟，而非HTTP200。

| 格式 | 无控制时钟推进 | 暂停两次观测 | 定位1000后继续推进 |
| --- | --- | --- | --- |
| MP3 | 1885→3865ms | 3865→3865ms | 1000→1860ms |
| WAV | 1805→2905ms | 2905→2905ms | 1000→1853ms |
| OGG | 1916→3798ms | 3798→3798ms | 1000→3305ms |

恢复均有实际playing/时钟推进，STOP均确认stopped/位置0。上传文件摘要分别C193E5…、CEE52E…、35CD27…，退出后独占读及摘要不变另核。
窗口2图片整幅SHA保持，不产生第三/第四显示窗口。静音与时钟不是实体出声证据；自然列表循环沿此前独立记录，本次未把短测称列表/所有竞态或长稳验收。

## RTSP/SRT断流与显式恢复

QA拥有四个精确FFmpeg发布者，均读既有视频30秒起回环发布H264，退出码0；MediaMTX实际path ready后才打开。
source11为RTSP、source12为SRT，每种执行OPEN→实际playing→原图→正常q停发布者→实际error→新发布者→用户式显式OPEN→playing/原图→CLOSE/null/全黑。
RTSP发布者UTC11:34:04.9908804退出、11:34:10.7666155报告error；SRT11:34:20.559876退出、11:34:25.6559032报告error。恢复不重放旧不确定命令。

17张原样PNG已下载并逐张核事件SHA，0不匹配；主agent逐张查看全部图。首次playing图为黑，RTSP首次恢复图为浅灰，后1.5秒图才有实际视频内容；不能把playing视为首帧已出。
RTSP/SRT后帧与断流后冻结帧中机械臂及仿真姿态不同，证明该有限区间确有后续画面；恢复各取得真实内容帧，但恢复后的两张连续非空运动图仍待补，不能把黑/灰到内容的变化叫连续运动。
无新菜单、桌面边条、防火墙提示或串窗；素材自带字幕/应用界面不当项目UI残留。窗口2基线/音频/两直播保持四图同SHA `2C3A02A0B73AB85586BA7A83F1D59619595CEC8217B0126225A97F438858CF9F`；三关闭图均全黑SHA `C6C634671806E7B55658335A1168F8A48B3E78E555800DD1DF9E6ACCCB8B490D`。

## 失败、告警与退出边界

- 首组`api-run-163490a5f0af4efcaf2e1125bb1c3d71`在MediaMTX启动失败：QA增加all_others环境路径，与原配置同义兜底路径重复，实际报`all_others, all and '~^.*$' are aliases`。503、QA2/Host0及诊断日志保留；只移除夹具重复配置。
- 后两组`api-run-34cede0ebd2444af8638276c1d44df6a`和`api-run-507f86cb7e584b6db226eb571d86ffd4`按文件视频规则等直播Time>3000误判。后者逐秒证实actual source8/playing/pending空而Time0；这是无文件时钟直播，改为状态与实际图像分层检查，不给生产加假时钟或延预算。两组仍QA2/Host0，旧失败保持。
- 成功组stderr仍有两条`buffer deadlock prevented`、SetThumbNailClip0x800706f4，以及故意断流时大量SRT peer rejection（486条相关拒绝行）/no sockets to check。没有吞日志、禁硬解或声称默认GPU长期健康；当前只有有限恢复与退出证据，后续需同条件长稳及日志量检查。
- shutdown200，Host于UTC11:34:38.1663331正常退出0；任务Ready/instances0/result0，项目进程及MediaMTX均不存在。后续只读核52904/52905 TCP与59679 UDP监听均消失，三音频文件独占读/摘要保持；仅删除该已完成自有任务登记，D4源码仍干净。MediaMTX由现有Supervisor停机协调处理；本次未取得MediaMTX协作退出码，不将它扩称所有组件均正常协作退出。

成功证据`.validation/008-live-audio-evidence-1791200250000/`包含事件、Host日志、四发布者日志及17PNG，不导出数据库/认证表；ZIP SHA `18804ECDDB65141DA5377BAEDCC58EA5BB71AED7B530D681F6BD030741AE7B5E`远端/下载一致。所有测试根、失败日志和原件保留。
本轮前端全套271/271、类型/Web构建通过，入口大于500kB警告保留；不是实际D4控制台动态SSE验收。
UDP重开、两窗交叉和PPT/直播/音频60分钟、恢复连续运动、完整桌面/手机/封装端、正式secondary源码同步仍欠，T073/T074继续未完成。
