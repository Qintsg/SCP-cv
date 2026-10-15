# HTTP默认HTTPS验证（2026-10-15）

## 实现与本机验证

用户最新要求取代此前“HTTP直接提供控制台”。已配置TLS时，网页服务在业务middleware前返回307/no-store，目标为同主机和实际HTTPS端口，路径/查询保持。GET、HEAD、POST均不先进入HTTP业务；HTTPS不会循环跳转。无TLS开发环境继续HTTP，错误TLS配置仍拒绝启动。

新增8项回归先复现4个HTTP200不跳转及缺少目标构造，修复后通过。首轮测试夹具的middleware不是可调用函数，已修正夹具再取得有效红灯，未把夹具错误当生产缺陷。

| 检查 | 结果 |
| --- | --- |
| 入口、TLS、跳转、地址与连接页相关回归 | 55/55，0失败、跳过；新增8项 |
| vue-tsc | 退出0 |
| Vite网页构建 | 退出0，17.93秒；既有大chunk/配置导入警告保持 |
| Redocly CLI 2.63.0 | access.yaml合法，直接CLI退出0 |

回归命令为`node --test scripts/http-https-redirect.test.mjs scripts/dual-protocol.test.mjs scripts/frontend-https.test.mjs scripts/https-upgrade.test.mjs scripts/client-connection.test.mjs scripts/server-connection-view.test.mjs`。没有变更后端或播放模块，本轮不重复全量.NET与无关前端回归。

HTTP socket回归是真实本机监听；HTTPS分支单测不等同真实握手，正常TLS代理与认证由D4另验。CLI下载最初受代理与Node信任链影响，最终通过当前进程的正常系统CA及npmjs直连取得工具；直接调用缓存Redocly再次退出0。仓库CERNET配置未改，不关闭TLS验证。首次pnpm子进程退出断言保留，不冒充干净退出。

## D4部署

已将`c4d62d2cbf379fcd00203324e65af2c9b6b26ab1`推送GitHub与内网GitLab，D4干净工作区正常快进；9个网页文件逐项核SHA256后替换，ZIP为`D182BA2DBB7792C575FDC2625A85F066908866843D1CF67EA00C1EA5B14705F6`，index为`6CCEDCC3079AFA2957A23B88F5EE627C12484726181ABEF6A5F455538CAA4BA3`。

原网页和启动摘要保存在`D:/SCP-cv/.validation/https-default-20261015/frontend-dist-before`及`frontend-startup-manifest-before.json`，11个旧哈希资源保留。原Node日志和任务XML也已保存。停止原网页任务并通过原PID/启动时刻/持有句柄确认Node23616退出后，以原任务`ScpCvFrontend-012h-20261014`启动Node23932，仍在会话1并拥有80/443。TLS、根信任、防火墙和原任务参数未变。

| D4检查 | 结果 |
| --- | --- |
| HTTP默认跳转 | 根页、带查询的媒体页、入口发现、HEAD深链、POST非业务测试路径，共5项均307/no-store且Location准确 |
| 跟随跳转 | HTTP根页恰一次跳到HTTPS，200且index摘要一致，没有循环 |
| HTTPS静态文件 | 9次请求全部200且逐文件摘要一致，正常验证证书 |
| HTTPS发现 | 200，公布443 |
| HTTPS代理认证 | 用现有CA正常校验，登录/CSRF/两窗状态/注销均200；口令与Cookie只留进程内存 |
| 播放运行组 | Host10436、Supervisor27108、Player28752/19400、Audio18540、MediaMTX29576的路径/会话/启动时刻全部保持 |
| 原播放与数据 | 窗口1仍源115/playing/1107、DISPLAY2；窗口2idle/772、DISPLAY3；原13源、声音/背景/静音/循环投影不变，健康Healthy |

八文件证据归档在本机/D4的SHA256均为`57BCE88E364A001191056221CF09488587E8DCD4560015FC68B80E0BC264143D`；本机另行比较重启前后运行组与状态通过。自有脚本/证据在忽略的`.validation/https-default-20261015/`；未复制证书、口令、数据库或用户媒体。

内嵌浏览器本次访问现场地址得到ERR_EMPTY_RESPONSE，错误页又受工具URL策略限制，浏览器导航未记通过；未修改浏览器代理或绕过策略。服务端跳转、正常TLS内容及认证由上述独立网络验证确认。不把本次入口检查扩展为媒体/采集/实体设备测试。
