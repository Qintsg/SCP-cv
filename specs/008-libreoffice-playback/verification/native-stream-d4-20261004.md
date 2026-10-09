# D4直播断流状态与恢复（2026-10-04）

## 结果与范围

复现SRT断流后原生Playing和全部计数停滞，正式Player增加仅直播输入的5秒无推进检查。
SRT实际失败→修复后错误/显式恢复/关闭通过；RTSP后续出画/错误/显式恢复/关闭有限通过。
UDP起播和断流识别可取证，但同资源重开及CLOSE后新建资源都未恢复，继续失败，不勾完整T073。
没有改媒体登记/编辑HTTP、前端、Office或设备控制，不是完整组、跨网络、双窗同时或长稳验收。

## 环境与自有夹具

D4 admin console1 active，原项目服务未运行。现有MediaMTX v1.17.1不升级；
项目qa-006/tools未找到FFmpeg，本机Scoop已安装9.0.2 Gyan静态构建（manifest保留来源），
仅复制到D4新`.validation/008-stream-tools-027633f90dc64bb9b693d80d4b72bd36`，
本机/远端exe SHA589E50B7…A28D0一致、-version实际执行成功；不改PATH、注册表或默认配置。
Archive SHA7B78F007…FA0908，文件/包保留，不将本机已有工具冒称新正式运行依赖。

自有MediaMTX配置仅127.0.0.1动态RTSP/SRT/API端口，队列512；其它协议关闭。
FFmpeg把机械臂副本从30秒附近循环推流，RTSP输入、SRT读取按
[官方FFmpeg示例](https://mediamtx.org/docs/publish/ffmpeg)与[SRT读取规则](https://mediamtx.org/docs/read/srt)，
UDP为自定义MPEG-TS协议分支。普通小窗绑定DISPLAY2/3，QA仅本实例静音，不测试音频。
所有服务器/发布者用本模块缓存精确句柄，模拟断流和夹具收尾会定向Kill；不称其协作退出。
Player本身必须正常Dispose/exit，原图、JSONL、stderr及副本保留。

## SRT真实红与原生统计

旧Player：PID13788、run-a30db069…，12秒断流观测仍playing，position890不动，task/parent2。
仅QA统计诊断再次失败：PID11996，task2，实际目录和完整日志保留在远端统计证据ZIP。
健康阶段ReadBytes117124→200784、Demux109110→184891、Decoded36→92、Displayed12→33；
发布者结束后直到约12秒，全部计数及time834不再变化，native_state仍Playing，SRT循环报Peer rejected。
这证明本样例没有输入/解码/呈现推进而状态假绿；不是所有SRT故障类型或库内部根因的完整证明。

首QA构建缺HttpClient using后修正；新增纯策略测试最初类型缺失/CA1822、接线CA1068编译失败保留，
不把编译失败称业务红。业务红是上述两D4实例；最终10项纯计数/暂停/缓冲/非直播/新尝试隔离等通过。

## 正式修复

`VlcLiveInputProgress`只接收累计ReadBytes、DemuxReadBytes、DecodedVideo、DisplayedPictures与Environment.TickCount64。
声明rtsp/srt/custom_stream的VLC尝试在native Playing且非用户pause/stop时，
连续5000ms四计数均不改变则锁存错误；任一计数变化重置窗口，包括整数溢出。
暂停、停止、缓冲退出检查，恢复重新给窗口。视频/音频文件尝试不启用此策略。
只在owner/UI门禁下读取自有Media统计，回调不UNO/VLC重入、不IO，不修改原生播放或自动重放。
已锁存错误即使旧输入之后推进也不擦除；显式重开按既有流程创建新尝试，直播标志随重播保留。
这表示“未确认输入推进”，不保证GPU首帧/每帧真实可见；低频/间歇数据流仍须兼容性样例复核。

正式策略及现有尝试相关18项先通过，补2保护后Windows非Physical123/123；
另五项目重新编译/执行41/127/229/269/303，总计1092/1092、0跳过。
六TRX为`.validation/008-stream-source/windows-live-final.trx`及full-nonphysical目录，前端未改未重跑223。
Player DLL E4459739…0BA79E；修复QA包4A86064A…F3307A，新版与原失败包均保留。

## 修复后实机

| 场景 | 归属与结果 | 尚未证明 |
| --- | --- | --- |
| SRT修复 | PID17540、run-ec2de9d1…、DISPLAY3，正常0 | 跨机丢包/低帧率/长期稳定 |
| RTSP未遮挡复测 | PID5092、run-2a5963b8…、DISPLAY3，正常0 | 首次首帧或同步恢复画面 |
| RTSP恢复后续取证 | PID4180、run-42aef987…、DISPLAY3，正常0 | 完整网络/所有HTTP控制链 |
| UDP同资源恢复 | PID8220、task2，第二发布者仍输出数据而Reader15秒loading0 | 具体失败层级 |
| UDP先CLOSE再新建 | PID8136、task2，仍loading0 | 尚不能归罪仅复用MediaPlayer |

SRT从首loss样本18:37:15.933 playing/time816，到18:37:20.838 error/time816；
显式新OPEN恢复，主agent核五张原图：两前帧可见机械臂/仿真姿态变化、冻结帧、恢复帧、关闭纯黑。
RTSP首playing与首恢复图为灰白，后playing图才有内容；单靠state/Hits不能授予同步完成。
补后续恢复图：DisplayedPictures2、Decoded24，原图出现与素材一致的机械臂场景。
无固定延迟生产补丁；QA观察间隔只为取证，不能推出精确首帧或GPU完成时间。

VLC buffer deadlock prevented、SetThumbNailClip与SRT退出无socket告警继续保留，不因正常0叫全部告警已修。
UDP素材关键帧约10.714秒间隔，是可能的头/关键帧因素；未有因果对照，不将它当确定根因，
也不延15秒QA预算或以显式CLOSE的变化把失败缩成成功。

## 防火墙提示与截图误判

最初RTSP PID4868/task0，但原图右下被Windows安全中心防火墙提示遮挡；中心点检查没发现。
撤回该组视觉验收，原图不编辑。未自动Allow/关闭提示、添加规则或降低安全。
只在指定另一大屏区域测试；QA截图改为九点根HWND检查，仍不是逐像素遮挡证明，不改生产几何。
原来的遮挡图/旧保护保持为失败历史，修复版原图未遮挡，操作员可继续自行处理该安全提示。
SSH非交互会话的GetForegroundWindow返回0，不能据此说session1提示已经关闭。

## 原始证据与收尾

RTSP旧遮挡ZIP3F8CEF6B…1B6A1；SRT失败7644DE95…5E6A5；统计诊断3CF7F98A…23F2A；
SRT修复A288547B…244D0；RTSP右屏76DCC3E4…8B3B6；RTSP后续6D6B0EDB…64BA6；
UDP失败641D4D94…4000B；UDP新建仍失败E2D366E7…45845。
成功取证ZIP已本机/远端核SHA一致并原样解压；失败ZIP/日志仍在远端验证根，未删除。
八任务各在Ready/instance0后移除，可由保存源码/参数重登记；最终Processes0/Tasks0。
项目服务保持停止；数据库/媒体原件、控制桌面、系统音量、墙面/电源和防火墙权限不改。
D4源码仍b2f09c5清洁、未secondary同步，QA二进制上传不是源码更新。
Spec/diff/UTF-8/LF检查通过。UDP恢复、背景音频、真实API、两窗并行100控制/60分钟及PPT门禁继续未完成。
