# T012/T013 原生直连预览验证

日期：2026-10-21。只在本机随机127.0.0.1端口，未连接D4、摄像机或现场设备；无computer use、鼠标键盘模拟或系统策略改动。

## 实现边界

- `frontend/electron/preview`实现可信顶层IPC、固定认证描述符、私有app能力租约、同源受控媒体进程、短有效期/票据/主机/来源修订围栏。只读流和静态文件解析分支独立，不提供任意URI/路径代理。
- preload在应用挂载前同步提供冻结`preview`子桥。共享页面只见UUID、HMAC共享身份、不可猜app流URL和期限；不见原始流URI/用户名/密码。
- 同URI多份验证/消费租约只启动一条进程，只有消费GET才拉流。heartbeat最多64个同归属别名，必须重新经认证主机解析当前来源，不能靠旧快照维持被修改的流。
- acquire同时计算在途和已创建租约，并在完成时复核128上限。换账号/主机、迟到结果、源失效、最后引用释放均有清理围栏。
- `ScpCv.ClientPreviewHost`作为win-x64自包含资产分发；FFmpeg使用明确包内路径，不查询生产PATH。独占Job先加入再运行，FFmpeg不继承宿主控制stdin；独立4秒Timer与stdin EOF保证异常回收。
- H264优先原码转封装、无音轨。兼容回退只允许一次，先确认旧宿主/Job退出，再转H264；兼容缩放最大1920×1080，不放大较小源。RTSP使用专有timeout；RTMP采用有界探测和live/零额外RTMP缓冲。
- 普通Web/Android仍使用服务端FLV，未增加工具安装要求。CSP只精确增加同源连接与blob Worker，未放宽Node、sandbox、TLS、eval或任意导航。

## 默认与构建验证

- Native模块10项默认测试通过；连同既有Electron安全/平台测试25项通过，0跳过。包括129并发最大128、同源引用、票据ABA、伪造nonce、迟到描述符、源别名、已取消GET不启动进程和回退门禁。
- Electron TypeScript编译、共享Vue类型检查通过。
- 新宿主加入`ScpCv.sln`；`RuntimeIdentifiers=win-x64`与锁文件一致，普通`dotnet restore --locked-mode`和Release构建通过，0警告/错误。私有自包含发布成功；不要求用户安装.NET。

## 真媒体与真Electron

- 实际MediaMTX1.17.1与FFmpeg9.0.2只在回环使用合成动态画面。
- 真实RTSP→私有宿主copy→正式共享注册表FLV→ffprobe：H264、1280×720、44帧；约1603ms首字节。首字节不是浏览器首帧。
- 真实RTMP同链：H264、1280×720、47帧；987ms首字节。
- 真实H265原码在H264分发门禁拒绝后，旧进程清理、单次兼容回退：H264、1280×720、46帧，非任意格式无损保证。
- 实际Electron44.2的正式Main/preload、同session认证Cookie/CSRF、正常受信HTTPS/CORS、私有app协议与mpegts Worker已取得12个真实视频帧，原码尺寸1280×720，0错误、无renderer Node暴露；末轮RTSP首帧1706ms、RTMP首帧1142ms。独立报告见忽略证据目录，不能用Node模拟Request代替该证据。
- TLS沿用用户已有证书及已有系统根信任；只在进程内使用证书，没有复制/输出私钥，未安装CA或忽略证书错误。认证HTTP端点是内存夹具，不是D4或ASP.NET认证全栈。

## 实际异常退出

`verify-client-preview-native.mjs`对端建立TCP后永不返回数据，验证的不是自然流结束：

| 情况 | 实测耗时 | 残留 |
| --- | ---: | --- |
| stdin EOF | 21ms | 0 |
| 停止心跳 | 4081ms | 0 |
| 强杀helper，Job关闭 | 35ms | 0 |
| 强杀Node父，EOF触发 | 70ms | 0 |
| 强杀实际Electron44.2主进程，上游仍停数据 | 47ms | 0 |

最后一项使用正式Main/app流及正常TLS假认证，按精确父PID/路径只读发现本用例helper与FFmpeg，再强杀自有Electron句柄；不是仅正常quit，也不是仅用Node父替代Electron。

## 调试中确认并修复的问题

1. Windows控制台管道的`ReadLineAsync(token)`不能可靠取消：原首版心跳超预算。改为独立Timer与后台stdin读取，不在退出时等待阻塞管道；未通过延长5秒门限掩盖。
2. FFmpeg RTSP输入不接受`rw_timeout`：同参数直接回环复现明确错误后改用专有`timeout`，RTMP保留协议读超时。
3. RTMP默认探测/缓存使首数据慢于5秒测试预算：采用live与零额外RTMP缓冲，保留非零有界探测，实际解码再验，不一律把探测设0。
4. Electron默认CLI同时接收两组未知选项及独立值时在用户入口前退出4294967295。最小无窗口应用、单选项和组合对照定位；改用探针专有环境变量。二进制与014已验归档摘要相同，无MOTW、没有对应AppLocker/CodeIntegrity阻止证据；未误判产品崩溃或修改安全策略。

## 分发与未验边界

- 本机私有资产位于`.validation/client-preview-020/assets`，FFmpeg exe SHA256为`589e50b766d251afdf181dd664d40bd94407e200b019989fd7468c7d118a28d0`，来自既有Scoop Gyan9.0.2清单并核验；发布路径不复用shim或全局程序。
- `media-assets.json`明确`development_only=true`。含GPL组件的FFmpeg不能标为纯LGPL；本轮没有匹配完整源码制品及公开分发合规审核，不能声称安装包可以公开发布。只复制LICENSE不等于满足源码义务。
- 外部进程CLI中URI可能被同机有足够权限的进程检查；不宣称对管理员隐藏。应用不将URI写进renderer、流URL或日志。
- 本轮未证明现场设备真实延迟、4K硬件解码、所有源格式、远端网络/TLS或实际安装体验；也未操作系统声音、显示分辨率或物理控制。
- 忽略证据：`.validation/client-preview-020/{native-lifecycle,electron-parent-death,native-media-copy,native-media-rtmp,native-media-compatible,electron-app-protocol-rtsp,electron-app-protocol-rtmp}.json`及合成FLV、原始启动诊断。

## 重跑入口

先按明确FFmpeg路径/摘要/许可证/来源运行`frontend/scripts/prepare-client-preview.mjs`，开发验证须显式`--development-only true`；再编译Electron主进程。

- 无GUI媒体回收：`verify-client-preview-native.mjs --assets <私有资产目录>`。
- 媒体字节/解码：`verify-client-preview-electron.mjs --assets <目录> --media-only --ffprobe <测试ffprobe>`，可加`--rtmp`或`--source-h265`。
- 真Electron：同脚本加明确`--cert <已有受信证书> --key <已有私钥>`，不使用`--media-only`；可加`--rtmp`。
- 真Electron父死亡：`verify-client-preview-electron-death.mjs --assets <目录> --cert <证书> --key <私钥>`。

## T017 本机目录包门槛

- `frontend/scripts/build-electron.mjs`使用electron-builder26.15.3公开API；固定Electron44.2已核本地dist，`publish:'never'`且仅Windows x64目录包。必须`--development-only true`，输出限制在`.validation/client-preview-020`的专有子目录；不会公开发布或覆盖仓库／用户目录。
- 从`pnpm run`的`npm_execpath`验证确为11.22.0并解析其专有bin；直接Node可显式`--pnpm-bin`。只给本进程PATH前置已核版本，不改全局或仓库包管理器，不删node_modules，也不忽略发布签名验证去下载其它引擎。
- 真实故障：全局PATH已是pnpm11.24，其新版版本切换在网络解析阶段等待；旧`manage_package_manager_versions=false`不足以阻止。`frontend/pnpm-workspace.yaml`真实存在，未删除／假设无workspace。匹配的缓存11.22+只警告依赖状态可按时完成；项目无生产Node原生扩展，因此明确不重建依赖。
- electron-builder默认下载适配会读取本机依赖不存在的`ElectronDownloadCacheMode.ReadWrite`。采用官方`electronDist`配置复用同版本已核目录，不修改node_modules或关闭供应链/TLS校验。
- 实际目录包`package-local-20261021/win-unpacked`成功。`electron-package-integrity.mjs`逐一核对**104个当前App/主进程文件与195个Native资产**完整SHA256；包含预览接线、CSP、固定`process.resourcesPath`引用。ASAR摘要`0a385916959685e9e834398874976da0b90d5d9c0696d3a97f80c7542c72eef2`，报告在该输出的`package-integrity.json`；不是以旧release目录存在当成功。
- 包内FFmpeg摘要仍与原件相同，self-contained宿主实际从包内路径运行，EOF/心跳/强杀helper/强杀Node父再次分别20/4003/33/26ms退出，0残留。全套资产保持development-only声明；源码公开分发门禁未解除。
- ASAR3.4.1在Windows校验接口需要原生`path.join`路径，正斜杠探针曾错误报告文件未找到；已用实际列表与原生路径核实，不将验证脚本错误当产品缺文件。
- 保留“默认Electron图标”构建提示；未把工具输出signing视为已具备可信签名，也没有为验证正式包而读取／替换现有控制端用户配置或连接D4。
