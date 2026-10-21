# 020 共享预览验证（2026-10-21，本机）

## 实现与范围

- Web仍是ControlHost／MediaMTX／FFmpeg的HTTP-FLV，客户端不安装工具；Electron由同一池选择受限Native适配，不静默伪装成直连。
- 应用scope池每target保留一份非消费验证租约，每实际URI另保留一份消费租约与一个decoder，多个canvas共享。相同target的65个消费者不是65份验证租约。
- `sharing_key`为进程随机私钥HMAC（完整Owner、Origin、实际URI）；URI、密码、协议、通道不同不误合并，几何／标签／quality提示不入连接key。
- 可选心跳`aliases`严格同Owner／Origin／实际URI、未消费且仍有效，最多64个。所有别名先独立续租；发生源修订只迁该target，消费租约以剩余有效别名继续围栏，不允许借其它URI无限续旧流。
- JSON心跳在分配对象前限制4096字节，未知长度／分块也有界。没有body保留原合同。
- 窗口3/4解析目录65/66；内置工作站／摄像机输入优先使用保存的系统源URI（含凭据与修订），仅已知chn0结尾按低清切chn1。
- 关闭mpegts.js的IO stash，并将受控输出GOP从1秒改到约0.5秒；不改完整GOP缓存、两组慢队列、总带宽、跨epoch单调时间戳与进程Job边界。没有使用未经验证的极低probesize／analyzeduration参数。

## 自动化验证

```powershell
dotnet test runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScpCv.ControlHost.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~StreamPreview&Category!=Physical'
node --test frontend/scripts/server-preview.test.mjs frontend/scripts/preview-pool.test.mjs frontend/scripts/preview-transport.test.mjs
node frontend/node_modules/vue-tsc/bin/vue-tsc.js --noEmit -p frontend/tsconfig.json
git diff --check
```

结果：预览非Physical **40/40**，前端组件／池／transport **28/28**，类型及diff检查通过。前端采用假传输／假decoder验证寿命，不据此证明真实媒体出画。

覆盖65同target单decoder、别名原子合并、几何与quality提示零重开、移除部分引用、1秒有界空闲宽限、隐藏立即释放、同ID重登／主机变化、迟到结果、独立源修订、销毁异常仍释放、旧空闲timer不能杀死新引用；普通Web不走IPC、Electron缺能力不假称直连。正式main.ts先mount后安装平台Adapter，首屏使用已就绪的同一受限preload桥，避免永久缓存成原生不可用，该顺序有独立回归。服务器覆盖HMAC身份、目录地址／保存凭据、源修订、无body及共享心跳、跨流／跨人／自引用／重复／超长／未知JSON拒绝。

一次早期`FullyQualifiedName~StreamPreview`未排除Physical，显式原生两项因未设置环境变量而按设计失败；随后明确区分默认40项与下面单独的真实媒体2项，没有隐藏或跳过失败。

## 显式真实回环媒体

```powershell
$env:SCP_PREVIEW_NATIVE_FFMPEG='E:/Data/Dev/Scoop/apps/ffmpeg/9.0.2/bin/ffmpeg.exe'
$env:SCP_PREVIEW_NATIVE_MTX='E:/Projects/SSPU/SCP-cv/tools/third_party/mediamtx/mediamtx.exe'
$env:SCP_PREVIEW_ARTIFACTS='E:/Projects/SSPU/SCP-cv/.validation/preview020'
dotnet test runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScpCv.ControlHost.Tests.csproj -c Release --no-build --no-restore --filter 'Category=Physical&FullyQualifiedName~StreamPreview'
```

结果：**2/2**，31秒。实际合成RTMP、65消费、仅一份上游／一份转换器；HTTP与WS捕获均由ffprobe解码到1280和320宽，时间戳单调，调档期间原连接未关闭，释放后共享数／上游归零。产物`.validation/preview020/63a563bd0adb451dbbf68f7d257f53fc/`，`result.json`与两份FLV保留。

这部分走真实FFmpeg／MediaMTX和TestServer应用管线，不是D4网卡、现场摄像机或TLS链路。

## 实际Chromium首帧对照

使用`webapp-testing`与`playwright-best-practices`：仅只读无头浏览器，不使用computer use、不模拟鼠标键盘。测试进程仅监听自有回环端口，不请求现场或D4。正式Vue预览／共享池／mpegts.js与生产Job／FLV解析器处理同一1280×720@20fps合成源；租约API在夹具内存替代，真实网络仅GET。

基准工具在忽略目录，复跑命令：

```powershell
dotnet build .validation/preview020/benchmark/Benchmark.csproj -c Release --no-restore
dotnet .validation/preview020/benchmark/bin/Release/net10.0/Benchmark.dll
node .validation/preview020/browser-server.mjs
uv run --no-project --offline --no-managed-python python .validation/preview020/browser-benchmark.py
```

前两条服务分别为18960（MediaMTX自有18961–18963）与18896，启动前检查端口空闲；每一轮新建按需路径及转换器、结束等待active=0。按baseline／current交替各5轮。时间从Vue订阅前到实际`requestVideoFrameCallback`，不是HTTP200、MSE ready或首字节。

| 配置 | 5轮冷首帧，毫秒 | p50 | p95（最近秩） |
| --- | --- | --- | --- |
| 018配置：IO stash开启，1秒GOP | 2920、4100、4088、4130、4150 | 4100 | 4150 |
| 020配置：禁stash，约0.5秒GOP | 3354、2747、2767、3573、2915 | 2915 | 3573 |

p50下降约29%，p95下降约14%。首轮受JIT／模块与输入关键帧相位影响，不能把该数据外推为所有实体源延迟上限。

随后同一正式Vue页面显示65画布，夹具自身自动重排、改变几何及quality提示、追加第66画布；最终**66画布均有真实非空像素**，仅1条实际HTTP消费流、1decoder、2份租约。每个figure计算边框1px，浏览器异常／错误0。此自运行内存夹具没有输入模拟或业务写入。

证据：`.validation/preview020/browser-result.json`（全部请求、每轮实际帧、统计、清理）、`shared-66-canvases.png`。已只读查看截图核实全部画布。截图中的非响应式JSON文本停留在自动重排时第4帧，最终JSON由浏览器直接读回为第7帧，未用截图文字冒充最终计数。

基准没有经过完整认证REST／TLS，也没有在D4执行，不证明现场4K／全部源编码／真正低吞吐网络或全机QoS。关闭stash在网络抖动时可能更易停顿，仍保留最后帧、黄色重连和有界重试。

### 五轮热新增消费者首帧

同一合成回环夹具再执行：

```powershell
uv run --no-project --offline --no-managed-python python .validation/preview020/browser-warm.py
```

夹具自身在实际decoder第2／4／6／8／10帧后依次追加一个正式Vue消费者；只观察、不模拟输入。记录从添加订阅意图到**生产drawImage完成后读回非空像素**，不是HTTP首字节、租约响应或新decoder创建。五轮为6.7／5.9／5.3／5.7／6.0 ms，最近秩p50=5.9 ms、p95=6.7 ms；来源实际帧的年龄5.3–6.8 ms。始终1条HTTP消费流、1decoder、2份租约，最终6画布；0浏览器错误。

证据`.validation/preview020/browser-warm-result.json`、`warm-five-canvases.png`；此轮首次冷连接为3178 ms，不混入热分位数。该热指标测canvas像素首绘，不包含物理显示器扫描／现场网络；复用的是已实际解码的最近帧，不承诺每次新增都触发源设备生成新帧。截图诊断JSON较长造成夹具fullPage宽于视口，不是正式页面的布局结果；正式界面另按下面18组验证。

## 清理

本轮18896与18960基准服务已通过各自执行会话停止。最终验证先前记录的自有MediaMTX PID72072及合成publisher PID70572不存在；18896、18960–18963监听均不存在。18894旧只读夹具、18895用户确认的原型由主agent管理，本轮未操作。

热新增补验同样用自有回环端口重新启动，结束active=0、total=1，记录MediaMTX PID57424与publisher PID23048；结束后再次按原执行会话停止，并核上述两个精确PID不存在，不依赖广义进程名清理。

## 正式页面布局

正式Dashboard／摄像机／预案的18897只读页面验收与上述真实媒体测量严格分开，不把静态空流夹具当实际预览验证。使用真实`main.ts`、路由、组件、Ant和全部正式样式；复用旧内存GET资料，补020 inputs／outputs合同，所有真实网络写入拒绝。预览POST仅在页面内存返回503依赖不可用，以红边检查失败样式；无流422在生产仍为中性，不误标错误。

```powershell
node frontend/node_modules/vite/bin/vite.js --config .validation/input020/vite.config.mjs --mode web
uv run --no-project --offline --no-managed-python python .validation/input020/browser-layout.py
```

两主题 × 1440×900／1280×720／390×844 × 三正式页面共**18组**；最终浏览器异常/console错误0、横向溢出0、外部网络0、真实写请求0。夹具审计累计GET768、业务写0。桌面侧栏均无需滚动；1440 Dashboard main为830/830。1280首次为726/650，收紧卡片后为651/650，仅1px舍入差，没有裁掉overflow；手机保留正常纵向滚动和预案表格内部横向滚动，document/main没有被750px表格撑宽。

所有预览figure计算边框1px，右侧电源／电视选源完整且箭头图标正常；已查看浅色1280仪表盘、深色1440仪表盘、浅色390摄像机和深色390预案截图。预览wrapper的旧双边框、Picker认证切换弹层残留及TV新主机只读恢复已由主agent接续修复。

最终`.validation/input020/result.json`及18截图；最初76px纵向差的指标保留`result-before-polish.json`。两次夹具定位失败分别是错误旧类名与Ant隐藏measure-row，不是产品错误；改用真实`.scenario-files .ant-table-row`后完整18组通过，未改产品来迁就定位器。

主agent再收紧短屏间距后，单独运行`uv run --no-project --offline --no-managed-python python .validation/input020/dashboard-height.py`补验1280×720明暗：**main 650/650、菜单609/609、document 1280/1280严格相等**，0异常。原始结果`dashboard-height-final.json`与`final-light-1280-dashboard.png`／`final-dark-1280-dashboard.png`；不把此前1px舍入结果冒称最终严格无滚动。
