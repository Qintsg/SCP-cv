# 本机实施与技术门禁记录

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

T014结构路径有实证但视觉/静态PDF语义仍未通过，不移除旧转换入口或假报全替换。T004正常关闭
仍为核心阻断，T009及以后生产接入/前端/安装/完整文档/60分钟门禁未执行。
