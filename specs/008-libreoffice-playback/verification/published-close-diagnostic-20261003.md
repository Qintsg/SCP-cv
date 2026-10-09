# 已发布文稿关闭诊断与真实视频关闭复核（2026-10-03）

## 完成范围

T067修复正式LibreOfficeSurface正常文稿关闭中嵌套finally覆盖原错误：shutdown、client退出及viewport释放均尝试，单失败保留原类型/栈，多失败以既有PresentationTransportException保Aggregate全部原因。
PresentationCloseCompletion继续共享首次终态；重复CLOSE/Host Dispose不得变成功或重放shutdown。正常关闭顺序、请求20秒/客户端退出15秒预算、原件/身份/承载不变，不新增REST/协议字段或错误码。
此诊断切片完成，不等于完整T018故障矩阵、T044/T053动画完成、T045/T046两输出/完整组/长稳完成。

## 公开路径红→绿

ScpCv.Presentation.TestHost增加外部stdio模式：open响应匹配Confirmed，shutdown可明确Failed，EOF退出0/2；不启动Office。正式PlayerRuntimeHost OPEN后CLOSE、重复CLOSE和Dispose走真实LibreOfficeSurface，不用PresentationFactory或直接测试私有helper。
首测试构建CA1859要求精确Task<WorkerExecutionResult>返回类型，属于编译错误，不计业务红；修正夹具类型后业务红为expected AggregateException、actual IOException，原关闭拒绝被exit2覆盖。证据在`.validation/008-close-diagnostic-red/red.trx`，没有放宽断言或预算。
最小修复后同例通过`008-close-diagnostic-green/green.trx`；随后四条行为保护加入并全绿`008-close-diagnostic-related/close.trx`，共5/5：

- shutdown拒绝+exit2：两原因及原Code/Detail保持，重复CLOSE/Host Dispose是同一失败实例。
- shutdown拒绝+正常退出：原InvalidOperationException保持，不包装为双失败。
- shutdown成功+exit2：原transport/exit2保持，不伪造第二原因或假成功。
- 全部正常：CLOSE completed，actual源/布局为空、页码0，重复CLOSE/Host退出正常。
- 自有隐藏父窗已销毁+shutdown拒绝+exit2：第三个真实DestroyWindow/Win32错误也保持，不以最后一个窗口错误降为安全失败。

仅EnsureHandle的自有200×150隐藏STA窗口，不Show；销毁测试仅调用该自有window.Close，不操作用户窗口。测试环境finally恢复，沿既有禁并发测试组。受控Confirmed不是实际出画或Office关闭证明。
原单失败通过ExceptionDispatchInfo重抛，多个失败是本地诊断链；API和持久命令不因此自动返回全部内部堆栈。旧实际身份/引用保留也不证明失败后旧物理画面仍在。

## 当前全量回归

六项目重新构建、项目间串行Category!=Physical，1059/1059、0skip：Contracts287、ControlHost264、Domain41、Infrastructure127、Integration229、Windows111。
六份TRX在`.validation/008-close-diagnostic-full/`。本轮未重复前端全量及加载拒绝Physical；此前177和2Physical仍是历史结果，不计入本轮1059。
本轮唯一生产修改是LibreOfficeSurface关闭错误保留，不改变转换/宏外链/导航/能力或UI行为；完整应用与设备门禁仍单列。

## 两轮实际视频关闭

重编既有NativePlayerProbe引用正式Player及正式Framework Host，使用Resources只读副本、本机自有小窗口；不接业务库或D4，不替换外部adapter。QA工程旧assets有NU1900审计告警，未禁告警；正式项目及随后locked发布正常，不把它称业务失败或以关闭审计修。
第一轮capture-50d44e041df642c2b97f094c738b6a3e，run-cbaef7ccf825474fa0866850305f6b10，parent57948，DPI96，正常exit0/38.2889377秒。
TestPPT第3页触发媒体，前/早帧黑视频区，后期人物及模型画面真实变化；播放中换AllinOne第2页、旧视频副本立即独占读成功，随后CLOSE空actual/idle/none/页0/布局null，Host Dispose正常。
原图根`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/c9ebc926f20948b8ae88d8e565cf947b/`，六图由主代理及独立只读视觉复核；研究页无原视频/第3页残留，关闭全黑，未见菜单/导航/密码窗。
两个引擎host50756/Office31952+4756/port32031、host67256/Office18624+37524/port62339，均document_closed/termination_requested及job active0/members空，未强清作为正常证据。

第二轮capture-6754d806f72e4778ac9e142f2ecec962，run-ca019e628cc74211a0d9887da5ed751e，parent52944，DPI144，正常exit0/34.3899522秒。
TestPPT第3页媒体前帧有PiPER标题、早帧黑、后两帧人物/模型变化，直接CLOSE和Host Dispose正常；host42500/Office54240+51796/port36924同样有正常完整退出证明。
原图根`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/7e2c80d860b34921b5fd2d41d9ad9098/`，五图主代理/独立复核无残留/UI泄漏。媒体时钟仍null、音频未测，不能借图像变化启用暂停/定位/音量能力。

## DPI与图像测量边界

96 DPI原PNG800×450，视频后期底部连续全宽RGB0仅2行；144 DPI原PNG533×300，后期底部连续全宽RGB0仅1行，关闭图300行全黑。这是原图精确读取，未裁图/缩图或按工具观感估黑带。
截图助手GetWindowRect/CopyFromScreen没有显式DPI感知；533×300与800×450的比例符合144/96缩放，但本轮未证明所有坐标/捕获虚拟化行为，不能把它当播放器物理viewport尺寸或完成混合DPI满幅门禁。
当前只读Screen.AllScreens仍仅主DISPLAY1/2560×1440，没有第二非控制输出。未调整Windows缩放、播放器几何或承载，不叠第四个旧几何猜测；完整DPI和两输出另待验收。

## 包、文件与进程收尾

新包`.validation/008-close-diagnostic-package-981767e6885c434eaa63099f9b56b524`包含T067修复；五EXE Amd64、16只读运行依赖installed，Net10不加载UNO。测试host未混入，MediaMTX仍外配、whole_runtime_verified=false，仅构建未启动/部署。
开发/包Player DLL SHA均D52C7AE7D4B14FEDEB52C31C7877953D46AE60CC038D520496B7A9E8314E895B；Framework Host本轮不改，包仍877A79373B81C631F9E2226B6604DC9B8A37E6F5F977D5521503E0EB747488E3。旧430158包仅历史、不含此已发布文稿关闭修复。
发布进程临时代理scheme socks5后恢复socks5h，地址凭据/机器源/审计/锁/TLS不变；合法GUID临时stage5d7a6d8fd8834360b6050efc28dba42f已删除且核不存在，重建可恢复，最终包/用户数据保留。
三副本再次FileShare.None只读成功、SHA与原件一致；Resources原AllinOne SHA2401652F…A55F0、TestPPT0BBAFC49…25C22保持。本轮精确三Host/六Office/两个QA父及32031/62339/36924监听全部消失，无按名称全局结束。
Spec Kit、diff空白、UTF8/LF和单文件不足500行检查通过；官方缓存Redocly0错误、仍三项历史410告警。requirements16/16标记未改，无extension hooks。
本轮没有提交/推送/部署、D4访问、电源/墙面/系统音量写入。真实关闭与业务红绿为进展；复杂动画/末页产品选择、viewport承载讨论、真实宏/历史媒体、用户未保存编辑、两输出/完整组/60分钟仍欠，goal保持active。
