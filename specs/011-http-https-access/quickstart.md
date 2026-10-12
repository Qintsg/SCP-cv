# 本机验证指南

1. 证书/密钥放frontend外；设置SCP_CV_HTTPS_CERT_FILE、SCP_CV_HTTPS_KEY_FILE、SCP_CV_HTTPS_HOSTS（逗号分隔SAN中的IP/DNS）。用户CA的公开root_ca.crt通过启动Node前的NODE_EXTRA_CA_CERTS提供上游信任；不导入系统根。
2. HTTP默认5173、HTTPS默认5174；VITE_FRONTEND_PORT/SCP_CV_HTTPS_PORT分别覆盖，preview CLI主端口优先。
3. VITE_BACKEND_HTTP_TARGET默认http://127.0.0.1:18444，VITE_BACKEND_HTTPS_TARGET默认https://127.0.0.1:18443；保留VITE_BACKEND_TARGET明确单协议配置，不静默重写。
4. ControlHost网页推荐Authentication__CrossSiteCookies=false，默认true也支持独立HTTP同站Cookie；AllowedOrigins精确包含两网页Origin，receiver仍loopback18444，不公开其cap入口。
5. pnpm --dir frontend test/build:web及.NET认证/全非Physical检查；本机正式ControlHost Simulation使用自有数据库，不启Player/墙面/电源。
6. 真实浏览器测试HTTP普通登录、确认取消/键盘/主题宽度、确认跳转到实际HTTPS端口及正确页面。未信任证书时浏览器正常警告，不自动采集。
7. 显式信任公开CA的独立HTTPS测试客户端验证TLS/并发/会话往返，不能当浏览器或D4共享出画证明。
8. 本机实现验证结束时关闭自有监听，Git/dist无证书秘密，证据记verification/local.md。后续用户授权的D4部署已启动HTTP5173/HTTPS5174；当前任务、私有路径、限定内网防火墙及验收边界见verification/d4-deployment.md，不沿用010旧端口启动第二组。
