# 011本机双协议验证

2026-10-12，基线`51ec916`，Windows本机。范围仅本机代码、独立Simulation数据库和自有网页监听；未推送/部署/重启D4，未操作墙面、电源、显示器或音量，未导入/删除系统根证书。

## 结果

| 层级 | 实际结果 | 边界 |
| --- | --- | --- |
| 前端完整回归 | 446/446，0跳过/失败 | Node/HappyDOM不当作真实排版或采集证明 |
| 类型及Web构建 | build:web包含vue-tsc，退出0 | 保留既有大chunk和Vite未来native loader扩展名告警 |
| .NET全非Physical | 1446/1446，0跳过/失败 | Domain41、Windows197、Infrastructure171、Contracts325、Integration279、ControlHost433；不启实体输出 |
| 真实双代理 | 同一主机Cookie罐三轮往返、独立注销、18次目录创建/改名/删除、128并发认证读取、两侧SSE均通过 | 自有新DataRoot，用户媒体/数据库不改 |
| 实际Vite socket | dev/preview双入口、128并发分流、WSS HMR、无证书发现null、同端口/两侧占用拒绝及清理通过 | 固定自有5240–5243；HTTPS使用正常CA校验 |
| 负向安全 | 明确fs.allow仍拒绝.crt/.key读取403，不受信HTTPS上游502且无HTTP回退，preview仅本机监听不暴露副入口 | 不关闭TLS/SAN/链验证 |
| 真实完整SPA | 8组：窗口1/2 × 1440/390px × 浅/深色；每组5次Enter/取消或Esc，焦点归还、无横溢，通过 | 40次取消及8次确认，HTTP零采集/共享写；只读在线字段是明确替身 |
| HTTPS主动许可 | 每组跳转正确安全端口/窗口页并需登录；登录后零自动采集，再次点击才调用一次原生getDisplayMedia，原生拒绝正确反馈且零共享写 | Chromium153.0.8010.12正常证书验证；QA官方auto-reject-capture参数，不称人工选择器/接收出画 |
| 合同及规范 | Redocly两合同退出0、Spec Kit通过、diff/UTF8/LF/密钥检查通过 | 三条原退役PowerPoint 410告警保留，bundle保留已有WindowId重名告警 |

最后服务/浏览器正常结束，5220–5223、5240–5243全部无监听；保留ignored证据，不删用户资料。

## 证书与信任

只读使用用户目录的`SSPU108.crt/SSPU108.key`，不复制私钥，不读`root_ca.key`。叶证书CN192.168.5.194、EC P-384、服务器认证，SAN含192.168.5.194/127.0.0.1/10.88.50.194，2026-10-12至2036-10-09有效；无localhost，内部TLS上游使用127.0.0.1。项目原门禁（内容、配对、期限、SAN、文件路径/实体路径）通过。

Node代理/正式网络客户端通过进程级NODE_EXTRA_CA_CERTS读取**公开**根，保留正常链及主机校验；普通Chromium实际无证书错误，ignore_https_errors=false，没有新增浏览器/系统根。此事实只覆盖当前账号/浏览器，不证明其它设备信任。

Python3.14/OpenSSL3.5.7默认严格模式独立报`Missing Authority Key Identifier`。保留原失败，不放宽严格模式、不称跨TLS栈全部兼容。建议重新签发时完善AKI/SKI扩展；项目实际Node/Chromium验证与这一严格模式失败分开报告。

## 红绿与回归修正

1. 地址与双入口、升级纯合同/面板、协议Cookie测试先红后绿。Vite代理回调原类型和正式类型不一致先编译失败，改用公开ProxyOptions对应字段后类型/构建通过。
2. 默认CrossSiteCookies=true的正式HTTP防伪Cookie被标Secure，新增默认配置回归红；按请求复制HTTP CookieOptions及分协议CSRF后两种配置三轮均绿。HTTPSSecure与原SameSite配置保持。
3. 旧网页“构建上游直连”断言与新同源代理设计冲突，原全前端438/439；修订合同后完整446/446，不改已选择主机或封装端回退规则。嵌套路由/fragment的passwd等秘密新增红例，统一敏感赋值匹配后绿。
4. 原全.NET1440/1441在接收准备GetReceiverGrant失败；独立10轮清理并发回归复现红。夹具直接调用Registry/Runtime绕过正式窗口门禁，改用Service.CreateAsync/OfferAsync后14项接收/协议测试绿，不禁后台或放宽生产期限。
5. 后一全量1441/1442显示目标已被后台撤销时音频接口正确409，但原断言只认503。回归分别覆盖四种目标变化的清理前/后：严格核503/share_player_offline，或409/share_audio_unavailable且实际stopping，均零音频排队；8项绿，最终全量1446/1446。此为测试时序语义修正，不改生产业务。
6. 实际preview的dev host0.0.0.0与preview host127.0.0.1不同，副入口漏用dev host；自有本机网卡TCP负例明确红。分别使用解析后的dev/preview host后socket全绿、原生关闭端口全释放。
7. 首个Node普通媒体写探针出现ECONNRESET，原证据保留。探针改为完整消费每个JSON响应、记录无秘密的失败路径后最终18次写及128并发通过；未加重试或修改生产运输，不确定底层重置根因，不称已修生产网络bug。

## 需求追溯

- FR001：地址/连接页合同；FR002：真实双监听/代理/负向TLS/端口；FR003–005：纯升级合同、实际Vue及8组浏览器。
- FR006：正式认证两配置、Cookie改名重放拒绝、同罐三轮和18普通写；FR007：原全部共享/接收/队列回归。
- FR008：外部密钥门禁、明确allow仍403、构建/追踪文件无秘密；FR009：HTTP警示与封装策略；FR010：重复点击、账号/主机/pagehide/卸载失效与迟到发现拒绝。
- SC001–005分别由地址合同、面板/浏览器、网络往返、真实几何/键盘/控制台、socket/TLS/无泄漏验收覆盖。

## 复现与原始证据

命令：`pnpm --pm-on-fail=ignore --dir frontend test`、`pnpm --pm-on-fail=ignore --dir frontend run build:web`、`dotnet test runtime-dotnet/ScpCv.sln --no-restore --filter Category!=Physical`、`redocly lint docs/openapi.yaml specs/011-http-https-access/contracts/access.yaml`、`uv run --offline --no-project python .specify/scripts/python/validate_specs.py`。

本机安装pnpm11.24.0而项目固定11.22.0，自动获取固定版本曾无输出等待；只使用本进程pm-on-fail=ignore执行已安装依赖，未安装新依赖或更改packageManager/全局镜像。CERNET仓库配置保持。

原始ignored证据根`.validation/qa-http-https-011/`：最终正式SPA/网络`run-289229d670214eefa472f05562194bb1/evidence.json`及8组截图；最终.NET`backend-complete/*.trx`；socket `socket-complete.log`；原失败分别保留在`backend-final/`、`backend-verified/`、`receiver-race-red/`和其它run目录。QA脚本仅在ignored目录，不新增生产Python设施。浏览器测试采用webapp-testing/Playwright分层流程，未把在线字段替身、许可拒绝或API200当D4出画。

## 未执行边界

本轮没有D4新版本部署、实体双Player接收、长稳、现场扬声器或人工系统选择器；010历史证据保留，但不用于宣称011已验这些项。无全局强制HTTPS重定向/HSTS，不新增封装端明文认证能力。公开根信任仍由用户管理，HTTP明文风险不能被提示框或独立Cookie消除。
