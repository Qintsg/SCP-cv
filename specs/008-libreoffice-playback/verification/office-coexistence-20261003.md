# 独立普通Office文稿共存与精确父异常退出（2026-10-03）

## 结论与边界

两个全新独立普通LibreOffice实例分别与正式Player快速动画、视频换源/正常关闭及父异常退出共存；另一个fresh实例复核明确CLOSE前的父异常。
682次只读阶段检查保持独立文稿第2页/总9页、只读与源URL、模型文字摘要、精确进程/监听/可见窗口；项目实例退出后再次实际调用UNO检查成功，最后由独立归属者自己关闭。
普通Office窗口前后页/内容经原样截图复核，没有项目视频串入。独立原件和项目副本均释放，两个Resources原件SHA保持。
T061仅本机有限共存夹具完成，不标T017/T018/T045全部完成。未访问真实用户默认profile/未保存编辑，也不涵盖其它Office版本、完整运行组、双物理输出、媒体时钟/音频或60分钟长稳；D4仍未访问/部署/启动。

## 不是第二播放器或受控假引擎

`.validation/008-office-coexist/`是独立Net48 MTA observer，使用已安装官方UNO程序集；普通文稿由XComponentLoader在_blank只读打开，保留菜单/导航，未启动放映、未参加Display租约/侧车协议。
为保护现场用户，observer创建自己的fresh `TMP/SCP-cv-coexist/<guid>` profile与Office job。复用生产OfficeProcess的精确启动/归属和自有MAR隔离，不改用户默认profile或安装；这不是“未经隔离的默认用户实例”验收。
observer只操作自身文稿：通过[XDrawView官方接口](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/drawing/XDrawView.idl)选第2页，再由同一MTA owner只读核页身份、URL、9页和形状类型/文本SHA。摘要不涵盖全部二进制模型或视觉几何。
窗口查找限定已缓存精确监听PID和自有副本标记，截图用screenshot技能助手，仅此已确认窗口，写系统临时目录；未截图整个桌面或控制用户既有Office。
项目程序仍正式PlayerWindow/PlayerRuntimeHost、生产候选容器及最新本机包LO，无PresentationFactory替换；各自独占profile/job与源副本。这里是小窗口QA，不冒称正式App、Broker或Supervisor组运行。

## 运行记录

所有capture/run根位于`.validation/008-office-coexist/`，项目各阶段stdout/stderr单独保留、事件追加不覆盖。

| capture根 / run根 | observer；独立Office launcher/listener/port | 检查数 | observer退出 |
| --- | --- | ---: | --- |
| 6dfd579c3931450795393af2eac545c2 / 703a5573cdc4469286927ce0c2539840 | 48792；13980/32996/15453 | 300 | exit0，84.74秒 |
| 15127892d621444a8c13433ef7b8d884 / 492a9a6a0cdc4892b039a0bcc8909e83 | 53948；30216/57196/35339 | 300 | exit0，84.76秒 |
| 325691d1e79a4c34bc014b40d0e78fe2 / b4b27983fb19493bb2fa59f71632cad0 | 59284；22304/40944/57270 | 82 | exit0，27.81秒 |

前两组各运行已有rapid（三轮25控制）、video-switch与视频后帧父异常三个项目实例。rapid父34988/13840、Office64208+54812/3676+29040、port49431/35347；视频换源父63100/59876，各两个Office，port49509+54121/14659+48886；这四个正常实例exit0，三个/组正式host job均active0/members空。
旧父异常项目53968/21612，Office21828+30668/39204+54620，port62144/48892；两次均只结束精确父句柄，项目Office及监听在原5秒检查内消失，独立Office仍可调用。它们是异常退出，不冒充正常文稿关闭或持久命令uncertain闭环。

## 更强的CLOSE前门禁

前两次在最后video-later-2截图后注入，stderr已有presentation_shutdown_close_enter，说明项目可能先进入CLOSE；不能把这两次称“CLOSE之前的媒体异常”证据。
QA的--video-hold新增明示门禁：实际帧取得后记录awaiting_external_parent_loss，再停在CLOSE前的有界stdin等待；产品原30秒请求/15秒关闭/5秒换页及媒体接口不变。门禁30秒仅QA，不收到外部注入就失败，不能自然释放当成功。
第三组--fault-only：项目父37696、Office41888/5876、port25111。已缓存两个Office精确句柄并确认都活，收到before_close门禁后再核独立文稿，然后只Kill该父句柄，exit-1。门禁07:29:13.660853Z，注入13.673991Z，项目Office/监听消失后健康检查13.949914Z；约0.276秒是采样确认窗口，不是通用性能保证。
项目stderr只有launch/owned，没有先前正常CLOSE的close_enter；没有把job兜底退出写为正常关闭、猜测是哪一个父监测分支或声明完整故障矩阵已过。
第三组真实video-later-1/2原图显示人物坐姿到左侧站立、屏幕模型视图改变，root及PPTX技能要求的独立视觉agent核对，不是仅页码/静态海报。播放position/duration仍null，未测音频。

## 图像、文件与收尾

普通窗口图片位于`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-coexist-shots/`的上述三个run目录，各independent-before/after.png。root核三组；独立视觉agent核第一和第三组：同文件只读提示、选中第2页/总9页、同研究内容，项目视频不串入。菜单/导航属于普通Office预期，不是项目纯播放泄露。
前后PNG完整SHA不同，不用整窗字节一致性假证明共存或把外缘/窗口活动像素当内容串扰；模型和截图结论各自限定。不声明全部像素/几何不变。
所有12份本轮PPTX副本最后FileShare.None成功并全SHA与原件相同，包含异常项目副本；独立文稿自身close、Desktop.terminate及ConfirmStopped完成后observer exit0，未用全局进程名kill。
TestPPT原SHA `0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`，AllinOne `2401652F7CD610DFCDFE4B5F2F32CD112D9645F1E30CEF44ECB3D716B82A55F0`，收尾仍保持。
精确observer/项目与Office进程、12个本轮监听最终均消失；本轮自有窗全部关闭。用户库/原件、profile、旧失败/dump、图与包保留，无文件删除、设备电源/墙面/系统音量/提交推送部署。

## 验证与复跑

复跑：`dotnet build .validation/008-office-coexist/Probe.csproj -c Release`，再`powershell.exe -NoProfile -File .validation/008-office-coexist/capture.ps1`；只做强门禁可加-FaultOnly。需先按前一记录构建实际Player QA，且最新侧车包路径存在。
observer首次编译DateTime与UNO同名类型冲突，限定System.DateTime后0警告/错误；不是业务红绿。Player QA仍有旧assets NU1900警告，0错误，审计未禁；没有生产代码/依赖变更。
本轮完整非Physical1002/前端177未重跑，仍是此前历史，不能冒充这次实际Office证明。Spec Kit/git diff --check通过，官方缓存Redocly2.55.0为0错误/3个既有退役410无2XX告警；全部本轮新增/编辑UTF8/LF、PS1带BOM且单文件不足500行，无extension hooks。副本唯一数量按实际路径重新核12，缺口仍归当前任务。
完整目标保持active；本轮新增实际共存与强异常归属证据，不是只复述状态。末页产品选择、复杂动画/通用完成、完整viewport/承载讨论、真实两输出/全组、用户式编辑及60分钟仍未完成。
