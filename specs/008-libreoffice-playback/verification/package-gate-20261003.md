# PPTX包拒绝与正常转换回归（2026-10-03）

本轮只在本机；D4、设备电源、墙面、系统音量、部署与推送未操作。原件/QA库/失败证据保留。

## 发现与修复

原Reader只识别TargetMode=External，而固定LibreOffice对其它非Internal token也可能按外部关系处理。
真实ZIP的TargetMode=true原先放行，回归1红1绿后拒绝，`008-package-gate/mode-red.trx`、`mode-green.trx`。
[固定版本上游实现](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/oox/source/core/relationshandler.cxx)用于解释差异；
门禁遗漏不等于已证明实际联网。

SlideOrder只核URI Host/path，会把https/ftp同host的绝对引用或带query引用解释为ZIP本地页。
三条真实ZIP回归全红后修正，`uri-red.trx`、`uri-green.trx`；
[OPC Internal相对引用合同](https://learn.microsoft.com/en-us/dotnet/api/system.io.packaging.packagerelationship.targetmode)为对应约束。
所有.rels拒绝明确非Internal模式或绝对Target，页引用另拒query/fragment；没有放宽外链或宏策略。

抽为共享纯ZIP/XML模块，Contracts/Net48宿主Compile Link同源，不把UNO代理加载到Net10。
默认wrap=none/spAutoFit/name/text匹配及TextAutoGrowWidth规则原样保留。
转换的包拒绝此前经export fatal→EOF/非零退出→transport uncertain；现在在作业/摘要校验后、引擎启动前检查。
动态候选在viewport/侧车创建前检查，拒绝不触碰旧画面。
catch仅围住纯读取，不涵盖原生加载、导出、关闭、已存在暂存输出或不确定传输。

## 包与状态证据

16条真实ZIP/XML测试覆盖模式、URI、DTD/实体禁用、正确相对引用、声明页顺序、原件摘要及独占读锁，全部通过。
Net48构建首遇旧框架无nullable注解的CS8602，显式null门禁后0告警/错误；未关闭分析器。

`008-reject-conversion/run-9e2b6a4428a543348fe98d5eb6cf0c83`使用全新SQLite、上传源和持久Running作业，
刻意配置不存在的宿主。External、true、非ZIP三类实际Convert返回Failed/presentation_package_rejected，
直接转换器未创建输出stage或启动侧车，正式准备逻辑记failed，原件摘要不变。
这是直接Converter拒绝证据：后台HostedService仍会先创建空stage，不能泛称整个后台流程零目录副作用。
非空stage仍由HostedService先记uncertain，未改成Failed或盲重试。

## 正常样例与显示关系

`008-production-conversion/package-487102520ffa45648d4d21a957572967`实际上传/中文目录/转换/发布/静态选择/移动：
AllinOne原/适配9页，TestPPT原/适配7页，四次正常关闭；在途移动拒绝、退出后移动与独占读通过。
约11.9/20.5/7.4/6.5秒，其中前两次与全解回归同时进行，不能用该负载数据宣称性能优化或退化。
Resources两个原件及上传副本摘要均保持。

独立fresh-eyes对比旧fit-ca19e同布局：32张PNG字节SHA完全一致、像素AE=0，四PDF SHA也完全一致。
PDF页数9/9/7/7、960×540pt、无表单/JS。沿用旧独立视觉结论：default两处窄标题问题仍在，fit消除标题问题但改变正文换行。
仅证明两样例的静态显示没有回归，不验证动画/媒体时钟或原生底部裁切。
视觉/比较报告在`008-package-visual/`。

## 完整验证与剩余

最新非Physical804/804、零skip（41/80/174/127/199/183），`008-package-full`；Framework宿主0告警/错误。
本轮前端/HTTP合同无变更，沿用此前结果，不冒称重新执行浏览器验收。
未完成：真实旧PPT/加密/宏样例、用户应用共存、全部原生故障和最终两窗60分钟；承载裁切架构讨论仍待用户答复。
