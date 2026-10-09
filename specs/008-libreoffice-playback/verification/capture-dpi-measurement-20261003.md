# 截图DPI测量工具与96 DPI实际复核（2026-10-03）

## 当前结论与未完成项

本轮完成QA捕获工具和96 DPI对照/实际PPT复核，不修改生产代码、播放器几何、Windows缩放或共享截图技能脚本。
当前窗口与明确aware助手均报告96 DPI；不能重现T067此前144 DPI/533×300的差异。因此T068保持未完成，未将工具通过当144 DPI原因已确定、混合DPI满幅或T004/T045验收。
本轮实际测试/工具变化及前端完整门禁为进展，整体goal仍active；两输出/完整组/60分钟、末页/复杂动画、真实宏/历史媒体和用户未保存编辑仍未完成。

## 官方依据与实验设计

Microsoft说明[GetWindowRect有DPI虚拟化](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect)，DWM可见边界不按DPI调整；[SetThreadDpiAwarenessContext](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext)可临时改变当前线程并用返回值恢复。
本轮分别使用官方[Unaware(-1)/PerMonitorV2(-4)上下文](https://learn.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context)，只改变截图助手线程，不修改被测窗口。文档原理支持调查方向，但不直接证明本机历史533×300的原因。
自有PlayerWindow显示已有静态页PNG，落位80,80/800×450；DWM矩形独立作为物理范围，同一个稳定窗口/内容各三轮默认助手、明确Unaware和PerMonitorV2捕获。
包装仍调用原screenshot技能助手，要求句柄匹配精确PID/start、全新自有临时图、线程上下文已确认且finally恢复；拒绝旧图覆盖/越界输出。工具仅在`.validation/008-native-player/`，不进生产包。

## 受控结果与QA错误

首新文件编译缺using System.IO，属于QA构建错误；修正后capture-b5c2bc4596d84dc9bb08953212d5f4b0，parent34280，正常exit0/7.129秒，三轮均800×450。初DWM结构被JSON默认写成空对象，范围仍有expected数组；后续显式数组补齐坐标，不覆盖旧记录。
补窗口/system DPI及awareness后capture-d682ab67681d4feeadb40418b75c08d7，parent42192，exit0/7.060秒：window/system均96、window awareness=1、助手分别0/2，矩形始终80,80,880,530。
随后安全负例首轮capture-feac4b45dc474229bc16c5ef3e1249b1，parent36420 exit2：错误PID实际已拒绝，但Windows PowerShell错误文本的编码与C# UTF8解码不符，断言误失败。它不证明播放器或拒绝门禁失效，原证据保留。
仅新QA包装设置Console.OutputEncoding=UTF8，调用方明确两流UTF8后，同样的四负例通过；没有改PowerShell全局配置、共享助手或正式运行时。

最终capture-bf8e47a8495f4935962b5e34763f5c65，run-56e9ff2634a041f3be690333d5b73032，parent52412正常exit0/8.895秒。
九张图均800×450、完整SHA仅一个：1C87526A4F453BAFC87E35A4A696A66545E61B57DA2B42E0D5D94FB868008559；明确上下文六次全部恢复、DWM范围不变。
错误PID、错误start ticks、覆盖旧图和越界输出四例均明确拒绝，无新拒绝图/旧图摘要变化。静态页源完整摘要保持，不使用页图当动态引擎证明。
图根`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/011aeaf4719f4bd2bdb890758af3af80/`。

## 正式Player的实际快速动画

QA包装增加仅子进程的SCP_CV_QA_DPI_AWARE_CAPTURE选项，启动后父环境恢复；默认旧捕获与历史图不变。实际Player/正式LO原件播放请求、原生预算及接口不变。
新capture-fa316e0636ba402bbea2836d67692878，run-b37fea0a990c4c13a493d3b0a38e5015，parent22144正常exit0/33.5809秒。
三轮25条控制，中间连续效果调用不插入等待/Observe；两next后标题/副标题像素2059/1721，两撤销后0/0，三next观测到页7，最后首效果后立即CLOSE正常。
十张明确aware图均PNG800×450与DWM800×450相同，window/system均96、每次上下文恢复。图根`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/15e9614647f24837b4a0dfd498a141d2/`。
主代理及PPTX要求的独立只读视觉复核确认三轮页6文字/空白撤销/页7，无菜单、密码窗或旧页残留，关闭全黑；未裁图/估黑带或改变原件布局。
原PNG后期连续全宽RGB0底部仅2行；关闭450行全黑。该精确图像结果不是全部物理viewport/混合DPI、复杂动画或媒体时钟完成协议。
实际host48000/Office31116+45120/port30780有document_closed/termination_requested及job active0/members空；TestPPT副本独占只读成功、完整SHA同Resources原件。

## 本轮前端与生产包边界

本轮重新执行实际pnpm CLI的前端全量177/177、0skip，typecheck和build:web均exit0；日志在`.validation/008-dpi-front-90c2219b2020414cb82f439c867e8520/`。未安装新依赖、改镜像/锁或声称实际控制台UI本轮通过。
测试总82.916秒、首上传DOM约72.337秒，属于本轮实际耗时，不当产品上传响应时延。构建14.58秒，主chunk仍1100.50kB/gzip313.57kB，有原大chunk及插件时序告警；没有调高阈值或关闭告警假绿。
生产代码/发布包未改，开发及当前981767…包Player DLL仍D52C7AE7D4B14FEDEB52C31C7877953D46AE60CC038D520496B7A9E8314E895B；未再生成同内容新包或冒称本轮重新跑1059.NET全量。1059仍上一轮真实结果。
共享截图助手原SHA A31A2242E41FD1C5471651AFBCCED660F98FBC884F11E531218348F700A3F826保持。PowerShell QA源码UTF8 BOM/LF，其余UTF8/LF且不足500行；Spec Kit/diff/官方缓存Redocly校验，仍三项历史410告警，无extension hooks。
精确自有QA父/Host/Office和30780监听已退出，QA环境标记恢复unset，两Resources原件SHA保持，原图/失败/库/制品/包保留。未提交/推送/部署或访问D4、电源/墙面/系统音量。

## 继续条件

当前没有高DPI对照差异，144 DPI原因及对应物理满幅不能结案；已询问近期是否变更缩放/远程桌面，未得到事实前不猜环境原因。末页保持完整画面或结束清空的产品选择也已单独询问，不能自行选择或据本次基础三轮动画推断全部完成。
