# 本机实施与技术门禁记录

最新补充（2026-10-07）：T076发布插件索引修复及D4视频/三格式音频/RTSP/SRT/UDP短测证据已核对，见[插件索引记录](verification/vlc-plugin-cache-20261005.md)。1140非Physical和3CLI守卫是2026-10-05执行结果，本日仅补归档、图像复核及任务清理，不重报为本日重跑。混合长稳、动态控制台及正式源码同步仍未完成。

**日期**：2026-09-29。当前为部分实施，不是LibreOffice完整交付或D4验收。

## 1. 保存点与范围

- 接入前提交：`549c57f08e0fc03149919edd3300015fe12b8e46`。
- 标签：`checkpoint-before-libreoffice-20260929`。未推送。
- 忽略的实验代码归档：`.validation/checkpoints/ppt-engine-source-before-libreoffice-20260929.zip`，57文件、3,359,424字节，SHA256 `4335B0A4F7F5869438F701670526E510DC174713FDF0816AAFF1C83C4C602788`。
- 保存点含006已知红例与007评估结果，不能当全绿发布版；原件、数据库、日志和证据不在Git恢复范围，未删除。
- 最新用户选择：全部LibreOffice、移除PowerPoint入口，不走源码fork。本轮先本机；未连接/启动D4，未操作电源/系统音量。
- `requirements.md`16/16通过；无Spec Kit扩展hooks。当前实施仍受T004门禁约束。

## 2. 官方互操作与纯播放门禁

独立探针/原始证据位于 `.validation/libreoffice-integration-spike/`，不是生产宿主。

| 项目 | 实测结果 | 边界/证据 |
| --- | --- | --- |
| .NET10直接加载官方CLI | 失败 | 编译0警告/错误不代表可运行；RealProxy TypeLoadException，exit2，`managed-net10.jsonl` |
| 纯C# .NET Framework4.8连接 | 通过连接门禁 | 官方GAC binding，明确自有profile/端口，`uno.ini`+`URE_MORE_TYPES=offapi.rdb`；`framework-connection-unoini.jsonl`，exit0；无Python生产桥 |
| 正常Frame打开/导航 | 有限通过 | 保持WinForms UI泵、串行MTA UNO调用；TestPPT7页、打开约2.91秒；不走已崩溃Hidden+ParentWindow路线 |
| 只读及去编辑UI | 未完整验收 | 设置ReadOnly/禁宏/禁外链更新；公开LayoutManager隐藏UI有局部图像证据，但完整viewport/GPU视频仍未验证，未做恶意宏负例 |
| 通用活跃媒体暂停/恢复 | 失败 | 实际媒体形状start/stopShapeActivity均false；不能以paused布尔值或静音代替媒体时钟冻结 |
| 协作关闭 | 超预算，慢点已定界 | 首轮20秒无回执；后续分阶段两轮均正常退出但presentation.end分别20.644/47.098秒，超20秒门禁；原生根因未判定 |
| PNG/PDF转换 | 未执行 | 需独立只读converter profile，不能复用播放实例 |

当前固定版本26.8.0.3的[官方源码](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/slideshowimpl.cxx)1270–1288将上述start/stopShapeActivity实现为NYI/false。这不证明所有替代接口都不可能，也不允许把未经证明的内部方法宣称可从binary调用。

首次退出超时的自有PID50220/30592/43148已按PID/start/path精确清理。12:49:50复核自有进程0、62211监听0、两副本文稿独占文件句柄可获得，摘要与Resources原件一致。`cleanup-ui-gate.json`原forcedCleanup字段误为null；以补充 `cleanup-ui-gate-verified.json`为准，原错误证据保留。强制清理不算协作退出通过。

### 协作关闭后续诊断

仅补充各边界enter/return/exception、UTC、managed/native线程及耗时；未改UI/媒体/退出代码路径。
第一轮重放paused=true，总exit21.473秒，其中end20.644秒；第二轮只把pause替成只读status，
paused=false，总exit47.160秒，其中end47.098秒。setModified/文稿close/desktop.terminate/Form.Close
均约1–59毫秒，最终自有PID/监听均0、无强制清理、原件SHA保持。

这只排除“必须paused才慢”，不证明取消pause或延长超时是修复。线程池自然调度差异仍存在：
第一轮打开/退出同MTA线程，第二轮不同MTA线程，后续需专用串行线程及静态/媒体对照，不偷改为通过。
分阶段原始日志/身份/清理见 `.validation/libreoffice-integration-spike/shutdown-diag/results.md`。

2026-09-29用户已接受统一媒体暂停/恢复/定位不支持时明确禁用，前后端拒绝不触发原生动作；这一失败不再阻断接入，但关闭/显示门禁仍未通过。没有切换默认路由，没有移除旧入口制造不能播放的中间版本。

## 3. 停机安全前置（T005–T007）

### 缺陷与修复

- 原`CompleteStopAsync`只退役Processing，Pending会转投下一组；旧两窗CLOSE可被新owner领取。
- 修复在现有WriteCoordinator事务内先核expected epoch及允许状态，再将Pending/Processing标记Superseded，清claim/lease并保留诊断；已有终态审计不改写。
- 同事务清当前窗口1/2和音频pending及args，严格推进变化投影的毫秒时间戳；不改源、Actual/Observed、播放状态及历史3/4投影。重复同epoch停止确认幂等。
- 停止入口总是执行确认清理并重新读取响应会话；重启先外部stop成功，再CompleteStop/BeginStart/start。stop失败保持Draining，不确认退出或启动新组。
- `IRuntimeSupervisorControl`为测试边界；生产DI仍使用原真实singleton，测试替身不作为原生停止证据。
- 独立复核发现仅最终epoch校验无法保护此前ResetAll/全局stop；新增单ControlHost singleton排他门禁，在最外HTTP边界取得并覆盖整个状态/外部IO/失败清理。忙时409 `runtime_operation_in_progress`立即拒绝，不排队、不变更意图或发外部动作；using释放，不把旧请求延迟到新代次执行。

### 红→绿与完整回归

| 验证 | 结果 | 原始目录 |
| --- | --- | --- |
| 停机事务原回归 | 18项：7失败、11通过、0跳过 | `.validation/008-stop-transaction-red/` |
| 修复+时间戳回归 | 19/19、0跳过 | `.validation/008-stop-transaction-green/` |
| 入口顺序原回归 | 4/4失败 | `.validation/008-system-stop-red/` |
| 入口/设备/实际控制器相关回归 | 15/15、0跳过 | `.validation/008-system-stop-green/` |
| Release全解编译 | 0警告、0错误 | 本轮root构建输出 |
| 首次全非Physical | 569通过、3失败、0跳过 | `.validation/008-safety-full-nonphysical/` |
| 代理环境单变量验证 | 相关11/11、0跳过 | `.validation/008-proxy-isolation/` |
| 清代理后全非Physical | 572/572、0跳过 | `.validation/008-safety-full-no-proxy/` |
| 跨请求并发原回归 | 4/4失败；其中2项迟到确认触发epoch异常、2项第二请求200而非409 | `.validation/008-system-concurrency-red/` |
| 排他修复相关回归 | 19/19、0跳过；包含4种shutdown/restart交错及正常完成后再次请求 | `.validation/008-system-concurrency-green/` |
| 加入排他后全非Physical | 576/576、0跳过 | `.validation/008-safety-with-gate-full/` |
| Spec Kit与diff检查 | 通过 | uv运行validate_specs、git diff --check |
| OpenAPI描述校验 | 通过 | pnpm运行Redocly；CERNET配置未修改，复用缓存包 |

首次全套的3失败均为localhost HTTP收到BadGateway：Hardware启动、该启动的fixture生命周期调用、Headless AllowedHosts。测试命令遗漏CONTRIBUTING要求的清proxy步骤，继承http/https/all_proxy且无no_proxy。仅在测试子shell清三项proxy，同一二进制相关11项和全套572项通过；未改产品、断言或机器全局代理，原失败TRX保留。

清代理后计数：Domain41、Contracts18、Windows78、Infrastructure80、Integration199、ControlHost156，共572；
加入排他后ControlHost160，其余不变，共576。两次全套均零跳过。

```powershell
dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore
$env:http_proxy=''; $env:https_proxy=''; $env:all_proxy=''
dotnet test runtime-dotnet/ScpCv.sln -c Release --no-build --filter 'Category!=Physical' --logger trx --results-directory .validation/008-safety-with-gate-full
```

### 不可扩大解释的边界

- 确认时退役全部Pending/Processing，不是“按命令的旧groupEpoch筛选”：现有CommandRecord没有该字段。停止确认瞬间的未完成意图不会保留到下一组。
- 完成停止必须已有真实自有组退出证明；本机替身测试只覆盖入口时序/库语义，不证明D4或LibreOffice原生退出。
- 排他仅覆盖同ControlHost尚在途HTTP启停；不能证明原生stop超时/取消后仍运行的helper已退出，也不约束第二个ControlHost或旁路操作。晚到原生动作仍需后续T008/T010明确身份与围栏，禁止宣称所有并发情形已安全。
- 未覆盖故障组尚存成员、转换/文件活跃锁与LibreOffice晚到操作；这些属于后续任务。
- 本轮没有变更REST形状或前端；补齐既有200/202/503与停机语义的OpenAPI描述并校验。未执行前端/浏览器；完整引擎接入后仍须执行，不将本轮后端全绿替代它们。

## 4. 剩余工作

T004去UI/真实媒体/正常关闭未通过，T008协议及生产Host尚未开始；默认LibreOffice、转换器、旧入口退役、运行组/前端/合同文档、两窗矩阵、故障注入、60分钟长稳均未完成。当前代码不可标为“完整LibreOffice已接入”。后续以tasks.md及明确范围答复推进，D4仍保持暂停。

## 5. 继续实施范围澄清

2026-09-29接受既有问题的答复1条，没有新增问题。`speckit-clarify`将答复写入spec的Clarifications、
US1/AC3、FR-006、SC-003及Assumptions，并同步既有设计/任务/合同。requirements质量清单
16/16→16/16，无新通过、回退或未勾选项；无扩展hooks。

| 澄清分类 | 状态 |
| --- | --- |
| 功能范围与行为 | Resolved：未支持统一媒体控制可禁用 |
| 领域与数据 | Clear |
| 交互与反馈 | Resolved：能力禁用及unsupported、无副作用 |
| 非功能质量 | Clear：关闭/显示门禁不放宽 |
| 外部依赖 | Clear：官方二进制/纯C#、无源码fork |
| 异常处理 | Clear：超时不明、禁止盲重放 |
| 约束与取舍 | Resolved：接受媒体限制、不接受假成功 |
| 术语一致性 | Clear |
| 完成条件 | Resolved：声明支持才要求真实媒体控制通过 |
| 占位/杂项 | Clear |

继续`speckit-implement`的T004关闭/显示诊断；并行T008纯值协议，不提前启动生产原生宿主。
上节“尚未开始”是前轮快照，后续实施结果逐轮追加，不将未完成任务提前勾选。

## 6. 接受媒体限制后的显示与重复关闭

主报告：`.validation/libreoffice-integration-spike/display-and-repeat-results.md`。
公开正常Frame、纯C#Net48、专用持久MTA；只读inspector真实ReadOnly=true、原件URL为受控副本，
页尺寸33867×19050、container/component1100×650。root实图显示约150%DPI的1652×1023窗口，
帧内无LO编辑菜单/工具栏/导航，诊断host标题栏仍在；页6正文经两次next从隐藏变可见，
页3视频1.5秒间隔出现实际变化帧。只对这个样本证明基本承载/顺序效果，不等于动画完全保真。

被遮挡、最小化或工具用户输入guard阻止的图排除，不假定WGC能穿过遮挡。一次临时区域截图
因捕获了非目标内容无效，已精确删除；有效实图由Computer Use在本对话内联返回，未重复解码/保存payload。
root的Alt+Space系统菜单诊断键导致index0→1，已由fresh status核对并排除错页配图，
不是API错页。后续只使用公开goto/next与只读截图。

| 实际轮次 | presentation.end | 全exit | 原20秒门禁 | 清理 |
| --- | --- | --- | --- | --- |
| 专用MTA最初单样本 | 242ms | 315ms | 通过 | 正常、残留0 |
| 可见显示轮 | 236ms | 340ms | 通过 | 正常、残留0 |
| 不抢前台重复第2轮 | 239ms | 305ms | 通过 | 正常、残留0 |
| 不抢前台重复第3轮 | 超20秒，追加观察仍无返回 | 无回执 | **失败** | 精确强清、残留0 |

最后一轮只open→goto index2→next→exit，paused=false、没有媒体暂停/shape控制；
context到end始终同MTA3/native52728，故线程一致性**不充分**。20秒失败后取证/协调时段很长，
约236秒仍未返回，不算延长门禁或另一次复现。第4轮未启动；client63852/engine49260/launcher50104
按PID/start/profile精确清理，62233空，原件SHA和文件锁恢复；强清不能算正常关闭。
两份MiniDumpWriteDump线程证据保留于repeat-media-round3，尚无符号栈根因，不猜GPU/解码/某vendor函数。

T004显示基本能力获得有限实证，但正常生命周期仍未通过；按speckit-implement暂停依赖它的
生产承载/默认引擎替换，独立纯值协议/只读转换诊断继续，不能把前端禁用选项当完整接入。

## 7. 纯值协议实现与双运行时验证（T008）

新库net48/net10.0同源编译，不引用UNO/WPF、不加JSON或Framework引用包。
固定DTO、角色与3种父身份、64KiB小端framing、严格UTF-8/对象数/深度配额，先捕获有限快照再
对可信currentScope、期限与能力校验；统一媒体暂停/恢复/定位硬unsupported无executor调用。
规范二进制字段顺序SHA、请求与结果指纹、所有11维scope、完成时间、确认观测与终态冲突匹配
已实现；accepted/uncertain不更新画面，shutdown仅记生命周期回执，不证明OS退出。
库只在Contracts.Tests引用，尚未接生产播放器、侧车、连接认证或运行组；不能拿整数/自报HWND
或指纹冒充OS归属、租约真实性。细则见库README。

| 验证 | 真实结果 | 原始证据 |
| --- | --- | --- |
| 首能力拒绝红例 | 1失败/0通过 | `.validation/008-protocol-red/` |
| 首green | 1/1 | `.validation/008-protocol-first-green/` |
| 参数policy红例 | 56通过/2失败（?及混合separator输出原件目录） | `.validation/008-protocol-policy-red/` |
| 完整codec初测 | 101通过/1失败（尾部第二JSON根未拒绝） | `.validation/008-protocol-full-green/`，目录名误称green，失败TRX保留 |
| 尾根/路径修复后 | 102/102 | `.validation/008-protocol-final-targeted/` |
| 再纳入3条纯回归 | 105/105、0跳过 | `.validation/008-protocol-105-targeted/` |
| 前次全套 | 678/678、0跳过，未含3条晚新增 | `.validation/008-protocol-full-nonphysical/` |
| fresh build后全套 | 681/681、0跳过 | `.validation/008-protocol-681-full/` |
| 双TFM全解build | 0警告/0错误 | root各轮构建 |

纯回归包含EOF/半帧/短读/取消/过大长度/UTF-8/深JSON/mandatory/尾根/引号括号、默认禁用、
参数安全、独立快照/超大可变输入有界、旧scope/指纹/确认观测和close/export/shutdown分类。
目标105是纯值与内存流，不包含任何真实GUI/Office/D4副作用。
全套计数：Domain41、Contracts123（原18+105）、Windows78、Infrastructure80、Integration199、Host160。

真实CLR互操作在 `.validation/008-protocol-cross-runtime/`：Net10生成帧→Framework v4.0.30319
实际exe解码/拒绝并重编码→Net10 v10.0.11核对，media_pause和open中文路径两fixture均返回0、
wire bytes完全相同、请求/结果SHA一致、native callback计数0。
最终中文open请求SHA `0C4ADB4C72A074A491C7D143D0A178C265725481FA2BC7473F15E6E9510D89AA`，
结果SHA `7ACE28F8DDC2867F047E2955971BB42977AB76CA81EF2D31B62F6C06FC4DB699`。
这是协议兼容，不是LibreOffice载入/播出/关闭完成。

## 8. 独立转换结构与视觉门禁

证据：`.validation/libreoffice-conversion-spike/limited-diagnostic-observations.json`；
独立视觉评审：`.validation/conversion-visual-review.md`。未接生产转换。

- AllinOne首批9张PNG均实际1920×1080、顺序无缺页，PDF真实9页；原件/副本SHA不变。
  首轮复合URL/modified保护报红，缺返回值，原红保留。仅补日志的有界诊断证明21个状态
  ReadOnly=true、URL完全相同且规范路径等价；modified在第7页PNG filter前false→后true，
  PDF前已true。内存dirty不等于物理原件写入，不猜chart/oMath内部根因。
  更正QA将dirty保留诊断，仍严格核RO/URL/实体SHA/锁/退出，没清标志来假绿。
- TestPPT独立实例PNG7/7实际1920×1080、PDF真实7页；所有modified=false、RO=true、URL/SHA保持，
  无setModified或源保存；文稿close65.5ms/terminate4.09ms，原生与监听正常退出、无强清。
  仅生成结构不证明全部可用静态内容或动态效果。
- 两样本converter所有UNO调用同一MTA、独立headless profile/62241；均正常退出，源/副本及产物
  exclusive读检查通过，profiles精确清理，无用户其它Office/D4操作。原始日志与产物保留。
- TestPPT导出PDF178,774,520字节（约178.8MB），实有6组EmbeddedFile/Screen/Rendition标记，
  pdfinfo JavaScript=no并不表示无媒体附件或活动annotation。不能标成无交互扁平静态PDF；
  未逐对象证实每个媒体映射或宣称这些媒体可在PDF播放器使用。
- 独立看完AllinOne9张PNG：第5页标题换行遮作者，第6页顶栏换行/上部裁切，第7页5个公式
  变为不透明黑块。源5个oMath的fallback PNG多数透明、角点alpha0且仅少量公式笔画，
  支持转换链可读性/透明度损失，而非原件刻意实心矩形；仍未区分导入或导出阶段。
  5/6页尚缺完整golden，不能硬归因某字体；1/2/9已参照旧原始PNG，1/2原图也有裁切不算新缺陷。

以上为2026-09-29阶段结论；后续状态以第9节为准，不回写历史失败。

## 9. 2026-09-30生产接入与继续验证

- 结束放映改用Office主线程的异步`.uno:PresentationEnd`（先parseStrict），再读回controller结束、关闭文稿和退出引擎。
  原专用MTA直接end的dump证据仍保留；不能从最近导出符号的偏移推定精确内部函数或Windhawk是根因。
  SDK独立新profile重复10轮正常结束约0.29–0.32秒，证据`async-end-20260930-c`及`repeat-1..9`。
  首个未parse URL的失败与62251旧实验残留单独保留，不能宣称所有历史进程均已清理。
- 生产Net48宿主、Net10父客户端和真实WPF已接通；正常Frame去菜单/导航，以独立隐藏候选子窗口暂存，
  原件摘要/OS父PID启动时间、会话和DPI核验。损坏候选失败后旧窗口可继续控制。
  侧车仅在OS实际父进程确认并加入独占job后放行；Office launcher暂停创建、入job后运行。
  未取得正常退出证明的兜底不作为成功，已传输后的取消/断线隔离通信并保持uncertain。
- PNG使用Translucent=true，第7页透明公式fallback黑块不再出现；9页独立视觉评审仍发现5/6页窄标题框换行/裁切。
  第7页原件wrap=none+spAutoFit被导入为AutoGrowWidth=false，单变量开启AutoGrowWidth恢复一行，原件不保存。
  第2页与旧参考不同，但旧参考的动画时刻/源指纹不足，不能据此确认原件完全相同。
  文字AUTOFIT实验能消除5/6页溢出，但不能未经选择默认缩小全部原件文字；可选处理尚待对齐。
- 生产转换已生成9张PNG和只含图片的PDF（约3.91MB），不直接采用带EmbeddedFile/Screen/Rendition的LO原生PDF导出。
  `008-host-probe/run-3c52597...`真实导出/正常shutdown成功，但父job成员即时核验曾失败；失败原样保留。
  带诊断的`run-f4bcbc...`成功且active=0/members空。微软job计数说明：终止进程的所有句柄引用释放后计数才下降；
  后续改为核成员实际HasExited与监听，不把对象引用残留当运行中，仍需多轮复测。
- 非Physical回归：`008-migration-fourth`681/681；新增原件占用8条红→绿（错误/空闲但ActualSource仍占用，移动/删除源或目录）。
  `008-migration-fifth`688通过/1失败，唯一失败为新增actual/generation字段后旧精确JSON字段断言，已更新合同断言，待最终全量重跑。
- 桌面1440×900、手机390×844真实Chromium：新设置、能力GET、静态模式选择、页面异常及横向溢出通过。
  证据`.validation/008-web-qa/results.json`和截图；IAB两次截图超时，只把其DOM/API读回计为通过，视觉由独立Chromium复核。
- 冻结版双WPF：10轮两窗同/异源打开→100次页/动画控制→关闭/重开，损坏候选失败留旧画面；
  随后连续3609.28秒，180次采样，两窗正常关闭，原件/副本SHA不变。
  证据`.validation/008-player-probe/run-b15543ca0ca34ef7a3d5a973c0739db8/events.jsonl`。
  此轮主要停在第6页，未覆盖所有媒体时钟；也早于后续PDF、只读观测、清理和语义兼容修复，不等于最终T019门禁。
- 当前仍须完成：最终版热切换/多媒体与故障注入、最终60分钟复跑、转换发布/静态PDF合同、前端原生控制态，
  安装打包及文档治理同步。D4、墙面、电源、音量、部署/推送均未操作。

## 10. 2026-10-02至03：媒体切页、实际布局与打包回归

本节是新的阶段快照，不覆盖第2–9节的历史失败。仍非完整引擎验收，D4继续暂停。

### 原生故障与有限修复

- 双窗旧直接切页在TestPPT视频页回退产生`MFPlay!CMFPInnerPlayer::NotifyMediaItemCreatedEvent`原生崩溃；
  dump为`soffice.bin.37160.dmp`，异常`0xc0000409 / FAST_FAIL_GUARD_ICALL_CHECK_FAILURE`。
  上游[异步媒体对象讨论](https://www.mail-archive.com/libreoffice@lists.freedesktop.org/msg359230.html)
  及[Windows视频页崩溃172071](https://bugs.documentfoundation.org/show_bug.cgi?id=172071)提供相近风险，
  不据此断言本机与上游bug完全同因，不修改源代码、CFG或用户第三方软件。
- 单窗、fit=false、无媒体暂停的30轮最小回退在第21轮卡住；原生动作未返回，45秒预算后兜底清自有job。
  `008-single-media-probe/no-fit-92ea8497...`保留原失败，第二个最小回退也失败并采样VCL主线程等mutex。
  首次debug采样误用了网络符号源，延长了暂停时间，不能拿该轮评估时延；后续只用本地符号缓存。
- 跳页改为公开`.uno:JumpToMark`异步UI调度，按当前放映列表取目标页而非假定文稿页序，
  真实页码匹配后才回执。30轮通过；随后原排版/适配各300轮视频页2→1、正常关闭、原件SHA通过。
  目录`ui-300-false-8631ec1e...`与`ui-300-true-69ed57dd...`。前者含短暂只读debug采样，后者无debug；
  不将两轮等同所有媒体、所有动画或最终长稳。
- 双窗新版一轮启动在30秒预算内未监听；未加重试或放宽预算。另一轮完成100控制、9个完整生命周期，
  第10轮关闭`ConfirmStopped`在5秒预算内失败，兜底不算正常退出，原始`run-dd2fc9d2...`和启动/退出日志保留。
  新增精确launcher/listener/job成员诊断，随后`run-45aef0581f684ee3814d9d715b91eb81`完整10轮/100控制/关闭通过。
  WindowsPS5的该包装器退出码仍为null，以动作事件、侧车正常退出及无残留证明其有限成功，不虚报包装器exit0；
  后续包装器启动即持有Handle，实际视觉轮已取得exit0。

### 真实视觉与尚未解决的裁切

- `visual-70903d.../run-e8f926cc...`的两窗：AllinOne页7图表/文字可见；TestPPT页2、页3各两次2.5秒间隔
  Computer Use实图有明显视频变化；页6两次动画后标题/正文可见。两窗无编辑菜单/导航，关闭约0.34秒，
  自有job为空、原件副本SHA不变，包装器exit0。图由工具直接内联返回，未另存或解码payload。
- 同时发现TestPPT页2/3/6下方约70px黑条。独立只读QA核原件16:9、白色master背景；已有PNG
  1920×1080及PDF800×450底行均全白，排除源设计/转换同形缺陷，不把视频已动当完整viewport通过。
- 只读尺寸诊断：父客户区、native SALTMPSUBFRAME与Frame container均800×450/DPI96；
  component加载时(0,78,800×327)，隐藏layout及放映后(0,0,800×450)。固定源码的内部放映尺寸
  来自ViewShellBase私有client-area缓存，外层尺寸不证明内部正确；实际视频大小又符合800宽源坐标，
  不直接归因于整页按380高等比缩小。
- 单变量`doLayout()`及实际450→449→450往返均未消除底条，实验修改已撤回。
  第三次单变量把候选父窗设为可见但移出自有客户区（不覆盖旧输出），黑条仍存在；该实验亦已撤回。
  按systematic-debugging的三次失败门禁，停止继续叠加同类修补，下一步须讨论承载方案。
  诊断日志/截图与失败保留；视觉人工门禁等待超时是QA流程失败，原生文稿随后正常清理，
  不能充当产品卡住或视觉通过证据。最终T004/T017/T019仍未完成，不开展最终60分钟验收。

### 行为修复与统一自动化

- 页图增加原件修订严格匹配，原排版/适配两个回归先红后绿；缺修订旧制品不静默使用。
- 抽屉按选定布局显示准备状态和页数，真实Vue/Naive UI DOM原9页/适配3页先红后绿。
- 增加可空`actual_fit_text`，仅随成功资源发布与fenced实际报告更新；失败换源保留旧实际布局，
  关闭/非文稿清空，旧/畸形同源观测不伪造新布局。实际观测不能由desired或OPEN受理推断。
  Worker页图/图片隐藏STA2红→2绿；认证HTTP/publisher8红→8绿，不是LibreOffice实际出画证据。
  前端“重新打开当前画面源”沿实际源/adapter/布局，明确actual=null不回退desired；5个真实DOM先红后绿。
- `fit_text`改变规范字节，纯值协议升级v2，旧版/未知版拒绝，默认布局false。
  版本回归9项7红2绿→完整Contracts139绿；新的Net10→Framework→Net10六组回环覆盖open/export
  两布局、unsupported media_pause、shutdown及各自v1拒绝；wire逐字节和结果SHA一致，无Office调用。
  首次export夹具把nullable父身份设为Guid.Empty导致拒绝，是QA夹具错误，修为null后全部通过，原失败保留。
- 发布脚本隔离Net10/Net48，拒绝覆盖现有包/不同摘要的同名文件，不包含退役PowerPointHost或用户数据。
  6项合同初4绿2红：子PS5继承PS7模块路径导致Get-FileHash缺失；改用内置SHA256流读后6/6，
  实际`-CheckDependencies`通过。尚未真正publish/安装/运行发布包，不把纯复制合同冒充包可用。
- 原脚本13项启动超时的早期全套保留；未延长预算/跳过。原脚本34项复跑、Host160项通过，
  间歇根因未确认。热切换脚本另外迁到新presentation设置合同，27项通过。
- 统一非Physical期望731项：Domain41、Windows80、Contracts139、Infrastructure98、Integration199、Host174。
  首次730绿1红仅为新HTTP字段缺少旧精确shape断言；同步正式合同后Host174绿，最终统一TRX另存
  `.validation/008-20261002-unified-final/`，实际731/731、0skip。没有改变断言为允许任意字段。
- 前端87/87、0skip，类型/Web构建通过；主chunk约1096kB的构建提示保留，未关闭告警。Spec Kit通过。
  Redocly复用pnpm既有缓存直接运行，0错误/3条410入口无2XX的预期告警，不捏造200。
  CERNET下载/包管理器自切换尝试失败保留，未改仓库`.npmrc`或清空node_modules；未用npm。
  NuGet锁还原首次漏洞源不可达，依仓库约定只清测试子shell代理后正常还原，全解0警告/错误，未禁用审计。

### 清理与继续边界

用户Resources原件SHA仍为AllinOne `2401652F...B82A55F0`、TestPPT `0BBAFC49...38F25C22`。
原件、数据库、转换产物、历史dump与失败日志均保留；本轮自有测试窗/Office退出已逐轮核对，未按名称全局清理。
剩余：底部裁切根因、启动/退出间歇风险、真实用户Office共存、较广故障与恶意/旧格式矩阵、最终60分钟、
发布包运行、最新浏览器/封装端实际布局界面。D4、设备电源、墙面、系统音量、部署和推送未操作。

## 11. 2026-10-03：重复关闭、真实发布包与当前主机诊断

本轮继续整体目标，但未取得承载方案调整答复，不再给底部裁切叠加第4个猜测修补。

### 关闭终态不可被重复调用擦除

Client和Surface先设disposed再关闭，首次抛错后的第二次Dispose直接返回，Host.CLOSE可能执行后续实际源清空。
纯重复回归及真实Client公共回归均红（`008-close-completion/red.trx`）；实际自有系统PowerShell只接客户端固定未知参数，
没有Command/File/脚本、没有Office，非零退出首次失败却被并发关闭擦除。
修复先发布同一TaskCompletionSource，再唯一启动外部关闭，成功/失败/取消/同步抛错均保留；不Task.Run改变WPF线程。
五项回归绿（`green.trx`），覆盖在途、16线程竞争、正常幂等、同步失败及真实非零退出；不能据此称原生风险已解决。

### 真实发布与有限运行

- 第一全新包`008-published-package-32894d72...`真实publish第一组件NU1004：全局-r win-x64传播到引用图，与锁冲突。
  失败目录/输出保留，未禁用LockedMode或改锁假绿；只以DefaultAppHostRuntimeIdentifier选择x64宿主、沿项目既有RID。
- 七条合同绿；新包`008-published-package-ae269053532b432fbb441b10d2b40b36`实际五组件publish成功，无PowerPointHost/用户数据。
  五EXE为AMD64/PE32+，两TFM协议SHA不同，Net48独立在libreoffice/，未覆盖Net10。
- 包内ControlHost在新Simulation根`008-package-smoke-0f66067d...`只监听127.0.0.1:20881，初始化、认证、真实SSE通过。
  未启动其它发布组件、LO/MTX；不能将控制台可登录提升为整个原生包通过。临时发布stage精确清理，包/数据/日志保留。

### 活浏览器与页面回归

- 自有IAB页20880。首次生产dist未配主机而用默认8000，preview环境不能改旧构建；通过/connect配实际20881后可达、认证/SSE正常。
  不使用shell HTTP代替页面证据，不请求D4或改变远程HTTPS约束。
- 页面发现实际请求20881，但品牌/开发摘要8000；七个公开/真实Vue DOM回归先红后绿，端口和六个API地址复用实际请求解析。
  重建后两断点均20881、六href均正确，未点击API链接启动另一任务；日志/.env文案对齐DataRoot、appsettings/环境/CLI及MediaMTX YAML。
- 手机390、桌面1440的scrollWidth分别380/1430，无横向溢出，浏览器error为空；依赖能力、三模式、fit默认关闭/明确开启、离线禁控实查。
  没有播放资源，不把模拟控制台当出画。图片在`008-web-qa/published-settings-desktop-fixed.png`、`published-settings-mobile-fixed.png`、
  `published-display-mobile-stable.png`。部分IAB图较模糊，文本以DOM为证据，不证明像素保真；开关过渡帧排除，稳定图与aria=true相符。
- 真实退出临时会话，恢复viewport并关闭自有tab；按PID/start/命令行精确停止ControlHost22780及preview12928，两端口空、LO0。
  用户应用、原件、库与失败证据未清理；域操作经验已写入本机use-my-browser对应127.0.0.1笔记，仅适用SCP-cv。

统一后端737/737、零skip（41/80/144/98/199/175），全解0警告/错误，证据`008-close-package-full`。
前端94/94、类型/Web通过，约1096kB的chunk提示保留；Spec Kit通过，OpenAPI结构无新变更。
整体未完成：原生裁切/间歇风险、最终60分钟、用户实例共存/较广故障、封装端与D4验收均保留原门禁。

## 12. 2026-10-03：生产转换闭环与启动路径诊断

### 静态模式元数据边界

原问题是畸形PDF元数据被共同兜底误报“页图未准备”，不是未捕获500。
新增29条选择器、8条认证HTTP边界；对象/数值类型、long/int边界、旧修订、默认/适配隔离及有效上界均覆盖。
选择器17红/12绿、HTTP4红/4绿后修正为TryGet及模式相关错误；42项相关选择器与8项HTTP绿。
证据`008-metadata-boundaries`，不增加64位SHA格式约束，不静默回退动态。

### 真实失败和单变量路径对照

- 失败根`008-production-conversion/run-851d648c...`、`run-660ef3c...`及`run-1a412aa8...`保留。
  文稿还未打开前ready失败。新增launcher_exit诊断先暴露Framework的GetProcessById对象不能读ExitCode，掩盖原失败。
- 共享暂停启动器在ResumeThread前缓存Process.Handle；同源Framework自有cmd退出7先红后绿，正式进程回归也通过。
  精确句柄保留终态，仍先加入自有job，未放宽监听归属/ready条件。
- 新鲜长profile根154字符，内部已有backenddb.xml为267，bundled路径预计更长；launcher返回-1073740791。
  自有soffice.bin.14132的WER/dump证实0xc0000409、subcode7 FAST_FAIL_FATAL_APP_EXIT、ucrt abort/terminate，
  与旧MFPlay subcodeA不同。离线cdb证据`008-native-debugger/startup-crash-14132.txt`。
  无完整C++异常文本/PDB，最近导出符号不是精确函数，不能泛称Windows所有>260路径必失败。
- 短根`cvt-27a4f92f`保持headless/job/预算，两个原件9/7页转换、监听归属和正常关闭通过。
  转换profile随后沿Player已有88字符临时目录、以EngineInstanceId独占，不再随DataRoot深度增长。
  原深度数据根`run-b1fddf674bf04652ba3cd99ddd183483`两原件再次通过；这项修正不是底部裁切第4次修补。
- 五条路径/进程回归通过。profile根120字符为保守工程预算，异常深TMP/TEMP在启动前拒绝，
  原件/制品仍由DataRoot管理；没有更改LO二进制/注册表/安全策略或把启动器早退当正常退出。

### 原件、双布局和发布

`008-production-conversion/fit-ca19e87b47ca42df8c32c066a4341790`：
实际MediaSourceService上传→中文目录移动→持久作业领取→LibreOfficeSlideConverter→PNG/图片PDF→Publish→明确静态选择。
AllinOne原/适配各9页，TestPPT原/适配各7页；各次约6.5–8.6秒，有在途移动拒绝、关闭后移动成功与独占读锁释放。
原件及上传副本SHA均不变；四个制品目录不同、请求/实体布局匹配。文件原件、所有QA库/产物/profile保留。
其中较早`run-b1f...`一次job计数仍1，HasRunningMembers为false、监听已消失；不只靠计数判断退出。
三个中断QA根经正式ReconcileInterruptedSlideJobs变uncertain，原/适配两类重试均拒绝，未直接篡改数据库为成功或自动重试。

独立fresh-eyes检查原布局16PNG、两PDF全页：无漏页、错序、页面缩角、静态底部黑条；公式可读无黑fallback。
AllinOne默认5/6页窄标题的换行/重叠/裁切与旧制品一致，不能标默认全视觉通过。
适配16PNG逐页、PDF全页联系表与关键页独立渲染确认：5页标题整行且与作者分开，6页蓝条标题无裁切，7页公式/下标正常。
未发现新增漏页/黑条/遮挡，但5/7/8部分正文换行改变（如“分子筛”跨行），不等于逐像素保真；默认仍关闭。
静态媒体仅海报，不代替动画/视频时钟或原生窗验收。
视觉证据`008-conversion-visual/review.md`及`fit-round/review.md`，同轮default/fit对照保留，不改原PPTX以迁就渲染。

### QA依赖与完整回归

外层Probe显式Import集中配置但未走NuGet自动导入，CentralPackageVersionsFileImported为空，
实际解析SQLite native 2.1.11并报告NU1903；不是正式产品已采用的2.1.13出了漏洞。
改同目录Directory.Packages.props包装，由NuGet正常自动发现；两个集中启用标志true，真实构建0告警/错误。
Probe实际native SHA为B7385D722C83FB52142A00477A726723745916D22A555711EE89834C1111FB2E，与正式库一致（SQLite3.53.3）。
无包版本变更、禁止审计或改锁；没有证据将该SQLite漏洞当成Office崩溃原因。

统一非Physical779/779、零skip：Domain41、Windows80、Contracts149、Infrastructure127、Integration199、Host183，
证据`008-conversion-full`。前端94/94，类型/Web通过，1096.24kB chunk提示保留。
最初QA命令误写type-check而非typecheck，明确失败；随后正确pnpm命令通过，未把失败隐藏成通过。
CERNET配置未变，pnpm自选版本等待较慢；未使用npm或改全局配置。Spec Kit/diff检查通过；Redocly0错误/3条退役410无2XX预期告警。
自有Office/Probe均退出，Resources两个原件SHA不变；D4/电源/墙面/系统音量/部署/推送未操作。
原生底部裁切及其它间歇风险、用户实例共存、最终60分钟和D4门禁仍未完成。

## 13. 2026-10-03：排队期限、真实stdio故障与父死亡

### 请求排队也必须有界

公开Client对A在途、B deadline=300ms的真实stdio用例先红：B等待超过2秒，测试外部WaitAsync报Timeout，
A放行后B收到负CancelAfter参数异常。旧gate只用caller token，deadline直到取得gate后才开始计算。
修复把链接deadline/caller/lifetime的预算前移，取得gate后再次检查；B未写入、明确取消、不poison，A与后续C成功。
红绿证据`008-transport-faults/queue-red.trx`、`queue-green.trx`。

新增外部测试侧车不引用Office，真实exe/stdio/启动门禁/自有job，而不是内存流替身。
六项回归覆盖排队期限、已flush半header的取消与完整迟到回复、非法长度、错误scope、截断EOF，
以及A在途/B排队时两次并发Dispose：A为transport uncertain，B为未下发取消，收条仍一份，精确子进程退出0。
失败收尾由缓存的精确Process.Handle和外部预算保护；兜底强清仍报失败，不能永远卡在finally或标成通过。
证据`fault-matrix.trx`、`fault-bounded-green.trx`、`final-transport.trx`（连原进程/退出回归8/8）。
这是Client传输门禁，不代替UNO媒体、原生卡住或窗口退出验收；未为测试添加生产HTTP/配置接口。

### 空converter父死亡（限定证据）

`008-parent-loss/run-6d7acecf...`、`run-3ef92456...`、`run-53406384...`三次均先记录自有监听归属和精确存活句柄，
只结束本轮父进程，Framework/Office在5秒预算内退出，实际约0.011/0.034/0.014秒，端口52802/65316/65340消失。
对应身份为parent/host/Office：28808/41828/44904、67928/28032/70804、38116/68304/52844；native日志与result.json保留。
只有空converter、无文稿/播放窗口；证明父job关闭的OS兜底，不证明ParentProcessLifetime timer分支，
也不能写成持有文稿的正常协作关闭成功。没有按soffice名称全局结束。

### QA接线和未解决采样

第一整解在`008-transport-full`有2失败：新fixture未纳入sln，整解Release引用构建Debug而复制旧Release，
received标记缺失；加入tests及六配置映射后夹具一致。未延长接收等待或放宽没有dispatch证据的断言。
`008-transport-full-fixed`另有旧即时job退出断言1失败，cmd精确句柄已退出7，但HasRunningMembers当时true。
单独复跑及128次8并发自有cmd都未复现；当前没有同次成员证据，不能猜作PID复用、引用计数或其它子进程。
新增可选同一次/同句柄的WaitForSingleObject、退出码、IsProcessInJob诊断，默认不采样、不改变判断，原即时断言保留。
该偶发根因未解决；不以job ActiveCount代替真实成员，也不通过延长timeout或删除断言制造修复。

最新整解`008-transport-job-diag`非Physical785/785、零skip：41/80/155/127/199/183。
最终相关8/8及Framework宿主构建0告警/错误；只是复跑通过，不覆盖前述偶发根因。
前端本轮无改动，沿用上一轮94/94与类型/Web结果，不冒称重新浏览器验收。
自有Office/Probe/TestHost已退出，未操作D4/设备/系统音量/推送或部署；008完整目标及原生裁切架构门禁仍未完成。

## 14. 2026-10-03：完整job退出与真实父身份/Timer

### 即时job断言的实际来源

4096次自有cmd、16并发，并行Contracts155项通过；job即时采样出现4次pending（轮14/306/345/355）。
launcher和成员PID不同。轮345的成员37364、轮355的50468在同句柄记录wait=258、exit=259、in_job=true，
是仍活的自有子成员；后一例随后读名为conhost且已经退出。两例诊断前即退出，不能补猜其名称。
各pending在下一次采样即消失，未将已退出对象引用计数当存活，也没有证据支持该失败来自PID复用。
因此cmd句柄回归改为仍在既有5秒退出预算内等真实完整成员，保留旧即时失败及原始诊断，而不是删退出断言。

Client也曾只等宿主，然后即时成员检查失败并清job。公开受限继承侧车证明此路径确会误报短活子成员。
最初普通Process.Start子夹具意外持有父stdio，WaitForExitAsync等待EOF而假绿（child-exit-red.trx其实通过）；
改为仅继承NUL、不创建另一个job/不breakaway后真正红：`restricted-child-exit-red.trx`，约233ms报残留。
修改只在原有同一15秒关闭预算内轮询完整job，不重新计时或改变HasRunningMembers。

正例加强为命名事件保持child：先确认宿主已退出0、child仍活、Dispose尚未完成，再放行，最终二者正常退出0。
30秒child负例15秒仍Transport uncertain，宿主已退出0而剩余child由唯一job关闭精确结束；重复关闭是同一失败。
相关10/10，`gated-child-budget-green.trx`，不以兜底后的消失假报正常成功；不是所有Office原生退出间歇风险的根因证明。

### 父归属与Timer（不用job/EOF推断）

Framework QA直接Compile Link正式ParentProcessLifetime源码，observer→parent→guard真实父链，
guard ready后主线程仅睡眠、stderr为独立文件，不创建自有job、不读stdin或父EOF。
先核父/guard存活及精确句柄，只结束父，要求正式`libreoffice_parent_lost`日志及guard退出2。
另三项只改变父PID、启动ticks或session：InvalidDataException、对应原因、退出2、无ready。
三轮共12/12，根`run-7a0339cae69d475c9331e5ac1572e406`、`run-ff6bb4a379ea450bb435b5e7549439da`、
`run-3c621dded3f44d71bb12f13c65b51553`；timer分别496/480/496ms。没有强清记录被计为成功。

同源逻辑已纳入仅测试ScpCv.ParentGuard.TestHost和正式PresentationParentGuardTests，不依赖忽略目录才能复现。
通过1个聚合公开进程回归（内部4项实证），`parent-guard-formal.trx`；不引用UNO/Office或进入五组件发布。
它证明父归属拒绝和timer分支，不证明PID复用、用户应用共存或持有文稿正常关闭。

整解`008-child-exit-full`非Physical787/787，之后加正式父监测并加强正例的最新整解`008-child-parent-final`为788/788、零skip（41/80/158/127/199/183）。
本轮前端/API无改动，不冒称重新跑浏览器或D4；历史失败与Resources原件/库均保留。
当前原生底部裁切架构选择仍待人类答复，最终双窗60分钟及完整原生故障/用户共存门禁未完成。

## 15. 2026-10-03：PPTX纯包预检与拒绝分类

16条真实ZIP回归、三类真实上传/作业/Converter提前拒绝、两文稿原/fit四次正常转换通过；全非Physical804/804，32PNG及四PDF与旧同布局制品摘要/像素完全一致，默认窄标题问题不据此消失。
具体红绿、失败分类、显示边界与剩余事项见[本轮包门禁记录](verification/package-gate-20261003.md)。

## 16. 2026-10-03：安全终态与上传接线

T038/T039/T041/T042、Broker停机展开T047及重连T048完成有限回归；最终后端827/827、前端100/100、零skip。
真实浏览器确认静态失败重试不重复上传、动态fit参数保留；夹具不启动Native。
红绿、真实外部门禁与剩余竞态见[本轮安全终态记录](verification/safety-terminal-20261003.md)。

## 17. 2026-10-03：实际布局预览闭环

T040的原/fit GET、列表、缺页重准备及前端实际观测/刷新闭环通过，最终后端876/876、前端122/122。
独立HTTP、DOM、桌面/390夹具与能力未验证边界见[本轮预览记录](verification/preview-layout-20261003.md)。

## 18. 2026-10-03：只读运行依赖诊断

T043的文件/平台/Framework/PE/五CLI绑定及设置页失败、目标切换反馈完成有限闭环。依赖、导航输入、更新隔离及动画事件的后续记录见[运行时诊断索引](verification/runtime-diagnostic-index-20261003.md)；静态检查及事件计数不替代原生视觉、完整加载或长稳验收。

## 19. 2026-10-03：创建候选与转换双原因诊断

T066公开Converter4项/实际Player adapter2项红绿、非Physical1054及另2真实Physical、真实转换/Player留旧关闭复核完成，见[创建与转换诊断](verification/creation-cleanup-diagnostic-20261003.md)。随后T067已发布文稿关闭五保护/1059非Physical、两实际视频关闭与新981767…包完成，见[正常文稿关闭诊断](verification/published-close-diagnostic-20261003.md)；包仅构建，本地异常链不新增API堆栈字段，原失败/原件及完整范围保持。

## 20. 2026-10-05：完整组有限长稳与大文稿上传

文件/网页正式两输出完成10轮切换、100条实际PDF控制和3613.7秒观察，240原图摘要核对、另一窗89图保持、资源同阶段有限稳定、正常退出及原件锁释放通过，见[长稳记录](verification/group-visual-soak-20261005.md)。
修复真实98MB PPT上传入口30MB默认上限后，1139非Physical通过；D4实际大文稿上传/7页转换、两窗动态短测、视频运动/暂停、实际末页持久回执及16原图独立复核通过，见[大文稿记录](verification/upload-limit-and-full-ppt-group-20261005.md)。VLC硬解警告保留；直播/音频/PPT混合长稳、控制台动态联动、封装端与正式源码同步等仍未完成，不标T074或整体完成。
随后MediaMTX纳入正式Supervisor组，三格式音频实际HTTP/Worker控制、RTSP/SRT断流及显式恢复出内容、另一窗保持和关闭有限通过；17原图逐张核SHA/查看、Host/task0、自有任务及监听清理确认。首次playing黑/灰、恢复连续运动与默认GPU/SRT告警不略去；前端271/类型/Web重新通过，见[直播音频记录](verification/full-group-live-audio-20261005.md)。
