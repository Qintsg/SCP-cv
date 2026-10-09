# 无主序原生末页与D4指定输出验证（2026-10-04）

## 当前结果与限制

正式Host仅在当前模型无主序且原生实际nextIndex无效/超出count时返回v3 noop，不执行resize/goto/效果推进；Player相邻整页改发next_slide/previous_slide并传递同一边界码。
D4两指定输出分别以自有800×450小窗验证AllinOne第9页：NEXT_EFFECT/NEXT均为completed/presentation_at_last_slide，实际源/9/9/playing保持，PREV到8及NEXT回9为ok；关闭后原图全黑、job正常退出、文件释放/摘要保持。
这不是动画耗尽保护完成；有主序仍走原行为，End.NONE候选/可信播放轮、隐藏/重复custom、完整组/满幅/双窗同时/10轮100控制及60分钟仍欠。T069/T070不勾完成。

## 先红与失败证据

本机公开Player/正式Host的新无主序末页QA先红：NEXT_EFFECT回ok而非边界码，capture-78dfe058f8c4460ca94d8f244a51da3d，父1912正常清理后exit2，host53128/Office34904+62604/3669退出。
公开Windows外部stdio保护新增整页NEXT先红（relative-red.trx），其旧GOTO不能收到next_slide noop；相邻整页路由修正后NEXT_EFFECT/NEXT两项绿，relative-green.trx。
第一次原生复核capture-384d725a456b414f8d60ccb530bc8939未在30秒监听：parent68784/host45024/Office23508+14316/21822，未执行末页动作，原启动失败/双原因仍保存，job兜底不算正常成功。
下一新独立实例capture-edbf4f9e91c446b3897d671cd6e65807状态码/关闭exit0，但四图均山谷照片，连关闭图都相同；原PPT末页是8图片拼贴，不是山谷。root及独立视觉复核明确否定该轮图像验收，不能以SHA相同假保持。
本机当时LogonUI存在；具体遮挡/输入桌面原因尚未由同一轮窗口归属证实，不把它直接当全部截图或启动失败根因。不改生产几何/预算，也不以延时或关闭用户应用修补。

## QA截图与远端执行边界

仅QA增加Win32可见/非最小化、DWM未cloaked、中心点ROOT属于自有HWND及物理矩形检查；截图线程PerMonitorV2并finally恢复。
远端文件脚本策略保持，采用编译的NativeCapture与一次性交互启动器，不绕过/更改PowerShell执行策略；PNG原样CopyFromScreen，不编辑图片或修改共享截图skill脚本。
新增非空出画/关闭全黑门禁，保留本机旧山谷假绿原图。中心点检查不是全窗口抗遮挡证明，当前完整图像另由人工核对。
QA包仅新验证根、当前同版Net10/48，未同步Git源码或启动完整服务。ZIP SHA A3E558DCB5E02D626048A95D855731D136D8C339AC8E9FABBD100C2EACE88256；Host SHA 1F567DA7A91A514F7C7CF0F8461670650E2E083BD21F81AA3E0A39F78A0B9056。
远端根D:/SCP-cv/.validation/008-d4-stage-2e7d9f396d3e46b8b3967b52b2f366ab/native-static-2e7d9f396d3e46b8b3967b52b2f366ab，保留包/源码副本/日志/图片/profile/失败记录。

## D4两次有限实测

| 窗口 | 绑定输出/截图物理矩形 | QA父与原生实例 | 结果 |
| --- | --- | --- | --- |
| 1 | DISPLAY2，[80,80,880,530] | parent2632；host3764；Office14552/14088；port56202；engine1ec08a37… | task0/parent0，31秒，job0 |
| 2 | DISPLAY3，[2000,80,2800,530] | parent13448；host3764（不同启动的新实例/PID复用）；Office11040/1808；port56249；engineb7da9cb2… | task0/parent0，18秒，job0 |

两次顺序执行，不能称并行隔离已验收；控制DISPLAY1及历史小电视输出不被窗口绑定。窗口2截图时其它应用仍前台，只读中心点核仍属于自有窗口，不关闭或接管该应用。
两个截图根分别为d26ba2af18674916a318de0f454c5c0f与3c7492049d0148a0b4926f75dbe74de6；证据ZIP经远端与本机SHA一致校验后保存到.validation/008-d4/evidence-window1/2-2e7d9f…目录。
两组各3张出画PNG均SHA 13746AC436C54EAB9CD1B4C725A9FD19E98B79F2CEED5AFA573159D4F29C88A1；两关闭PNG均B49D30627992836FC7A85674CB02CC8289BFDB1A923B10EDEA545D9CECC95C34。
root与独立视觉agent逐组核图：多图片拼贴保持、无菜单/导航/结束提示；关闭360000像素全黑。底部原有2行全黑未变，密集报纸正文在小窗不易读，不推断满幅/原始图片裁切或动画稳定。

## 验证与收尾

- 正式Net48构建0警告/0错误，完整非Physical1082/1082零skip，六TRX在008-native-terminal-final；Windows113包含新增整页桥接保护。未新增/执行仓库Physical用例，D4QA是单独真实记录。
- 本轮前端未改，沿用上一轮211及type/Web构建，不称重新执行；忽略QA项目旧NU1900审计不可达告警保留，未禁审计或改源。
- 本轮自有两次任务确认Ready/result0/instance0后精确移除；可从保留启动器和执行记录重新登记，无用户任务变更。D4项目/QA/Office进程、56202/56249及项目服务监听为0，所有相关测试服务保持停止。
- 本机自有QA/Office/TestHost最终0，Resources两原件SHA保持；D4 QA AllinOne原副本仍2401652F…B82A55F0。没有改电源、墙面映射、音量、Windows登录或用户Office。
- D4源码仍b2f09c51367e87a96166c862c79a1dbae0241aec，git status空；QA上传不是secondary推送/拉取，正式同步与当前完整包/最终长稳仍由T046/T070完成。

下一步采用[生命周期研究](../research-terminal-lifecycle.md)验证带动画末页受控重显示与耗尽事实；不把本轮无主序路径替代完整要求。
