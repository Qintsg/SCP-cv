# 创建候选、转换动作与后清理双原因诊断（2026-10-03）

## 有限完成范围

T066完成公开Converter及实际Player adapter的诊断切片：动作失败后退出也失败时，同时保留两原因，以既有PresentationTransportException保持uncertain；正常退出则保持原异常或原Failed结果。
这是本地异常链诊断，不是新增REST字段/错误码，也不宣称全部堆栈已经持久化或由API返回。原件、退出预算、身份围栏、重放规则和原生承载不变。
此前T065加载终止分支已通过；本轮不改其判定，不将未知清理降为failed。完整T044/T045/T046及整体目标仍未完成。

## 业务红→绿与夹具错误

外部ScpCv.Presentation.TestHost按--office-program末级目录选择受控模式，回匹配响应但不生成制品，或返回明确Failed；stdin EOF按模式退出0/2。它不引用UNO、不启动Office，也不进入正式包。
Converter测试通过真实独立SQLite、公开上传/Claim/LibreOfficeSlideConverter，使用8字节OLE头占位文件，不把它当实际PPT格式或出画证据。
Player测试调用公开PlayerRuntimeHost，使用正式LibreOfficeSurface及真实stdio客户端，而非PresentationFactory替身；隐藏STA窗口仅EnsureHandle、不Show，候选不得发布。

- converter-red.trx：制品缺失后exit2，原FileNotFound被Dispose覆盖，断言两原因真红。
- player-red.trx：首夹具未提供content_digest，KeyNotFound属于测试设置错误；修正后另存player-business-red.trx，实际OPEN拒绝被exit2覆盖的业务红成立。
- converter-response-red.trx：原Failed响应后exit2也丢失动作诊断，另一个业务红成立。
- 首生产实现编译遇CA2219，属于构建失败，不计业务红；改用显式单次关闭而非finally中抛异常，未禁规则。

原证据在`.validation/008-cleanup-diagnostic-red/`，未覆盖。修后green/related目录保存对应绿：Converter4/4（抛错/返回Failed分别配退出0/2），Player2/2（OPEN拒绝配退出0/2）。
测试环境变量在finally恢复，测试组禁并发避免污染其它测试；不会借外部25秒QA预算扩大客户端生产退出预算。

## 最小实现

LibreOfficeSlideConverter把既有转换主体移入小私有函数，外层唯一负责Dispose。动作异常+清理异常以AggregateException保持原异常实例；清理成功用throw保持原栈。
返回非Succeeded响应+清理异常时，Aggregate第一项是保留原Code/Detail的显式诊断异常，不冒称它是原生抛出的异常；第二项仍是实际清理异常。正常响应/正常退出行为不变。
LibreOfficeSurface在OPEN失败时尝试自有client及viewport清理，收集原动作和所有清理原因；仅动作失败则外层throw保持原异常，多失败保持typed uncertain。候选没有发布，没有盲重试。
本切片不改变构造函数之前的失败路径、成功文稿关闭策略、UI几何或动画完成协议。

## 完整自动化回归

六项目重新构建、项目间串行Category!=Physical，1054/1054、0skip：Contracts287、ControlHost264、Domain41、Infrastructure127、Integration229、Windows106。
另显式运行真实LibreOfficeLoadRejectionPhysicalTests为2/2，不计入1054；已有输出负例仍uncertain且证据保持。七份TRX在`.validation/008-cleanup-diagnostic-full/`。
本轮前端没有生产变更，177是此前全量结果，本轮未重复运行或冒称真实控制台已验收。

## 真实转换与Player复核

三源生产转换新根`008-formats-conversion/run-3c0a9a75ad4e46c7a1c091981d4145e9`，capture-ec442124ff0e433cbc54bc7dc53679a4，parent59876正常exit0/26.81298秒。
旧PPT公开ready/9页/10文件；加密PPTX仍纯包Failed无stage，加密ODP为原生Failed/load_rejected及空stage。两个实际Office job均正常active0/members空。
旧PPT9PNG与图片PDF同上一run-08d02a7abe424347aa54369d4f4e9f71完整摘要10/10相同；没有借静态导出来证明动画/媒体/满幅。

实际正式Player新根`008-native-player/run-77f09d0603294874ab64ae374f5d0eeb`，capture-3e6f1709aeeb41a8ae10f527265295d4，parent30584正常exit0/44.5480591秒。
旧PPT从2→3→9，末页NEXT保持9，再PREV到8、GOTO回2；加密ODP原生拒绝及PPTX纯包拒绝均保持source11/gen1/page2。明确CLOSE后actual为空、idle/none、页码0、布局null。
六原图位于`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/20fc0fe4bb2f484cb24435128bb2d886/`。主代理及只读视觉复核确认2为轨迹研究、3为材料研究，返回2及两次拒绝后三张仍为2，无菜单/密码提示。
四份第2页PNG完整SHA均为1BA80948CEF291F4D6205C52897467CE4D5A57110937C3BB18F59459DC2A71C5；CLOSE图360000像素全黑。一次工具误呈PPTX图为空，原图/返回数据相同后撤回，不算产品失败，不加重绘补丁。
这是800×450、DPI96自有小窗样例，不外推至两物理输出、其它PPT、复杂动画或D4。

六份本轮上传/Player副本再次独占只读成功，SHA分别与原有效夹具一致；Resources两原件也保持原摘要。
转换host55264/Office25520+22552、host4920/Office58724+58044；Player host66828/Office25548+39744、候选host63204/Office70180+18084均退出。20956/40359/59426/38626监听均消失，无按名称全局清理。

## 当前包与收尾

新完整包`.validation/008-cleanup-diagnostic-package-430158c1ff68410db853491d21a68c24`仅构建、不启动/部署。五EXE Amd64，16只读依赖installed，.NET10域未加载UNO；测试host没有混入。MediaMTX仍外配，whole_runtime_verified=false。
包/开发ControlHost DLL SHA均22F7F73DA54DA8B1789DC27B14094B9ABEDF3340E4F0C3EC3AAAF9D616EF4368；Player均B88F8B898E8F0DD576919B40FA4DD0D48F40B137CF521F021CF65FC21D3B53F8。
Framework Host本轮未改，包SHA仍877A79373B81C631F9E2226B6604DC9B8A37E6F5F977D5521503E0EB747488E3。旧baf5c4包仅历史，不含本轮主调用方诊断修复。
构建时仅进程代理scheme临时socks5后恢复socks5h，地址凭据/机器源/审计/锁/TLS不变。脚本只删除本次合法GUID临时发布stage，已核不存在，重建可恢复；最终包、原件、库、失败、profile/dump均保留。
Spec Kit、diff空白及官方缓存Redocly校验，仍保留三项历史410告警；文档/源码UTF8、LF且不足500行。无extension hooks、requirements16/16标记未改。
本轮没有提交/推送/部署或访问D4、电源/墙面/音量。快速动画规则沿已接受的“快进当前再推进”，但末页产品选择、复杂序列、viewport承载讨论、宏/历史媒体、完整组/两输出/60分钟仍欠；goal保持active。
