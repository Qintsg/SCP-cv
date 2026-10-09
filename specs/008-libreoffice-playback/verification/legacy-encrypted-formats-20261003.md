# 真实旧PPT、加密副本与文稿格式接线（2026-10-03）

## 当前结论

AllinOne只读原件的新PowerPoint97二进制副本已实际重载9页，正式Player小窗出画/整页导航/边界/CLOSE和生产转换发布9PNG/图片PDF通过，原件保持。
两个有效加密副本（ODP、OOXML）正确密钥可加载9页，无密钥被终止；产品动态OPEN拒绝后保留旧PPT画面/身份，候选正常退出与文件释放通过。
另真实发现媒体登记接受ODP/PPS/POT等而协议只允许ppt/pptx，T064公开红例后对齐，18相关及最终非Physical1044/1044通过。宏/外链/身份/期限约束不变，不新增密码功能。
加密ODP静态转换仍侧车exit2、作业uncertain，T065未修；T063总体仍未完成，不能把动态拒绝或新导出夹具当所有历史/宏/加密格式验收。D4未操作。

## 真实副本，不只文件头

`.validation/008-format-fixtures/`仅QA，官方Net48 UNO、独占fresh profile/Office job；原件Hidden/ReadOnly/MacroExecutionMode0/UpdateDocMode0，storeToURL新目标/Overwrite=false，未store或storeAs原件。
官方依据：[MS PowerPoint97过滤器](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/filter/source/config/fragments/filters/MS_PowerPoint_97.xcu)、[MediaDescriptor的Password/Overwrite/InteractionHandler](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/document/MediaDescriptor.idl)。不修改或编译LibreOffice。
首capture21a7a223…/parent7704：PPT和加密ODP对照完成，OOXML无密钥却抛明确检测终止异常，夹具只期待null因而exit2/21.52秒、自有job兜底。失败保留；不是正确密钥加载失败或产品故障。
夹具仅增加“已实际选择Abort的IllegalArgumentException”拒绝判据，fresh capturebc8edf7…/parent60424、Office24644/28740、port34051正常exit0/21.58秒。
验证目录`008-format-fixtures/run-bfbd7f1993cb4aa5918f86db7b5eca76`有三份完整文件，均重新只读加载9页、关闭后独占读及摘要保持；每个密码只在进程内生成/保留，没有写日志或产品配置。

| 文件 | 大小 | 对照 | SHA256 |
| --- | ---: | --- | --- |
| Legacy-AllinOne.ppt | 16062976 | 无密码正常加载9页；OLE头与实际导入同时确认 | 659232F24043030778F1DD5B7711A76158045CC49A4642D05308621A3631CD32 |
| Encrypted-AllinOne.odp | 15165991 | 正确密钥9页；无密钥2次交互/2Abort/null | 08DEB0DB2FFB6E8E6C4247B15CB1995485B50E3CF9CDB4046336EC6804C48AAC |
| Encrypted-AllinOne.pptx | 15313408 | 正确密钥9页；无密钥1次交互/1Abort/明确异常 | F4102474843BCB34A765942954C836DA3E18B5CB1FCC202510793708055C07A1 |

PPT及加密OOXML实际头均D0-CF-11-E0-A1-B1-1A-E1，头本身不证明格式/内容，本轮还取得真实导入与密码正负对照。未产生跨作者历史PPT、真正VBA宏、旧格式媒体或所有版式样本。

## 生产Player与接线根因

实际PlayerWindow/PlayerRuntimeHost→生产候选/真实LO、自有800×450/DPI96、默认fitfalse。旧二进制2→NEXT3→GOTO9→NEXT仍9→PREV8→GOTO2，实际页码及原样图分别核。
首capture9d21ac0d…/parent44416、旧host19780/Office69208+37724/port1506；ODP候选只有launcher16700/34076，15秒退出清理不确定覆盖原错误。源代码及公开策略红例证明请求在Send快照/结构校验先被invalid_source_path拒绝，不能把未owned日志当密码加载挂死或猜全部启动原因。
协议18项公开回归：ODP单红→绿，其余变体8红/3绿→全绿；已登记10种非PDF扩展对齐，未知、PDF、伪后缀/相对URL仍拒绝。v2规范字节/指纹不变，双TFM同版重建；混用旧白名单宿主仍可能拒绝，须完整更新。
第二captureda1f201d…/parent46900：ODP已经原生拒绝、候选host4164正常job为空并留旧；之后夹具持自己的FileShare.None又Digest同文件，自锁导致exit2/34.29秒，保留该QA失败，不算Office残留。
释放夹具句柄后第三fresh capture2d9d2b98…/parent37236、run88e4ff57…、旧host20024/Office32400+23872/52652、ODP候选host21680/Office67444+11904/52669，exit0/36.48秒。
ODP明确presentation_open_failed/InvalidOperationException且候选job成员空；加密PPTX在ZIP预检前拒绝/InvalidDataException，不启动第二候选。两次均实际source11/generation1/page2保持，最后CLOSE请求gen3→source=null/idle/页0/fitnull及旧job正常退出。
图在`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/03af89734a054bfda3adcd5fa520a6c8/`：legacy-page-2/3/return-2、after-rejected-odp/pptx、after-close.png。root及PPTX技能要求的独立视觉agent核六图，无密码/编辑提示、源串扰；关闭全黑。第2页/返回/两次拒绝四PNG完整SHA同为1BA80948CEF291F4D6205C52897467CE4D5A57110937C3BB18F59459DC2A71C5；agent初次白屏疑点经原图/像素核对撤回，不修播放器掩误读。不判满幅或全部历史格式。

## 静态转换的不同结果

新QA`008-formats-conversion/`先因未继承集中钉定而NuGet审计/旧SQLite2.1.11告警；继承正式Directory.Packages.props后匹配2.1.13、0警告/错误，不关审计/改生产依赖或源。
首rund47be4f1…/parent42304：真实PPT转换9页完成/host70800正常退出，但夹具stage用了root/staging，公开Publish正确拒绝越界并进程异常。只修QA为cache/staging；旧库经正式Reconcile变uncertain、两布局重试拒绝，不删数据或重放。
正确fresh run3e1f8351…/parent36816、capture5735ba71…：Legacy转换发布ready/9页/10文件、host55188/Office67388+37784/18332正常；加密PPTX纯预检Failed/presentation_package_rejected、stage不存在、公开PreparationState=failed；加密ODP加载null使Program(export_slides)未捕获的普通异常、host55776/Office12208+13196/4596故障exit2、自有job兜底，作业uncertain/0页，无盲重试。整个QA保持exit2/37.35秒，不标全通过。
T065处理这个已知加载拒绝分支与清理异常掩原错误，必须保留导出开始后/无法确认退出的不确定，不能广泛catch或把强清当正常。旧uncertain作业不改为failed/重试。

## 验证、最新包与收尾

最终六项目重新构建/项目间串行Category!=Physical：1044/1044、0skip（283/264/41/127/225/104），TRX在`008-source-formats-full`；18相关与两个红根保留。前端未改未全重跑，沿历史177，不冒称本轮浏览器/双输出。
含格式协议修复的新完整包`008-source-formats-package-64fbc8d20b0b4e7baa7aefeedb195ba1`，五EXE Amd64、16只读依赖installed，.NET10未载UNO。旧200bc0包有T062安全修复但没有T064白名单扩展，只历史；新包不等于全组/双屏运行。MediaMTX仍外配。
仅构建进程临时socks5h→socks5且恢复，地址/凭据/锁/审计/TLS不变；本次GUID临时发布stage删除可重建，最终包/旧包/库/源/失败/profile/dump都保留。
最终九份夹具/Player副本/转换上传副本独占读及SHA原值，两个Resources原SHA保持；自有窗口/Office/测试与本轮监听退出。没有D4/电源/墙面/音量/提交推送部署。
Spec Kit/git diff --check通过，官方缓存Redocly2.55.0为0错误/3个既有退役410无2XX告警；全部本轮文件UTF8/LF、PS1带BOM、单文件不足500行，requirements16/16，无extension hooks。
T063/T065及宏/历史媒体验收、末页选择/复杂动画/通用完成、viewport承载讨论、双非控制输出/全组与60分钟仍未完成。已再次询问末页选择，未收到答复，不擅定；整体goal仍active，本轮为代码修复和真实新证据进展。
