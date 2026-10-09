# 已知转换加载终止、正常退出与不确定保护（2026-10-03）

## 完成范围

真实加密ODP在静态转换中被交互handler终止、没有模型/组件/输出时，现在返回Failed/presentation_load_rejected并正常退出自有Office；公开作业为failed、原件保持。
有旧输出、没有Abort证明、仍有组件/无法读安全状态或导出阶段错误仍不走该分支；关闭不确认继续typed uncertain，不靠强清证明失败可重试。
T065的已知加载分支完成；原错误被后清理异常覆盖的诊断需求另由未完成T066承接，完整范围没有删除。T063本轮生成的有限旧PPT/加密夹具流程也完成，不代表T045全部宏/历史媒体/版式或整体目标完成。

## 真实红→绿

新增仓库`LibreOfficeLoadRejectionPhysicalTests`，用显式SCP_CV_TEST_ENCRYPTED_ODP指向已验证的自有加密夹具，只读上传到新SQLite根，不携带密钥或修改原件。
首公开Converter红例取得PresentationTransportException/exit_code=2，TRX`.validation/008-load-rejection-red/red.trx`保持；即此前加载null被SlideExporter普通异常抛出，宿主仅捕获open而export_slides fatal退出。
修复后同测试绿`.validation/008-load-rejection-green/green.trx`，实际已盖章Failed、0页/空stage、公开状态failed、文件摘要/独占读保护及正常客户端Dispose均通过；不是纯mock结果或仅页面字段。
随后2/2 Physical（`008-load-rejection-related/physical.trx`）覆盖正常加载拒绝与“已有stage证据”：后者仍typed uncertain，prior-output.txt内容保持，不能用加载规则重放或删旧输出。
Physical用例显式运行；缺本机有效夹具配置即说明失败，常规Category!=Physical不启动Office或默默把它当通过。没有把它计入1048非Physical。

## 实现与安全边界

RejectInteractiveRequest仍只选择Abort，不批准密码/修复/保存/外链；新增逐实例原子Abort事实，动画/播放默认新handler行为不变，不写日志/密码或回调重入模型。
独立PresentationConversionLoader仅包住loadComponentFromURL：实际Abort后未返回模型、独占Desktop.getComponents无元素、stage存在且无任何文件/目录才抛typed PresentationLoadRejectedException。没有Abort的null仍普通原生错误；已返回模型的任何后续导出/关闭不由该helper捕获。
若加载异常后的安全验证也异常，保留原错误+验证错误Aggregate，不假称已知拒绝。宿主只捕获export_slides的typed加载拒绝，重新核空输出/原件SHA，再生成当前scope/请求指纹匹配的Failed结果，无成功Observation或虚构制品。
父Converter既有await using不改：返回失败前仍等待真实stdio EOF、自有引擎退出和完整job成员；退出失败会覆盖返回并抛typed uncertain，不能靠失败响应提前落已知失败。原请求/关闭预算、父/job/fencing均保持。
四个同源纯文件guard用Compile Link实际exception验证缺stage/空stage/已有文件/子目录，0副作用；它们不引用UNO，不当Desktop真实组件或退出证明。真实2项Physical是对应原生层证据。

## 三源完整实际复核

新根`.validation/008-formats-conversion/run-08d02a7abe424347aa54369d4f4e9f71`、capture-de33c11f96054295b988285ef6498ff3、parent7928正常exit0/26.51秒。
Legacy-AllinOne.ppt正常生产转换并公开发布9页/10文件/ready，host57992、Office6120/67332、port15032，正常document_closed/termination_requested/job active0/members空。
Encrypted-AllinOne.pptx仍纯包预检Failed/presentation_package_rejected、无stage、不启动Office；Encrypted-AllinOne.odp则原生Abort后Failed/presentation_load_rejected、存在空stage、0文件/公开failed，host64184/Office30308+44360、port5969正常EOF退出、job active0/members空。
三份上传副本关后独占读及SHA与已验证夹具一致；原Resources保持。新旧PPT9PNG+图片PDF与上一run3e1f8351…逐个完整SHA10/10相同，没有因拒绝修复改变正常导出或默认排版。
旧加密ODP作业的uncertain、首夹具失败库和原fail/dump/profile仍保留；新代码没有Reconcile/Fail重写这些旧终态或自动重试。

## 自动化与当前包

六项目重新构建/项目间串行Category!=Physical最终1048/1048、0skip（Contracts287/Control264/Domain41/Infra127/Integration225/Windows104），TRX在`008-load-rejection-full`；Net48正式构建0warning/error，另2/2 Physical与4guard。
本轮前端未改，177仍历史未全重跑，不将后端失败分类证明当本轮浏览器/双输出验收。现有HTTP/SSE字段不变，只新增既有字符串错误码。
新完整包`.validation/008-load-rejection-package-baf5c4113a87435d8b714760d3af9add`包含此修复，五EXE Amd64、16只读依赖installed、.NET10域未载UNO。开发/包Framework Host SHA均877A79373B81C631F9E2226B6604DC9B8A37E6F5F977D5521503E0EB747488E3。
旧64fbc8…包仅历史、不含此加载修复；MediaMTX仍外配，全组/两物理输出未运行。发布仅构建过程临时代理scheme socks5后恢复socks5h，不改地址凭据/机器源/审计/锁/TLS。
脚本只清理本次合法GUID临时stage，重建可恢复；最终包、原件、库、失败/制品/profile/dump不删除。自有Office/QA/窗口与本轮监听退出，未部署/推送/访问D4、电源/墙面/音量。
Spec Kit/git diff --check与官方缓存Redocly校验复核；原三项退役410无2XX告警保留。新文件/编辑文件UTF8/LF、单文件不足500行，requirements16/16，无extension hooks。

## 后续仍欠

T066创建候选/转换原始动作与后Dispose错误组合仍待公开回归/修复，不能据当前helper保Aggregate当那两个调用路径已修。
宏/跨作者历史格式/媒体、末页选择/复杂动画与真实完成、viewport承载讨论、两个非控制输出/完整组和60分钟仍未验收；goalactive，本轮实际业务红绿/新原生证据/新包是progress，不complete或blocked。
