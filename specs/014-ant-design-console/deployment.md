# 014 D4网页发布（2026-10-18）

本日后续已发布012 T044/T045的前端安全补丁c2bcf0a，Ant布局与服务入口保持；最新网页以[接续发布记录](../012-directory-camera-console/verification/20261018-venue-web-deployment.md)为准。下文014/9594c19是本日较早阶段，备份与原证据保留。

## 结果与范围

已按用户授权推送GitHub和内网GitLab，再在D4的`D:/SCP-cv`从干净工作区正常快进拉取。网页制品固定源码为`9594c19e4baa8f2d6b6ec08d020e320adfb3062c`；本机959/959前端测试、Web/App含类型构建、Electron主进程编译、54SFC、90次重点浏览器及原账户/连接/操作和56组页面通过。

仅替换静态网页和原登录启动清单的四个网页字段。Node、ControlHost、Supervisor、两Player、AudioWorker和MediaMTX均未重启；未下发墙面、电源、摄像机、媒体或音量控制命令。后端仍运行012k/06d342e，未升级.NET或其它运行环境。

入口保持[HTTP80](http://192.168.5.194/)默认307到[HTTPS443](https://192.168.5.194/)。已打开页面不会被强制刷新，用户刷新后进入新版Ant控制台。

## 制品与保留

- 新包67文件，ZIP SHA256：`5E1A700F53167ACC7D0B0CA094B32B5B571737BFC7616234F820575E8FC083EE`。
- 新index SHA256：`29B65117AFB17B38DCCAA2FAAA7DD973E6E28C8AAC5FF9F4DF8860760B02B3A8`；主JS为`index-BxmTguhu.js`，CSS为`index-ILbdnaac.css`。
- 远端先验证归档边界、文件数和逐文件摘要，再替换。原网页完整保存在`D:/SCP-cv/.validation/deploy-014-20261018/frontend-dist-before`，原登录清单保存在同目录`frontend-startup-manifest-before.json`。
- 18个旧哈希资源逐项保持原摘要，其中17个作为额外文件保留，避免已打开页面的旧懒加载资源被发布移除。新旧同名资源必须摘要相同，不能静默覆盖。
- 仅更新原012h清单的`frontend_hash`、`frontend_archive_hash`、`frontend_files`和`frontend_commit`；其余字段正确投影比较完全一致。原Node仍使用相同配置与80/443，不需要重新安装前端开发依赖才能提供已打包网页。
- 数据库、媒体、证书/私钥、信任、系统代理、防火墙、显示器及运行任务均未修改；不冷拷贝运行中的数据库。

## 实际检查

| 项目 | 证据 |
| --- | --- |
| 版本 | GitHub、内网GitLab及D4正常快进到制品源码；最终记录提交另行同步，不重复部署 |
| 七进程身份 | Node23000、Host23684、Supervisor24500、Player28320/19944、Audio29728、MediaMTX21620；PID/实际启动ticks/路径/会话逐项不变，均会话1 |
| 原两窗 | 窗口1/2均源及实际源116、playing；代次1113/785已匹配，DISPLAY2/3保持；窗口1音量100/未静音/循环，窗口2音量100/静音/不循环保持 |
| 其它投影 | 原10个源ID集合、系统音量/静音、背景音频及列表投影完全一致；无pending、错误或清理残留 |
| 服务 | 原网页任务Running，80/443仍属于原Node；后端`/health/ready`为200/Healthy |
| HTTPS文件 | 本机正常TLS下载67个新文件，全部200且SHA256与制品清单一致 |
| 默认跳转 | HTTP307保留路径和查询；首页只跟随一次到HTTPS200且index摘要一致 |
| 真实浏览器 | D4实际HTTP→HTTPS登录页，1440/390两种宽度，正常证书校验、0未知错误/警告/横向溢出、0业务写入 |

远端安全证据7文件ZIP、本机独立副本摘要均为`51A29F8BEF98E2157C3C4F358204D1A2FF59C85BD0057CB99DFE9A505E0B97A6`，位于双方忽略目录`.validation/deploy-014-20261018/`。包含前后只读状态、发布回执和原/新启动摘要，不含Cookie、密码、数据库或私钥。

## 工具问题与边界

- 内网GitLab的旧HTTP凭据未自动用于HTTPS；仅在当前命令进程内将已有同主机凭据用于受验证HTTPS，正常推送成功，无凭据落盘或TLS绕过。SSH22主机指纹与已有记录及该实例HTTPS公开指纹不一致，未忽略检查、未删除known_hosts或继续SSH认证。
- 首版辅助脚本在Windows PowerShell5被按ANSI解析，未进入正文；改为明确UTF8/BOM后执行。旧index预检还拦住手抄SHA多一个字符的笔误；独立核D4实际文件、原启动清单和上轮本机HTTPS副本完全一致后修正为64位摘要。两次失败均发生在网页替换前，不把它们当生产服务故障，也未放宽门禁。
- 本轮D4只有认证登录/注销和GET/HEAD检查，不做真实控制功能点击或共享采集。无.NET改动，本轮未重跑.NET/Redocly或重新验收原生播放；012的10项现场/人工欠账保持未完成，摄像机测试仍按用户要求暂停，不以网页发布关闭长期目标。
