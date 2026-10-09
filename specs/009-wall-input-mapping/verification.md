# Verification: 大屏组合与协议接入

日期：2026-10-09。本轮只在本机独立SQLite/Simulation与回环TCP验证，不连接D4、不操作墙面或电源。008未提交工作区保留；本轮没有提交、推送或部署。

后续用户另行要求停机部署，源码已提交推送并在D4拉取，发布包已同步但未启动；详见[2026-10-09 D4停机部署记录](../../docs/deployments/20261009-d4-stopped.md)。以下保留本轮软件验证的原始边界，不因此升级为实体出画验收。

同日后续紧凑界面、美化与63/64修正已提交并部署启动，最新运行状态见[D4紧凑界面部署记录](../../docs/deployments/20261009-d4-compact.md)及文末部署核验；前述停机及以下“未部署”说明均属于对应历史阶段，实体画面边界不变。

## 实现与证据映射

| 需求 | 实现与验证 |
| --- | --- |
| FR-001/002/004 | Planner及输入55/56/81配置；12完整组合、6单侧、重复窗口独立Sign/Level、A/B/C和逐片裁剪；旧两预设合计400黄金样本保持 |
| FR-003/009 | 四种codec与原业务6102/6210/6160/6120向量；Type3同节点序列，公开合成DES向量；无效配置/未登记IP清屏前拒绝；增量窗口回读和真实回环收集 |
| FR-005/006 | 阶段屏障、重试/取消、控制器排他；新增跨Runtime实例共享门禁；发送失败与持久失败只报未确认/不确定，不自动重放 |
| FR-007/008 | 独立草稿/发送布局与修订；重建读取、旧预案替换、手动快照unset、静音、旧版本409、无效版本400；重启恢复和失败保留 |
| FR-010 | OpenAPI、运行指南、README、使用/维护、CHANGELOG及pitfalls同步，005历史门禁注明由009接续 |
| SC-001..004 | 以下包/HTTP/SQLite、DOM及真实浏览器分层结果；没有实体出画证明 |

## 自动化结果

最后完整.NET命令（显式清空HTTP_PROXY/HTTPS_PROXY/ALL_PROXY，只影响该命令进程）：

```powershell
dotnet test runtime-dotnet/ScpCv.sln --no-restore --filter 'Category!=Physical' --logger 'trx;LogFilePrefix=final009b' --results-directory .validation/qa-wall-009/results --verbosity quiet
```

1202通过、0失败、0跳过：Domain41、Infrastructure164、Integration262、ControlHost286、Contracts322、Windows127。6份原始TRX文件匹配`final009b_net10.0_20261009132*.trx`，位于忽略的`.validation/qa-wall-009/results/`；以原件为准，不以本摘要代替原件。

相关子集另外通过：Infrastructure VideoWall52（之后补4条原单播/扩展向量）；ControlHost VideoWall9；Integration VideoWall/ScenarioVolume33（之后补发送前提交失败回归，最后VideoWall子集26通过）；DOM大屏14。全量已包含这些测试，数字不重复相加。

```powershell
pnpm --prefix frontend run test
pnpm --prefix frontend run typecheck
pnpm --prefix frontend run build:web
redocly lint docs/openapi.yaml
python .specify/scripts/python/validate_specs.py --specs-dir specs
git diff --check
```

- 前端全套288通过、0失败/取消/跳过，两轮通过（首次约350秒，后续约104秒）。
- 类型检查、Web构建通过。保留现有单包超过500kB的构建告警，未以提高阈值隐藏。
- Redocly通过、0错误；3条现有已退役PowerPoint/PPT重置入口只有410、无2xx的告警保留，不为通过校验伪造可用入口。
- Spec Kit全目录校验通过；diff whitespace检查无输出。

## 真实浏览器与视觉

使用webapp-testing与Playwright实践：先查看实际DOM，使用真实ControlHost/Vite与认证/CSRF，不模拟业务API。服务仅绑定回环18943/5199，自有随机账号和GUID DataRoot，SafetyMode=Simulation，不启动Supervisor/播放器。

- 桌面1440×1000，UI逐项选择、保存、应用12完整组合，核对200响应、草稿修订和实际返回映射；明确显示“模拟结果：未向实体墙面发包”。
- 另验证6单侧布局；未映射区域由后端全节点清屏规划覆盖，不据浏览器证明物理清除。
- 刷新保持IP81布局；修改未保存禁用；另一客户端改稿返回真实409，读取新状态但保留当前表单；未登记239.1.2.3草稿可存但应用禁用。
- 390×844窄屏检查无横向溢出。原图`desktop.png`、`desktop-viewport.png`、`mobile.png`、`mobile-inputs.png`、`mobile-result.png`、`unsupported.png`保留在`.validation/qa-wall-009/`；人工查看原图，左右预览、输入和结果换行正常。窄屏全元素长截图可能带固定顶/底栏，不能把截图中固定栏位置当布局错位。
- 两轮12组合真实浏览器通过，最后一轮同时通过12完整组合和6单侧布局。页面异常0；控制台只有主动制造旧版本产生的1条409资源错误，无额外脚本/控制台错误。
- 本轮启动的自有Host/Vite由finally精确结束；不全局结束node/dotnet，不操作远端服务。

## 失败与修复记录

1. 编译先暴露DES安全分析、返回类型/常量数组/token参数位置等项目分析器错误，按限定协议兼容处理；DES抑制仅在显式兼容方法，不全局关闭，不提交现场密钥。
2. 新Simulation未知模式提示与旧断言不一致，恢复旧错误文案；新HTTP测试集合语法修正。不更改黄金包或放宽正确性断言。
3. 全套运行期间启动追加测试的交叉编译，产生DLL/Manifest锁：追加编译失败，Host5项IOException。停止交叉编译后完整重跑1197通过，增加恢复边界回归后最终1202通过；原失败保留于`wall009_*.trx`。
4. 重启中断状态先红后绿；非法JSON版本类型已补400回归。手动布局Capture先红后绿，旧预案不保存custom或误切单/双屏。
5. 数据库连uncertain标记也无法写入时，原提交失败被标记异常遮盖：新增故障注入先1失败/2通过，修复后3通过。随后增加初始发送标记提交回执失败回归：0次设备调用，状态标为uncertain并明确尚未发送。4项恢复回归均纳入最终全套。保留原外层错误，数据库恢复/下次启动可将dispatching转uncertain，不重放墙面。
6. 浏览器首轮在重负载下自有服务45秒启动超时，收尾后重跑成功；不更改生产预算。原生radio隐藏input不适合check，改普通可见标签点击再核对checked；Naive菜单离场且保留隐藏DOM，等待可见弹出层退出，不强制点击、不固定sleep。
7. 真实页面发现旧“两预设可用”文案，同步更正；手动布局显控按逻辑窗口号标记，PPT专注页标题也不绑定物理左右。

## 剩余实体边界

未部署D4、未发送50节点新组合、未现场抓包、未确认输入IP81真实信号和尺寸。6102窗口回读仅被动诊断，原软件查询触发/操作版本关联未证实，不自动标verified。旧黄金包/原程序模拟向量不替代设备固件行为或画面。

异形/混尺寸墙、拼缝、不可整除裁剪、特殊型号6150/6301/6304、任意未登记IP流以及KVM/固件维护不在本轮范围。电源与两电视toggle保留独立；本轮软件验证不表示已开机、关机或恢复现场布局。

## 补充验证：紧凑交互块与D4输入更正

日期：2026-10-09，任务T015..T021。按用户后续需求，首页/运行设置改为单/双屏及区域内选源，不列12个组合按钮；窗口1/2默认绑定D4的192.168.5.63/64、224.1.1.63/64，IP81不变。此前55/56验证记录及抓包是历史证据，不覆盖当前配置。

本轮只在本机软件层验证，不连接D4、不开关电源、不下发实体墙面、不自动提交推送或部署。D4仍运行此前deploy-20261009-be29491发布包；本补充的界面和63/64绑定尚未在远端生效。

### 自动化与配置

```powershell
dotnet test runtime-dotnet/ScpCv.sln --no-restore --filter 'Category!=Physical' --logger 'trx;LogFilePrefix=compact009-finalb' --results-directory .validation/qa-wall-009-compact/results --verbosity quiet
pnpm --prefix frontend test
pnpm --prefix frontend run typecheck
pnpm --prefix frontend run build:web
uv run --no-project python .specify/scripts/python/validate_specs.py --specs-dir specs
git diff --check
```

- 最终.NET全套1209通过、0失败、0跳过：Domain41、Infrastructure171、Integration262、ControlHost286、Contracts322、Windows127。六份`compact009-finalb_*.trx`在`.validation/qa-wall-009-compact/results/`，命令进程级清空不被NuGet支持的代理，不改全局配置。
- D4绑定回归先红：12组合12失败/11通过；修正默认值后相关协议/映射63通过，Host的9项包含真实Hardware装配与63/64单播/组播断言。两个当前预设也用63/64，显式历史D2配置的400黄金包保持原字节；不修改历史fixture迎合新默认值。
- DOM新增三项先红（原界面14通过/3失败），最终17通过：区域内选源、单/双屏往返保留、选择零自动POST/PUT、同源左右及保存修订；未登记IP和编辑后禁用保持。最终前端全套291通过、0失败/取消/跳过，原始日志`frontend-tests-final.log`。
- 类型/Web构建通过，分别保存`frontend-typecheck-final.log`、`frontend-build-final.log`。既有大入口超过500kB的构建警告保留，不提高阈值隐藏。
- 规范和现行指南同步区分D4当前绑定与D2历史证据。API结构未改，本补充不重跑Redocly；此前009合同校验结果不升级为本轮新证据。
- 所有本轮编辑文件使用UTF-8/LF，主动实现文件均不足500行；扩展hooks配置不存在，按skill规则跳过。规范与空白校验不代替现场验收。

### 真实浏览器与视觉

自有ControlHost与Vite仅绑定回环18944/5198，新GUID DataRoot、随机自有账号、真实认证/CSRF和业务API，SafetyMode=Simulation；不启动Supervisor或播放器。先读取实际DOM及截图，再以可见菜单和真实键盘操作验证，不调用Vue内部状态或模拟业务响应。

- 在首页逐项保存/模拟应用12完整组合，核对实际映射、保存修订与applied_revision；单/双屏往返保留各自选择，选择期间不保存/应用。
- 键盘选源、折叠名称编辑后禁用应用、设置页单组件及已保存草稿一致、刷新保持IP81、未登记239.1.2.3可存但应用禁用通过。
- 桌面1440×1000与窄屏390×844均保持区域内选源，手机左右仍并排，无横向溢出，选源目标至少44px。窄屏保存/应用并排，按可用宽度自然换行；名称和发送详情折叠。视觉按frontend-design沿用现有Fluent令牌，没有另造风格或铺满组合按钮。
- 页面异常0、控制台错误0；明确显示模拟结果而非实体出画。原图`discover.png`、`desktop-single.png`、`desktop-double.png`、`dashboard-desktop.png`、`mobile-single.png`、`mobile-double.png`、`unsupported-mobile.png`及`combinations.json`位于`.validation/qa-wall-009-compact/`。截图前核对控件恢复可用，不把HTTP返回当Vue等待结束。
- 自有Host/Vite由finally按自有PID精确退出；不全局结束node/dotnet，未改远端服务。

### 失败与修正边界

1. 最初`dotnet test --no-restore`退出0但未执行测试：缓存缺少TestSdk props。恢复锁定测试依赖后真实红绿执行，空跑不计为通过。首次全量又发现旧兼容宿主的Office互操作DLL缓存缺失，4个项目820项通过但全量构建失败；完整锁定restore后重跑最终1209通过，原失败保留`dotnet-full.log`，不修改兼容代码或跳过项目。
2. DOM测试选择器误点Naive保留的隐藏菜单，名称断言又误取新增选择器的内部input；修正测试夹具后核对真实区域和可访问布局名称，不改业务规则掩盖失败。
3. 浏览器键盘测试聚焦不可聚焦的选择器外层，焦点仍停留单屏radio，ArrowUp实际切回双屏，后续全屏选择器消失。诊断记录真实焦点/DOM；改为聚焦tabindex=0的选择标签并先核对焦点，再按Enter/方向键选择。只修QA夹具，保留失败/诊断日志，不把此超时称作生产选源缺陷。

### 仍待远端验证

本补充未部署或热改D4。需要后续明确发布/部署后才能让新绑定生效；12组合的实际抓包、真实信号、裁剪/重复窗口及IP81出画仍需现场验收，软件通过不代表物理屏正确。电视电源入口和仅两块大屏输出的限制不变。

## 补充验证：大屏交互块视觉精修

日期：2026-10-09，任务T022..T025。沿用frontend-design与现有Fluent主题，增加屏幕边框、细10×5拼接纹理、柔和分段选择、草稿徽标和发送状态条；保存/应用分出主次，保留折叠选项。Vue业务脚本不改，样式独立为同目录CSS；草稿徽标不代表墙面确认，示意网格不是实际媒体预览。

范围只含交互块视觉、辅助文案及360px顶栏自然换行修正；不改后端/API/输入绑定。未连接D4、未改远端服务、未下发实体墙面或电源，也未提交推送部署。前轮未提交改动保留，D4仍是先前发布包；前轮1209项.NET结果不计为本轮新运行。

### 回归与真实浏览器

- 两个新增DOM回归先红（17通过/2失败），绿色19通过、0失败/取消/跳过：保存徽标与发送状态独立，淡化的空白区域可重新选源，编辑后禁止直接应用，选源零自动PUT/POST。原始红绿日志为`.validation/qa-wall-009-compact/polish-dom-{red,green}.log`，不覆盖前轮日志。
- 自有回环ControlHost/Vite18945/5197、随机测试账号、新GUID数据目录、真实认证/CSRF，SafetyMode=Simulation；不启动播放器。复用前轮12组合流程但将新原图/JSON输出到独立`.validation/qa-wall-009-polish/`。
- 最终`browser-verify.log`退出0：12完整组合逐项保存/模拟应用、单/双屏往返保留、零自动下发、键盘选源、编辑后禁用、首页/设置一致、刷新保持及未登记IP门禁全部通过，页面异常0、控制台错误0。
- 浅/深主题各验证1440、390、360宽，文档宽度不超视口；390和360交互块均高410px，主要选源及保存/应用至少44px。真实Tab到折叠摘要的焦点环至少2px；减少动效实测过渡0.01ms，无可见持续过渡。
- 人工查看初始图及修正后的浅深手机原图，分段对齐、区域内选源、主次操作和状态行均正常。原图`polished-{light,dark}-{1440,390,360}.png`、`measurements.json`、`combinations.json`留在上述新QA目录，旧QA原图未覆盖。

### 失败与修正

1. 第一版截图发现分段越界：Naive外框默认34px，子项已44px；真实几何回归先红，改外框高度为auto后54px正确包含两44px子项。图标和文案实际在内层label里，将布局间隔放到该层。浏览器将inline-flex子项计算为flex，修正测试计算值断言，不改业务控制。
2. 360px首次完整检查发现应急按钮超出视口；DOM边界及原图定位到既有顶栏无换行，非交互块。只增加顶栏flex-wrap/justify-end，保留全部状态/主题/应急入口，并在现有壳层回归补断言；最终六主题/尺寸检查无溢出。失败日志`verify-first-pass.log`、`verify-overflow-red.log`及`overflow-light-360.png`保留。
3. 默认全套测试并发时剩余物理内存约430MB，浏览器登录出现ECONNRESET，Host/Vite无对应错误，不能把资源压力相关性当作已证实根因。精确结束本轮自有测试树后剩余内存约7.7GB，再以test-concurrency=2运行；中断执行不计通过，保留`frontend-test-interrupted.log`及`verify-connection-interrupted.log`。
4. 减少动效首断言强求0s失败；全局base.css已有!important的0.01ms。按可观察的无持续过渡要求改数值预算为不超过1ms，实测全部0.01ms；不覆盖全局规则，保留`verify-reduced-motion-assertion.log`。

### 构建及交付边界

- 最终前端全套通过293项、0失败/取消/跳过，耗时273.88秒；命令为`pnpm --prefix frontend exec node --test --test-concurrency=2 'scripts/*.test.mjs'`，仍覆盖全部30个测试文件，不跳过慢测试。原始`frontend-test.log`包含实际计数，不计默认并发的中断运行。
- 独立类型检查及最终Web构建退出0，`typecheck.log`、`build-web.log`保留；既有大入口超过500kB告警保留，不提高阈值隐藏。
- Spec Kit全目录校验及`git diff --check`通过；需求质量清单16/16通过，扩展hooks配置不存在按skill规则跳过。本轮主动实现文件均不足500行，编辑文件为UTF-8/LF；在known-pitfalls §2追加坑76，防止把DOM绿色或源码样式当真实视觉证明。
- API合同未改，不重跑Redocly，后端和实体测试本轮未执行。自有Host/Vite由finally按自有PID精确退出，18945/5197监听数最终为0，不全局结束node/dotnet；本轮没有D4新出画/部署证据。

## 补充验证：D4紧凑界面部署

2026-10-09，用户另行授权，任务T026..T029；实施与制品完整摘要见[部署记录](../../docs/deployments/20261009-d4-compact.md)。发布代码为三签名提交后的`c824924`，正式五组件Release发布退出0，已有同源码Web制品同步，归档997条目及本机/远端摘要一致。文档后续提交不改变运行二进制；没有将此前293前端或1209.NET结果重复计为本轮测试。

- 内网secondary推送成功，D4干净工作区仅快进拉取；GitHub推送及直连超时，未称全部远端同步。协作停止旧组后冷备份原数据，保留媒体、私有配置、旧包和可恢复旧Web。
- 新交互任务启动在admin session1，group_epoch115；四受管成员登记，两播放器在线且idle、目标仅DISPLAY2/3。原前端任务仍运行pnpm Vite；HTTP明确读取新Vue/CSS模块，不单凭静态包摘要证明界面更新。
- readiness、前端页、新组件/样式及真实代理认证的sessions/runtime/displays/layout均200。TCP18443/5173/8554/9997、原生UDP8890及对应PID确认；最终只读脚本退出0并输出`D4_DEPLOY_LIVE_VERIFIED_READ_ONLY`，远端源码`c824924`、工作区干净。
- 失败保留原日志：WinPS5.1默认ANSI配置读取、JSON数组双包装、UDP CIM RPC失败及Transcript下裸git查询控制台缓冲失败，均以独立原始证据纠正QA读取方式；未改生产包、延长预算或放松门禁。原生netstat只确认UDP绑定，不宣称修复Windows提供者。
- 未下发墙面、电源或重放媒体，未执行D4浏览器视觉/实际像素验收；63/64是新配置，不是本轮实体切换证明。旧`sent_unconfirmed`布局保持，IP81真实信号、12组合裁剪/重复窗口仍待现场验证。
