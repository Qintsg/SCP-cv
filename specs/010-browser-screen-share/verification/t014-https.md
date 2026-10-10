# T014 显式HTTPS与证书门禁（部分，2026-10-11）

## 状态与范围

从签名保存点`7a13a11`继续。requirements只读16/16，extensions.yml不存在。配置、文档和服务器TLS/API已验；普通浏览器信任未通过，T014仍未勾选，010目标不完成。没有部署010到D4、启动新现场组、改变墙面/电源、请求麦克风或采集用户桌面。

## 实现

- `frontend/config/https.ts`在Vite dev/preview服务器读取三个SCP专用配置；无配置保持HTTP，部分/错误配置终止，不生成默认证书或降级。日期、叶证书、可选EKU、全部主机SAN、私钥匹配和TLS上下文均校验，最低TLS1.2。
- 文件限frontend目录外、普通`.pem`、256KiB；错误不回显路径/内容/解析异常。配置路径与实体路径分别核边界，frontend自身由目录链接访问也不能绕过。Vite public不适用fs.deny，不能靠后缀阻断保护其中的密钥；见[官方规则](https://vite.dev/config/server-options#server-fs-deny)。
- build:web/封装构建不读取私钥。SCP变量不使用VITE前缀、不进入客户端；不改认证/CSRF装配、Cookie语义或changeOrigin。HTTPS代理验证上游，本地CA用Node的公开NODE_EXTRA_CA_CERTS文件提供，不关闭校验。
- [指南](../../../docs/screen-sharing.md)记录正常CA签发/存放、Windows当前用户信任和确认、Node上游信任、Kestrel监听与Vite入口；不将实施步骤当作已在D4执行。

## 自动化结果

- 专项31/31、0失败/跳过、实际退出0：6种部分配置、8种非法主机、文件/大小/PEM/期限/私钥/SAN、无配置HTTP和相对路径，另6项文件位置/链接门禁。公开测试叶证书无私钥；每次密钥临时生成在自有mkdtemp目录，结束清理。
- 路径回归先27项25通过/2失败，修正后30/30；根目录链接新增回归31项30通过/1失败，定位为配置路径错误地与实体根比较，分别核配置根/实体根后31/31。不用畸形PEM异常冒充路径保护。
- 最终全前端425/425，0失败/取消/跳过/todo，实际退出0，103654.6787ms：`.validation/qa-screen-share-010/t014-secure-files-frontend-full.log`。此前419/419原日志保留，不与425相加。
- typecheck实际退出0：`t014-secure-files-typecheck.log`；build:web实际退出0：`t014-secure-files-build.log`，Vite2.87秒，入口1138.20kB/gzip324.68kB，既有500kB告警保留。真实Vite另提示新增无后缀配置import不适用未来native默认loader；当前8.2.0默认loader可运行，未用环境变量抑制提示。
- 本轮未改后端、不重跑.NET；上一T013的1427/1427不当本轮或T018证据。API合同未改，不重跑Redocly；最终完整功能门禁仍待。

## 真实TLS、正式ControlHost与代理

ignored QA直接拥有Node和正式ControlHost进程。Host为Simulation、独立DataRoot、本轮随机账号，无Worker/硬件。口令、Cookie值、私钥不输出或记录。Kestrel/Vite各用自有叶证书；Python默认SSL上下文仅添加该公开CA，仍核链及主机名；Node上游也正常验证。

1. `a433aa9ea4a3471e83cb083f468ead51`取得真实登录/me200、Secure Cookie、会话HttpOnly、缺CSRF400 `csrf_failed`、带CSRF503 `share_player_offline`；普通Chromium随后拒绝未信任CA，不称浏览器成功。
2. 最终路径门禁后dev专测`c25c4d3f18714e4a8a65fd2e8f2d268c`实际退出0：登录/me/注销200、两Cookie均Secure、会话HttpOnly、CSRF400和真实离线503；`/@fs/`私钥403且正文无PRIVATE KEY。此前`7ae3247e11524ca4aacb5c8d4532fb02`同条件成功证据保留。`api_completed=true`、`browser_pending=true`、整体`completed=false`分开。
3. 不给Node本轮CA的负向实验：先经正常SSL确认Kestrel/API与Vite页面就绪，再测代理。`c54203f0f1434e50864e6b3764029a51`实际退出0，上游502、日志明确无法验证首张证书；不是连接拒绝或Host未就绪。`upstream_validation_completed=true`不等于浏览器/功能完成。
4. Web构建的preview入口正向`5e89b146b7b742bc90b91af5f51e660c`和未信任上游负向`767b87f2274242bf9b8a9c84888b2c5a`均实际退出0，同样保持Secure/HttpOnly、CSRF400/离线503和上游502证书拒绝。preview没有dev的/@fs入口，私钥URI返回SPA index200而非403，正文无密钥；不能把该200说成私钥可读或完整功能成功。客户端dist独立扫描无TLS私有配置/密钥，全部改动文件严格UTF8、LF、不超过500行，Spec Kit/diff通过。

证据位于`.validation/qa-screen-share-010/t014-https-runs/<上述ID>/`。服务器在finally终结并等待原进程退出（主动终结返回1，不作测试失败），独立TCP5212/5213监听0。正常TLS不证明采集、媒体传输或D4出画。

## Windows信任仍待确认

- CA/私钥仅在ignored `t014-trusted-20261011-b/`；不运行全局mkcert install，不分发根私钥，只尝试添加精确公开CA到CurrentUser/Root，不改LocalMachine、保护注册表、浏览器策略或安全flag。
- 同一`certutil -user -f -addstore Root` exec句柄`93274`仍活着，实际certutil PID53176/session1；Windows等待系统确认。独立查询精确CurrentUser根为false。观察超时不是退出，不因此重启导入；签名匹配也不是信任完成。
- 完整Chromium新上下文明确`ignore_https_errors=False`，正常导航实际`net::ERR_CERT_AUTHORITY_INVALID`。没有可信LAN安全上下文/采集证据，不改为忽略错误或点击继续。
- 确认后核原句柄终态和精确根存在，再做正常浏览器TLS/真实getDisplayMedia/轨道释放；结束只移除本轮精确CA并核不存在。等待期间保留句柄/ignored材料，不启动第二份或声称已清理信任。

## 保留失败与边界

- 初次辅助脚本用CreateFromPemFile读公开CA还会寻找私钥，失败在导入前；改只读X509CertificateLoader.LoadCertificateFromFile，不导出个人证书。
- API就绪前Vite502被当JSON，保留`86e774913dc241cd9f038135e16a92a6`；只修夹具有界就绪/非JSON处理，不给生产添加副作用重试。
- 首次负向夹具期待500/原始Node错误码，实际Vite8.2.0为502/证书错误文字。保留`9de401fc73814fe2a218f178db04eed1`，按已定位合同修QA，负向绿不称生产缺陷已修。
- 带管道字符的pnpm测试过滤在Windows cmd解析失败，未运行测试；随后完整专项实际红绿，不计作业务失败/通过。
- T014普通浏览器可信LAN采集、T015完整页面/真实选择器取消、T016正式两Worker/D4跨机/连续5分钟/十轮异常清理及T017/18最终门禁保持原范围。不把两Host、TLS代理、425前端或D4稳定009健康复核当完整目标实现。
