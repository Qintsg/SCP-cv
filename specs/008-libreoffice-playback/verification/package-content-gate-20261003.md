# OOXML扩展与改名包安全门禁（2026-10-03）

## 已确认缺陷与修复

上传分类接受PPT/PPS/POT/ODP及OOXML变体，但共享PresentationPackageMetadata此前只在文件名为.pptx时检查ZIP/XML。公开转换实证外链包.ppsx及同包改名.ppt都越过预检、尝试启动宿主，不满足既有外链拒绝规则。
测试把Host路径指向本次新根下不存在的EXE，故真正红为Win32Exception/PresentationProcessClient启动栈，而不是Office弹窗或已发生网络访问；没有让危险包进入实际Office。首.ppsx业务红记录`008-package-extension-red/red.trx`，后改名.ppt业务红`008-package-rename-red/red.trx`均保留。
共享纯文件实现现在先识别可解析ZIP；六种OOXML声明扩展.pptx/.pptm/.ppsx/.ppsm/.potx/.potm按同一结构门禁处理，非声明扩展的ZIP若包含ppt/presentation.xml也进入相同检查，不靠改名绕过。
非OOXML ZIP/非ZIP旧格式保持既有引擎路由；带PK头但损坏的改名ZIP明确拒绝。XML/页关系/外链/现有预算规则保持，不提取文件、不访问网络、不改原件、不增加原生动作或预算。
不是完整文件类型识别/恶意多格式容器审计，也不以这种有限探测证明旧PPT、ODP或宏/加密文稿已安全播放。

## 分层验证

按TDD一条.ppsx先红→覆盖声明扩展后绿，再一条.ppt改名红→按内容检查后两绿。没有预先写整矩阵让实现追猜测；生产接口没有新字段或行为降级。
公开上传/AddUploaded→Claim→LibreOfficeSlideConverter共11个格式用例通过：六OOXML及大写PPTX、改名.ppt/.pps/.pot/.odp；Failed/presentation_package_rejected、无制品目录、原件摘要保持。测试环境collection独占且finally恢复Host变量，不影响并行测试或用户配置。
共享包26项通过（旧16+新10），含合法改名元数据、非OOXML ZIP、OLE头保原路、坏PK拒绝。OLE头/最小ODP ZIP只证明路由兼容，不是可播放旧文稿或安全样例。
公开Player隐藏STA三项.ppsx/.pptm/.ppt在生产候选创建前InvalidDataException，未分配窗口HWND、无资源采样、NEXT拒绝。首夹具错期待无资源也有实时Sample导致3失败，`windows.trx`保留；核SampleCore既有合同后仅改测试为null/NEXT拒绝，`windows-corrected.trx`3绿，不为该误判改产品。
首测试类型名称以Collection结尾触发分析器，改名Group后编译通过；不是业务红。全部测试通过真实公开入口，内部同源包矩阵作为补充，不冒称实际Office已访问/拒绝外链或持久uncertain完整矩阵。

## 当前完整回归

六项目重新构建、项目间串行运行Category!=Physical：1026/1026、0skip。TRX在`.validation/008-package-content-full/`，Contracts265、ControlHost264、Domain41、Infrastructure127、Integration225、Windows104；原两业务红与错误夹具记录不覆盖。
Net48正式Host同源构建0警告/错误。本轮没有前端生产变更，前端177仍是之前结果，不冒称全量或浏览器重新测；REST形状不变，上传文档旧PowerPoint说法同步改LibreOffice。

## 正常原件真实转换保护

新根`.validation/008-package-format-conversion-0d9b79fa97ed400785a3feab5377860e`，公开上传→中文用户目录→真实生产Converter→发布→关闭后移动与FileShare.None：AllinOne原/fit各9页，TestPPT原/fit各7页，8.74/8.03/6.33/6.06秒。
四个正式独占Host/Office均正常document_closed、termination_requested、job active0/members空；Running移动锁保持，原件/副本SHA不变。四个监听55756/5728/21119/21124收尾消失。
32PNG+四图片PDF与`008-production-conversion/package-487102520ffa45648d4d21a957572967`同源同布局逐个完整SHA相同36/36，没有借静态图证明动画/媒体/满幅，也未修改或重新优化原排版/fit策略；旧默认窄标题缺陷仍在。
QA Probe旧assets NU1900警告保留，正式项目/Host没有关闭审计、TLS、锁或分析器。原Resources只有两份PPTX，没有真实旧PPT/宏/加密样例，不能因此把T045安全/兼容完整门禁勾完。

## 更新的本机完整包

全新包`.validation/008-package-format-current-200bc0bbb6cc46a895f2e35d170f5ac8`由既有publish-runtime脚本生成，五EXE均AMD64，Net10/Net48协议隔离、16只读依赖installed/availabletrue，检查域未加载UNO；MediaMTX仍外配，不启动整个组。
本次含修复的Contracts DLL SHA `C74A47461B1C2D23BC97D8B1F8C4E9E704F5459037FF7E68BCA063F401239CFF`，Framework Host SHA `E4D765054D28266DAE0AF338291CB959AA8722CDD82B528E28E0443A69531EC3`，开发输出/包一致。
T058的9ce543…旧包保留历史证据，但不含本次安全修复，不能继续称其为当前代码最新包。新包生成不是实际Player/Audio/MediaMTX/双输出启动验收。
仅发布进程三个继承代理scheme暂从socks5h改socks5，地址/凭据不变、不打印，审计/locked保持，finally恢复；机器/NuGet/.npmrc不改。脚本验证绝对边界后只删除本次GUID临时发布stage（可重建），两个旧包及新最终包都保留。

## 文档与剩余

当前禁止宏执行与自动更新外链（MacroExecutionMode=0/UpdateDocMode=0），并拒绝OOXML外部关系；不按.pptm后缀自动拒绝整个源，不宣称所有含宏/加密/旧格式都已在纯包预检阶段识别。旧“严格拒绝宏”笼统描述据实现更正，宏拒绝实机仍单列待测。
已同步task/plan/合同说明、上传OpenAPI/运行指南/维护/使用/CHANGELOG/known-pitfalls。Spec Kit与git diff --check通过，缓存官方Redocly2.55.0为0错误/三个既有退役410缺2XX告警，不改成假2XX；全部本轮编辑UTF8/LF且单文件不足500行，requirements16/16，无extension hooks。
原件/数据库/旧失败/dump/制品/包保留，本轮没有D4、设备电源/墙面/音量、提交/推送/部署。完整目标active；实际旧PPT/宏/加密、末页产品选择/复杂动画、viewport承载讨论/双物理输出/完整组与60分钟仍未完成。
