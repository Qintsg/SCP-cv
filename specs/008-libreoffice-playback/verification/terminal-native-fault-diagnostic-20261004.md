# 重复主序原始错误与合法零时长夹具（2026-10-04）

## 本轮变化

仅独立QA：在动作catch前记录qa_operation_failure，保留原失败与后清理错误；随后额外把自有Office stderr接新profile文件。正式Program/进程job/动画策略不改，没有新的动画noop或普遍稳定性声明。
合法zero-leaves样例取得真实初始节点事件、完整文字图、关闭空黑与正常退出；repeat2仍失败，但更早的observe错误与Windows同实例报告明确了原生异常退出，不再仅看Close的CLI注册栈。
T069/T070及全目标保持未完成，重复原因仍须原生调用栈/对照证据，不把KERNELBASE位置或stderr警告当根因。

## 原动作诊断

新小QA宿主ZIP D8A00FE9E00C47872373A253DA8055F4D3559EAB0C9AB79874B6226B66CE7DA2，复制旧QA客户端/原件到D4新根`.validation/008-d4-diagnostic-e87d22648d5d41a88e36ef4f28c1b1bf`，不覆盖旧包或源码。
repeat2运行parent11120/host17124、Office17324/11628、port57725、engine1d74345a…，task/parent2，job0仅兜底。
qa_operation_failure首先在observe/getController记录DisposedException “Binary URP bridge disposed during call”；之后Close/removeSlideShowListener的错误为“already disposed”，不能拿后栈替代前错。
Windows Application Error1000及WER1001在本轮时间窗指向同一Office11628（0x2d6c）及启动时间，APPCRASH soffice.bin26.8.0.3、KERNELBASE+0x43b19、异常代码0x00000000，报告2e009c67-cb2f-4800-b5e7-97f6f7892f7f。
这证明本次原生实例异常退出；该模块/异常码不证明具体业务根因。WER归档只有Report.wer，事件提及的临时mdmp已不在，无可用故障栈，不能假装已分析dump。
完整QA日志与原Report.wer保存于证据ZIP12134F76B9A06E2C2D3E9FF77A5D04D89AAFB4D18E2BE4AAFE311FF51AA02E61，已核远端/本机一致并原样解压。

## 合法自动零时长显示

同诊断包另新实例，window1/DISPLAY2，自有800×450；parent3008/host3164、Office15732/16944、port57993、engine965e54b6…，正常parent/任务0，约28秒，job0/原件释放。
zero-leaves只将两组Begin自动及末级Duration0，容器不裁断、不保存原件；初始transition1→main begin3→最后直属child begin12/end22都在OPEN观测窗口1内，end约在main begin35.4ms后；它是本次原生事件时序，不是通用生产等待时间。
三轮九张complete/extra/rewound全部SHA8D7993F3FCAABEDEDABDEFC27F7D014F3997DC2C3721D2E9A47A60F6B5980190，标题/字幕完整；rewound触发自动重播因此仍有字，不按原点击夹具期待空白。
after-close全360000像素黑，B49D3062…C95C34；root及独立视觉agent逐图核，无菜单/残影/切字，原底部两黑行保持。仅小窗/该fixture，不证明repeat、满幅或通用耗尽。
证据ZIP EABC0C2D6A2DA9AC04AD699ED492AD099E4FE3F136544061EFEB82706DE79DBA，本机`evidence-diag-zero-e87d…/c66247ef537940c78cac5078588ab6a6`原图保留。
旧zero容器裁断的全白失败不改写；自动初始事件与有效图像已分层核验，但额外next仍调用原生且ok，尚非完整noop功能。

## 原生stderr诊断仍不足

原生产job把Office stdin/stdout/stderr接NUL。仅QA复制job/OfficeProcess代码，为stderr开CREATE_NEW文件，HANDLE_LIST只增加该自有文件，stdin/stdout及job/预算/归属仍原样，不放宽全局句柄继承。
小QA ZIP 5E96BA2918C83D01E1E7E4B28F79659C37327ACE5DCB74BA9F1072D51C7AE337，新D4根`.validation/008-d4-stderr-3ae9fb368a184dbca9da5cd01c12671e`。
parent14496/host13956、Office10844/16588、port58312、engine3dab5c05…再次observe/URP断开，task/parent2、job兜底0，非正常关闭。
自有qa-office-stderr.log只有56字节“Could not find platform independent libraries <prefix>”，无实际故障栈；不把该Python前缀警告当崩溃原因或新增生产Python依赖。
完整日志/该文件证据ZIP C7D3357A5F507E3B6B41313B2A46386038D11A822F1878CAAF0C57E07150C916已保留。

## 收尾及下步

三次各新独立实例/lease，不重放旧不确定动作；三个任务在Ready/instance0后移除，两个失败的result2保留。测试/Office进程及相关监听0，服务继续停止，原件/包/旧失败/profile/证据保留。
TestPPT本机与D4原副本仍0BBAFC49…8F25C22，控制桌面/用户Office、电源、墙面、音量及系统执行策略不改；源码未secondary同步。
仅QA宿主构建0warn/error、Spec/diff本轮通过；正式代码/前端未改，未重复1082/211全套，不重标任务完成。
已只读核Microsoft签名的现有CDB/DbgEng/DbgHelp与官方文档，可进一步研究仅附加自有精确Office实例取得原生故障栈，不安装全局AeDebug/LocalDumps或调试未知用户进程。尚未部署/运行该附加诊断，不能称已抓栈。
