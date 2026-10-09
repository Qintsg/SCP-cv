# 导航输入闭环与原生调查（2026-10-03）

## 当前结论

T049完成有限导航输入切片：缺失/非法goto目标不再默成第一页，UI不再将2a/1.8截成页码。
15HTTP、15真实DOM及桌面/390夹具通过；最终重新编译串行执行六测试项目962/962、零skip，前端164/164。
T044原生导航/动画完成仍未验收：本轮两个独占实例均在Office开始监听前超时，未执行任何导航；旧失败保留。
不改变动画能力或将同页Confirmed当效果结束，不修改原生预算/承载架构，不访问或恢复D4。

## 输入红绿与保留语义

公开认证上传自有占位PPTX→OPEN受理→保存完整GET sessions及额外正式存储队列投影，
POST goto没有target_index，修前HTTP200而期望400；`navigation-target-red.trx`真实业务红。
RuntimeStateService在Enqueue前只对goto要求正int32；修后400 invalid_navigation，两观察面不变。
first/last仍可按已知页数派生，未知总页数、seek、动画及wire500上限未在本切片修改。

HTTP保护15例：缺失、null、字符串、小数、int32越界、0/负数、布尔/数组/对象，
以及合法1/7/500的意图参数、first/last按自有已知total7派生1/7。500受理不证明Native有500页或已显示。
主验证是HTTP拒绝/GET会话不变，队列只读投影为额外副作用证据；未新增生产命令查询入口/任意SQL/私有反射。
TRX在`.validation/008-navigation-input/`，最终matrix15/15；分析器或夹具错误不冒充业务红。

真实控制页DOM输入2a点击“跳转”，修前外部HTTP账本出现navigate POST；修后不发送且显示既有整数提示。
UI采用trim后完整十进制数字、安全正int32，不加500或超总页数限制。
15DOM覆盖2a/小数/0/负数/空/空白/Infinity/int32及安全整数越界/科学计数拒绝，
1/trim2/0002/501/int32max合法提交；合法HTTP夹具只受理并保留旧实际页码，不造Native完成。
输入框原本没有Enter入口，本轮不新增；真实按钮的键盘激活另由浏览器验证，不合成click伪称键盘。

## 原生公开wire调查：两次启动失败

新`.validation/008-wire-navigation/`只引用正式PresentationProcessClient，Form为本机800×450/DPI96自有父窗，
TestPPT只读副本/摘要独立，scope/OS父身份/stdio/job沿生产路径，非D4或正式双输出包。
正式Framework宿主和QA构建均0警告/错误；这是运行调查夹具，不是新增生产依赖或交付播放器。

| 新实例 | 父/侧车/launcher/bin | 端口 | 实际结果 |
| --- | --- | --- | --- |
| ab5d4cac9f7441ffaf89accdd9875902 | 67164 / 33216 / 59768 / 31288 | 30281 | open未返回，Office监听30秒超时，Host退出2 |
| c709e81793494f70b2bf9671ac2393ba | 54080 / 56096 / 69604 / 23104 | 65261 | 同阶段监听30秒超时，Host退出2 |

第一根`run-4db00189f71b40edb4cf7faa28b56b0c`/`launcher-13fff6e32bd14c4494677f9b4e07848a`，
第二根`run-eb8506581f6741118fc571c564a4a10f`/`launcher-8617de3385ef4be8bacd6dc7a06b5587`。
两次都是新profile，前次进程已确认消失才开始下一次，不复用故障实例或在观察超时后盲重启。
client30秒请求到期保持transport uncertain，Dispose读取Host exit2保持cleanup_unconfirmed；
Host自有job兜底，日志active0/members空，精确进程及两端口最终均消失，不能记为正常文稿关闭。
未进入Frame创建/导航/动画，故不能计次数或把这些超时叫作导航死锁复现。
没有现场像素/媒体截图，本轮不宣称原生视觉通过。用户原件与副本SHA保持TestPPT既有值。

只读源码链：wire next_slide/previous_slide/effects仍直接Controller；Player NEXT/PREV/GOTO已UI goto，非同一入口。
官方Controller nextEffect是启动下一批触发效果，previousEffect可异步撤销/跨页；末页next_slide可结束屏而页码仍末页。
listener begin/end可来自自动动画或撤销重播、无OperationId；整页animationsEnded不能当每步回执，
同页redisplay也可有transition事件。未用事件计数猜完成，也未绕Controller直接调用engine作为新方案。
实际启动根因未定位；自有profile更新日志不证明更新检查就是阻塞原因，后续OS内存错误亦不能倒推两次启动根因。

## 全量失败保留与复跑

首轮.NET/前端测试/类型构建同时运行，后端960/962：
ReconnectSendQueue在GetProcessInfos分配时OutOfMemoryException；OrdinaryBenchmark的PowerShell加载失败HRESULT0x800705AF。
Windows将后者定义为提交内存限制错误，见[官方1455错误说明](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--1300-1699-)。
原六TRX保留于`.validation/008-navigation-final/`，不删失败/skip断言或把它们当产品回归已修复。
事后07:51:30采样commit约38.26GB/47.84GB、79%、可用物理7845MiB；不是失败瞬间采样，不能证明完整因果。
本机Vite自有PID44160当时私有内存约2.11GB；未关闭用户或MCP进程、调页文件/安全配置来制造成功。

前端/构建已结束后，仅改变项目执行并行度`-m:1`重新编译执行全部非Physical项目，内部并发测试/断言/预算不改。
最终Domain41/Windows86/Contracts247/Infrastructure127/Integration212/ControlHost249，共962/962、零skip，
六TRX在`.validation/008-navigation-final-serial/`。这证明串行项目回归通过，不声明并行资源压力或Native风险解决。
前端164/164、零skip，typecheck/build:web退出0；主包1100.29kB及分块/插件计时告警保留。

```powershell
dotnet test runtime-dotnet/ScpCv.sln -c Release --no-restore --filter 'Category!=Physical' -m:1 --logger trx --results-directory .validation/008-navigation-final-serial
pnpm --prefix frontend test
pnpm --prefix frontend run typecheck
pnpm --prefix frontend run build:web
```

## 真实浏览器与收尾

IAB自有tab5/6、20885回环夹具，实际PlaybackControl/Pinia/API/主题/通知宿主。
desktop2a拒绝POST0；按钮Enter提交trim2，POST1；390的1.8拒绝仍POST1、合法3变POST2。
账本后续HTTP读回确认仅两个合法目标2/3，不以本地注入store或后台curl模拟浏览器操作。
首夹具遗漏专注路由造成Vue Router告警，并在并行负载期间有三次账本fetch失败；这些日志/截图保留，不标0错误。
只修夹具路由装配及账本失败明示/手动只读刷新，未修改产品主题/通知/导航控制以掩盖。
新tab6在390重新输入2a并实际刷新账本：POST仍2、当前3；warn/error为空，scrollWidth380/viewport390，无横向溢出，既有通知实际渲染。
截图在`.validation/008-navigation-browser/desktop-invalid.png`、`mobile-invalid.png`、`mobile-invalid-final.png`。
模拟受理不是正式HTTP端到端或Native跳页；HTTP真实服务合同已由另一层15回归验证。
viewport恢复，自有tab关闭，Vite PID44160/start07:43:18/父728/命令核对后停止；20885无监听。
Resources摘要未改、Office/Host/导航Probe为0；用户数据、安装、旧dump及QA失败profile均保留。
Spec Kit/diff通过，官方缓存Redocly2.55.0为0错误/3个退役入口告警；新增oneOf的区分字段已显式必填，不保留本轮组合告警。
一次性夹具无可泛化域名新事实，既有localhost笔记不改；D4未访问、未推送/部署/启动。

## 剩余门禁与官方依据

T044仍需真实wire相邻页/动画步、边界/跨页/视频往返/画面/正常关闭完成证据。
T045裁切架构需人类确认，不叠第四个几何猜测；用户实例共存、旧版/加密/宏样例、最终双窗60分钟仍缺。
T046完整最终发布包与两输出绑定仍未验收。本轮T049完成不缩小这些要求。
官方固定版本：[Controller语义](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/presentation/XSlideShowController.idl)、
[动画事件](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/animations/XAnimationListener.idl)、
[SlideshowImpl](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshowimpl.cxx)。
