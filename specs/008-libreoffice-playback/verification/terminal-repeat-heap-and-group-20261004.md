# 主序越界对象证据、包装失败与动画组重复（2026-10-04）

## 本轮结论

堆转储已实证当前样例主序重复的越界，不再只有源码推导：有效子项2、已完成下标2、剩余重复1，
发布二进制直接将end槽当下一子项。包装到PAR的候选改变了放映语义，已否定并撤回活动入口。
不同结构的末PAR动画组重复取得两轮事件/画面与正常关闭证据；这不是主序重复的修复或替代验收。
正式播放/前端/协议未变，T053/T069/T070以及完整双窗/长稳继续未完成。

## 自有实例堆转储

仅QA将CDB转储从/m改/miw：保存同一自有目标的间接内存及私有读写页，不采集用户Office/其它进程。
参数依据[Microsoft转储文档](https://learn.microsoft.com/en-us/windows-hardware/drivers/debuggercmds/-dump--create-dump-file-)；
不使用全局调试器、覆盖选项、处理异常继续假成功或改变原30秒请求/15秒关闭预算。
D4管理员console1在线，事前项目/Office/debug进程无残留、C/D容量足够，源码b2f09c5清洁。

新QA包FFC68F7D7ABB1E91C9A77BE8425D69F2DA9FFBDBD9B1E482D174BDDA53CE2FA2，
根`.validation/008-d4-heap-c90272e3bb6043bfb2610a93dca35d03`；自然End/no-tail、主序RepeatCount=2不改。
parent8944/start05:39:14.9090448Z、host10420、Office12620/15544、port59508、debug2512，
engine c209450b4c1d490e9f224057f250ce4d。第二组播放期间AV，采样变error；task/parent2，job0仅兜底。
新固定相对名qa-native-fault.dmp实际生成421344569字节，SHA BE14A4CC45EFE27E1E48183E636C26A018CDA4FA3AF15924E734FA6EBB1501D8。
证据ZIP3B6DF3499DF06CAA75FD9CDC9CF5FB6E7E7F47AD6A4DA118A84D4090CD07F77A，远端/本机一致。
原件、旧小dump、完整失败日志和profile保留；仅一张首步原图，不伪造第二步/关闭图。

## 实际对象、寄存器与指令

沿用既有官方私有PDB，未强制忽略不匹配；既有PE/PDB Age差异不抹去。
在调用者notifyDeactivating栈帧，dv给出this=`000001947a1e3850`。
最初使用@rbx读到异常帧的子项引用槽，显示无效vtable/荒谬字段；不能把它当对象已销毁。
改明确this地址后，vtable、mpSelf、parent与ACTIVE状态相互一致，原错误解析文件也保留。

| 事实 | 实际值 |
| --- | --- |
| 主序自身 / 父容器 | 7a1e3850 / 7a53e340（均前缀00000194） |
| mbIsMainSequenceRootNode / meCurrState | 1 / ACTIVE(4) |
| maChildren.begin / end / capacity_end | 7a9f0360 / 7a9f0380 / 7a9f0380 |
| 子项大小 / 有效数量 | 16字节 / 2 |
| mnFinishedChildren / mnLeftIterations | 2 / 1 |
| mbRepeatIndefinite / mbRestart | 0 / 0 |
| resolveChild参数地址 | 7a9f0380，恰等于end |

异常指令读取空子对象指针；不是从此次零地址倒推所有旧非法地址都同类。
发布二进制notifyDeactivating在父调用返回false后，按`begin + (mnFinishedChildren << 4)`计算下一子项，
随后直接call resolveChild，中间没有size检查。内存数量/下标、实际参数与反汇编一致，证明本轮访问一越界槽。
父重复分支排队下一轮、下标尚未重置的机制与[固定版本源码](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/basecontainernode.cxx#L123)吻合。
不是推断线程竞态、用户注入模块或所有PPT重复动画都坏；当前夹具是在内存主序上设置重复。

离线原始解析：`.validation/008-d4/repeat-heap-state-c902…txt`（错误寄存器路径保留）及
`repeat-heap-explicit-c902…txt`（明确this与反汇编）。本轮读取的上游master仍有同一索引路径，
不能据版本号猜升级已修；master不是固定发布或新版本实机证据。

## 单子项PAR包装候选失败

仅QA将原SEQ放入一个PAR，重复时序移到外层，原子效果/顺序/目标不复制或保存原件。
官方[容器API](https://api.libreoffice.org/docs/idl/ref/interfacecom_1_1sun_1_1star_1_1animations_1_1XTimeContainer.html)
与[PAR结束路径](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/paralleltimecontainer.cxx#L45)只支持候选推导，不证明语义保真。
包DD85BF956AFD1CD3AD8E6FD200587BD20C1CF87DB4EF75E1CC3F99F74CBE6FC2，
根`.validation/008-d4-wrapper-ecf31d4014bd421c8b87514d872d2b8c`，parent14096/host10252，
Office18060/18184、port60763、debug1628、engine13e9f41fbfa24adaadfac769b490af9b。
日志outer_repeat2/inner_repeat1确认变体生效；第二步后root/wrapper/main结束并发slide_ended_forward，
原图为“按鼠标左键退出演示…”黑屏，第三步没有当前放映，task/parent2；无AV转储不等于稳定。
主agent与独立视觉agent明确判第二图失败，首图标题正常但仍有1右列/2底行黑边。
证据ZIP6F0EC2E790726B4F8724F9ED0AA3C9AD3DE5229ACCE2FD51E7AD48931592A2EC，摘要一致。
活动QA调用已撤回，QARepeatWrapper源码和该包/失败仍保留，不接正式代码、不添加时延掩盖。

## 末PAR动画组重复：不同结构的有限证据

新夹具只将最后直属PAR组RepeatCount=2，主序不设置RepeatCount；End.NONE/no-tail保持候选。
叶子时长/媒体/目标/顺序原样，仍只有TestPPT6自定义单页；不是作者提供的跨格式repeat样本。
包2CB20F16803DB8D40FC2C382C0F884B613B82DBC8260FA4E2A67AA6317E9A4C9，
根`.validation/008-d4-group-repeat-803f0c4fd3b54bd388372f5e5608f8f7`，parent3336/host10400，
Office8732/13444、port60885、debug8584、engine4a2748a9dcf049b7ac290ad040fee94b。
window2/DISPLAY3小窗800×450；8明确控制（6前向、1撤销、1再进）与观察、关闭分开。
task/parent0，约26秒，正常关闭/完整job0、文件释放和原件摘要通过，没有forced_cleanup或dump。

首轮最后组begin12；其同一叶子begin16→end20→第二轮begin28→end33，最后组才end37。
撤销触发旧main end38、新main begin42；再进后的叶子begin55→end59→第二轮begin67。
这证明本轮重复实际启动，初轮两次结束后组才结束；不把同页码、调用次数或采样延迟当耗尽。
回退重播最后采样还在渐显/位移动画中，随后关闭；不冒称第二次重播也已自然结束。

八PNG：首步标题，第二步及previous-next为动画中间帧；3–6完整标题/字幕同SHA8D7993F3…0190，
after-close B49D3062…5C34全黑。主agent检查四个不同阶段，独立视觉复核八图另记。
右1列/底2行旧黑边仍在，不称满幅。额外NEXT仍实际调用原生并返回ok，未产生动画noop，不能完成T069。
证据ZIPB8FA6530BDDEC2900478D48175837BFFD0B24DA846AC9C2E197D26EF5A111EAB，远端/本机一致。

## 收尾与剩余门禁

三个本轮任务分别在Ready/instance0后精确移除；两个result2不改。项目/Office/debug进程、
59508/60763/60885及服务监听无残留，正式组仍停止，源码未secondary同步。
本机两原件及D4使用的TestPPT副本SHA保持；媒体、数据库、日志、图片、dump与QA包不删除。
QA宿主构建0告警/错误、Spec/diff校验通过；正式代码/前端未变，未重跑历史1082/211全套。
仍须真正解决主序重复、绑定耗尽事实与不可变播放轮、隐藏/自定义重复页、完整两窗/媒体/60分钟，
不得因为不同层级的动画组通过而缩减原主序故障或将未知repeat一律取消。
