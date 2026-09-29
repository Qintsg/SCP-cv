# 本机 PPT 引擎比较结果（2026-09-29）

## 当前结论与验证边界

两条路线都能播放部分动态内容，但都不能从本轮短时测试推导“稳定无严重 bug”。网页路线的基础接入/关闭时序成本较低，兼容性和极速切换仍有风险；UNO正常Frame生命周期可行，但纯净窗口承载出现原生崩溃，实际集成成本中高到高。

**真实WPF首轮已运行**：自动启动两次被执行策略拒绝，没有改入口绕过。核对exe存在并给用户链接后，用户手动连开两个；分别为PID54444/61628、独立profile和浏览器PID37864/60380。两个都完成46条请求/响应、26张页面PNG，无fatal/page_exception/process_failed，随后webview_disposed并退出。首轮仅两次文稿加载/基本动作，不扩大成10轮多开稳定性；新增A/B专项探针待用户再次运行。

## 范围与版本

- 本机QintsgD；D4不连接、不恢复服务，不操作系统音量或设备电源。
- 原件：Resources/AllinOne.pptx（9页/15,594,183字节，SHA2401652F…A55F0）和TestPPT.pptx（7页/98,756,629字节，SHA0BBAFC49…25C22）。完整SHA见fixtures/inventory.json。
- 前端：Node24.15.0、pnpm11.24.0、Vite7.3.1、pptx-vanilla-viewer3.13.0、three0.183.2，CERNET安装；Chromium151.0.7922.34。
- WPF探针：.NET10.0.400、WebView2 SDK1.0.3537.50；Release构建0警告/错误。用户实际运行WebView2 Runtime153.0.4234.48；与辅助Chromium151不同，结果分开记录。
- UNO：本机已有LibreOffice26.8.0.3；.NET Framework4.8 WinForms承载+自带Python3.13.15/pyuno桥，不是.NET10WPF直接UNO互操作已经证明。
- 两文稿没有页面过渡，仅TestPPT第6页两项视觉点击入场，另有媒体触发。未验证复杂路径/Morph、旧PPT、全部格式、声音输出或长期稳定。

## 同材料比较

| 项目 | 网页播放器：真实Chromium层 | LibreOffice：真实.NET Frame层 |
| --- | --- | --- |
| 文稿加载 | 9/7页与16页截图；非纯图片展示 | 9/7页、原生Frame实际出画 |
| 点击动画 | 第6页白场→标题→标题/正文→第7页；真实按钮进入present | 白场→标题→正文；小播放区域及chrome影响完整保真判断 |
| 多媒体 | MP4/WAV时钟推进；暂停/恢复、离页旧媒体停止 | MP4两张间隔图真实变化；放映paused=true时视频仍变化 |
| 多开/替换/关闭 | 2独立context10轮隔离；真实WPF2进程完成基本序列和退出，专项10轮尚待跑 | 两profile/两.NET窗口，10轮切换并关闭，B源/页/暂停保持；最终文件锁释放 |
| 100次导航 | 页码回读100次，但有Blob请求/console错误，不能称全过 | 准确goto/source/index/running读回100条；不是100张视觉验收 |
| 清理风险 | 立即destroy发生逐轮DOM/listener残留；等待公开退出状态后10轮计数稳定 | 正常Frame关闭/退出成功；额外纯父窗口组合触发APPCRASH |
| 当前稳定化难度 | 中等接入成本；任意文稿完整保真另有较高风险 | 中高到高；窗口承载、原生故障、桥/状态和媒体控制需要处理 |

上述难度是基于本轮证据的判断，不是工期或无bug保证。

## 网页多开、切换和关闭

1. `first-inspect.json`首次逐页16张正常，pageerror/console/requestfailed为0；不能推广到压力批次。
2. `multi.json`两context10轮：A同/异文稿交替load/close，B保持TestPPT页7，页码和实际DOM不串台。关闭后旧媒体暂停且时间不再推进、当前媒体DOM0、Blob live0。
3. raw multi总判定false，40次Blob ERR_ABORTED未逐URL与revoke关联。不能把这些全抹成成功，也不能仅凭中止数断言40次播放失败。
4. `stability.json`100次goto回读正确、TestPPT重建10轮完成，但故障测试前183次Blob ERR_ABORTED、9次Blob ERR_FILE_NOT_FOUND、9条console error。极速切换的加载/撤销竞态尚未定位，不算稳定门禁通过。
5. 缺失URL和坏ZIP明确失败，随后健康文稿恢复成功；原始故障和QA脚本错误分开归档。

### 关闭时序定位与最小对照

原型遵循 `await exitPresentation(); destroy()`仍遇到退出事件尚未收敛就销毁的竞态。20轮纯public load/close、无QA旧元素强引用、每轮GC、末再等10秒，仍9220→9217 nodes、2145 listeners，约每轮+436 nodes/+101 listeners。只读定位document mousemove/mousedown残余来自PresentationToolbar。

单变量对照：public exit后等 `getMode()==preview && document.fullscreenElement==null`（3秒上限）再destroy，10/10退出39–55ms，GC后61 nodes/24 listeners保持，暖态堆约22.09→24.52MB，末10秒不继续增长。说明**当前Chromium完整放映模式下，明确公共退出时序能缓解本次残留**；没有修改vendor，不证明WPF fullscreen fallback也相同。

空create/destroy10轮58 nodes/23 listeners稳定；同viewer交替reload10轮节点635/538随源切换、173 listeners稳定；导航about:blank再reload回56/23。建议后续评估复用实例或隔离页面重置，不把原失败覆盖为通过。

### 打包与内容

- Vite初始500：Node专属@napi-rs/canvas动态import被静态解析；确认浏览器环境guard不会运行该分支后使用调用即throw的浏览器alias。未启用3D仍需要安装three才能构建。没有安装原生canvas来冒充浏览器支持。
- 生产构建34.10秒，2351modules，主块9463.02kB/gzip2599.91kB，Node externalize和大块警告保留。
- 同SHA静态参考1/2/9的独立目视：页1联合两图的弯括号变成细三角/斜线；页2顶缘多半截“算法设计”。页9拼贴主要结构相符。页7图例/轴标签重叠明显，但没有可信同页golden，不推测全部原布局。
- TestPPT6真实逐步截图不证明1秒fade/Y插值完全正确；音频图标及字体继承存在待核差异。静音时钟不证明声音品质。

## UNO多开、切换和关闭

- 正常Frame：`Toolkit.createSystemChild(HWND)→Frame.initialize→loadComponentFromURL→startWithArguments(IsFullScreen=false)`。不是猜窗口标题再SetParent。
- 10轮A播放中替换另一文稿并关闭，B始终TestPPT索引5/paused=true；源、索引和状态不串台。A预热句柄1000，私有内存506.5→530.8MB；B403.1MB/1010。短时样本不是长期不泄漏证明。
- 最初要求A关闭后独占同文件时B合法仍持有，属于QA错误；修正记录B持锁，最终B也关闭后两文件均FileShare.None可读。初次profile冷启动真实39秒，而非3秒观察失败即崩溃。
- B正常Frame追加100次goto回读，独立图像复核第6页内容逐步出现、第3页视频区域真实变化；带菜单/导航且播放区很小，没有UNO AllinOne同页图片，不宣称全排版优于网页。

### 严重失败和控制差距

尝试Hidden文稿直接ParentWindow纯放映，TestPPT加载0.95秒成功，start约11.64秒后BinaryURP disposed；原A PID64504实际退出。Application1000/1001、相同ReportID确认APPCRASH，模块KERNELBASE.dll，minidump237305字节、SHA CBF60513…2570CA。没有栈根因；UseSkia=true不等于GPU故障。不得把这次崩溃归为所有正常Frame都失败，也不得靠无限重启消除风险。

第3页调用controller.pause，前后均paused=true，但约4秒后视频矩形内场景明显变化。这里证明**不能用放映暂停标志代表所有媒体冻结**，不直接断言官方API违反自身语义；项目的统一暂停功能需要额外控制/观测。

正常Frame仍带Impress菜单、工具栏和导航，纯净WPF输出/混合DPI/焦点/.NET10直接binding尚未验证。最初宏模式数值4误当禁宏，实际ALWAYS_EXECUTE_NO_WARN；本次两个PPTX无VBA/bin，未测试宏文件。后续原型参数已改NEVER_EXECUTE0，不能将旧日志说成禁宏已验收。

## 证据与收尾

- 素材：`.validation/ppt-engine-eval/fixtures/inventory.json`、`coverage.md`、`web-visual-review.md`。
- 网页：`web/REPORT.md`、`web/evidence/{first-inspect,stability,multi,memory-control,attribution,exit-order,ui-animation}.json`及原始PNG。
- UNO：`uno/results.md`、`uno/logs/{dual-cycles-v2,navigation-100}.jsonl`、崩溃事件/dump、动画/视频图。
- WPF首轮：`webview/manual-20260929-030129-24c5d1/`和`manual-20260929-030132-5f1a54/`。启动UTC03:01:29/32，结束03:02:04/05；各输出9/7页及动画中间态、媒体状态、关闭图。AllinOne加载3023/927ms，TestPPT3109/2916ms，这是桥接完成耗时，不是视觉出帧p95。每次清理Snapshot empty/DOM0/媒体0；两宿主与带本轮profile命令行的msedgewebview2已无残留，没有收集原生退出码。
- WPF第6页每步350ms取图过早：第二图仍标题，第三图可见灰色渐入副标题；证明动态出现，不用于一请求一动画或完整1秒效果的最终态断言。Root目视真实WPF的括号失真、图表标签重叠与辅助浏览器一致。
- WPF媒体暂停真实时钟：两实例分别保持0.427455/0.441957秒，间隔约0.5秒不推进；resume后约0.5秒到0.949065/0.968810秒，muted=true/error=null/readyState4。只证明本段视频暂停恢复，不证明音响或所有PPT媒体触发。
- UNO自有进程/62131–62134端口已0，原件和副本SHA一致；没有关闭用户应用。
- 网页59123服务暂保留供用户手动运行WPF，PID58452/startUTC02:40:27.6596266；因此当前不能说所有实验端口已停。
- 生产代码和006暂停中的改动未调整；新007规范/报告未提交、未推送。Spec Kit校验通过。要求清单12/12通过只代表评估需求完整，不代表引擎验收。

## 下一步边界

真实WebView2双进程专项：程序已更新默认dual配对，两窗不同位置，B固定TestPPT页7，A10轮交叉20次load/100goto/关闭，公开closeStable等待退出收敛。用户需再次手动连开两次；专项之前不把首轮基本播放当完整稳定性验收，也不选择正式默认引擎。

专项读码复核已发现并补强测试自身的假通过风险：B身份/PID开始时间、A每动作前确认B活跃、运行时错误中止后续命令、末尾释放后再发布terminal，B末尾重查固定状态。双侧最终日志和进程检查仍由root联合判断，单一a-finished不等于整体成功。准确计数为20load/100goto/10round、约20次有文稿关闭及额外空关闭；只读state和少量截图不是100次像素验收。新程序已编译但尚未执行，不能把读码补强记成运行通过。
