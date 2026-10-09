# SQLite生产与QA依赖对齐（2026-10-04）

## 结论与更正

上一轮NU1903是真的，但“生产依赖尚未升级”的判断范围不准确，现更正：
当前HEAD的runtime-dotnet/Directory.Packages.props已集中固定原生包2.1.13，
Infrastructure/ControlHost锁与实际assets均解析2.1.13；正式输出DLL摘要也匹配该包。
位于runtime-dotnet之外的忽略目录QA没有继承集中设置，独立解析2.1.11并复制旧DLL。
因此上轮D4列表测试是行为证据，不能说它用了正式生产的SQLite版本。
旧包、日志、DB和告警仍保留，不回写历史为“同版已通过”。

本轮不升级/替换生产provider、不改生产版本表或许可。
[2.1.13包元数据](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.13)
及本机官方nupkg的nuspec声明Apache-2.0、requireLicenseAcceptance=false，无需购买商业许可。
不引入付费或新版3.x provider来解决一个QA配置问题。

## 根因与修正

旧QA deps/assets为SQLitePCLRaw.lib.e_sqlite3/2.1.11，实际SELECT sqlite_version()为3.49.1，
载入DLL SHA256 `4DCBB2AD197D893355EFA004257E048F2C850FEB1078158F4BAAB0F7B53AC45F`。
只有查询真实应用provider和已加载模块才能证明实际版本，独立sqlite3 CLI不能替代。

第一尝试在csproj后置导入集中表：最终MSBuild属性虽true，NuGet恢复仍2.1.11；
再加无Version引用时NU1015。两失败完整保留，不当修复或屏蔽告警。
按[NuGet官方CPM规则](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)，
为该QA添加同级Directory.Packages.props，早期自动导入生产集中表。
删除试验性的显式包引用后，传递固定仍解析2.1.13，避免多余依赖和第二份版本号。
修复仅在忽略目录的QA；旧失败包/源副本不清理。

恢复审计开启、build0警告/错误，QA实际载入SQLite3.53.3；DLL SHA256
`B7385D722C83FB52142A00477A726723745916D22A555711EE89834C1111FB2E`，
与本机生产输出及2.1.13官方包相同。仅QA审计配置/进程代理协议适配，环境随后恢复；
没有关闭NuGet审计、抑制NU1903、改全局源或手工替换生产DLL。

新维护回归SqliteNativeRuntimeTests通过Microsoft.Data.Sqlite真实内存连接查原生版本，
要求不低于CVE-2025-6965已知修复底线3.50.2；当前正式测试实际3.53.3，结果入TRX。
完整非Physical1102/1102、零跳过（41/303/127/269/239/123）。
正式解决方案当前--vulnerable --include-transitive审计未列已知漏洞，
JSON保留在`.validation/008-sqlite-runtime/production-audit.json`。
审计结果/单一CVE底线不等于所有安全风险已排除，未运行攻击载荷或宣称全部安全。
前端与原生音频生产代码本轮未改，旧232/3Physical不算本轮新执行。

## D4同版实证

新验证根`D:\SCP-cv\.validation\008-d4-sqlite-aligned-1791122065467`，
补丁ZIP `9F50F3279349280C8CCC70676C743E6BF34AA67BC8965E694F57565C9EECC2AC`核后执行。
parent2864/start UTC13:56:31.0182203，worker14324/start13:56:36.826925，ready14784ms。
实际SELECT版本3.53.3、已加载文件为本轮根e_sqlite3.DLL，SHA正是B7385D72…1111FB2E。
正式Broker/AudioWorker/独立新SQLite在对齐后复测：不循环1→2停止、循环1→2→1、
显式STOP均实际状态匹配且pending空，十命令Completed/ok，正常协作退出0。
两副本摘要/独占读释放通过；没有业务HTTP、听音、显示窗口或整个运行组验收。

证据ZIP `E87D05F47CCDF7F0951DE5370C28C3508E88C0A56048BDAB848513D7DF0E68AE`
本远核SHA并原样解压到`.validation/008-sqlite-runtime/d4-sqlite-aligned-1791122065467/`。
任务Ready/instances0后精确移除，最终OwnProcesses0/ProjectServices0/OwnTasks0。
原库/媒体、控制桌面、显示绑定、电源/墙面、系统音量、执行策略/防火墙未改；
D4源码仍未secondary同步，本轮QA不是正式部署包更新。

## 剩余范围

该依赖差距已定位并对齐，生产基线无须本轮升级。
最终同版完整发布包仍须复核实际DLL/版本，不能用源码pin代替包级证明。
前端音频状态、HTTP、多源/两窗100控制及60分钟、PPT危险特性/动画末页与UDP恢复继续未完成。
T074完整目标不据本切片勾完；Spec/diff及UTF-8/LF通过，“说人话”只整理更正后的事实。
