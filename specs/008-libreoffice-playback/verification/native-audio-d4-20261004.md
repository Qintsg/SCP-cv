# AudioWorker换源与D4三格式检查（2026-10-04）

## 结果

本机公开AudioCommandExecutor→实际VlcAudioAdapter复现：MP3正在播放，OPEN WAV且autoplay=false，
结果却把旧输入的Playing套到新source_id/generation。释放Media托管引用并不停止播放器旧输入。
正式修复仅在OPEN改变Media/身份前Stop旧播放器；不引入自动重试或改变播放列表策略。
新Physical红1→绿1，原文件SHA保持并可独占只读打开；完整非Physical1092/1092、0跳过。

后台公开play-source接口目前固定autoplay=true，不把本轮worker非autoplay红例冒称该REST已复现相同缺陷。
修复还使普通换源从退役旧输入开始，但所有失败/取消/自然结束竞争仍需独立测试。
首Physical shell finally恢复变量使工具exit0，TRX明确failed1才是判据；后续命令保存真实exit再恢复环境。
原红TRX未覆盖，不把工具exit0当红例通过或产品bug。

## 本机与D4证据

本机资源为Resources音频集合1.mp3/Example3.wav；仅自有实例静音，无用户窗口/系统音量操作。
证据`.validation/008-audio-source/audio-native-red.trx`、`audio-native-green.trx`、
`audio-windows-nonphysical.trx`及full-nonphysical目录，六项目41/303/127/269/229/123均重新构建测试。
QA加入现有忽略目录Probe的audio分支，不创建WPF窗口；实际适配器/命令入口不替换。
QA可只读反射自有播放器来记录Mrl和Media统计，不改变原生对象或生产接口。
依赖版本/锁没有升级，构建0警告/错误；临时审计配置和进程代理协议修正后恢复环境，不关闭审计。

D4 admin console1 Active，source b2f09c5清洁、项目服务先停；本轮不同步源码。
新根`D:\SCP-cv\.validation\008-d4-audio-1791116674461`只复制已有QA依赖和本轮补丁，
上传ZIP SHA256 `454B9A57382284F51942E7ED1B0126A32613B9EEBCB96D4FFEC6603131602355`本远一致。
正式Audio DLL `5306728070F9AE7687D83DE5AA5B315B409C853D129854774221FCBB0DD4FF5F`执行前核验。

原样副本：MP3（C193E5EC…5ACBEA4）、WAV（CEE52E93…A697FBE）、OGG（35CD27D1…57A6EF6）；
OGG是Resources空中课堂的完整版样例，不截断/重编码原件。
任务`ScpCvQa008Audio-audio-1791116674461`、PID11276/start UTC12:27:30.8014282，
run-2517dd4f43794697ab1b00818b5155cb，parent/task均0，约20秒正常退出。
无显示窗口，不占用控制输出或改变两大屏绑定。

## 原生检查

| 格式 / 阶段 | 时钟首→末（ms） | DecodedAudio首→末 |
| --- | ---: | ---: |
| MP3打开 | 0→207 | 1→101 |
| MP3恢复 | 341→601 | 111→132 |
| MP3定位后推进 | 1000→1291 | 141→247 |
| WAV打开 | 0→252 | 0→54 |
| WAV恢复 | 354→604 | 58→68 |
| WAV定位后推进 | 1000→1300 | 72→128 |
| OGG打开 | 3→940 | 1→183 |
| OGG恢复 | 940→1447 | 183→245 |
| OGG定位后推进 | 1000→2883 | 275→453 |

三组暂停300ms前后分别207/252/940保持不变；每组STOP实际stopped。
观察要求五秒内时钟和新解码共同推进，Mrl匹配本格式副本；全部播放阶段muted=true。
SEEK后有推进，不证明精确毫秒定位，尤其OGG最后样本2883不能冒称精确1000ms画面/声音。
最后MP3 Playing→WAV non-autoplay返回stopped/source201/generation5，三文件独占读和完整SHA保持。
STOP/non-autoplay原生Volume=-1、Mute=false原样保留；ControlHost音频投影不按这两个字段改已保存设置，
不能将无活动音频输出的读值当音量/静音实际验证。

本轮stderr为空，不代表所有VLC告警已解决。没有录制/试听实体音频，
静音状态/解码/缓冲计数不是“现场已听到声音”的证据。
未验收真实REST登记/列表控制、AudioFinished链路、循环、异常源或完整组。

## 收尾与后续

原ZIP、编译包、源副本及日志保留。证据ZIP
`6C4E278190F3B389B9F2A591EF5193C0407CDB4F164F902C9405D4E7ECB53AB2`已本远核SHA并原样解压，
本机`.validation/008-audio-source/d4-audio-1791116674461/`。
任务Ready/instances0后精确移除，项目Processes0/OwnTasks0；服务保持停止，D4源码未改。
业务库/原件、系统音量、控制桌面、电源、墙面、防火墙和执行策略均不改。

新增源码核对发现一个必须实测的语义差距：前端/合同叫“列表循环”，Worker循环时直接重播本曲、
不发Finished；后台自然推进又无条件模列表长度回到首项。尚未做公开列表/实际结束对照，
下一轮先验证再修，不把本轮三格式检查当列表循环或全部背景音频稳定。
T072/T073/T074及PPT末页/危险特性、UDP恢复、双窗100控制/60分钟仍未完成。
