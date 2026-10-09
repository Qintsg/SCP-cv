# AudioWorker周期进度与D4对照（2026-10-04）

## 实际红例

正式AudioWorker只在执行命令后报告时钟，RunAsync没有提供sampleState。
不能仅凭源码认定用户已见冻结，故先用独立D4正式Broker/真实Worker/SQLite/公开服务复现。
15秒PCM派生夹具的3.5秒观察只GET服务状态，不补控制：18样本position均0，duration15000、pending空。
观察完成后单次PAUSE作为独立时钟对照，报告真实位置3956ms；parent/task2。
这证明本夹具确实播放、空闲公开进度未推进，不是靠不停控制授予进度成功。

夹具源为Resources的u.wav，原件不改；FFmpeg循环生成15秒PCM副本，SHA256
`4960D23F140D050130314604ACB06EC92862BB3090532C50C3129A836999064E`。
首FFmpeg把stream_loop放在输出位置失败，只修QA参数顺序；原件不覆盖、观察预算不延。
旧WorkerC4DDF2A1…A6BB5672，parent15568/start UTC15:07:06.1413913，worker2504，ready16395ms。
旧ZIP `9B5B35A5BF68A27B56C3D7685EF8143C594AF772DE8FFA316994691CF883A761`已核后拉取保留。

## 修复与保护

AudioCommandExecutor新增只读SampleProgressAsync，复用既有WorkerStateSample及RuntimeWorkerSession空闲循环，
仍使用既有约一秒poll与owner/generation投影，不新增后台计时线程或命令重放。
命令与样本使用同一纯值Snapshot，成功命令保存源/代次；OPEN进入时先使旧采样身份失效，
失败的同源OPEN也不能回报上一成功身份。采样取消保持取消，不触发Play/Pause/Stop/Seek。
未知源或不匹配源不采样；只报playing/paused/error。
原生Ended也映射stopped，因此留给带身份/代次Finished及显式STOP结果处理，
避免采样先把控制端改stopped而挡住自然列表推进。
状态只读一次并传入Snapshot，避免二次读取跨自然末尾；不冒称VLC各属性都是原子帧。

公开执行器纯回归先红→绿，另同源失败OPEN旧身份真红→绿。
首空stub遇CA1822编译失败不当业务红，未放宽分析器。
新增4保护加原有10，共14/14通过；完整非Physical1107/1107零skip（41/303/127/269/240/127）。
最终Windows项目另新编译复跑127/127，五其它项目仍同版源码，TRX均保留。
前端/REST结构本轮未改，旧245不算本轮重跑，Physical听音未执行。

## 最终D4同条件验证

首修复版进度正常0，但随后补单次状态读取保护，旧结果不作最终包证明。
最终Audio DLL
`81B81CC635833F2C693690EAED5C3481CA210B8757681B7D2872A135057E4971`，
补丁ZIP `C264AEA778423AEE35F5D15B0D8B678F5C95C9C95F453A2978B78DC392390967`。
新根`D:\SCP-cv\.validation\008-d4-audio-progress-final-1791127000363`：
进度parent10788/start UTC15:17:58.3510006、worker10380，ready15577ms，正常0；
列表parent18024/start15:24:50.7454857、worker1628，ready616ms，正常0。
启动仍正式30秒，播放仍15秒，progress观察3.5秒，未调高任何生产或QA观察预算。

| 对照 | 无控制公开样本首→末 | PAUSE独立真实位置 | 样本数 |
| --- | ---: | ---: | ---: |
| 旧版 | 0→0ms | 3956ms | 18 |
| 最终版 | 0→3053ms | 4150ms | 18 |

最终duration15000、pending空，暂停/显式STOP及协作退出正常，副本240078字节/独占读/SHA保持。
随后相同最终DLL短曲目列表1→2停止、循环1→2→1及显式STOP通过，Finished推进未被采样抢先挡住。
没有证明毫秒精确、声音实际可听、所有结束竞争/故障或长期稳定；静音意图仍非声卡读回证明。
原生对象仅留Worker，样本是纯值；ControlHost仍唯一正式库写入者。

最终证据ZIP `2BFA6A6C974A7EDEF7A458B44FBF82576146474CCB007ABDB54917CEADCE9B6A`
本远核SHA后原样解压到`.validation/008-audio-progress/d4-final/`；旧版在d4-red。
首修复版ZIP2D396008…695F73及各旧包/日志/新库留远端，不清旧失败。
三进度与最后列表任务各Ready/instances0后精确移除，最终OwnProcesses0/ProjectServices0/OwnTasks0。
旧源码/业务库/媒体、控制桌面/显示绑定、墙面/电源、系统音量、防火墙/执行策略不改；
QA上传不是secondary源码同步或正式发布组更新。

## 剩余门禁

本轮只修周期进度及有限保护。真实HTTP/SSE控制台、离线按钮/缓冲、旧页面通知、
进度error/异源快速切换/父异常完整矩阵仍须核验。
PPT危险特性/有动画末页、UDP恢复、完整同版组与两窗100控制/60分钟仍欠，T073/T074不勾完。
Spec/diff及UTF-8/LF通过；执行器112行/采样33行，无依赖版本变化。“说人话”只整理汇报。
