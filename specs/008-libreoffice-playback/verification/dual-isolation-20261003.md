# 两独占实例：快速控制、切源与关闭隔离（2026-10-03）

## 当前结论与范围

两个正式公开Client/Framework Host并存，轮换窗口1/2作为活动端；每端十轮100控制、20次异源切换通过。
另一端始终保持TestPPT第六页标题阶段；逐轮页码/标题与字幕像素、中部图像差异、关闭后副本独占读取及原件摘要通过，最终四个job正常退出。
这是两自有Form承载的有限原生回归，不是完整Player包、两个物理输出、用户LibreOffice共存、媒体时钟、完整viewport或60分钟验收。
本轮没有生产代码/协议/能力变化；T055有限完成，T053/T044/T045/T046及动画末页策略仍未完成，D4未操作。

## 真实实例与结果

| capture（`.validation/008-wire-navigation/`） | 活动/保持窗 | 明确控制 | 异源切换 | 父正常退出 |
| --- | --- | --- | --- | --- |
| dual-capture-b8746e4b15884bfda81ae99b93eaf63c | 1/2 | 100 | 20 | exit0，109.25秒 |
| dual-capture-25062187a285462880fd93f6f3e7778e | 2/1 | 100 | 20 | exit0，108.67秒 |

第一组parent45596，Host46800/31808，Office launcher41104/55348，listener46628/47720，port47543/47544。
第二组parent15596，Host53548/70396，Office launcher13200/41036，listener49088/44404，port9027/9028。
每个实例独占profile/engine identity，role与windowId对应；两个Form客户区800×450，实际DPI96，各自副本和文稿scope独立。
活动端文稿代次1→21，保持端一直1；不是在一个全局Office槽内换文稿，也没有访问用户Office。

## 控制、像素与文件证据

每轮十条控制：goto2、goto6、next×2、previous×2、next×3、goto6；紧邻效果调用间不采样、不sleep、不等自然动画结束。
各阶段随后1.5秒有限采样只作QA观测，不进入产品执行完成规则或扩大30秒请求预算；三次快速推进后观测页7。
每轮活动端两次快速推进后Title/Caption=2059/1721，两次快速撤销后0/0；保持端2059/0，页码6。
保持端原样PNG中部区域`x=[40,width-40), y=[50,height-40)`逐轮差异0；整窗和客户区的所有差异另报，完整viewport始终false。
活动端关闭TestPPT→打开AllinOne第二页→关闭→打开TestPPT第六页，每轮两次源切换；二十次转换为明确源切换计数，不把observe/open/close伪算100控制。
从实际response账本独立重算，两组各100控制、21open/21close、22关闭后副本独占读取记录、十轮、非succeeded与中部像素失败均0。
关闭后FileShare.None成功仅证明相应副本已可独占读；未证明Player活跃源占用/移动策略或媒体时钟。
每轮校验两原件和本组四副本SHA；收尾重查仍与Resources原值一致，不保存、修改或覆盖原件。

root核对失败组四图及两个最终组第十轮各三图；独立视觉agent核第一组首轮四图和交换角色首轮三图。
代表图显示完整标题/字幕→空白，保持端只有标题；无内容区重叠、菜单或导航。不是人工逐张核完62图或普遍版式保真证明。
原样图片目录：`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-dual-shots/b4e05a33b1b54a10ab91b735533f6d43`、`ef68779eea884270a986af85839bb09b`。

## 首两次整图门禁失败保持，不冒称原生修复

dual-capture-d3aded2b0c3d40b3b9d62632431fb879与9c8eb174c67f4455972c3586c5f0f507均exit2，约19.97/19.92秒。
原标题/字幕仍2059/0；重新查看原图，B标题栏由活动变非活动，近边缘有微色差。第一组整图42606像素不同，声明客户区3741，中部0；第二组42603/3741/0。
最初在参照前给A发goto2，整图仍失败；这不是已验证的活动态归一方法，已撤回该QA改动。
只读差异工具保留全区/客户区/中部计数、边界和原始样点；例如近左缘white -1→-65794，不把这些样点的所有根因强行归到Windows，也不隐藏边缘差异。
最终有限门禁按明确的中部内容与文字区域判断串扰，整客户区并未相同；不能用收窄判据冒称原整图门禁已修、满幅或T045完成。
没有为测试整图差异改正式承载、增延时或实施第四个几何修补；旧失败、图片及独占profile均保留。

## 收尾与验证

QA/正式Host重新构建均0告警/错误；两个正式组诊断OFF，shutdown文稿关闭→terminate→job active0/members空，精确父exit0。
外部180秒看门狗仅是完整QA寿命；每个正式请求30秒、Client关闭15秒、原生切页5秒/结束10秒保持。失败仅精确缓存父句柄清理、自有job兜底，不按Office名称kill。
自有Office/Host/Probe及上述监听均退出；所有数据库、原件、副本、旧失败/dump/profile保留，不提交、推送、部署或操作D4/墙面/设备电源/系统音量。
本轮生产前后端未变，不把历史985后端/174前端当本轮重跑；Spec/diff通过，官方缓存Redocly校验0错误/3个既有退役接口告警。
上一轮为实际生命周期修复和冷开证据；本轮为两角色原生100控制/20切源及退出证据，不是状态重述，整体目标保持active。

复现命令（自有只读窗口，先构建，不复用旧profile）：

```powershell
dotnet build .validation/008-wire-navigation/Probe.csproj -c Release --no-restore -m:1
dotnet build runtime-dotnet/src/ScpCv.LibreOfficeHost/ScpCv.LibreOfficeHost.csproj -c Release --no-restore -m:1
powershell.exe -NoProfile -File .validation/008-wire-navigation/dual-capture.ps1 -Rounds 10 -Diagnostics 0
powershell.exe -NoProfile -File .validation/008-wire-navigation/dual-capture.ps1 -Rounds 10 -Diagnostics 0 -Reverse
```
