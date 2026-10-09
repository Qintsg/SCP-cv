# 动画事件调查与诊断门禁（2026-10-03）

## 本轮结论

新增默认关闭的官方listener诊断，回调仅记录有界纯值事件；容量业务红→四纯保护通过，完整非Physical970/970、零skip。
两轮诊断开启及一轮关闭各62次真实返回、正常关闭/job退出；已看到正向两点击组、主序结束、跨页及撤销重播事件。
第二次点击控制器约1.4536ms返回，而该受控样例主序结束通知在1055.6104ms；完成请求与动画结束不能混为一谈。
没有原生像素截图、不证明文字实际显现位置或完整媒体/裁切/双窗60分钟；T044仍未完整验收。
控制按钮在播放中再次推进的期望已向用户询问，未收到选择前不改正式协议/能力或把触发受理重新定义为视觉完成。

## 样例选择：文件声明不是运行证明

只读ZIP/XML与原件SHA核对，未改/解压覆盖文稿或重新生成样例。
TestPPT第六页：mainSeq id2的顶层等待组id3/7均delay=indefinite，分别clickEffect id5/id9。
第一组针对shape2标题“第六页”，visibility子节点声明1ms；第二组针对shape3副标题/段0，淡入和x/y行为声明1000ms。
两个grpId=0是分组，不算一个点击；十三时间节点也不算十三次效果。第六页无advTm/transition，第七页无timing。
声明可用于调查“两个点击主序→再推进跨页7”，不能从它直接认定LibreOffice实际完成或版式正确。
第3页主序是机械臂视频playFrom(0.0)、声明158322ms，另有点击shape4的interactive togglePause，非第二次主序NextEffect。
AllinOne九页无timing/transition，只作翻页基线，不假冒动画样例。

## 回调接口与记录范围

正式Net48官方CLI的uno.util.WeakBase+XSlideShowListener，11个官方方法完整实现。
startWithArguments后注册到已取得controller，原owner强引用listener/controller；第一张自动事件可能已过，baseline明确不完整。
SCP_CV_PRESENTATION_DIAGNOSTICS不是1时不创建、注册、移除或记录事件；生产接口/DTO/fingerprint/capabilities未新增。
callback不读/保存Node或source、不访问hyperlink、不重入UNO、不写IO、不等待buffer锁。
容量128、Monitor.TryEnter无等待，满额/争用false并计丢失；主调用线程Drain提供不可变批次，释放锁后写stderr。
序号、Stopwatch ticks、事件种类与观测窗口ordinal均纯值。owner另写operationId/窗口/返回ticks映射；不是事件因果授权。
旧listener固定实例/文稿epoch；关闭前Retire并从捕获旧controller移除，不跨新文稿重用。

三边界保留：诊断开启时同步remove和owner日志可能增加等待；Retire/Flush与已在途callback存在竞态，末批不是完整最终账；
窗口标签保持到下次请求，迟到/自动/重播事件可看到后来窗口，不能按window计数判本次效果完成。
丢失为0仅指已排空批次，不抬成所有晚callback都已入账。事件结束也不等于GPU像素、视频时钟或文件锁证明。

## 纯 buffer 红绿

接口baseline允许Queue增长，cap2录3后首业务红Actual3/Expected2；限制容量并明确drop后绿。
CA1512是Net10共享源码分析器前置，按NET8_0_OR_GREATER/Net48兼容分支修正，不关分析器、不算业务红。
四保护：满额保留前序/丢失1；Drain重用/快照不变/序号标签单调；8×500并发attempt=accepted+dropped；非法容量/default128。
不使用private反射或锁注入制造延迟证明，不宣称压力下零耗时。源码及tests都小于100行。
TRX在`.validation/008-effect-events/event-buffer-capacity-red.trx`、`event-buffer-green.trx`；Net48 Host与QA构建0警告/错误。

## Native：异步页码与效果事件

本机800×450/DPI96自有Form，正式client/Host、独占profile/线程、只读TestPPT副本，不连接D4或操纵用户Office。
effects QA：open6→有限只读采样→两次next_effect各后续采样→第三次推进页7→previous_effect回6→再撤销/推进→常规页边界→close/shutdown。
每段1.5秒仅是有界采样窗口，不当动画通用完成预算；不在正常效果采样做dump扰动时序。

首次`run-070168feb0404dab9d0ab88df6ed026b`将第三次next_effect即时页码硬断7，实际返回6，产生失败。
稍后同观测窗口出现slide_ended_forward/transition_started，说明过早断言不能覆盖异步转换；保留失败，未把它叫Native执行失败或删断言。
QA改为记录全部中间页码，仅在采样末再核预期页；这不是改产品预算或把固定等待包装成完成协议。

| 新运行根 | 诊断 | 返回/失败 | 记录事件 | 正常退出总耗时 |
| --- | --- | --- | --- | --- |
| run-0d950160e4344562abca41c3da477934 | 开 | 62/0 | 101 | 18.41秒 |
| run-4537468d7720488baad672547e606722 | 开，增加同一单调时钟比较 | 62/0 | 101 | 18.38秒 |
| run-ce8a0a0c36b64230b3eb6a9ac0898fec | 关 | 62/0 | 0 | 18.23秒 |

第二轮实例3f7fc1e2的第二次next_effect窗口17：start ticks5774032964177、返回5774032978713。
之后end events进入observe窗口18/21，主序结束sequence28 ticks5774043520281；frequency=10,000,000。
返回耗时1.4536ms、主序结束1055.6104ms、返回后约1054.1568ms；两轮均无已排空batch丢失。
这说明该控制调用先返回、样例效果后结束；窗口从17变21不是新的效果因果关系，也不是普遍可用回执。
第三次next即时6→后续观测7，previous_effect即时7→后续6；撤销重播出现多begin/end与同页transition。
不能据事件数量恢复通用动画步序，不能将任意transition当换到不同页，不能把声明1000ms当真实媒体时长。
三轮effects_visual_verified=false始终保留；close/完整job正常退出与原件/副本SHA通过，不表示视觉/长稳通过。

## 全量与收尾

最终全非Physical970/970、零skip：Domain41/Windows86/Contracts255/Infrastructure127/Integration212/ControlHost249，
六TRX在`.validation/008-effects-final/`。前端与API本轮未改，沿用上轮164/164，不冒称重测桌面控制台/完整包。
全部本轮自有Office/Host/Probe与监听已退出，诊断环境只在工具子进程设置/恢复；旧失败、原件、用户库、profile、dump均保留。
本轮未部署/推送/启动D4，不控制电源/墙面/系统音量；没有全局结束或修改用户Office实例。
T044后续必须明确控制触发与动画状态/视觉完成语义、取得实际像素/视频边界证据；T045裁切仍等人类架构确认，T046完整包仍欠。

官方依据：[XSlideShowListener](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/presentation/XSlideShowListener.idl)、
[XAnimationListener](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/animations/XAnimationListener.idl)、
[官方WeakBase](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/cli_ure/source/ure/uno/util/WeakBase.cs)。
