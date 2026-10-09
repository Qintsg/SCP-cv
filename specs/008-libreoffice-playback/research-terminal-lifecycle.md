# 原生末页生命周期研究补充（2026-10-04）

本轮按speckit-plan进行两项只读研究；下述来源固定为LibreOffice26.8.0.3，不升级引擎、不修改其源码。
源码推导不是实机结论；仅无主序终点分支已在正式Host接入并经D4有限验证。

## 决策：实际终点与动画耗尽分开

真实终点采用controller.getNextSlideIndex()，无效或>=getSlideCount表示没有后继。ALL常规next跳过隐藏尾页，当前隐藏页则走相邻项；FROM/CUSTOM末项可返回count而非-1。因此current==count-1既不足以确认实际终点，也不证明动画耗尽。见[固定版本next规则](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshowimpl.cxx#L395)，root已直接读取原始源码395–430行核对。

无主序模型页可在原生动作前读取终点，返回noop而不resize/goto/gotoNextEffect。有主序页不能沿该分支猜耗尽，完整T069继续保持门禁。

## 决策：不能把监听者移除当旧回调排空

Controller注册/移除采用proxy的mutex；forEach建立迭代快照后解锁再调用listener。旧快照可能继续调用已移除对象，必须先本地retire不可变listener身份，不能从回调读取“最新全局epoch”给旧事件盖新章。见[注册代理](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshowimpl.cxx#L3740)、[快照投递](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/include/comphelper/interfacecontainer4.hxx#L335)；root已直接读取原始后者335–352行。

候选状态绑定engine/document/controller及实际occurrence/model/main/last-child，区分AwaitingTransition、AwaitingMainBegin、Active、Exhausted、Unknown、Retired。previous/goto前撤销旧耗尽事实；实际transition_started驱动新播放轮，而非next命令数。旧end、未知节点、丢事件或身份不匹配不得授予noop。callback仅纯值/OID，不UNO重入、IO或等锁。

## 首次零时长与repeat仍须实验

注册listener后强制同页异步JumpToMark有可能取得完整初始播放轮；原生合法same-index仍display，但项目现有same-target提前return阻止该路径。须等待真实transition及匹配main begin，不能只看currentIndex相等。见[显示入口](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshowimpl.cxx#L1896)。

最后子项end前父容器可能已排队repeat；主序repeat不保证再发main begin或repeat通知。因此sentinel end或slideAnimationsEnded计数不能作通用耗尽事实。见[父容器repeat](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/basecontainernode.cxx#L136)。必须补自动零时长、RepeatCount=2/indefinite、Restart/Fill、快速末动画撤销再推进和迟到回调实际门禁；尚未据该设计启动生产listener。

## 自定义重复页的模型共享风险

Presentation属性是CustomShow/FirstPage/IsShowAll；页隐藏应读Visible而非猜IsExcluded。自定义容器返回实际XDrawPage且保留重复，属性也不能直接当startWithArguments参数。启动前模式还会受当前编辑页是否隐藏影响，前置快照后须比对真实controller列表。见[属性](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshow.cxx#L109)、[列表生成](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshowimpl.cxx#L2520)。

Custom[A,B,A]的两个A共享模型/动画；预先对最终模型A设End.NONE也会改变早期A的非终点行为。末项clone并新建自有custom show是可研究候选，但尚未证明复杂媒体、页码字段、事件目标保真，不提前接默认路由。不用replaceByIndex替换重复末项：其remove通过std::find删除首次相同页。见[容器实现](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/sd/source/ui/unoidl/unocpres.cxx#L114)、[动画克隆](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/sd/source/core/CustomAnimationCloner.cxx#L89)。

未完成的实际实验、隐藏尾页/手动隐藏末页/重复occurrence导航及复杂序列，都仍属完整T069，不因无主序页与两个小窗通过而删除。

后续实证：原样单页受控同页UI重显示在D4捕获初始transition/main begin并保持/撤销；zero构造裁掉叶子内容，repeat2失去完成并有CLI listener重复注册后清理异常。只确认有限重显示能力，不据此授予通用耗尽。失败及下一实验见[候选验证](verification/terminal-replay-candidate-20261004.md)。
