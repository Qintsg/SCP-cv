# 未终结指令投影与动画中断调查（2026-10-03）

## 结果与边界

T052完成有限投影切片：最早Pending/Processing在领取、Worker状态报告、新HTTP入队期间保持；终态后转下一条或空值。
首五条按三轮真实红→绿，最终15条公开接口/认证HTTP/独立SQLite回归通过；两控制页面8条真实DOM保护通过。
完整非Physical最终985/985、零skip，前端172/172、类型/Web构建通过；首完整后端有一条丢ACK重连超时，原记录保留，不宣称根因已修。
原生动画中关闭/立即换页及精确job退出两短轮通过；快速连续三次效果调用仍停第六页，不能算效果步序已完成。
没有动正式动画协议/能力或界面禁用策略，没有原生像素/视频时钟验收；T044/T045/T046、裁切及60分钟门禁仍欠。

## 真实投影缺陷与修复

已有durable队列按目标顺序领取，不是单槽覆盖；动画和相邻翻页不会互相合并，同目标Processing存在时不能再Claim。
旧CommandRepository刷新与RuntimeProjectionPublisher只查询Pending，实际出现三种过早投影：

- 领取NEXT_EFFECT后认证GET报告空值（Expected NEXT_EFFECT / Actual空）。
- Worker报告时把Processing头项换成后来Pending：显示NEXT_EFFECT→PREVIOUS_EFFECT，音频PLAY→PAUSE。
- 执行中再HTTP入队SET_VOLUME，显示/音频头项被SET_VOLUME遮住。

三轮TRX分别为`.validation/008-unfinished-command/claim-pending-red.trx`、`state-pending-red.trx`、`enqueue-pending-red.trx`，旧失败没有覆盖。
修复保持写协调器、目标、claim及一般替换/合并策略：Claim及Enqueue业务回调后统一刷新最早未终结项，Worker采样也包含Processing。
Enqueue新实体尚未SaveChanges，SQL旧状态可读到已在本事务终结/替换的行；刷新合并Local并按tracked当前状态过滤，避免漏新记录或复活旧行。
命令参数投影沿同一头项。只增加队列投影，不反向修改命令Args、Actual源/页码、文稿generation或动画完成状态。

最终15条在`pending-all-final.trx`：领取1、两目标采样2、新HTTP入队2、completed/failed/uncertain与重复回执6、音量合并2、显示CLOSE替换1、三目标隔离1。
终态推进下一条、Processing阻止并行领取、最后清空、重复回执不擦后项；窗口1完成不影响窗口2/音频，公开Claim核名称和参数。
这些完成请求由受控测试Worker提供，不是UNO动画完成证明；Simulation不启动原生播放器或改变声卡。
独立只读复核未找到可确认回归，事务刷新异常仍整体回滚、提交前不Wake；未证明全部故障/时序矩阵。

## 前端保护而非动画语义实现

`frontend/scripts/presentation-effects.test.mjs`保留真实Vue/Naive UI/Pinia/API，只替换外部fetch，两页面各四项：
网络在途同步双点/交错上一步只1POST；HTTP400真实错误/释放且显式再点才重发；受理保持实际页与参考图无完成提示；旧REST不覆新SSE页码/图/pending。
8/8是已有行为保护，未虚称这些有业务红或本轮前端生产修复。显示页/专注页对pending禁用仍不一致，未据此擅自选择动画策略。
DOM不是GPU原生像素或浏览器桌面视觉测试；本轮没有新页面布局，未重做实际浏览器截图。

## Native有限中断与快速调用

正式Client/Net48Host，自有Form/TestPPT副本、fresh独占profile，不操作用户Office或D4。
本轮记录DPI=144，不沿用旧96DPI结论；请求父尺寸800×450，诊断client533×300。几何字段不足以证明满幅，也不与旧裁切条件混为一谈。
流程：open6→两效果调用→立即close→副本FileShare.None只读独占句柄成功→documentEpoch2重开6→两效果→立即goto2→goto6→三次快速效果→有限采样→明确goto7→末页效果/采样→goto1/上一效果/采样→整页边界→视频页2→close/shutdown。
每段1.5秒仍仅采样窗口，不作为产品完成预算；未做dump扰动，不改变原生30秒/关闭15秒等预算。

| capture根 | 返回/失败 | 最终正常退出 | 判据 |
| --- | --- | --- | --- |
| run-45a41ef70fc648bfb5a9db89fdbbb7e5 | 19/1 | 未作为整体通过 | 三次快速推进后硬断7，实际6；父ExitCode未取得、启动器误返0 |
| run-0ca52f85ee4740359c20b9785ae5e79f | 19/1 | 失败保持exit2 | 同失败复现；未改原生动作，启动器缓存句柄后取得2 |
| run-de2fb986633c4ca080da7d0eda77f9c7 | 43/0 | exit0，14.356秒 | 诊断开；快速推进缺口明确记录，边界页码/中断/关闭通过 |
| run-200f981fcd16431380ea9e3ac4196db5 | 43/0 | exit0，14.156秒 | 诊断关；同有限判据，非效果步序/视觉通过 |

后两轮先关闭约0.054/0.252秒，独占副本句柄成功；最终close约0.451/0.459秒并完整job退出。
首两失败保留；后两轮改QA为明确记录快速三次仍6，再显式goto7独立测边界，不是修产品或以新判据撤销旧缺陷。
末/首页页码不变不证明没有黑色结束画面或所有效果内容正确；`effects_visual_verified=false`始终保留。
PowerShell5.1父对象需在运行期缓存Handle、退出后验证非空ExitCode，避免失败日志被启动器成功掩盖；这是QA修复，不是原生成功证明。

## 全量失败与复核

首完整后端984项有1失败：`LostResultAcknowledgementReconnectsAndReplaysWithoutExecutingAgain(uncertain:true)`等待replayedResult在原15秒取消，第一连接已读到结果。
六原TRX在`.validation/008-unfinished-final/`；没有记录第二次连接/握手/回放的细分阶段，也未在等待时及时观察serverTask/run提前fault，根因未知。
该测试只用自建NamedPipeServerStream和RuntimeWorkerSession，不实例化本轮改的CommandRepository/写协调器/投影；不能直接归因到投影或凭压力猜根因。
原预算单独两分支复核2/2（154ms）在`.validation/008-unfinished-recheck/lost-ack-recheck.trx`；随后完整重新构建985/985、零skip：
Contracts255/ControlHost264/Domain41/Infrastructure127/Integration212/Windows86，六TRX在`.validation/008-unfinished-final-recheck/`。
多1条为后补三目标隔离，未偷偷排除原失败；未改15秒预算、内部测试并发或重连生产实现。不将复核绿说成该偶发根因解决。
前端pnpm全172/172，typecheck/build:web绿，1100.29kB主包及插件计时告警保留；Redocly2.55.0为0错/3退役410告警，Spec/diff通过。

## 用户澄清与剩余工作

用户确认动画中再次下一步应“快速完成当前动画后下一步，类似普通PPT放映”。已通过speckit-clarify写入spec的2026-10-03回答、US1/AC5和FR-014，质量清单16/16保持，无标记改动。
对应T053先调查官方控制器/UI路径，不能用无证据双调用、固定延时或事件数实现；当前代码尚未满足新行为，不勾完整T044。
后续只读官方26.8源码给出解释线索：gotoNextEffect调用nextEffect及update；SkipEffectEventHandler快进后是否继续取决于skipTriggersNextEffect，主序末子项该值为false。
因此快速第三次仍页6可能是最后效果只被快进的原生行为，与本轮观测一致，但没有节点因果/像素证据，不直接确诊丢事件；需与用户要求的“快进后推进”分别验收。
依据：[控制器与键盘推进](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/sd/source/ui/slideshow/slideshowimpl.cxx)、[SkipEffectEventHandler](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/usereventqueue.cxx)、[主序子项注册](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/sequentialtimecontainer.cxx)。
本轮自有Office/Host/Probe/测试与监听均已退出；Resources两个原件SHA保持原值，用户库/旧QA/profile/dump保留。
未提交、推送、部署、访问或启动D4；未操作电源、墙面、系统音量、用户默认Office配置或安装。
