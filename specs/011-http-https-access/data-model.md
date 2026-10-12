# 入口与跳转状态

- AccessInfo: version=1；httpsPort为1..65535整数或null；无主机、路径、upstream或秘密。
- ServerProfile: 无协议补HTTP，明确HTTP/HTTPS保持；拒空值、其他协议、凭据、非根路径、query/hash。旧开发标志保留读取兼容。
- ProtocolCookie: HTTPS命名不变；HTTP追加_http。读写注销均按Request.IsHttps，另一协议不能替代。
- ProtocolTicket: 框架保护的票据属性绑定签发协议；改Cookie名或缺少旧标记均不能跨协议认证。升级后旧会话需重新登录。
- Upgrade: idle→confirming→discovering→navigating或idle/error；账号、主机代次、在线状态、组件寿命必须仍相同，失效aborted。
- Navigation: 同hostname、实际HTTPS端口，保留合法路由/search/hash，移除秘密参数；新页面无采集授权恢复。
