# 网页屏幕共享

## 当前实现状态

2026-10-15入口规则更新：D4的HTTP80默认跳转HTTPS443，保留原路径/查询；旧5173/5174停止。本机未配置TLS的开发环境仍提供HTTP。原用户证书保持，共享必须在HTTPS正常登录后主动选择内容，浏览器许可和历史现场验收边界保持，见[默认HTTPS记录](../specs/011-http-https-access/verification/https-default-20261015.md)。

已接入网页显示控制页并部署到D4。正常可信HTTPS、真实登录/队列/两正式Worker完成两路连续312秒、十轮32次共享、普通换源及发布浏览器退出；原生DISPLAY2/3运动画面独立核对通过。有声双路360秒及12组网页音量/静音控制通过。详细版本、故障修复、原始证据和清理见[D4记录](../specs/010-browser-screen-share/verification/t016-d4.md)。

原Player异常退出后的旧共享实际状态残留已修复，D4明确重启后两新授权/持续后帧及停止通过；当前两窗在线空闲。本轮临时测试CA已从本机精确撤销，正式访问仍须配置设备正常信任的有效HTTPS证书，不能把测试证书视为永久组织证书。

目标是从桌面Chrome/Edge的原生屏幕/应用/标签页选择器直接共享到窗口1或2，不用额外推流程序。不请求麦克风，不保存屏幕媒体，不新增媒体库源，不改变墙面布局或电源。

T013原生桥、本人认证API/领取复核、实际回读及网页声音区已接线。默认接收静音，有实际音轨和后帧后可明确开启；本机有声链路和渲染信号已验，系统输出保持原静音，不声称扬声器可听。详见[原生](../specs/010-browser-screen-share/verification/t013-native-audio.md)、[HTTP](../specs/010-browser-screen-share/verification/t013-audio-http.md)及[网页/有声验证](../specs/010-browser-screen-share/verification/t013-audio-ui.md)。

## 网页操作区

已配置TLS的网页入口自动将HTTP访问转到HTTPS，登录与控制在HTTPS进行。连接页仍兼容裸地址解析，建议直接填写HTTPS。旧HTTP页面应刷新；无TLS开发页保留共享前的确认和缺入口提示，不能自动采集或猜测端口。显式连接另一主机时，先打开该主机的HTTPS控制台。

在可信HTTPS控制台登录后，打开“显示控制”，选择窗口1或窗口2，再主动点击“选择共享内容”，在浏览器原生选择器中确认要分享的屏幕、应用或标签页。另一目标可由第二个独立网页同时共享。默认只分享画面；需要声音时先勾选内容音频，并在取得实际音轨/后帧后明确开启声音。结束时点“停止共享”，或使用浏览器自己的停止分享。

显示控制页的“屏幕共享”操作区在HTTPS主动点击栈直接打开原生选择器，取得视频轨道后才预约和协商；取消许可不提交OPEN。HTTP仅确认/导航，包括localhost也不自动采集。内容音频默认关闭，是否有音轨以浏览器实际返回为准，不请求麦克风；接收默认静音，须通过共享专用控件明确开启声音。

“实际音量/静音”与设置草稿分开：编辑数字后显式应用，切换静音不顺带应用尚未提交的数字。无本地或接收音轨、未收到后帧时禁用；实际状态未知时只能先静音。202只表示提交，精确命令成功及实际属性匹配才确认。八秒内未确认明确提示，不自动重发；停止、关闭网页或换账号/主机立即取消，音频失败不影响有效视频。

面板分别显示选择中、连接中、实际接收、失败、停止待确认。只有本人状态为receiving、接收帧为正且已应用answer才显示“已收到画面”；心跳、offer受理或RTC连接不单独授予出画状态。目标标签代表窗口1/2，不代表墙面左/右布局。

停止优先结束本地所有采集轨道、peer和轮询，再向原身份发一次停止。返回stopping或请求失败时保持“等待播放器确认”，不自动重试或开始新共享；“查询停止状态”只发GET，迟到查询失败不能覆盖已经确认的关闭结果。选择器、预约或应答迟到不能复活采集。

共享由应用按窗口持有。切换控制台页面、窗口标签和浏览器标签保留同一采集与连接；其它控制台页面提供紧凑停止入口。心跳由独立Worker时钟驱动，两端遇到暂时disconnected等待12秒，永久失败立即停止。

关闭网页、账号改变、切换播放主机或目标离线时停止本地资源。主机代次改变同步终止原共享寿命，不等待网络注销；旧停止不会发送到新主机。新身份下需要再次主动选择共享内容。服务端独立一秒周期处理过期、普通换源和运行组失效；只有原播放器实际释放或原进程确证退出才确认已关闭。

完整页面另以正式构建/真实登录/可信LAN HTTPS核权限策略与浏览器原生拒绝、采集后真实API失败释放、浅深1440/390px及键盘；原生拒绝使用官方QA参数，不冒称人工系统选择器。公开HTTP/SSE状态和停止替身不当作真实接收或Player释放证据，见[完整页面验证](../specs/010-browser-screen-share/verification/t015-browser-ui.md)。手机播放控制条停止按钮至少44px，实际滚动后可达；手机本身不支持发起采集。

## 显示控制与临时输出

窗口1/2是两个逻辑播放器，不是墙面左右区域。单屏、双屏或自定义墙面布局下都可以进入两个窗口；导航或选择目标不会下发墙面映射。窄屏页签可用方向键、Home/End、Enter/空格切换，选中状态与焦点明确。

共享没有媒体库ID。会话的`share_id`表示当前请求，`actual_share_id`表示匹配原Worker的最后实际共享资源，二者和代次相符、正视频帧、在线且playing才显示“已收到画面”。`actual_frames=0`不能当出画；离线时明确是最后报告，不显示采集缩略图、文件地址或伪进度。`actual_audio`只表示接收资源有无内容音轨，不代表声音已播出。

播放控制条的“停止共享”按原`share_id`停止后刷新，不发送无条件窗口关闭；意图已清而实际共享尚在时显示“共享停止待确认”并禁用重复关闭。重开须重新选择内容，不从历史自动恢复。暂停、定位、循环及普通音量/静音仍禁用；声音仅由本人当前共享的专用控件控制。

## 会话与信令

取得原生许可后才预约，取消许可不得向播放主机提交OPEN。每窗一个共享，两个目标独立；预约/协商20秒硬上限，实际接收后发布者心跳期限30秒，不能自动续采集或重播。

- `POST /api/screen-shares/`仅预约，要求登录和CSRF；`window_id`只接受数值1/2，`has_audio`来自实际采集内容音轨。
- `POST .../{id}/offer/`只接受一次offer，再在SQLite事务中重核原Worker实例/owner/group并排动态OPEN；202不是已经出画。
- `GET .../{id}/`只返回本人状态和answer；`POST .../{id}/heartbeat/`不增加视频帧，其null实际音频字段不是控制失败/已静音的证明；实际属性读本人GET或会话HTTP/SSE。`DELETE .../{id}/`先撤销媒体授权，再按share_id/source_generation条件停止。
- 已OPEN停止返回`stopping`，不把CLOSE入队或“不再是当前意图”当作资源已退出；实际确认后才能释放目标预约。未OPEN取消直接`closed`，不排CLOSE。
- `PATCH .../{id}/audio/`严格仅volume整数0..100及muted布尔。只允许本人当前已接收分享；事务核原Worker/组、当前OPEN及真实后帧，领取再核授权。202仅提供submitted_command_id；`GET /api/playback/{window_id}/commands/{command_id}/`核精确终态，再读本人actual_volume/actual_muted或HTTP/SSE实际属性，不用请求值冒充成功。失败/缺失/非当前或离线时null，不证明声音已停止；不改普通媒体音量或源代次。网页普通轮询与操作回读有序，旧在途结果不能覆盖较晚读取。

SDP解码后UTF-8字节不超过64KiB，HTTP请求体另有有限上限（含JSON转义开销）。所有共享响应no-store；错误只返回稳定说明/代码，不输出SDP、候选地址或私有授权。完整[OpenAPI](openapi.yaml)已登记端点。

## 私有接收边界

接收cap是内存256位随机值，绑定分享、窗口、源代次及预约时的原Worker实例/owner/group；发布者Cookie不能领取或报告接收证据。新Worker即使已通过普通管道认证，也不能重放旧共享。

已认证且匹配原身份的OPEN lease才注入私有`receiver_uri`，数据库命令只留`source_type=screen_share/source_id=0/share_id`。cap仅放URI fragment，不放HTTP查询串。WPF首次导航通过[官方NavigateWithWebResourceRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatewithwebresourcerequest)设置Authorization Bearer；接收脚本读取fragment后立即从历史地址移除，后续fetch使用同一header且不携带用户Cookie。

`/api/screen-share/receiver/{id}/`及其offer/status/answer/frames/end操作同时核loopback、cap、身份、来源及阶段；代理的loopback地址不是授权。HTML/JS嵌入发布包，脚本和样式用精确CSP哈希，无外链或用户脚本拼接。预备帧和协商不能授予`receiving`，只有激活后实际视频帧才上报。

正式Player每次共享使用独占WebView控制器和InPrivate profile，不进普通网页保温池。禁止外链导航、新窗和采集权限；原生消息同时核文档来源、分享/窗口/代次及有限schema。协商完成和预备帧只允许切入准备，实际切入后还须新的`activated=true`视频帧才能完成OPEN；首帧失败释放候选并保留仍可用的旧画面。所有OPEN阶段共用有限预算，过期租约在原生创建前拒绝。

Worker纯值观测包含`share_id/actual_frames/actual_audio`和null媒体ID，音轨存在不等于可听声音。HTTP/SSE仅在原Worker实例/owner/源代次门禁和提交后映射实际字段，内存版本围栏防旧报告复活，重启不恢复授权。默认静音且普通播放/暂停/定位/翻页/循环/音量拒绝；专用音频桥只确认实际属性，现场可听效果另验。

接收脚本停止会关闭peer、停止远端轨道、清视频srcObject、取消帧回调/HTTP/轮询及ICE等待；Player随后Dispose控制器，停止/换源/退出不保温，旧实例消息不能改变新实例。页面的closed消息不等于原生WebView已Dispose。Player在完整原生Dispose完成后提供原分享/代次的私有IPC回执，后台按已提交原Worker身份采信；CLOSE入队、换源、离线或新Worker均不能冒充旧资源死亡。无法取证保持“停止待确认”，不会擅自杀其他应用。

## 重启与预案

服务重启立即丢弃内存cap/SDP，不恢复采集或重新播放旧共享。后台只核当前动态OPEN审计，按原分享/代次条件清除孤立意图，不关闭后来的固定源。原播放器的退出观察严格绑定已验证PID/start/session/role/instance；取消观察或取证失败不算退出。

原资源确证释放后同步清除其临时实际身份/帧/声音投影，不因旧源报告被围栏而在新组中残留。原面板因组离线结束寿命后，重新打开显示控制页并主动选择内容才能再共享；不自动恢复授权。

共享是临时内容。“从当前状态生成”发现任一窗口仍有共享请求或实际资源，会明确提示先停止共享或切到媒体库固定源，不把它保存为空源。后端捕获返回400 `scenario_screen_share_not_persistable`，已有预案不被覆盖。手动新建仍可选择固定媒体、“保持”或“黑屏”；set目标必须是正媒体库ID，否则400 `scenario_source_invalid`。预案“保持”不终止现有共享，“黑屏”或固定源是明确替换，不会恢复共享授权。

## 配置和验证边界

接收地址从ControlHost的`urls`首个监听地址派生，0.0.0.0/IPv6通配绑定转换为本机地址；沿用正式启动脚本的默认`https://localhost:18443`。可通过`ScreenSharing__ReceiverBaseUrl`显式选择**已监听**的loopback HTTP(S)纯地址；拒绝远程主机、用户信息、路径、query或fragment，且不自动新增监听或绕过证书验证。非通配LAN绑定需另行提供本机监听与明确接收地址。

浏览器采集必须是安全上下文；localhost只支持本机开发，普通局域网HTTP不满足要求。配置步骤见下节，不能使用关闭安全策略/不安全来源flag作为正式方法。D4当前普通入口为`http://192.168.5.194/`，共享入口为`https://192.168.5.194/`；任何采集设备都须正常信任用户证书，HTTP确认只负责导航，不自动建立信任。

捕获保持原始尺寸，优先文字细节和30fps。服务端下发视频策略，默认发送上限为1080P 16Mbps、1440P 24Mbps、更高48Mbps；浏览器按网络与编码能力决定实际码率。捕获尺寸进入新档位时更新发送参数，质量详情显示浏览器实际接受的上限和实时统计。

通过`ScreenSharing:Video:FullHdBitrate`、`QhdBitrate`、`UltraHdBitrate`配置各档位（bit/s），`MaxFrameRate`配置帧率（1–30）。三个码率必须递增或相同，最低1Mbps，最高200Mbps；无效配置在启动时报告错误。策略随共享快照的`video_policy`返回，接收端沿用原始WebRTC轨道。手机控制台停止加载预览并隐藏共享发起面板。

自动化覆盖登录/CSRF、争抢、有界信令、原身份围栏、失败撤销、接收来源、停止及专用音频，D4双Worker长稳/十轮/换源已有独立证据。边界：人工普通系统选择器未代操作；跨VLAN/AP隔离或外网未验，默认没有外部STUN/TURN。D4独立音频meter未取得有效采样，未验现场扬声器可听；实际属性/RTP与本机渲染信号不混为硬件听音。见[验证记录](../specs/010-browser-screen-share/verification.md)。

## LAN可信HTTPS配置

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

先创建并限制上述目录只允许服务账号读取，生成后恢复本次进程原CAROOT设置。`d4-key.pem`和`rootCA-key.pem`都是秘密，不能分发、提交、打包或放入frontend/public；访问设备只需要公开的`rootCA.pem`。配置的PEM编码叶证书支持`.pem/.crt`、未加密私钥支持`.pem/.key`，须为frontend目录外、不超过256KiB的普通文件（推荐整个仓库外）；配置路径和实体路径均检查，不接受从frontend通过目录链接指向外部密钥。保管好私有目录，不能把它链接进public。

当前用户叶证书CN为`192.168.5.194`，SAN包括`192.168.5.194`、`127.0.0.1`、`10.88.50.194`，没有`localhost`；内部TLS上游应使用`127.0.0.1`，不要填写未覆盖的名称。已补齐AKI/SKI并部署D4，正常Node/Chromium及OpenSSL3.5严格模式均通过；原AKI缺失是历史失败，修复见[证书记录](../specs/011-http-https-access/verification/local.md#后续补齐服务器证书-akiski2026-10-12)。原根在2036-03-09到期，早于叶2036-10-09，链可用期限仍受根限制。修复时仅在本机签名使用原根私钥，不输出/复制/上传；播放服务只读叶证书/叶私钥及公开根，不自动安装根。CN/SAN不含协议、路径或端口；更换访问地址必须在SAN中覆盖。

### 2. 在访问设备正常建立CA信任

在服务机器和每台访问机器上，通过可信渠道核对公开根证书的SHA256及证书指纹，导入相应登录账号的“受信任的根证书颁发机构”。Windows当前用户导入公开根的示例：

```powershell
certutil -user -addstore Root D:/SCP-cv-private/tls/ca/rootCA.pem
```

Windows可能要求系统确认；核对身份后由操作者确认，等待命令退出并检查该精确指纹确已在CurrentUser/Root中，再用新浏览器会话访问。命令仍活着、超时或出现“签名与公钥匹配”不代表导入成功，不能因此重复导入或禁用保护。临时QA结束时只移除本轮精确CA指纹，不能批量删除其他根证书。Player所在D4交互用户也须信任对应CA，否则私有接收页同样会拒绝TLS。

若用户已正常安装，但自动化命令仍查询不到，先在实际登录用户的普通会话核对精确根和浏览器导航。2026-10-11复验发现同一账号/SID下两种执行上下文的存储结果不同；不要据此要求反复安装、改机器策略或忽略证书错误。测试可在已正常信任的登录会话运行，仍保持严格HTTPS校验；这不表示其它访问账号或设备已获信任。

### 3. 配置ControlHost和同源前端

ControlHost使用[官方Kestrel证书配置](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)：同时监听`http://127.0.0.1:18444;https://0.0.0.0:18443`，`Kestrel__Certificates__Default__Path`及`Kestrel__Certificates__Default__KeyPath`指向叶证书/私钥。AllowedHosts及AllowedOrigins明确覆盖实际两网页Origin，禁止扩大为任意来源。私有接收地址继续使用**已监听**loopback18444，不公开接收cap。修改启动环境前先停止原项目运行组并确认退出，不能让两组同时占用显示器/端口；数据库和媒体沿用原DataRoot。

HTTP使用`sessionid_http/csrftoken_http`，HTTPS保留`sessionid/csrftoken`和Secure；票据绑定签发协议，改Cookie名也不能跨协议认证，注销仅清当前入口。旧无协议标记票据会被拒绝，升级后需重新登录。网页同源代理推荐`Authentication__CrossSiteCookies=false`；即使沿用默认true，HTTP也仅使用非Secure/SameSite=Lax的独立Cookie，HTTPS仍保留Secure及跨站配置，封装端安全策略不变。

`frontend/.env`中的配置示例（该文件忽略，不提交）：

```dotenv
VITE_FRONTEND_PORT=80
SCP_CV_HTTPS_PORT=443
VITE_BACKEND_HTTP_TARGET=http://127.0.0.1:18444
VITE_BACKEND_HTTPS_TARGET=https://127.0.0.1:18443
SCP_CV_HTTPS_CERT_FILE=D:/SCP-cv-private/tls/d4.pem
SCP_CV_HTTPS_KEY_FILE=D:/SCP-cv-private/tls/d4-key.pem
SCP_CV_HTTPS_HOSTS=192.168.5.194,127.0.0.1
```

启动Node进程前另外配置`NODE_EXTRA_CA_CERTS=D:/SCP-cv-private/tls/ca/rootCA.pem`，让HTTPS代理正常验证本地CA的后端；它不是前端VITE变量。前端三个SCP项必须同时提供，完全没配保持既有HTTP，部分/错误配置拒绝启动，不降级。SCP项只在服务器读取，不进入客户端；build:web/封装构建不读取密钥。

使用`npm --prefix frontend run dev:web`，或Web构建后`npm --prefix frontend exec -- vite preview --mode web --port 80 --strictPort`；本地开发可将两端口改为5173/5174。两固定端口不可相同或占用，失败不随机换端口、不得留下半运行服务。网页REST/SSE默认走同源代理，VITE_BACKEND_TARGET只覆盖对应协议上游，不让浏览器绕过入口；已选择的播放主机仍优先，封装端的明确后端配置及回退不变。每请求按实际TLS socket分流，HTTPS正常验证上游，不设置secure=false或NODE_TLS_REJECT_UNAUTHORIZED=0。dev/preview用于本项目开发/联调，不宣称生产静态服务器；替换证书后重启自有服务并复核。

### 4. 分层检查

新部署先检查HTTP返回307和准确的HTTPS Location，再检查HTTPS页面、登录/CSRF与只读状态；HTTPS自身不能重定向循环。发现接口在HTTPS返回版本和端口，HTTP同样跳转；无TLS开发时返回null。证书须正常受信，共享仍由用户主动选择内容。浏览器安全上下文、原生许可、指定Player实际后帧及停止释放分别验收，不以HTTPS200代替出画。历史双入口会话隔离记录见[旧部署记录](../specs/011-http-https-access/verification/d4-deployment.md)，当前跳转见[默认HTTPS记录](../specs/011-http-https-access/verification/https-default-20261015.md)。
