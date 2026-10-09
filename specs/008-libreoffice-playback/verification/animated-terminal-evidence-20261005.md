# 动画末页事实与D4红绿对照（2026-10-05）

## 先验证启动后模型修改

T069继续沿FR-014/015执行，不把单页保持图或同页码当无播放动作。
独立QA先保留正式主序空尾/重复危险保护并启动放映，再按实际无后继页移除自有空尾、设End.NONE、重显示。
第六页唯一CustomShow在D4 parent18392/start UTC18:24:17.3971454正常0，三轮保持/撤销/关闭通过。
随后加强前提：先完成一次真实重显示，再修改模型，再取得第二次重显示。
parent10656/start18:33:29.3040059正常0，日志before0/after1→first_replay→children3变2→before1/after2，
三轮保持/撤销/关闭仍通过。两次只改只读内存模型、不改原件，不授予noop。
这只是缓存/重显示的有限实证，不证明所有页序/自动动画或共享重复模型安全。
源码研究仍固定26.8.0.3，实际页序逻辑见
[控制器](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshowimpl.cxx)，
首次动画导入/加载标志见[Slide实现](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/slide/slideimpl.cxx)。

## 正式实现切片

PresentationEffectAdvance预绑定原主序、原末组和本宿主添加的空尾；终点保持只移除自有空尾，不按时长猜测删用户效果。
启动后捕获实际controller列表，最后项/最后可见项的模型采用保持；其它主序继续原空尾推进。
初始受控UI重显示覆盖同页，不按currentIndex相同提前返回，仍5秒预算，以真实transition发生检查。
终点仍读getNextSlideIndex，不按文件页数或当前==总数猜。

PresentationTerminalListener每轮不可变epoch、冻结官方CLI本地OID目录，只记录主序/最后组的begin/end和过渡。
回调无UNO属性读取/IO/等待，16条纯值buffer；争用/溢出使证明失效，不能凭随后end补造完整事实。
owner只在匹配模型、本轮主序活动、最后组已begin/end时授予耗尽；旧epoch、其它/未知模型不授予。
倒退新轮只继承已观察的活动主序，不继承末组/耗尽；过渡/主序end撤销事实。
旧listener先retire后从捕获controller移除，晚快照不盖新epoch；原生移除/退出失败仍不确定，不全局kill或盲重放。
noop在resize/goto/原生效果调用之前返回，保留实际源/页码；既有协议v3/Player/持久精确回执/前端提示路径不另改。

实际自定义页序重复引用同一模型目前提前拒绝，原因是书签导航无法区分重复occurrence，且保持属性会共享。
这是页序重复限制，不是禁止所有重复动画；可选择不重复页序或手动静态页图/PDF（标准文稿页序、无动画/媒体）。
主序重复>=2/无限的既有原生危险拒绝保持。多个主序无法绑定时也明确拒绝，不静默删效果。
重复页拒绝的独立真实负例、其它模式/隐藏页/零时长/PAR重复回归仍须后续补，不据设计宣称已验证。

## 测试优先与实际红例

纯值模块先有效生命周期红→绿，再丢事件假耗尽红→绿、未知模型0假耗尽红→绿。
旧epoch/其它模型、缺主序/末组begin、倒退新轮与过渡/end等共12/12。
完整非Physical1135/1135、0未执行：41/322/127/277/241/127，六独立TRX在`.validation/008-terminal-evidence/full/`。
Release solution/Framework/QA0警告/0错误；NativePlayer QA首旧NU1900缓存告警保留，既有audit配置重新restore后0警告，不屏蔽审计。
本轮前端源码/REST结构未改，上一轮263/类型/Web不是本轮重跑；原有末页反馈DOM还须最终同版全组复核。

为避免“图相同即绿”，更新自有实际Player QA要求每轮extra1..9均返回presentation_at_last_slide。
旧启动后保持候选与相同新版父测试在D4 parent2520/start19:12:36.4791033：
第一轮完整/extra原PNG相同，却extra1返回completed/ok，业务硬断言失败，task/parent2；
其自有document_closed/termination_requested/job0，失败不是Office崩溃或正常通过。
旧候选EXE仅在新QA根重命名供同父测试对照，不冒称正式Host版本；旧日志/源码/原图远端保留。

## 当前版本D4绿色

仅QA设置第六页唯一CustomShow，其它末页绑定/事实/listener/协议/关闭链接本轮正式实现。
新根`D:\SCP-cv\.validation\008-d4-terminal-runtime-1791140898884`，
parent12548/start UTC19:15:04.6851915/session1，host9024，engine67cc2dc0-d46c-41d0-8cd2-3a082c72f4a8。
窗口2 DISPLAY3，物理800×450@(2000,80)-(2800,530)，控制DISPLAY1及其它输出不动。
三轮首extra及额外27次均completed/presentation_at_last_slide；Host共30条terminal_noop日志，
边界不进入Control的resize或效果推进。每轮两效果/撤销后重进仍可用，第二轮第三next即时控制也正常。
完整/extra各轮原PNG SHA `8D7993F3FCAABEDEDABDEFC27F7D014F3997DC2C3721D2E9A47A60F6B5980190`，
副本98756629字节独占读、原件/副本SHA `0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`保持。
关闭、EOF完整job正常，task/parent0，结束19:15:32.8517274Z；不把进程兜底消失当正常退出。

正式Host EXE `4FD341D04388215C9725348FFADF15509363A7F8E8F371E3808A9BE66977DBC4`；
QA单页EXE `857B09E808298A4A5847056EC4533C8C9117FBE383361347B56265569D3A4AEE`，不是同一个EXE。
基础QA补丁DA2166E4…5E4DB29，最终未知模型保护补丁6A3A3734…7B92BF3。
绿色证据ZIP `F74427C9F93DAFFD9AAD08FE9E6AAE6803CA7DF42351B0AE9FF2E327B34FD958`本远核SHA，
保留`.validation/008-terminal-evidence/d4-green/`的原图、日志与副本。
按pptx技能独立逐图复核10图：三轮标题/字幕完整，extra未进入黑色结束屏，撤销无残字，关闭黑；
只读逐像素测量complete/rewound均仅2条全宽RGB零底行，关闭450行全黑；
不凭目测把底部估计成几十像素或修改图验收满幅，轻微差异登记后不追求逐像素保真。

四个自有任务均Ready/instances0后精确移除（0/0/2/0终态保留），最终OwnProcesses0/ProjectServices0、远端git clean。
只删任务登记，包、库、原图、失败证据和profile保留。未操作电源/墙面/音量/防火墙/策略、用户Office或业务库。
QA上传不是secondary源码推送/拉取；尚无完整正式运行组/两窗同步/60分钟证据。

## 剩余

T069仍不勾完成：普通完整文稿/隐藏尾页与手动隐藏项、GOTO/重开/换源、零时长/最后PAR重复、
事件丢失/晚回调的真实宿主矩阵、两窗及完整精确前端反馈仍须复核。
新限制不是删除这些门禁，也不把单页QA当最终发布Host实播。
T071/T072/T073/T074、UDP恢复/登记API/最终混合100控制与60分钟保持完整范围。
shuorenhua只整理文档和汇报，不改事实/失败终态。
