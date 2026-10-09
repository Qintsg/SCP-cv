# 快进后推进、节点身份与视觉复核（2026-10-03）

## 当前状态

已实现主序零时长自动收尾，使“最后点击组运行中再下一步”由原生一次调用快进并推进；没有固定等待或盲补发第二次调用。
无主序效果的页按真实模型页身份复用UI换页，修复刚打开AllinOne后第一条下一步被吞的样例；回到有动画的前页仍沿原生效果重播。
正式宿主基础推进、三轮循环、视频页码边界和正常关闭通过；原件摘要保持。初次复核曾误报撤销副标题残留，本日重新核原图/摘要/像素后撤回，见下述更正；T053/T044仍未完成，动画末页进入结束黑屏另有真实证据。
完整非Physical985/985、零skip通过，不替代实际原生视觉/媒体/混合DPI/两窗60分钟或最终包门禁。D4未操作。

## 节点身份不是事件计数

默认关闭的`SCP_CV_PRESENTATION_NODE_DIAGNOSTICS=1`只有同时开启既有演示文稿诊断时才生效。
owner线程按页读取动画模型，最多4096节点/64层，重复/环明确拒绝；属性读取只在owner，目录建立后不变。
回调只用官方CLI代理的本地OID hash/Equals查标签，不调用UNO属性或保留传入节点，仍用有界纯值buffer；不把标签、begin/end或末批当视觉完成回执。
官方CLI对System.Object方法单独处理，Hash和Equals来自代理本地OID；与真实TestPPT节点关联实测一致，不依赖指针/对象引用相等猜测。
第六页主序`p6/root/0`及点击组`/0`、`/1`运行begin/end均对应导入模型；首次门禁的日志pattern错误修正后关联通过，不能把该QA错误当业务红。
原先无标签日志只能计事件，不能定位正在运行的组。本轮未新增wire字段、效果完成指纹或capability声明；同页Confirmed仍仅页码观测。

## 单变量原型与正式实现

源码依据：原生skip是否继续推进取决于主序当前子项是否最后；最后子项不自动next。
三种方案分别验证，不叠加为正式实现：

| 方案/运行根 | 结果 | 保留的结论 |
| --- | --- | --- |
| `processEventsToIdle`：8bd628… / 2b72d0… | 正常短轮；第二点击约1.065秒返回，快速判据改变而失败 | 会等自然动画，不能用来满足快进要求，也没有进入正式代码 |
| 单次`reschedule`：de4713… | 正常短轮，快速三次仍6 | 不构成完整事件队列/操作边界，未进入正式代码 |
| 仅第六页零时长空尾：498488… | 26返回、正常exit0 | 前两次保持6，第三次快速调用后到7，原件不变 |

真正旧行为红：6b789a…，三次调用均返回但采样仍6，期望7失败，正常自有job退出；不是超时延长或隐藏失败。
正式`PresentationEffectAdvance`在自有只读内存模型的非空MAIN_SEQUENCE末尾追加Begin=0/Duration=0的无目标ParallelTimeContainer。
原有原子效果时长/目标/顺序保持，交互序列不遍历/调整；不保存模型，不应用到PNG/PDF转换。空尾让原末项成为非末项，由原生skip队列继续推进。
只扫描容器主序，不逐叶做RPC；按真实XDrawPage身份保存是否有主序效果，不将放映列表索引误作源文件页号。
无主序页下一步/上一步复用已有有界UI goto；上一页有动画时保留原生previous_effect以重建已播放效果，不把它改成全页从头打开。

正式首次绿f16b1e…：26返回/0失败、正常exit0，第三次窗口4进入ticks5817580894889；
第二组begin5817580718079、end5817582556974，空尾begin5817582559017/end5817582564717，forward end5817582901772，频率10M。
该组约183.89ms结束，第三调用进入后约166.21ms，明显早于此前约1.05秒自然结束；随后实测页7。
这是已关联节点事件和实际页码证据，不是GPU完成时刻或所有文稿的因果回执。
诊断关闭的正式三轮5f430f…、c68f5a…及a0134c…各67返回/正常exit0（17.69/18.81/18.03秒），不依赖节点诊断实现推进。
模型有界预算与未知页拒绝保持；复杂自定义/隐藏页、重复/无限动画、末页带动画等仍需独立验收。

## 无动画页与视频边界

AllinOne立即下一步34fb1a…：9返回后页1不变，期望2失败；模型主序为空，没有追加空尾。
加初始1.5秒采样的对照7a5fac…正常，说明存在早期推进差异，但不能将固定等待当就绪协议或根因证明。
正式按无主序模型身份直接UI换页后f3922e…，不加初始等待，open1→next2（22.84ms）→previous1（17.18ms）、末页整页边界及正常关闭通过，23返回/0失败。
此项不宣称所有OPEN已具有原生画面就绪证据；有动画的初始页仍有独立门禁。
TestPPT视频页5cd066…：open3→效果调用/页3采样→下一步后页4→UI上一整页3→2→关闭，22返回/正常exit0（11.58秒）。
导入模型的播放命令主序很快结束，不能从PPTX声明158秒或这个命令的end推断视频时钟；本轮没有视频像素/音频时钟证据，统一媒体控制仍不支持。

## 视觉：发现异常，不只核页码

使用screenshot skill的Windows助手，仅截图本轮自有Form矩形，保存于系统临时目录。
首隐藏启动cc218f…截到桌面/Codex，四图无效，不能作为播出证据；旧图保留，不复用为成功。
视觉QA后改为仅visual模式显示自有Form、临时TopMost，结束窗口消失；没有修改正式承载/尺寸/系统显示设置。

正确四阶段图片：空白→“第六页”→标题及副标题→“第七页/结尾页 无提词器”，无编辑菜单或导航；QA Form标题栏不是产品编辑器。
原型3c54f1…目录`78ebf5e230014945952fef7def9ca227`的四图，及52d9ab…目录`e1b229655497476b933aff244ca1ac32`的回6/两撤销图由独立视觉agent复核通过。
正式31ec4a…目录`4424fdb8bbda471793f6454914db5e01`：初次独立复核误报undo-subtitle仍有副标题；2026-10-03后续重新逐张读原图，实际上只有标题，undo-title全空。
更正依据：undo-subtitle与title-only的完整PNG SHA同为7B4E94C2…DE6B57C，undo-title与before-effects同为ABE061F6…E8B4D5F7；文件创建/修改时间仍在原10:52阶段，未覆盖。字幕区域深色像素为0，独立复核再次确认无残留。该误读报告撤回，不把它继续归类为未定位产品缺陷，也没有为其增加重绘/等待补丁。
随后不加空尾的独立基线93152e…目录`b1c0a178d62c4e29a2763a4efc9070af`与正式复核ef3e43…目录`ce14a854cc2b4d4ca4c2263afd8f77ac`均显示回6完整、一次撤销只标题、二次撤销全空。
这些相关运行的原始身份均DPI96；先前口头提到DPI不同已更正；撤销残留又经原图核对确认为误读，两项更正均保留审计。不能由这些图证明跨DPI可靠。
上述截图均在`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-animation-shots/`；不是连续动画录屏、完整边界/缩放、其它样例或D4验收。
截图阶段1.5秒只为有限视觉采样，不是生产完成判据；程序保留effects_visual_verified=false，人工复核结论单列，不扩成整体通过。

## 验证与下一步

正式Host、隔离原型和公开Client Probe构建均0警告/错误；首次CLI属性误写Java式getter产生编译错误，按实际官方程序集属性修正，不算业务红。
完整非Physical985/985两次绿，最后六TRX在`.validation/008-effect-advance-final-navigation/`：255/264/41/127/212/86、零skip。
最后一处“无主序回退到有动画页保持原生重播”调整后正式Host重新构建/真实visual运行，前端本轮未改，沿上轮172/172，不冒称重新跑前端/实际控制台。
known-pitfalls超过500行，已将坑51起逐字移到`docs/known-pitfalls/libreoffice-runtime.md`并修相对链接，迁移正文一致性核对通过；旧编号/结论不改。
本轮所有自有Office/Host/Probe/监听/窗口退出，原件SHA保持，用户库/原图/失败/profile/dump保留；未提交/推送/访问/部署或启动D4。
T053仍需稳定撤销、末页带动画、初始就绪与复杂序列、真正快速多次点击和真实UI接线；T044/T045/T046与裁切/双窗60分钟/完整包仍未完成。

官方依据：[CLI本地代理身份](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/cli_ure/source/uno_bridge/cli_proxy.cxx)、
[SkipEffectEventHandler](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/usereventqueue.cxx)、
[主序子项](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/sequentialtimecontainer.cxx)、
[XTimeContainer](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/animations/XTimeContainer.idl)、
[原生回退计数/重播](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/effectrewinder.cxx)、
[Reschedule/排空区别](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/vcl/source/app/svapp.cxx)。
