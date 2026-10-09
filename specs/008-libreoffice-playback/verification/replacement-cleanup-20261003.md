# Player切源双清理失败：保留不确定结果（2026-10-03）

## 当前结果

公开Player OPEN真实复现：旧文稿关闭抛PresentationTransportException，候选清理又抛IOException，原catch只返回第二个普通异常。
Worker仅对直接PresentationTransportException保持uncertain，因此该覆盖会把已下发的旧原生不确定结果降为普通failed。
修复保留两原因并报告PresentationTransportException；候选清理成功时仍原样抛旧异常，正常切入不变。
公开隐藏STA一条业务红→绿，新七项保护+原六生命周期共13绿；真实Worker/管道/SQLite六分类通过，完整非Physical993/993零skip。
T056有限完成；本轮不启动Office，不证明实际UNO关闭或旧物理画面仍在，T011/T018及T044/045/046/053等完整门禁仍未完成。

## 根因与最小修复

PlayerRuntimeHost.OpenAsync先准备next，再关闭旧libreoffice资源；旧关闭失败的catch中直接await next.DisposeAsync，第二次await抛错使后面的throw永远不到达。
链路：旧已下发关闭异常→候选清理异常覆盖→ExecuteAsync抛IOException→RuntimeWorkerSession普通failed。
没有修改Worker全局异常分类，也没有把纯包/文件预检的一般IO异常都升为uncertain。
在现有生命周期partial增加私有DiscardCandidateAfterRetirementFailureAsync：候选清理成功返回，让外层throw保持旧异常实例/类型/栈；双失败以PresentationTransportException包AggregateException(retirement,cleanup)。
两次外部清理均已开始且未确认，不能靠最后一个异常类型称“未执行、可安全重试”。内部两原因可供调用方/诊断查看；持久命令证据本轮仅沿现有顶层类型/错误摘要，不声称完整两个异常栈均入库。
候选在该失败路径不设置_current、源/布局或新WPF表面；原观测身份保留，不以失败候选生成新实际源。
这不保证真实旧原生画面仍在：旧关闭可能已发生动作，LibreOfficeSurface的job/viewport清理和物理结果另须证明。测试只断言宿主引用/公开快照与输出树不被候选覆盖。
不改变准备→旧退役→切入顺序、Frame/尺寸、源摘要/宏策略、原生预算或末页产品选择，不盲重放。

## 先红与保护

新PlayerRuntimeHostReplacementTests沿既有外部IPresentationSurface seam、最小纯包元数据夹具，使用真实Player/WPF/Dispatcher，不反射私有字段或替代宿主实现。
测试窗口始终未Show、NativeHandle0；文件只在本轮GUID临时目录创建，不作为可交付/可渲染PPTX，更不证明幻灯片内容或视觉效果。
第一条业务红保存`.validation/008-replacement-cleanup-red/red.trx`：expected PresentationTransportException，actual IOException/“候选文稿清理未确认”，堆栈到原OpenAsync catch。
最小修复后同用例绿保存`008-replacement-cleanup-green/green.trx`；不是构建/错误夹具红，也没有跳过断言或放宽等待。
新增七项：四双失败组合（transport/io、io/io、io/transport、transport/transport），两候选成功清理保持原异常，正常切入一项。
业务红例同时验证候选不发布，旧source101/generation1/fittrue、WPF原表面引用保留，两个外部清理各确实被尝试；新候选source202/generation2/fitfalse不能覆写。
正常例成功发布source202/generation2/fittrue，退出只关闭当前候选；原六生命周期保护仍绿，共13。

Worker/真实管道/独立SQLite的分类回归新增replacement_cleanup双异常形态，共六项，包括原纯包/已知IO仍failed、三个原transport仍uncertain。
该层注入的是外部异常结果，不是把本次完整Player接入Broker，也没有实际Office；验证uncertain持久终态、最后实际源/布局/占用保留、仅一次执行。
相关结果在`.validation/008-replacement-cleanup-related/lifecycle.trx`与`persistent.trx`，保持边界，不据此宣称整个App/封装客户端或物理输出通过。

## 本轮全量与收尾

六项目逐个重新构建后运行Category!=Physical（项目间串行，项目内原并发/断言不改）：

| 项目 | 通过/总数 | 跳过 |
| --- | --- | --- |
| Contracts | 255/255 | 0 |
| ControlHost | 264/264 | 0 |
| Domain | 41/41 | 0 |
| Infrastructure | 127/127 | 0 |
| Integration | 213/213 | 0 |
| Windows | 93/93 | 0 |

合计993/993，六TRX在`.validation/008-replacement-cleanup-full/`；本轮未再复现旧lostACK重连失败，不以此次绿宣称其原根因解决。
前端没有变化，174为上一轮全量，不冒称本轮重跑；无新依赖、协议版本或REST模式改动，pnpm/CERNET配置保持。
Spec/diff与缓存官方Redocly复核，退役入口既有三个无2XX告警保持；没有浏览器/桌面截图或新原生视觉证明。
所有自有测试进程退出、Resources摘要不变；原件、库、旧失败、profile/dump保留，D4/墙面/电源/系统音量未访问或操作，未提交推送部署。
上一轮真实两实例100控制/20切源与本轮业务异常红绿/完整回归均为实质进展，目标保持active而非complete/blocked。
