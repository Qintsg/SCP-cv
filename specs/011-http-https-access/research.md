# 双协议研究记录

2026-10-15用户更新：已配置TLS时，HTTP只负责307/no-store跳到实际HTTPS端口；未配置TLS保留HTTP开发。以下“不自动跳转”是2026-10-12旧需求的设计理由，现已被新需求取代；会话兼容和安全门禁保持。

2026-10-12，主代理读现有配置/认证/测试；按plan skill派只读研究代理，未改D4。

## 双入口

- Decision: HTTP主监听、独立HTTPS companion共用middleware；HTTPS upgrade事件交给原HTTP服务器的公开upgrade事件，复用Vite HMR。
- Rationale: 同端口不能承载两协议；端口必须显式提供，不猜HTTP端口+1。
- Alternatives considered: 两Vite进程重复配置；无条件重定向违反普通HTTP要求。
- Evidence: [Vite server.https](https://vite.dev/config/server-options#server-https)、[preview.proxy](https://vite.dev/config/preview-options#preview-proxy)，已安装Vite8.2类型及实现。

## 代理

- Decision: HTTP默认127.0.0.1:18444；HTTPS默认127.0.0.1:18443，严格验证；自定义upstream必须协议匹配。
- Rationale: 请求Secure属性必须对应实际入口。使用proxy.before的每请求options副本，保持changeOrigin=false。
- Alternatives considered: 单HTTPS upstream使HTTP认证失败；secure:false/剥离Secure不安全；bypass修改共享options有竞态。
- Evidence: Vite8.2公开HttpProxy.before类型及requestOptions浅复制；现有AuthServiceCollectionExtensions.cs。

## 会话

- Decision: HTTPS保留sessionid/csrftoken；HTTP使用sessionid_http/csrftoken_http，委托ChunkingCookieManager。
- Rationale: Cookie不按端口或协议分区；否则Secure覆盖明文会话。HTTP始终仅同站Lax/非Secure；HTTPS仍Secure，并保留CrossSiteCookies设置。网页推荐false，默认true也已正式回归，不因沿用旧默认而破坏HTTP登录；封装端继续现有HTTPS策略。
- Evidence: 默认CrossSiteCookies=true的红例明确发现HTTP防伪Cookie被标Secure；按请求复制HTTP选项后，两种配置三轮往返及独立注销均通过。票据另绑签发协议，Cookie改名重放仍拒绝；旧无标记票据需重新登录。
- Alternatives considered: 全部SecurePolicy.Never、URL携带令牌均拒绝。

## 发现与跳转

- Decision: 同源GET /.well-known/scp-cv-access只给版本/HTTPS端口，no-store；确认后5秒限时查询，失效取消；新origin正常登录，不自动恢复采集。
- Rationale: 自定义端口不能只改protocol。信息不含host/秘密，不形成开放重定向。
- Alternatives considered: 硬编码端口；携带密码/会话；跨主机明文profile升级时悄悄改控制目标，均拒绝并提示使用同源网页。

## 用户证书

- Decision: 支持目录外PEM编码.crt/.pem证书、.key/.pem私钥；所有内容、SAN、期限、配对门禁保持。
- Rationale: 用户叶为bbt Root CA签发的EC P-384服务器证书，SAN含192.168.5.194/127.0.0.1/10.88.50.194，无localhost；使用127.0.0.1本机TLS上游。根信任由用户管理。
- Alternatives considered: 自动导入系统根、读或复制根私钥、忽略校验均不采用。
- Compatibility: 用户证书在Node正常校验与本机普通Chromium通过；Python3.14/OpenSSL3.5严格模式拒绝，原因Missing Authority Key Identifier。保留失败证据，不关闭严格模式；正式代理/网络验证采用生产Node正常校验，不能声称所有TLS栈兼容。

后续用户授权补齐AKI/SKI，默认严格OpenSSL验证通过，见[本机补签记录](verification/local.md#后续补齐服务器证书-akiski2026-10-12)；同证书已部署D4并以真实握手指纹/两端文件摘要核验，见[部署验收](verification/d4-deployment.md)。上述失败仅为原证书的历史记录。
