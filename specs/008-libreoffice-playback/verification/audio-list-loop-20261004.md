# 背景音频列表循环与真实管道（2026-10-04）

## 修复与本机验证

现行前端和OpenAPI定义列表循环，不是单曲循环。Worker原先循环本曲并吞Finished，
控制端自然推进又无条件模列表长度回首曲。公开服务末曲不循环测试真红（期待源2、实际1）；
原生短夹具旧分支真红（无Finished且仍playing），两处修复后通过。
Worker始终报带源/代次的结束事件；ControlHost在同一事务选择下一曲或带TriggerEventId的STOP。
不循环末曲保留末曲/代次，中间曲仍推进；循环末曲回首。手动上一首/下一首保持原回环。
原幂等/代次围栏不放宽，没有协议或数据库迁移。

公开服务/真实SQLite共10项：新增9项含单曲、多曲、中间/末曲、循环开关、手动、重复/旧源/旧代次；
原有跨服务重建幂等仍通过。它们用外部Worker事件，不冒充原生结束。
实际适配器与命令入口循环开/关收到真实结束，加原换源回归Physical3/3；
六项目非Physical1101/1101、0跳过（41/303/127/269/238/123）。前端未改未重跑旧232。

初夹具u.wav实际mu-law/8kHz/5.504500秒，超过5秒事件观察；初red、名为green但失败的TRX、
诊断time4808/duration5505均保留，不当原生业务红。没有延预算或改原件。
FFmpeg从原件生成2秒PCM s16le副本short-end.wav，SHA256
`42117F97B503BBD69C02A9B2680DF81C059791D96A196354C9BD5DFC83F06F97`。
该同一夹具上临时复原旧分支：5秒仍playing/time851/duration2000且无Finished；恢复修复后约2秒绿。
证据`.validation/008-audio-loop/audio-list-native-*.trx`、boundary-*.trx及full-nonphysical。

## D4：实际AudioWorker→管道→队列→公开服务

QA使用正式Broker/身份和真实AudioWorker程序集入口，独立SQLite与两份同内容、不同源ID/路径的短副本。
没有FakeAudioAdapter或业务HTTP；只登记audio角色，不把Supervisor/Player/MediaMTX缺席当全组就绪。
显式提交静音意图，不启动显示窗口或改业务库，不做现场听音。

首任务parent11792/start UTC13:06:03.5916406、worker9400，结果2。
只读DB证实Armed/workerOnline，首SET_MUTE约15秒后才领取，SET_LOOP/OPEN还Pending；
QA在ready前开始了播放计时，未取得列表实证。原ZIP
`C226B115BB076C49A449B9E433BE5AEABA56C4305D538CD20033E69041BC3E1D`及DB/日志保留。
不把启动时间混成播放预算或称列表修复又坏了。

QA按正式RuntimeSupervisorOptions分开既有30秒启动与15秒播放检查，
只读确认audio readiness，不改生产预算、不授予全组ready。
成功新根`D:\SCP-cv\.validation\008-d4-audiolist-ready-1791119799260`，
parent13108/start UTC13:18:04.8029406，worker2512/start UTC13:18:12.4027897，
run-bb220a61cccf4bb981c32f39ae5932d8，ready实耗14570ms；parent/worker/task均正常0。
冷启动耗时记录，不称性能已优化。

| 已完成的实际阶段 | 源 | 状态 | pending |
| --- | ---: | --- | --- |
| 不循环首曲 | 1 | playing | 空 |
| 不循环下一曲 | 2 | playing | 空 |
| 不循环末尾 | 2 | stopped | 空 |
| 循环首曲 | 1 | playing | 空 |
| 循环下一曲 | 2 | playing | 空 |
| 循环回首 | 1 | playing | 空 |
| 显式停止 | 1 | stopped | 空 |

十命令Completed/ok；四结束事件登记两次OPEN推进、一次末曲STOP、一次回首OPEN。
末曲STOP保持generation2，显式STOP保持5。两副本32078字节、独占读释放、SHA匹配。
正常协作退出再关闭Broker，没有失败/不确定/未完成命令。
存储is_muted是意图，归档不含每曲原生Mute读回，不能据它称实际声音或声卡通过。

QA完整ZIP97B02DF4…FB4EEC1，ready仅QA补丁27BBDDCB…930B7D5均核后执行。
Audio DLL `C4DDF2A1F481945F474A1444E19747DC8C3FF081A711EB3CEE8EAD40A6BB5672`，
Infrastructure3DFE711D…AD4F6C23、HostBE57B243…5272B66F。
成功ZIP `0234B1BFC817BB6B5E56A647652344DFD597EBDBC089C463DEA8BAC0B80EF6BA`
本远核SHA后原样解压到`.validation/008-audio-loop/d4-audiolist-ready-1791119799260/`。
两任务Ready/instances0后精确移除，最终OwnProcesses0/ProjectServices0/OwnTasks0。
D4 source b2f09c5清洁；不是secondary同步。控制桌面/显示绑定、电源、墙面、系统音量、
防火墙/执行策略和原库/媒体均不改。

## 文档与剩余风险

OpenAPI只补语义，无字段/路径扩展；Redocly通过，三410退役入口的2XX告警保留，不加假成功。
Spec/diff和UTF-8/LF通过，音频服务422行。
当前ControlHost依赖在QA审计报NU1903：SQLitePCLRaw.lib.e_sqlite3 2.1.11。
[官方审核公告](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)列<=2.1.11受影响、无该包修补版本，
底层SQLite须至少3.50.2；生产依赖尚未升级。QA只有固定内部SQL，不执行攻击载荷，
但不能据此宣布业务安全；保留1高危告警，不关闭审计/抑制或写0警告。
免费许可/兼容修复继续纳入T074收敛。

后续更正：上文将QA告警扩成“生产依赖尚未升级”的判断撤回。生产HEAD/锁已pin2.1.13；
旧QA漏继承CPM而使用2.1.11/实际3.49.1，故旧告警真实，但不代表当前生产也解析该版本。
本轮对齐后QA实际3.53.3且D4同版列表复测通过，详见[更正与证据](sqlite-qa-alignment-20261004.md)。

真实HTTP/前端晚到和离线、听音、父故障/复杂结束竞争、全组/混合长稳、PPT危险特性/动画末页、
UDP重开与SQLite风险未完成；不勾完整T073/T074或整个目标。“说人话”只整理真实结论。
