# T043：只读运行依赖诊断（2026-10-03）

## 结论与范围

T043完成有限静态诊断闭环：缺失/损坏/不兼容或无法确认的运行依赖不再仅凭host与uno.ini存在标可用。
后端最终非Physical947/947、零skip；前端149/149、类型/Web构建通过；真实浏览器桌面与390布局通过。
本机正式Reader/Probe单次报告16项通过，未加载cli_*进.NET10、未启动Office或创建profile。
原生底部裁切、动画完成语义、最终双窗60分钟及完整发布组件仍未验收；D4未访问、部署或启动。

## 设计与检查边界

Contracts/Presentations/Dependencies将只读事实采集与纯判定分开，小接口Inspect()返回同次不可变报告。
设置端点不自行拼文件判据；公开字段为components的id/status/detail/reason_code，不返回真实路径或环境配置值。

- 固定9个必需文件事实；缺失/重复不能被额外假事实替代，非空可读仍须相关映像规则通过。
- Windows、OS和进程均实际x64；Framework Full Release至少528040且Framework64 CLR可读，接受4.8.1。
- 宿主x64/net48/合法托管EXE入口；配套协议net48身份匹配、当前literal ProtocolVersion=2。Net10同名协议不冒充Net48。
- soffice.exe/bin为兼容可执行映像；cli_uno为x64混合托管DLL。非零原生入口只为结构判据，不验证代码可执行性。
- 五个官方CLI按宿主/桥/选中候选的传递引用检查名称、版本、culture、token及MSIL/混合架构，不取首个或最新同名DLL。
- 有限GAC枚举；publisher配置只从程序集manifest关联的文件取得，app重定向后按major/minor族选择publisher。全局或单项禁publisher沿声明处理。
- Framework64 machine.config有CLI覆盖或未知全局规则时未确认；无相关默认配置不误拒。XML禁止DTD/外部解析，限64KiB。
- codeBase/probing/qualifyAssembly、错误根/namespace/布局及尚未支持的appliesTo/processorArchitecture限定不猜兼容，保持未确认。

运行诊断不检查SDK、编译器或targeting pack。纯健康事实加“构建工具不存在”保护例通过，不声称真实本机没有开发工具。
不实现完整CLR binder、原生import/延迟加载/CRT加载器、加密签名验证或publisher关联文件hash验真。
元数据中的强名token是身份匹配，不是安装文件可信性证明；静态通过不证明UNO连接、具体文稿或实际出画。

## 后端红绿实证

先通过真实认证设置HTTP建立首条业务红：host与uno.ini存在但缺soffice仍available=true。
随后逐项发现并修复映像、事实清单、入口和CLI缺失假绿；每次重新编译，不用旧二进制制造通过。
主要TRX保留在`.validation/008-dependency-diagnostics/`：

| 证据 | 修前实际缺陷 | 修后保护 |
| --- | --- | --- |
| files-red / files-green | 两文件存在误报可用 | 公开HTTP逐项不可用、不泄路径 |
| metadata-red / metadata-green | 占位文件误当宿主 | 2条公开HTTP，不加载/执行UNO |
| fact-red / fact-green | snapshot漏host事实仍通过 | 固定必需清单、缺/重复/标签保护 |
| image-kind-red / dependency-entry-red | DLL/纯原生CLI、无效托管RID0或原生入口仍放行 | 纯事实及真实自有Net48 PE副本，原件不改 |
| dependency-cli-red / dependency-cli-green | 宿主引用正确但候选空仍通过 | 五CLI含传递ure、身份/架构/重复/未知引用 |
| protocol-version-red | literal未读，无法核协议常量 | 正式Protocol PE只读+2改1副本拒绝；Net10 PE不冒充Net48实证 |
| config-root-red / global-policy-red | 错根误确认、合法全局no误拒 | 根/namespace/manifest关联与合法禁用 |
| machine-config-red / machine-config-green | machine CLI覆盖被忽略 | publicReader自有配置、默认无CLI配置兼容 |
| publisher-family-red / publisher-family-green | 未知版本族被确认 | 必须确认族；跨族不套，用app后版本选publisher |
| binding-qualifier-red | appliesTo/processorArchitecture被忽略 | 原5绿+新2红后，最终7/7通过 |

CA1711/CA1861等分析器构建修正不是业务红；既有正确规则的最终保护矩阵直接绿，不伪称全部独立先红。
最终新增保护25例：5各CLI缺失、身份/架构、重复/未知引用、合法app/publisher/禁用/族顺序及真实自有XML/machine配置。
策略类最终69例、设置HTTP2例；全部包含在重新编译的最终全量中。

```powershell
dotnet test runtime-dotnet/ScpCv.sln -c Release --no-restore --filter 'Category!=Physical' --logger trx --results-directory .validation/008-dependency-final-qualified
```

Domain41、Windows86、Contracts247、Infrastructure127、Integration212、ControlHost234，共947，通过/总数相同且零skip。
此前945版本与所有失败TRX保留。正式Framework Host仍为已安装官方互操作方案，没有引入新生产包/Python或源代码fork。

## 前端红绿与浏览器

实际Vue/Naive/Pinia/API组件27条DOM测试，外部fetch/deferred响应验证公开行为：
缺CLI原因、请求错误清旧green、17种畸形/不一致合同、HTTP200但success=false、公开切主机目标代次、
旧HTTP错误/finally不清新pending、重复按钮只有一个在途GET、卸载迟到不影响新实例。
新检查先清旧snapshot，失败或无效保持“待检查”；切主机只失效，不自动向新主机重放GET。

```powershell
pnpm --prefix frontend test
pnpm --prefix frontend run typecheck
pnpm --prefix frontend run build:web
```

149/149、零skip，typecheck/build:web退出0。主包1100.25kB与500kB分块/插件计时告警保留，未抬阈值或隐藏。
Spec Kit/diff通过；既有官方Redocly2.55.0校验0错误/3个退役410无2XX告警。旧pnpm缓存shim指向失效路径，改用当前实际store内同一官方CLI，未改CERNET配置或下载依赖。
IAB自有tab4、127.0.0.1:20884回环夹具使用真实设置组件/生产主题；“真实快照”回放正式报告，不是真实生产HTTP。
缺CLI/503/删除components均只改变自有HTTP夹具，不破坏安装、不注入store；每次通过实际“重新检查”按钮取结果。
桌面real→missing→503，390 invalid→missing→real，缺组件逐项建议可读，失败无旧green/列表残留；最终16项恢复。
手机viewport390、文档scrollWidth380（无效状态390），没有横向溢出；日志warn/error为空。
GET共6次；QA场景POST仅控制模拟，不代理任何业务动作、Native或D4。
截图在`.validation/008-dependency-browser/desktop-missing.png`、`mobile-invalid.png`与`mobile-missing.png`。
恢复viewport、关闭自有tab；本轮一次性夹具没有可泛化站点新事实，既有localhost笔记不改。

## 官方依据

Framework运行检测依据[Microsoft Full Release规则](https://learn.microsoft.com/en-us/dotnet/framework/install/how-to-determine-which-versions-are-installed)。
CLI版本与publisher策略依据[LibreOffice官方CLI说明](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/cli_ure/readme.txt)。
app/publisher/machine顺序依据[Microsoft程序集定位](https://learn.microsoft.com/en-us/dotnet/framework/deployment/how-the-runtime-locates-assemblies)。
忽略限定属性会改变适用范围，依据[运行版本限定](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/assemblybinding-element-for-runtime)和[架构限定](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/assemblyidentity-element-for-runtime)；本实现未支持，故保守拒绝确认，而非静默忽略。

## 收尾与剩余

本轮自有Vite服务按PID70844/start2026-10-03 07:05:18/父30708及命令核对后关闭；证据、QA源码/报告及旧失败保留。
AllinOne/TestPPT原件SHA保持既有值。用户库、原件、官方安装和历史dump不修改。
20884已无监听，自有Office/Probe/TestHost进程为0；截图与报告保留，不删除测试失败证据。
Spec Kit前置因旧.venv已删除，用uv管理解释器执行既有脚本；不是重新引入Python业务设施。
原生裁切已有三个独立修补失败并撤回，仍等待人类选择独立放映窗嵌入的架构实验，不叠第四个尺寸猜测。
T044导航实际完成、T045用户实例/旧版样例/原生故障与最终60分钟、T046完整包仍保持未完成；不推送或部署D4。
