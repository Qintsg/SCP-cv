# 自有实例更新隔离与相邻页回归（2026-10-03）

## 本轮结论

T050完成有限更新隔离：只在本次全新Office profile预置官方MAR Enabled=false，强制开启覆盖存在时启动前拒绝。
不改用户默认profile/ProgramFiles、不用假地址作生产依赖、不伪造初始化完成、不扩大30秒启动预算。
四条纯文件回归通过，完整非Physical966/966、零skip；另有三次新实例导航/正常关闭及四次真实转换通过。
T044的wire整页NEXT/PREV复用既有UI goto；动画真实完成、媒体/裁切、双窗长稳及完整包/D4仍未验收。

## 从日志到转储，再到单变量对照

此前两次均在Office监听前30秒超时，没进入UNO/文稿；本轮A保持正式Host/自有Form/原预算，增加只读诊断。
为避免NUL丢原生日志，仅在本次launcher临时设置官方SAL_LOG/SAL_LOG_FILE并恢复；本机未生成SAL文件，空日志不推断流程未执行。
精确parent/profile/PID/start/自有父链核对后，在11.2秒采Office49296的96,331字节minidump。
CIM微秒时间与句柄100ns时间仅容许尾部精度差，实际句柄startTicks保留；转储bool=true为成功依据，旧GetLastError=6是成功后残值，不当失败码。

Windows自带DbgEng配合本机MinGW头文件构建只读控制台工具，未安装调试器/附加活进程/操作用户桌面。
官方模块PE CodeView GUID B56A2BED-91A7-480F-A14F-3F6F11F002DD定位TDF符号文件；下载约219MB压缩符号，展开约1.31GB。
PE Age=1、官方PDB内部Age=2，保留差异；DbgEng正常加载为private pdb symbols，未使用强制忽略签名的reload。
仅有导出符号时巨大的WeakImplHelper偏移不当真实函数名；私有符号将实际栈解析为：

- VCL Main：std::thread::join → desktop::Desktop::doShutdown+0x7f → Desktop::Main。
- 网络线程：curl_multi_poll → update_checker+0x52f。
- URP Acceptor：osl_waitCondition → desktop::Acceptor::run+0x4f，仍等待初始化放行。

官方doShutdown唯一更新线程join与这一栈吻合；尚未单独证明为何首次初始化提前进入停机/重启，不能猜为风格hook、路径或OOM。
转储内存在Windhawk模块，仅为模块事实，本轮未禁用或修改用户软件，也未将其认作根因。

| 对照 | 唯一变化 | 结果 |
| --- | --- | --- |
| A：run-9917a3dcd2d44a8394b8c74ef436f38a | 官方默认更新目标；只读诊断 | 30秒无监听，Host退出2，uncertain保留 |
| B：run-7ebdd0d21d6040adb5a6a46f4d67ef55 | 自有子环境LIBO_UPDATER_URL指向已核无监听的https://127.0.0.1:1/ | open6.90秒、整轮11.30秒、正常退出0 |
| C：run-d9d290d217c64d5d9cdf99ba441e85f6 | 撤销URL覆盖，仅新profile官方Enabled=false | open4.96秒、整轮9.00秒、正常退出0 |

A parent33172/Host20072/launcher64156/bin49296/port45105；B parent62216/Host54252/launcher49056/bin48652/port35996。
C parent49048/Host30784，配置在真实初始化后仍Enabled=false，无updates日志。
A的dump/stacks-export/stacks-pdb、B/C全部out/err、旧失败profile保留在`.validation/008-startup-diagnostic/`及短TMP根。
B是假地址诊断，不是正式配置；三个旧启动失败不因C通过删除或改写。内存采样非峰值，不倒推启动原因。

## 实现与纯文件红绿

OfficeProcess现有“拒既存根/创建新根”抽成OfficeEngineProfile小接口，Compile Link纯文件模块至Net10测试，不引UNO/Host程序集。
首条业务红为创建后XCU不存在，修后正确路径/Enabled=false、UTF8无BOM/LF、CreateNew、仅一个配置项通过。
另一条业务红为forceUpdater=true仍创建成功；修后在任何目录/启动前拒绝。
真实OfficeProcess只检查LIBO_UPDATER_TEST_ENABLE是否存在，不改或输出变量值；错误只显示配置名。
既存目录含sentinel、既存文件路径的两保护项均不覆盖/不复用；不写Setup/FirstStart/Migration状态。
首CA2219清理guard在finally内是构建问题，移至try前才得到真正业务红，不混算产品缺陷。
四TRX相关证据在`.validation/008-owned-profile/`，正式Net48构建0错误/告警。

真实强制覆盖测试`force-225af4f1794a4715b478fc64c8b4625a`：父退出2、固定原因存在、Office launch0、实例profile不存在。
只有该隔离工具子环境注入覆盖，工具退出后不存在全局新增值；无Office启动或其他profile修改。
这是MAR单项隔离，不是全网络沙箱。文稿外链/宏保护仍独立，用户LibreOffice更新策略保持原样。

## 冷启动放行后发现的独立导航阻塞

带配置的首批三个新实例均建立监听，但第一轮`run-f8755f5ce53b475ea61060bff34afceb`在视频页next之后的previous_slide直调卡住。
已返回next页3，previous请求没有返回；30秒transport uncertain、15秒Dispose未确认后仅自有job兜底。
其Office36760/58173实际监听，10秒dump里VCL Main等待SlideShowImpl::update的mutex；不能把此失败归为启动超时。
其余两轮正常退出，不将首失败从计数剔除；旧日志/dump全部保留。

PresentationDocument的wire next_slide/previous_slide改为按当前放映页序钳制目标，再复用既有GotoSlideOnUi/5秒实际页码确认。
这是Player NEXT/PREV已采用的同一路径，不是第四个几何/承载猜测，也没有改动画步的直接调用或完成声明。
末页不再调用可进入结束屏的direct next接口；同页页码仍不证明像素、媒体或动画结束。
修后重新构建正式Host，三个完全新实例，每轮13条open/goto/相邻页/效果请求/边界/close返回，shutdown及job完整退出0：

| 新运行根 | 总耗时 | 返回/失败 | 正常退出 |
| --- | --- | --- | --- |
| run-5de102d4ead74725b8c2f5dc46b82ce0 | 9.66秒 | 13/0 | 0 |
| run-cf53745b604848c6b690ea82bfe19e38 | 9.25秒 | 13/0 | 0 |
| run-c52689ae1e8946cc93ef7c670a0177c7 | 8.77秒 | 13/0 | 0 |

profile对应0ce407f0/cb60a18b/3e194173，实际初始化后Enabled=false、无updates日志；原件及副本摘要保持。
效果字段effects_visual_verified=false始终保留；没有用同页6证明effects完成，没有原生像素截图或双窗/60分钟证明。
QA capture.ps1原来仅打印父exit，脚本仍返回0，使第一失败后批量继续；已修退出码穿透。
这一测试启动器错误不改产品终态，原ParentExit2未丢失，修后新三轮真实退出0，不伪造历史批次全绿。

## 转换角色与全量

新根`conversion-369036ed3f734173b4daa182b7b85d57`重编译既有生产上传→转换→发布→中文目录移动探针。
原/fit各AllinOne9页、TestPPT7页四次成功，约8.25/8.07/6.54/6.27秒；PNG/PDF同源摘要、running移动锁及关闭后独占读取通过。
该根/SQLite/制品与用户数据隔离；原件摘要不变，不把导出页数当动画/动态满幅证明，本轮未重新视觉审稿。
headless converter的自有profile也保持Enabled=false，正常shutdown/job退出；没有重新使用深profile或日期媒体目录。
QA首次no-restore缺资产是构建前置，正常restore/build成功但NuGet漏洞数据请求不可达产生NU1900告警，保留而未禁audit。
实际deps的SQLite native2.1.13与正式集中版本一致，未引入新生产包或用旧native版本制造通过。

全非Physical966/966、零skip：Domain41/Windows86/Contracts251/Infrastructure127/Integration212/ControlHost249。
六TRX在`.validation/008-owned-profile-final/`；Native相邻页改动由另外正式Host重建和三真实轮验证，未以纯测试代替。
前端本轮无改动，沿用上一轮164/164/typecheck/build；不冒称本轮重新验收控制台或完整发布包。

## 收尾与剩余

原件、用户库、安装、用户默认profile及Windhawk配置不改。全部自有Office/Host/Probe/调试进程和监听已退出，失败与约1.5GB符号证据保留。
本轮没有浏览器/电源/墙面/系统音量/D4操作，不提交/推送/部署。
T050仅更新隔离闭环；T044动画实际完成、视频边界完整矩阵及正常关闭长稳仍欠；T045裁切架构仍等人类确认，T046完整包仍欠。

官方依据：[更新配置](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/officecfg/registry/schema/org/openoffice/Office/Update.xcs)、
[启动/停机join](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/desktop/source/app/app.cxx)、
[profile预设复制](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/desktop/source/app/userinstall.cxx)、
[TDF符号服务器](https://redmine.documentfoundation.org/issues/3518)、[Windows Dump加载](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/dbgeng/nf-dbgeng-idebugclient4-opendumpfilewide)。
