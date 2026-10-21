# 020 本机实施与验收

日期：2026-10-21。用户确认A后正式接入；不使用computer use。浏览器只读截图，没有真实鼠标键盘模拟，没有连接D4、发现场控制包或修改本机显示/声音/信任策略。

## 完成范围

正式A输入菜单、固定31路目录／有界发现、受保护工作站媒体、65/66预览、所选摄像机预览、右侧电源与电视内容、大屏按钮内发送进度、独立电视矩阵协议、逐输出持久记账与预案去重、预案编辑／捕获／YAML均已接线。原生PlayerWorker仍恰两窗。

Web同实际流共享一个传输／decoder和多个canvas；Electron受限Main与随包自包含helper直接拉RTSP／RTMP，普通Web不走新IPC。原码H264优先、不兼容受控回退，完整票据/主机/源/顶层所有者和Job回收保持。

019原型B/C与临时入口已删除，A吸收进正式组件；原型保存点19356ee保留，不删除用户资料。

## 默认完整回归

| 检查 | 最终结果 |
| --- | --- |
| dotnet restore ScpCv.sln --locked-mode | 通过；含新ClientPreviewHost锁文件，不关闭NuGet审计 |
| dotnet build ScpCv.sln -c Release --no-restore | 通过，0警告/错误 |
| dotnet test ScpCv.sln -c Release --no-build --filter Category!=Physical -m:1 | **1766/1766**，0跳过 |
| node --test frontend/scripts/*.test.mjs | **1144/1144**，0跳过 |
| vue-tsc --noEmit、Web/App构建、Electron TypeScript | 全部通过 |
| Redocly OpenAPI3.0 | 有效；4项原有101/退役接口2xx警告，没有新增错误 |
| Spec Kit全目录校验／diff空白检查 | 通过 |

.NET各程序集：Contracts346、ControlHost519、Domain50、Infrastructure264、Integration315、Windows272。最终串行日志`.validation/input020-dotnet-serial.log`；前端`.validation/input020-frontend-complete.log`。本轮增加的输入／工作站／HTTP／相同输入强制重发／Popover／归属／电源进度／摄像机当前一路及固定禁控都在完整套件中。

只读文件检查：手写变更代码均小于500行；EF自动生成的ModelSnapshot原已超过500行，本轮新增列按框架固定结构维护，不人为拆坏生成器。代码有头注释，新增锁文件统一LF。既有Vue头注释在script首段，不是缺失。

## 真实媒体与原生

- Web合成RTMP／实际MediaMTX／FFmpeg／TestServer出口：显式Physical **2/2**，65消费仅一上游一编码，两FLV出口可解码，调档、时间戳和释放通过。
- 同源65→66真实canvas：1 HTTP消费流、1 decoder、2租约；几何重排与提示改变不重连，全部有真实非空像素、1px状态边。
- 冷首帧5+5轮，018→020：p50 **4100→2915ms**，p95 **4150→3573ms**。热新增画布5轮：p50 **5.9ms**、p95 **6.7ms**，来自实际decoder帧，不用首字节代替。
- 实际Electron44.2、正常受信HTTPS和正式Main/app协议：RTSP/RTMP各12帧1280×720，末轮首帧1706／1142ms，0错误、无Node暴露。H265原码拒绝后兼容输出46帧，前一进程退出才切换。
- 上游建立后不返回数据：EOF21ms、心跳4081ms、helper强杀35ms、Node父70ms、实际Electron父强杀47ms，均零自有残留；不是只测正常quit。
- 电视原构造纯CPU对照186向量一致；真实127.0.0.1单TCP回环 **1/1**，同连接35字节6231+610D，不套用50节点大屏序列。

详情／命令分别在[共享预览](verification-preview.md)、[Native](verification-native.md)、[电视／预案](verification/tv-routing.md)。认证REST/TLS内存夹具、真正媒体字节、真正客户端和现场实体严格分层。

## 正式页面只读验收

Dashboard／摄像机／预案×1440×900／1280×720／390×844×明暗两主题，**18组**零页面/console错误、零横向溢出、零外网/业务写；追加1280短屏明暗两组严格main650/650、菜单609/609，无裁overflow。手机保留正常纵向和预案表内部横向滚动。预览错误样式为内存空流夹具，不冒称现场画面。

查看的最终界面：`.validation/input020/final-light-1280-dashboard.png`、`final-dark-1280-dashboard.png`及摄像机／预案手机截图。原始指标`result.json`、`dashboard-height-final.json`；初版需滚76px的指标保留，不覆盖失败证据。

## 失败记录与修复，不隐去初次失败

1. 原3/4拒发断言与新65/66需求冲突：替换为登记后可应用，并补未知75保存可预览但不可发送，未放开任意地址或余数几何。
2. 电源按钮文案/组件拆分后旧测试按CSS类和加载时文字查找失败：改用稳定可访问名及当前按钮，原防重/危险确认断言保持。
3. 输入同轮认证卸载真正触发Ant4.2.6空ref异步异常：定位vc-input延后受控回读，改菜单内非受控default-value+同步change，关闭重建，保留同轮与缓存重开回归，不吞全局异常。
4. 左右选源Portal被Ant缓存：旧测试误点隐藏左菜单。aria-controls唯一关联到当前弹层，仍核往返布局和实际提交，未直接调用私有方法。
5. .NET并发全库一轮出现原MediaMtxShutdown的4项控制台退出码/1秒健康超时，以及原IpcFraming的6秒取消。没有改这些生产路径、延长门限或删断言；隔离MediaMtx **8/8**，最终串行全库 **1766/1766**。保留`input020-dotnet-tests-final.log`失败及`input020-console-isolated.log`，不冒称并发那轮通过。Host519实际为较慢完成，不是已证热循环。
6. Native RTSP超时参数、Windows stdin取消、Electron探针CLI、包管理器/打包下载适配问题均保留分层证据，详见Native记录；不通过关闭TLS、sandbox、执行策略或重装依赖掩盖。

## 本机包与边界

本机development-only目录包由受限`build-electron.mjs`生成，固定已安装44.2与锁定pnpm、只dir/publish never；原CLI曾在CacheMode.ReadWrite依赖适配失败，改走公开build API的固定electronDist，不改node_modules。打包前检查自包含运行时和FFmpeg摘要；发行检查对development-only正确拒绝。

目录包位于`.validation/client-preview-020/package-local-20261021/win-unpacked`。自动逐SHA核对当前dist-app/electron-dist共104代码文件与195个Native资产，Main预览接线、CSP、包内process.resourcesPath均通过；末轮包内helper实测EOF21ms／心跳3789ms／Job31ms／Node父22ms零残留。使用独立验证壳，不自动打开可能读取用户既有资料的正式控制UI。本机包不是已安装体验或公开发行证明。

公开发行仍缺匹配FFmpeg二进制的完整对应源码与分发审计，本轮不发布安装器。新客户端依赖020后端描述符接口，不能把旧D4上的018当作已具备新API。

**未执行**：D4部署／现场电视出画／所有源SPS及4K实机解码／摄像机机械运动／实体声音／现场长期弱网与全机QoS。用户授权本轮仅本机实施，不以包存在或API成功冒称这些已验。D4版本、原服务与数据完全未动。

临时真实媒体／浏览器夹具已按本次PID与执行会话回收，端口18896/18897/18960–18963监听0；旧18894/18895不属本轮回收对象。未按进程名杀用户Node、FFmpeg或Office。
