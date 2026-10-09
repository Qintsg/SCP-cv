# PDF缓存修复与D4非PPT源实测（2026-10-04）

## 本轮结果与范围

修复PdfPlaybackAdapter缓存命中不更新CurrentPage；两真实PDF测试红→绿。
D4窗口1/2各完成一轮图片→PDF→视频→自有网页→关闭，另两轮补视频定位/恢复取证。
两指定输出均有真实内容截图、关闭全黑、文件释放及正常进程退出证据；不是两窗同时、完整运行组、
媒体登记/上传HTTP或60分钟稳定性验收。直播和背景音频尚未在此轮执行。

## PDF公开回归

OpenAsync(1)会预渲染相邻第二页。旧RenderPageAsync(2)在门禁外返回缓存，CurrentPage仍为1。
实际公开Physical测试得到expected2/actual1；另一项坏页号/释放后访问通过，原红TRX保留。
缓存命中移入原_gate，ValidatePage后更新CurrentPage再返回，不改页内容、预渲染范围或原件。
两Physical最终2/2、0跳过；首/次页PNG摘要不同，回退当前页1，原PDF摘要保持。
PlayerRuntimeHost本来另行设置自己的_currentSlide，不能据此宣称之前所有前端PDF页码都错误。

新测试`PdfPlaybackAdapterPhysicalTests`通过SCP_CV_QA_PDF显式提供只读多页样例，不启动Office/窗口。
本次用既有3.9MB、9页、960×540、无加密/JavaScript/表单的转换PDF；Poppler实际渲染前两页并直接核对，
测试输入及生成的QA参考图保留，不重导出原件。它不是任意PDF或复杂表单兼容性验收。

证据在`.validation/008-media-source/pdf-cache-red.trx`、`pdf-cache-green.trx`。
六项目串行重新编译/执行Category!=Physical，1082/1082、0跳过：41/127/229/269/303/113，
六TRX在同根full-nonphysical目录。前端未改，本轮没有重跑上一轮223/223或浏览器。

## QA包与失败记录

独立Net10 QA复用正式PlayerWindow/PlayerRuntimeHost及原生截图保护，不连接业务库/命令管道，
租约为测试自有值；图片/PDF/视频都是全新根副本。VLC仅该测试实例静音，不调整系统或另一窗音量。
截图仅自有可见/非cloaked、中心根HWND匹配的800×450区域，线程DPI上下文恢复；
这不是全屏/视频墙物理映射或控制桌面检查。

新项目首无assets构建失败，正常还原遇NU1900：官方漏洞base下载60秒超时；
随后curl可读同URL，不等于NuGet已经正常，原失败/下载内容保留。
使用既有独立NuGet.audit.config的官方data.nuget.org审计端点，包源仍官方、审计/告警保持，
临时将本构建进程socks5h改socks5并finally恢复，正常还原/构建0告警0错误。
仓库/用户配置不改，也不能据本次成功称原端点超时根因已解决。

QA在本机审查时复现自身FileShare.None句柄阻止第二次读，先释放测试独占句柄再算摘要；
原80MB基础包已上传但未执行旧QA入口，用小覆盖包修正后才运行。旧包保留，非生产文件锁缺陷。
Framework csc首次误解析正斜线源路径，改实际Windows路径后编译成功；
首证据归档因抄错GUID失败，窗口2尚未启动，随后按目录/身份枚举修正；不是播放故障。

基础ZIP F65C5C2A…D9E544，小修正ZIP02A2B37F…5D44C3；正式Player DLL
4AB18ABD8B7BA6E2868EF3E60CBD89FC89FB2F22FC1D29DED6E56367D8845D4E，远端解包后核一致。
后两诊断覆盖包分别1F62E3C8…CC7865、B0DFE7F1…EF398B6，仅补QA采样，不改正式代码。
新D4根`.validation/008-d4-media-71d0e0052d8345aaad0d7ce2199d96c1`，上传包不是secondary源码同步。

## 两输出有限基线

| 运行 | 归属与输出 | 结果 |
| --- | --- | --- |
| 窗口1基线 | PID7300，session1，DISPLAY2，rect80,80,880,530；run-12a7bf89… | 约23秒、task/parent0，三副本独占读和摘要通过 |
| 窗口2基线 | PID18124，session1，DISPLAY3，rect2000,80,2800,530；run-3fcfaf94… | 约6秒、task/parent0，同样正常释放 |
| 窗口1定位补测 | PID2120，DISPLAY2；run-35361345… | 约7秒、正常0，真实时钟及定位后画面 |
| 窗口2连续帧补测 | PID17780，DISPLAY3；run-f6c5bb2d… | 约8秒、正常0，两运动帧及正常退出 |

缺失图片FileNotFound被明确拒绝，旧图片保持；图片原图/失败后两PNG同SHA4E48A3CF…2A97B5。
PDF1→2→1真实图分别12FA077B…E0FE5、9720E95D…8F7EC6、12FA077B…E0FE5，当前页回执2；
两输出基线对应图字节摘要相同，主agent逐图核内容，不依摘要单独判出画。
网页为自有回环HTTP静态HTML，截图显示中文QA内容，成功导航后退出；未测试复杂站点/刷新/媒体或断线恢复。
所有after-close为360000像素纯黑，SHAB49D3062…C95C34；原图未编辑。

VLC输出D3D11VA/NVIDIA RTX A4000信息，同时有SetThumbNailClip failed 0x800706f4；
stderr保留，不因正常退出/截图存在而宣称该告警根因解决。
第一视频采样是全黑、稍后是片头（8A1653AD…C30952），只能证明有限解码，不能当机械臂运动。

## 视频时钟与实际画面分层

素材为Resources/机械臂.mp4，H.264/AAC、158.321429秒，SHA32B725D3…DC4EDF不变。
窗口1基线playing0→652，paused652→652；窗口2基线同类暂停检查通过，不能称音频已测。
窗口1补测：0→658，paused658保持，恢复后playing，定位30000→30583。
窗口2补测：playing0→400，paused654→654，恢复后定位30000→30584→31335。

两定位即时截图仍是旧片头，虽时钟已30000；延后采样显示与原视频30秒参考一致的机械臂场景。
窗口2后两图EBFBEFAD…AD38D1/5B09D56C…973DB9可见实际机械臂及右上仿真模型姿态变化，
不是仅靠SHA不同猜运动。首时刻/后时刻不是精确GPU完成时间，QA800ms观察间隔不进入生产等待或协议。
这一异步取证限制继续保留，不以回执或时钟当画面立即完成，也未为其叠加固定延迟修补。

## 证据与收尾

四份ZIP均远端/本机核SHA一致，原样解压，包含日志、副本及原图：

- media1-evidence：D2F3259C958407FD61180D9CB09E16FC8110A9867E77AE01AF7B560350AF5798。
- media2-evidence：AAEFCA4BAFCB2576D4E8923E7578EABC63AA87A69C58204B83EC638C235ADE1A。
- motion1-evidence：9E38EBE4255458CFF71375B0009B8EF9CC0AFD09A604DACD6E97E2AC31B89276。
- motion2-evidence：4632D2F51A24C5C16E463E0A0CF09949C91E70A482BAC83944EA949BB91C05FE。

四任务仅在Ready/instance0后精确移除，可按保存的参数/包重新登记；不删除包、数据、原件或日志。
最后OwnProcesses0/QaTasks0，含项目WebView2进程；项目服务保持停止。DataRoot/业务库、电源、墙面、
系统音量和控制桌面不改。D4源码仍b2f09c5且清洁，正式源码尚未secondary推送/拉取。
Spec Kit/diff及UTF-8/LF检查通过；当前全部优化和T073/T074仍未完成，后续必须补直播、背景音频、
真实登记/编辑/API、两窗同时与混合10轮100控制/60分钟，不以这些有限门禁替代。
