# 重复主序原生转储及自然结束对照（2026-10-04）

## 结论与范围

本轮仅修改独立QA诊断宿主。D4取得两份实际Office访问冲突转储，官方私有符号将故障定位到
`SequentialTimeContainer::resolveChild+0x22`；取消QA的End.NONE改造后仍在同一位置失败。
这排除了“只取消End.NONE即可消除本轮故障”，没有证明所有承载、监听或重显示因素均无影响。
正式播放策略、协议和前端未变，T053/T069/T070继续未完成，不把取得故障栈称为修复。

素材是只读TestPPT副本，在内存主序设置RepeatCount=2；不是实际作者文稿全集或所有repeat类型。
两次仍使用同页受控重显示、官方监听、唯一自定义页、原生效果入口及800×450窗口2。
只有本轮End策略作对照，不是完全未经项目适配的LibreOffice放映对照。

## 诊断工具门禁与失败保留

自有Office监听PID先经端口/job归属确认，再以CDB `-p` 精确附加；不按进程名附加，
不设置全局AeDebug/LocalDumps，不修改系统策略或用户实例。六个已有Microsoft组件在上传/展开时核有效签名。

| 新QA根后缀 | 实际结果 | 解释 |
| --- | --- | --- |
| 15a0a714… | parent12816/host14160；附加前失败 | CDB参数错误，Office4112/11104及debug11744退出；不是新动画故障 |
| d37e0d6c… | parent16052/host12636；日志读取失败 | 已附加但File.ReadAllText未允许并发写，Office5672/9596及debug18296收尾；未播放 |
| b2d25e8d… | parent11904；采样不确定/关闭超时 | debug9748在Office12172的首次AV处停住，测试预算受到影响；未核定宿主PID，不得称正常退出验证 |
| ccaf8d74… | parent17596/host15824；第三效果失联，task/parent2 | 自动AV转储后detach，保留原动作URP断开及清理失败 |
| 466b45cc… | parent13832/host15108；第三效果失联，task/parent2 | 自然End对照，同位置AV；job0仅兜底 |

前两失败的包/日志/profile保留。CDB自己的帮助文本列`-netsym:no`，实际拒绝该拼写；
本机自有隐藏cmd对照中，仅移除此参数或改官方`-netsyms:no`后附加/脱离成功、目标仍活。
随后QA用FileShare.ReadWrite取得活动日志只读快照，解决已实证的共享冲突。
参数依据[Microsoft CDB文档](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/cdb-command-line-options)。

最初只监视WER代码0，CDB默认在真正AV处停住，没有自动保存；新增AV自动命令保存minidump后qd。
首机会异常停顿会改变时序，不能把附加诊断当无干预长稳。
[Microsoft异常控制说明](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/controlling-exceptions-and-events)。

## End.NONE候选实际故障栈

QA包136FB59B6722CF28726039285E158C981171996AC6A1713CCD816E307D75B278，
D4新根`.validation/008-d4-debugger-ccaf8d746867419aadf3fc1ceff38d8a`。
Office12596/5232、port59147、debug2504、engine c23de237cc4a45bf89c24cd103a11c34。
原始异常为首次`c0000005`读`0000002f00720061`，故障指令`mov rax,qword ptr [rcx]`。

本机离线加载既有TDF PDB，未强制忽略符号不匹配；DbgEng报告private pdb symbols。
本机/D4 mergedlo.dll同SHA AB197EF23B5B6475084BA9A8845B51921DC1AC5F5785920A444AAC76AA4D5CD2，26.8.0.3。
既有PDB的Age与PE差异仍保留，不把正常加载扩大为所有符号/系统模块完全匹配。
未加载私有符号的远端大偏移export名称不是实际故障函数；离线解析栈为：

```text
SequentialTimeContainer::resolveChild+0x22
SequentialTimeContainer::notifyDeactivating+0x35
BaseNode::notifyEndListeners / deactivate
BaseContainerNode::notifyDeactivatedChild+0x10e（多层）
Delay::fire → EventQueue::process_ → SlideShowImpl::update
VCL主线程调度
```

证据ZIP7DDAFAB34AB012DC892E61818E9220AD1805565AC7A919CA78ED984F544D72CF，远端/本机摘要一致。
转储179194字节，SHA9306DD68C4C72C6D0E9D4AD387513B8214BFFB0E56FAB9D055E2E79B40845F41；
是寄存器/栈/部分内存的minidump，不能据此宣称已验证完整堆对象寿命。

CDB嵌套命令解释绝对路径反斜杠，使两份dump落在原自有profile内但文件名不同于预期，均按实际路径保留。
后续QA改固定profile工作目录下的相对名；本机自有测试目标经stdin触发合成AV后，
实际生成qa-native-fault.dmp且debug自行退出、外层精确收尾通过。此烟测不是Office故障复跑。

## 自然End单变量对照

QA包BC8480C9E6D37434EB616246634B051BA49EE1BAEC1BA41E8C3D9B08FFBC5C03，
新根`.validation/008-d4-natural-466b45cc13b84500aa45cc221c527fa3`。
仍RepeatCount=2/no-tail，唯一业务区别是不设置End.NONE，日志natural=True；其它重显示/监听/客户端顺序相同。
Office13048/16980、port59310、debug18248、engine e6dc2562b86f4485bd6fb6db45f95f2a。
第三效果失联，原生首次AV读`00000073006c0064`；离线解析同resolveChild+0x22/notifyDeactivating+0x35调用链。
没有把失败丢失或将job兜底称正常关闭。

证据ZIPCFDDA0B06D4F774919CBAD704744CD68EE28736536267A368034306A9BDBF8B4，远端/本机一致；
转储171121字节，SHA3EBE4101935BFE588A5EF7DDCCA4CD17171B13223EC23DD5B9BE3D939CAE7FC5。

固定版本[顺序容器源码](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/sequentialtimecontainer.cxx#L89)
在notifyDeactivatedChild返回false后直接读取maChildren[mnFinishedChildren]；
[父容器重复路径](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/basecontainernode.cxx#L123)
可在计数到size时为待重复轮次返回false并排队repeat，计数重置在后续init_children。
该越界路径与故障栈高度吻合，但仍属源码推导；本次小dump未读出故障时完整容器/下标。
下一步优先补自有实例对象/下标证据及不含主序repeat改造的自然作者样例，不立即fork源码、消除repeat或延预算。

## 图像、收尾与验证

两组故障前各两PNG字节SHA相同：首步46F393C2…5FE6BA0，第二步8D7993F3…B5980190。
主agent及独立视觉agent核第一组：标题/字幕完整，未见菜单/导航/残影，右侧1列及底部2行黑边保持。
自然End首图主agent复核及两图摘要一致；仅证明故障前两个采样时刻，不证明重复完成或满幅。
没有after-close图，不伪造清黑证据。首次收集脚本猜了不存在的图片目录，ZIP只含日志/dump；
随后按真实out.log路径单独拉回两图，原ZIP不覆写。自然组ZIP包含原始两图。

五个本轮任务各在Ready/instance0后精确移除，所有result2保留；没有递归删除根、媒体、profile或转储。
最后只读核D4项目/Office/debug进程及相关监听无残留，服务保持停止。
收尾脚本最初要求动画QA中不存在的AllinOne副本导致提前终止；已改为实际存在的TestPPT并重新完成检查，
不以首次脚本失败声称清理已验证。TestPPT本机/远端SHA不变，本机AllinOne原件SHA不变；
D4源码HEAD仍b2f09c5且清洁，QA包上传不是secondary源码同步。

Net48 QA宿主及调试烟测最终构建0告警/错误，Spec Kit与diff检查通过。
正式代码/前端未改，不冒称本轮重跑历史1082非Physical/211前端；完整双窗/媒体/末页/60分钟仍未完成。
