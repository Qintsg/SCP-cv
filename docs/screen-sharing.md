# 网页屏幕共享

## 当前实现状态

010正在开发，尚未部署到D4，尚不能作为完整功能使用。已完成内存会话、HTTP信令、Worker租约授权、正式Player接收资源、网页采集/音频控件、HTTP/SSE身份及后台释放确认。本机两个正式Host十轮共30次真实采集/换源/释放，以及两目标分别有声传输/音量/静音验证通过；不等于正式双Worker或D4验收。显式HTTPS、真实TLS/API和普通浏览器可信LAN采集已验，完整页面、连续两会话及现场可听效果仍待，阶段门禁不当完整交付。

目标是从桌面Chrome/Edge的原生屏幕/应用/标签页选择器直接共享到窗口1或2，不用额外推流程序。不请求麦克风，不保存屏幕媒体，不新增媒体库源，不改变墙面布局或电源。

T013原生桥、本人认证API/领取复核、实际回读及网页声音区已接线。默认接收静音，有实际音轨和后帧后可明确开启；本机有声链路和渲染信号已验，系统输出保持原静音，不声称扬声器可听。详见[原生](../specs/010-browser-screen-share/verification/t013-native-audio.md)、[HTTP](../specs/010-browser-screen-share/verification/t013-audio-http.md)及[网页/有声验证](../specs/010-browser-screen-share/verification/t013-audio-ui.md)。

## 网页操作区（开发阶段）

显示控制页新增当前逻辑窗口的“屏幕共享”操作区。“选择共享内容”在点击时直接打开原生选择器，取得视频轨道后才预约和协商；取消许可不提交OPEN。内容音频默认关闭，是否有音轨以浏览器实际返回为准，不请求麦克风；接收默认静音，须通过共享专用控件明确开启声音。

“实际音量/静音”与设置草稿分开：编辑数字后显式应用，切换静音不顺带应用尚未提交的数字。无本地或接收音轨、未收到后帧时禁用；实际状态未知时只能先静音。202只表示提交，精确命令成功及实际属性匹配才确认。八秒内未确认明确提示，不自动重发；停止、离开或换账号/主机立即取消，音频失败不影响有效视频。

面板分别显示选择中、连接中、实际接收、失败、停止待确认。只有本人状态为receiving、接收帧为正且已应用answer才显示“已收到画面”；心跳、offer受理或RTC连接不单独授予出画状态。目标标签代表窗口1/2，不代表墙面左/右布局。

停止优先结束本地所有采集轨道、peer和轮询，再向原身份发一次停止。返回stopping或请求失败时保持“等待播放器确认”，不自动重试或开始新共享；“查询停止状态”只发GET，迟到查询失败不能覆盖已经确认的关闭结果。选择器、预约或应答迟到不能复活采集。

离开页面、账号改变或目标离线会停止本地资源。切播放主机在代次改变时同步终止原页面的共享寿命，不等待网络注销；旧请求随后被拒绝也必须释放本地采集，不把旧停止发送到新主机。主机/账号切换或页面结束后，原面板不自动重采且入口保持禁用；重新打开显示控制页后才能在新身份下再次主动选择内容。后台独立一秒周期处理过期、普通换源和运行组失效，不依赖网页继续查询；只有原播放器实际释放或原进程确证退出才确认已关闭。

本机面板验证使用正式UI和真实getDisplayMedia/RTC，但隔离HTTP信令且没有接收播放器，不把这些测试中的closed当作正式Player释放证据。真实许可选择器取消、LAN HTTPS与D4跨机另验。

## 显示控制与临时输出

窗口1/2是两个逻辑播放器，不是墙面左右区域。单屏、双屏或自定义墙面布局下都可以进入两个窗口；导航或选择目标不会下发墙面映射。窄屏页签可用方向键、Home/End、Enter/空格切换，选中状态与焦点明确。

共享没有媒体库ID。会话的`share_id`表示当前请求，`actual_share_id`表示匹配原Worker的最后实际共享资源，二者和代次相符、正视频帧、在线且playing才显示“已收到画面”。`actual_frames=0`不能当出画；离线时明确是最后报告，不显示采集缩略图、文件地址或伪进度。`actual_audio`只表示接收资源有无内容音轨，不代表声音已播出。

播放控制条的“停止共享”按原`share_id`停止后刷新，不发送无条件窗口关闭；意图已清而实际共享尚在时显示“共享停止待确认”并禁用重复关闭。重开须重新选择内容，不从历史自动恢复。暂停、定位、循环及普通音量/静音仍禁用；声音仅由本人当前共享的专用控件控制。

## 会话与信令

取得原生许可后才预约，取消许可不得向播放主机提交OPEN。每窗一个共享，两个目标独立；预约/协商20秒硬上限，实际接收后发布者心跳期限30秒，不能自动续采集或重播。

- `POST /api/screen-shares/`仅预约，要求登录和CSRF；`window_id`只接受数值1/2，`has_audio`来自实际采集内容音轨。
- `POST .../{id}/offer/`只接受一次offer，再在SQLite事务中重核原Worker实例/owner/group并排动态OPEN；202不是已经出画。
- `GET .../{id}/`只返回本人状态和answer；`POST .../{id}/heartbeat/`不增加视频帧；`DELETE .../{id}/`先撤销媒体授权，再按share_id/source_generation条件停止。
- 已OPEN停止返回`stopping`，不把CLOSE入队或“不再是当前意图”当作资源已退出；实际确认后才能释放目标预约。未OPEN取消直接`closed`，不排CLOSE。
- `PATCH .../{id}/audio/`严格仅volume整数0..100及muted布尔。只允许本人当前已接收分享；事务核原Worker/组、当前OPEN及真实后帧，领取再核授权。202仅提供submitted_command_id；`GET /api/playback/{window_id}/commands/{command_id}/`核精确终态，再读本人actual_volume/actual_muted或HTTP/SSE实际属性，不用请求值冒充成功。失败/缺失/非当前或离线时null，不证明声音已停止；不改普通媒体音量或源代次。网页普通轮询与操作回读有序，旧在途结果不能覆盖较晚读取。

SDP解码后UTF-8字节不超过64KiB，HTTP请求体另有有限上限（含JSON转义开销）。所有共享响应no-store；错误只返回稳定说明/代码，不输出SDP、候选地址或私有授权。完整[OpenAPI](openapi.yaml)已登记端点；开发期间不要用这些入口替代尚未完成的控制台流程。

## 私有接收边界

接收cap是内存256位随机值，绑定分享、窗口、源代次及预约时的原Worker实例/owner/group；发布者Cookie不能领取或报告接收证据。新Worker即使已通过普通管道认证，也不能重放旧共享。

已认证且匹配原身份的OPEN lease才注入私有`receiver_uri`，数据库命令只留`source_type=screen_share/source_id=0/share_id`。cap仅放URI fragment，不放HTTP查询串。WPF首次导航通过[官方NavigateWithWebResourceRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatewithwebresourcerequest)设置Authorization Bearer；接收脚本读取fragment后立即从历史地址移除，后续fetch使用同一header且不携带用户Cookie。

`/api/screen-share/receiver/{id}/`及其offer/status/answer/frames/end操作同时核loopback、cap、身份、来源及阶段；代理的loopback地址不是授权。HTML/JS嵌入发布包，脚本和样式用精确CSP哈希，无外链或用户脚本拼接。预备帧和协商不能授予`receiving`，只有激活后实际视频帧才上报。

正式Player每次共享使用独占WebView控制器和InPrivate profile，不进普通网页保温池。禁止外链导航、新窗和采集权限；原生消息同时核文档来源、分享/窗口/代次及有限schema。协商完成和预备帧只允许切入准备，实际切入后还须新的`activated=true`视频帧才能完成OPEN；首帧失败释放候选并保留仍可用的旧画面。所有OPEN阶段共用有限预算，过期租约在原生创建前拒绝。

Worker纯值观测包含`share_id/actual_frames/actual_audio`和null媒体ID，音轨存在不等于可听声音。HTTP/SSE仅在原Worker实例/owner/源代次门禁和提交后映射实际字段，内存版本围栏防旧报告复活，重启不恢复授权。默认静音且普通播放/暂停/定位/翻页/循环/音量拒绝；专用音频桥只确认实际属性，现场可听效果另验。

接收脚本停止会关闭peer、停止远端轨道、清视频srcObject、取消帧回调/HTTP/轮询及ICE等待；Player随后Dispose控制器，停止/换源/退出不保温，旧实例消息不能改变新实例。页面的closed消息不等于原生WebView已Dispose。Player在完整原生Dispose完成后提供原分享/代次的私有IPC回执，后台按已提交原Worker身份采信；CLOSE入队、换源、离线或新Worker均不能冒充旧资源死亡。无法取证保持“停止待确认”，不会擅自杀其他应用。

## 重启与预案

服务重启立即丢弃内存cap/SDP，不恢复采集或重新播放旧共享。后台只核当前动态OPEN审计，按原分享/代次条件清除孤立意图，不关闭后来的固定源。原播放器的退出观察严格绑定已验证PID/start/session/role/instance；取消观察或取证失败不算退出。

共享是临时内容。“从当前状态生成”发现任一窗口仍有共享请求或实际资源，会明确提示先停止共享或切到媒体库固定源，不把它保存为空源。后端捕获返回400 `scenario_screen_share_not_persistable`，已有预案不被覆盖。手动新建仍可选择固定媒体、“保持”或“黑屏”；set目标必须是正媒体库ID，否则400 `scenario_source_invalid`。预案“保持”不终止现有共享，“黑屏”或固定源是明确替换，不会恢复共享授权。

## 配置和验证边界

接收地址从ControlHost的`urls`首个监听地址派生，0.0.0.0/IPv6通配绑定转换为本机地址；沿用正式启动脚本的默认`https://localhost:18443`。可通过`ScreenSharing__ReceiverBaseUrl`显式选择**已监听**的loopback HTTP(S)纯地址；拒绝远程主机、用户信息、路径、query或fragment，且不自动新增监听或绕过证书验证。非通配LAN绑定需另行提供本机监听与明确接收地址。

浏览器采集必须是安全上下文；localhost只支持本机开发，普通局域网HTTP不满足要求。配置步骤见下节，不能使用关闭安全策略/不安全来源flag作为正式方法。T014普通浏览器可信LAN采集已通过；它不等于D4出画或整个010验收。

当前自动化覆盖登录/CSRF、争抢、有界信令、原身份围栏、失败撤销、接收来源、停止及专用音频。真实本机Host/有声链路不当D4两Worker；正式双窗5分钟、十轮/异常切换、正常选择器取消、可信LAN及现场可听效果仍须执行，见[010任务](../specs/010-browser-screen-share/tasks.md)与[验证记录](../specs/010-browser-screen-share/verification.md)。

## LAN可信HTTPS配置（实施前置，尚未部署D4）

首版支持同一局域网内的桌面Chrome/Edge。手机、Electron/Capacitor内置控制端、无getDisplayMedia或被浏览器/组织策略禁止的环境明确不支持；不要请求麦克风补音轨，不用忽略证书错误、点击继续访问或不安全来源flag替代可信连接。

### 1. 获取并安全保管证书

优先使用现场已信任的组织CA证书。服务器叶证书的SAN必须包含实际访问的DNS/IP，也包含Node代理和Player访问ControlHost时使用的localhost/127.0.0.1；证书须在有效期内、允许服务器认证，不能用CA根代替叶证书。

无现有CA时，可按[mkcert官方说明](https://github.com/FiloSottile/mkcert)在专用私有目录签发仅现场使用的证书。以下是操作示例，不表示已在D4执行；已有证书/CA目录不要覆盖。先创建并限制私有目录只允许服务账号读取，用`Get-Command mkcert`确认工具可用：

```powershell
$scpPreviousCaRoot = $env:CAROOT
try {
  $env:CAROOT = 'D:/SCP-cv-private/tls/ca'
  mkcert -cert-file D:/SCP-cv-private/tls/d4.pem -key-file D:/SCP-cv-private/tls/d4-key.pem 192.168.5.194 localhost 127.0.0.1
  if ($LASTEXITCODE -ne 0) { throw '签发证书失败，不能继续部署' }
} finally { $env:CAROOT = $scpPreviousCaRoot }
```

先创建并限制上述目录只允许服务账号读取，生成后恢复本次进程原CAROOT设置。`d4-key.pem`和`rootCA-key.pem`都是秘密，不能分发、提交、打包或放入frontend/public；访问设备只需要公开的`rootCA.pem`。配置的证书/私钥必须是frontend目录外、不超过256KiB的普通`.pem`文件（推荐整个仓库外）；配置路径和实体路径均检查，不接受从frontend通过目录链接指向外部密钥。保管好私有目录，不能把它链接进public。

### 2. 在访问设备正常建立CA信任

在服务机器和每台访问机器上，通过可信渠道核对公开根证书的SHA256及证书指纹，导入相应登录账号的“受信任的根证书颁发机构”。Windows当前用户导入公开根的示例：

```powershell
certutil -user -addstore Root D:/SCP-cv-private/tls/ca/rootCA.pem
```

Windows可能要求系统确认；核对身份后由操作者确认，等待命令退出并检查该精确指纹确已在CurrentUser/Root中，再用新浏览器会话访问。命令仍活着、超时或出现“签名与公钥匹配”不代表导入成功，不能因此重复导入或禁用保护。临时QA结束时只移除本轮精确CA指纹，不能批量删除其他根证书。Player所在D4交互用户也须信任对应CA，否则私有接收页同样会拒绝TLS。

### 3. 配置ControlHost和同源前端

ControlHost使用[官方Kestrel证书配置](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)：本机监听的HTTPS端点、`Kestrel__Certificates__Default__Path`及`Kestrel__Certificates__Default__KeyPath`指向上面的叶证书/私钥。保留原认证、CSRF及明确AllowedHosts/AllowedOrigins，不关闭Secure Cookie或扩大来源名单；接收loopback地址必须是已监听且证书有效的地址。修改启动环境前先停止原项目运行组并确认退出，不能让两组同时占用显示器/端口；数据库和媒体沿用原DataRoot。

`frontend/.env`中的配置示例（该文件忽略，不提交）：

```dotenv
VITE_FRONTEND_PORT=5173
VITE_BACKEND_TARGET=https://localhost:18443
SCP_CV_HTTPS_CERT_FILE=D:/SCP-cv-private/tls/d4.pem
SCP_CV_HTTPS_KEY_FILE=D:/SCP-cv-private/tls/d4-key.pem
SCP_CV_HTTPS_HOSTS=192.168.5.194,localhost,127.0.0.1
```

启动Node进程前另外配置`NODE_EXTRA_CA_CERTS=D:/SCP-cv-private/tls/ca/rootCA.pem`，让HTTPS代理正常验证本地CA的后端；它不是前端VITE变量。前端三个SCP项必须同时提供，完全没配保持既有HTTP，部分/错误配置拒绝启动，不降级。SCP项只在服务器读取，不进入客户端；build:web/封装构建不读取密钥。

使用pnpm启动既有`dev:web`或Web构建后的`preview --mode web`。两入口复用[Vite HTTPS配置](https://vite.dev/config/server-options#server-https)和同源代理；显式HTTPS开启后，未信任的上游证书会被拒绝，不设置secure=false或NODE_TLS_REJECT_UNAUTHORIZED=0。Vite dev/preview用于本项目开发/联调，不把它宣称为生产静态服务器。替换证书/私钥后重启其自有服务并复核，不能仅凭原页面缓存认为新证书生效。

### 4. 分层检查

先访问`https://192.168.5.194:5173/`，确认浏览器无证书警告；验证登录、Secure/HttpOnly会话Cookie和原CSRF，再在显示控制页主动选择自有测试标签页。只有浏览器安全上下文、原生采集返回、指定Player激活后真实帧及停止实际释放分别成立，才算功能可用。禁止把HTTPS200、本机localhost采集或API受理当作D4出画证明。本轮服务器TLS、安全API、私钥访问/上游拒绝与普通浏览器可信LAN采集证据见[T014记录](../specs/010-browser-screen-share/verification/t014-https.md)，D4未据此宣称已部署。
