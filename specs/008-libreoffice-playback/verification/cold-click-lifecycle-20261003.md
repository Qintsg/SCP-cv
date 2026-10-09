# 冷开首点击与动画请求生命周期（2026-10-03）

## 当前结果

两个fresh正式实例在OPEN返回后立即next_effect，首点击实际只显示标题，第二次才显示副标题；诊断OFF/ON都通过，原件保持。
新增前端卸载迟到错误回归先红后绿：组件离开后不再向共享通知发布旧请求错误；当前组件真实HTTP错误仍正常反馈，不改变已受理命令、不重放。
完整前端174/174、typecheck/build:web通过；只改Vue生命周期守卫与测试，不改原生实现、REST/wire、动画进度或末页策略。
T053/T044仍未完整验收，动画末页产品选择及状态闭环、复杂序列/快速多次真实UI、裁切/两窗长稳/完整包仍欠；D4未操作。

## 原生冷开首步：不在点击前加等待

TestPPT第六页同前述样例，自有Form800×450/DPI96、公开Client/正式Host、独占profile/只读副本。
ColdVisual在OPEN返回后立即第一条next_effect，之前没有Observe、截图或sleep；之后1.5秒仅有限采样与截图，不是生产完成预算。
首步要求标题区深色像素≥300且字幕≤20，次步要求标题≥300且字幕≥100；区域同上一轮经过实际图片定位，不是通用OCR/版式判据。

| capture根 | 诊断 | 首步像素 | 第二步像素 | 正常关闭 |
| --- | --- | --- | --- | --- |
| run-7ac14ffe4f6942708b4eff26f2b351b7 | OFF | 2059/0 | 2059/1721 | exit0，11.80秒 |
| run-5957a887defc4b60b4e75b942b1fd2ac | ON/节点ON | 相同 | 相同 | exit0，12.37秒 |

图片在`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-cold-shots/34e7f245253d4cb2a72e2eed315f43f2`及`6c116f8a96fc46eb85ac98a2af59342a`。
root逐张查看，独立视觉agent按文件名核后一组：首图只有“第六页”，次图增加副标题，可见文字完整、无菜单/导航泄漏。
这不是对所有首次打开、first/last、自定义放映、自动/重复/交互或任意延迟的证明；效果命令返回仍早于视觉完成。
没有原生修复，所以不能将两个绿实例说成冷开缺陷已修复红绿；只是补足当前样例的直接证据。

## 前端：共享通知下的真实迟到错误

PresentationEffectControls的网络pending只持续到HTTP响应，不等待自然动画结束；现有两按钮共用防重并且没有动画已完成成功提示。
新增测试保持真实Vue/Naive UI/Pinia/API，只在外部fetch设置门闩：发动画→卸载旧页面→同一应用store挂后续页面→旧HTTP400到达。
首版夹具重新创建Pinia时误得绿，旧通知store同时被隔离，与实际路由不符；不能算生产行为已通过。
夹具增加可选reuseStoreScope，独立测试默认仍新建，afterEach清理；保留同一应用store后两个真实页面均出现旧“动画步操作失败/已卸载旧页面的动画失败”，先红。
生产修复仅onScopeDispose使本实例active=false，catch在仍active时才发布通知；pending防重、store合并、已受理请求寿命与重放策略不变。
两条业务红→绿，原八条仍绿，相关10/10；没有忽略当前页HTTP错误或将卸载当服务器撤销。
此次覆盖“组件卸载”，不是同实例windowId/source变更、任意异常或跨主机状态矩阵的泛化证明；这些其它场景需各自证据。

## 浏览器可见验证

use-my-browser按browser-required路由使用任务自有IAB tab7，回环20886夹具保留真实组件、主题、全局通知与应用Pinia；没有真实后端代理/数据库/Office/D4。
发第二条模拟POST后立即离开控制页、释放旧HTTP400，刷新账本为POST2/pending0，后续页面无动画错误，共享通知provider仍在。
第三条保持控制页不卸载，释放同类400，账本POST3/pending0，真实“动画步操作失败/本机模拟动画HTTP错误”出现，两按钮恢复。
第一条跨工具等待期间超过既有10秒请求预算，在卸载前正常显示超时；它不是迟到隔离失败，也不改10秒预算。随后批量有界动作重新验证。
warn/error控制台最终为空；夹具截图是页面级通知证据，不冒充完整控制台、Native或D4播放证明。
截图`.validation/008-effect-lifecycle-browser/after-unmount.jpg`与`current-error.jpg`，本轮未设viewport覆盖，使用现有浏览器窄视口。
该fixture与普通SCP-cv连接/认证规则不同，没有可泛化的新域名操作事实，site-pattern保持不变。

## 全量、文档与收尾

pnpm前端174/174，零skip；vue-tsc与build:web通过，1100.31kB主包分块告警保持，未修改预算/依赖或CERNET配置。
后端本轮没有变化，不重跑985历史全量冒称覆盖本轮新Native像素；QA构建0告警/错误，Spec/diff/Redocly复核。
本轮测试进程/自有浏览器页与Vite回环监听结束，Resources两原件SHA保持，数据库/原图/旧失败/profile/dump保留；无提交、推送、部署、D4、设备电源/墙面/系统音量操作。
上一目标turn为真实QA实现、误报更正与末页失败证据进展；本轮新增冷开像素门禁、两个DOM业务红→绿及真实浏览器验证，也不是仅状态重述。
