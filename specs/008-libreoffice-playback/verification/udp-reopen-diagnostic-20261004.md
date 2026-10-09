# D4 UDP重开分层诊断（2026-10-04）

2026-10-05补证：下文保留当日失败。后续已实证自有发布者继承旧UDP句柄会污染重开测试；隔离发布者并先non-autoplay初始化后，同一正式Host/Media的实际重开/连续画面/关闭有限通过。冷首次接入仍有独立无帧失败，不能将此更正叫全部UDP稳定，见[引擎与句柄对照](udp-engine-and-handle-inheritance-20261005.md)。

## 结论

独立接收器在首发、保持接收器的发布者重启、发布后才加入三个场景均收到可解码MPEG-TS。
实际Player重开仍在15秒观察预算内保持loading/time0，尚未修复。
强制TS解复用、先CLOSE新建MediaPlayer、关闭硬件解码的QA对照均未解决问题；不接入正式默认值。
这些结果缩小调查范围，不证明所有网络/媒体条件或某一VLC内部层级就是根因。

本轮只改忽略目录中的QA；正式Player、前端、数据库、设备控制和原件未改。
T073仍未完成，不能据独立FFmpeg成功授予项目播放器恢复通过。

## 独立收包与解码

自有loopback发布者复用前轮MP4副本，从30秒附近开始循环、复制视频码流、无音频，UDP输出MPEG-TS。
新`UdpWireProbe`不创建Player/WPF窗口；接收器把4秒内数据写入新TS文件并检查188字节同步，
然后独立调用自有FFmpeg解一帧PNG，8秒预算不变。三个原图均人工核对有素材内容。

| 接收场景 | 数据报 / 字节 | 同步异常 | FFmpeg退出 / PNG |
| --- | ---: | ---: | --- |
| 接收器先绑定再发布 | 363 / 408148 | 0 | 0 / 有真实内容 |
| 接收器保持绑定，发布者重启 | 364 / 409088 | 0 | 0 / 有真实内容 |
| 发布者重启后新接收器加入 | 311 / 342536 | 0 | 0 / 有真实内容 |

任务`ScpCvQa008UdpWire-3ca017e7c579470a871fca87b6881e73`，PID7572，
start UTC11:15:06.8175633，run-ea145e85319f413ca6a9d6bf10527fc8，退出0。
ZIP SHA256 `FAE2C4475F0478A4446713B54FEFC7243B528C8515BB678FD7251A46E012C17A`，
已原样拉取到本机`.validation/008-udp-wire/`并核摘要。
复制码流的寻址受关键帧影响，不称三个PNG都是精确30秒帧。

## 实际Player统计与有限对照

以下均在同一独立QA根、DISPLAY3指定小窗执行，正常Dispose后父进程退出2表示业务门禁失败。
不扩大15秒预算、不自动重试；日志、原始包和历史编译包装器保留。

| 场景 | PID / start UTC | 结果与证据ZIP SHA256 |
| --- | --- | --- |
| 默认解复用，CLOSE后新Player | 12812 / 11:19:28.9824993 | Opening、time0、四统计0；`B7234B7A31E4C18CF3CB9F9356434AEA6E1E089969454EEB6A09426DF57E30B4` |
| 强制TS，同Player重开 | 8636 / 11:26:04.7900591 | 仍Opening/time0；`ADF070F4589E7C1AE2DD56E385E17CB646BA14A377A7524F4228B8FBDB1C2D46` |
| 强制TS，CLOSE后新Player | 17168 / 11:30:10.8749217 | 仍loading/time0；`32A60F6C1E6758CDBEA23068F6EE74C1DE67603C17368D917AF4272023041039` |
| 上项加软件解码 | 18024 / 11:35:26.7561598 | 仍loading/time0；`31FF4F2B214BD18CCD0D61F119C729699E4FABD09DC7CD4212A6116585304AE3` |

默认分支有TS discontinuity及`mjpeg demux error: cannot peek`；强制TS后有H264
get_buffer/thread_get_buffer/decode_slice_header/no frame和缩略图告警。告警保留，不能单独当根因。
同Media重用时统计可能仍是旧输入累计值；Opening时四项0也可能尚未有有效统计，
不能把非零旧值称新输入推进，或把0称网络没有收到数据。

“新Player”仅表示新MediaPlayer，仍使用该QA进程内同一LibVLC；未验证全新LibVLC/独立进程恢复。
VLC固定版本[UDP入口源码](https://raw.githubusercontent.com/videolan/vlc/3.0.21/modules/access/udp.c)
只用于理解URI解析，不作为本轮失败的因果证明。

## 收尾与下一步

五项任务均在Ready/instances0后精确移除；最终本轮自有Processes0/Tasks0，项目服务保持停止。
D4源码仍未同步，QA上传不是secondary源码更新；原件/数据库、控制桌面、系统音量、墙面/电源、
执行策略和防火墙权限不改。旧防火墙提示没有被自动允许或关闭。

下一诊断优先区分同LibVLC残留、实际UDP套接字/新输入与长GOP夹具影响，各做单变量对照；
不因此阻塞可独立推进的前端、背景音频及登记/编辑检查。
正式代码未变，本轮不重报前轮1092非Physical和223前端为新执行结果。
