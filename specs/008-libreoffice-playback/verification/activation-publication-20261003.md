# 原生候选切入、空输出与隔离状态（2026-10-03）

## 当前结果与验证范围

公开Player复现候选Activate抛错后，采样仍source202/generation2/playing；实际身份发布早于切入，且异常未清候选。
现改为切入成功后发布。旧原生已确认关闭、候选也已释放时返回failed/presentation_activation_failed及明确空输出；旧页图仍活时不清旧源。
候选清理未确认时保留自有归属、typed uncertain、拒绝普通控制/采样，CLOSE与退出仍检查它；不重放外部关闭。
确认空输出后的后端翻页不再假completed，控制页与专注页键盘不再发送导航；恢复后控制重新可用。
最终非Physical1002/1002、前端177/177、类型/Web构建与实际浏览器证据另列。本轮没有Office、实际显示器或D4操作，不完成满幅/媒体/完整包/60分钟门禁。

## 业务红与修复

- `.validation/008-activation-red/red.trx`：Win32切入异常已发生，后续公开Sample仍返回source202、playing、fitfalse；不是只测试异常是否抛出。
- `008-activation-cleanup-red/red.trx`：切入与清理双失败最后只抛普通IOException，掩盖第一原因且候选归属遗失。
- `008-activation-retirement-red/red.trx`：T056双清理失败后NEXT_EFFECT仍不抛错；同样必须隔离，不能只保护Activate路径。
- `008-activation-identity-red/red.trx`两项：内部空模式为字符串空值，不能让投影解析明确清None；内部无current现在发送none，REST仍沿既有合同返回空串。
- `008-activation-navigation-red/red.trx`：确认空输出后的公开NEXT仍成功；现在NavigateAsync无_current明确拒绝。

新独立Activation partial只拥有候选切入/空输出及未确认候选集合；准备→旧原生退役→切入顺序保持，源/布局/playing在Activate返回后提交，不改Frame/尺寸或原生预算。
清候选成功、旧原生已关闭或冷开无旧源：current/source/页码/布局清空，error、当前代次和失败原因明确返回；不是把失败候选宣布可播或保留已关闭旧源的playing。
旧页图未退役：候选失败清理成功后原异常保持，旧源/布局/表面不变。未知候选不能直接报告“全部已退出”。
未确认候选集合同时接收切入失败清理与T056退役失败清理；只由WPF owner访问，采样在所属Dispatcher核隔离。
普通命令包含新OPEN均明确不确定拒绝；CLOSE/RESET可尝试自有清理但未确认不能成功，Dispose检查集合且保持真实首次终态。
测试adapter自行缓存一次外部关闭结果验证CLOSE/Dispose不重放；实际LibreOfficeSurface既有共享关闭Task保持，没有新增UNO重试。
T056原受控adapter的“仍采实时旧playing”断言已改为隔离时无新采样；历史源/布局保留由既有持久结果保护证明，不把未确认外部资源假报实时健康。

## 公共回归与持久边界

Player隐藏STA、真实WPF/公开Execute/Sample，只有外部IPresentationSurface可控；未Show、handle0、纯包元数据夹具不作为可渲染PPTX。
新增切入失败、双失败归属/隔离、冷开/换源两空身份恢复、真实页图保留、CLOSE不重放、退役双失败隔离及空输出NEXT拒绝，共8项。
之前13生命周期与4页图保护保持；本轮不把树引用、外部回调或关闭次数称实际Office退出/出帧。
真实Worker/管道/独立SQLite新增明确空输出结果形态：failed终态、ActualSource/Fit/Adapter/页码清空、模式None而desired源保持。不是把整个Player接入Broker或证明cleanup_pending全部生命周期。
初版该层误把HTTP None预期为字面none，实际规范和PlaybackModeName均为空串；原失败`008-activation-related/pipeline.trx`保留，仅修断言，不改HTTP合同。
测试缓存关闭曾误用内部PresentationCloseCompletion而CS0122构建失败；改为外部adapter自有Task缓存，未开放生产内部类型或把编译错误当业务红。

## 前端与真实浏览器

真实Vue/Naive UI/Pinia/HTTP三DOM：无actual源禁用导航、专注页ArrowRight不POST、恢复实际源后启用；首禁用false及专注页POST真红→绿。
只对明确actual_source_id=null保护，旧缺字段兼容保持；不禁用关闭，不把desired源当实际播放。专注页nav/jump/key及两处动画禁用同样保护。
真实IAB自有tab8/127.0.0.1:20887夹具保留真实PlaybackControl/主题/store，模拟REST而无真实代理/数据库/Native/D4。
初图错误/未确认正确但翻页仍可用；热更新后翻页/跳转disabled、动画消失，恢复阶段重新启用，再回空输出仍禁用，控制POST始终0。
页面保留候选文稿202作为所选源标题，实际参考明确“未确认”；没有将该标题或恢复模拟状态称原生出画。
截图`.validation/008-activation-browser/before-empty.jpg`、`after-empty.jpg`；实际正常viewport1280、content1270无横溢，未设置覆盖。
页warn/error日志最终空；首次工具Statsig初始化提示不当应用缺陷。该一次性fixture不符合普通控制台连接域规则，无新可复用域事实，site-pattern不变。

## 全量与收尾

首次本轮后台1001全部绿后补空输出NEXT保护，最终六项目重新构建、项目间串行1002/1002零skip：Contracts255/Control264/Domain41/Infra127/Integration214/Windows101。
六TRX在`.validation/008-activation-final/`，前段1001于`008-activation-full/`；相关/原红记录保留，不将本次全绿称旧lostACK根因修复。
pnpm前端177/177零skip；类型检查与Web构建通过，主包1100.50kB分块告警保持；Spec/diff通过，缓存官方Redocly0错/3既有退役入口告警。Session schema同步确认空输出时清实际源/布局。未安装新包或改CERNET，pnpm启动保留同一真实进程链，未因观察超时重开服务。
浏览器自有页及Vite回环20887收尾、测试进程退出，Resources原摘要保持；用户库、原件、旧失败/profile/dump保留。
无提交、推送、部署、D4访问/启动、墙面、电源或系统音量；目标保持active，本轮为公开业务修复与真实UI证据，不是完成全部原生门禁。
