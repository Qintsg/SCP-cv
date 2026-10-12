# 011 D4双协议部署记录

2026-10-12，用户明确授权“部署到D4”。当前状态：独立包已安装，双入口及原两窗已启动，网络/认证/只读状态/真实页面验收完成；这不是新一轮媒体或共享出画测试。

## 部署前

- D4主机192.168.5.194，admin交互桌面会话1保持Active；SSH主机密钥与本机known_hosts匹配。
- 原源码51ec916，远端工作区干净；旧运行包deploy-20261011-4f76a10，运行任务ScpCvRuntime-20261011-4f76a10，前端任务ScpCvFrontend-010-20261011。
- 原DataRoot为D:/SCP-cv/.validation/t129-workstation，control.db存在；窗口1/2在线空闲、无媒体/共享，代次分别844/577且已观测一致。
- 播放目标DISPLAY2/3；DISPLAY1控制桌面和DISPLAY4/5均不属于播放目标，未重新选择显示器。

## 源码、包与证书

- 签名提交6581f15、fd71f6e、0b3a659均有效；推送至本机gitlab（既有secondary用途）远端，D4 origin为同一GitLab，已ff-only拉取0b3a659。
- 本机按publish-runtime.ps1生成完整Windows包，Web构建含vue-tsc通过；D4 Vite8.2.0与现有依赖一致，无新增依赖或镜像切换。
- 归档package.tar.gz为122128260字节，SHA256为D928D4BF38152F245A22CE2E1809B4B1707A76F301FA67C30CAC562D88FE53B9；仅runtime和frontend-dist，不含证书/密钥、数据库或媒体。
- 独立新运行包D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261012-011；ControlHost DLL摘要988AE5F25052661941A91494199102F0A9505A04F9DE3B33A4DBC30DCAE14CD9，Infrastructure DLL摘要313C0CC96158B01A040DA25C0916C9A3882EEACA8D57DEFB122C72BE19780C1F。
- Web index摘要D25A2F06A115562D53FF5770A1EAE125403757EA0A9FFC6F66F1AAAF77A849E4。
- 叶crt/key及公开root_ca.crt经SSH传输至D:/SCP-cv/.validation/private/web-access-011-20261012；目录取消继承，仅admin/Administrators/SYSTEM有FullControl。根私钥未上传，未导入系统/浏览器根。
- 叶证书使用已补齐AKI/SKI的SSPU108.crt，三个SAN IP不变，无localhost；Node加密上游使用127.0.0.1并通过进程级NODE_EXTRA_CA_CERTS正常验证。原证书修复记录见[本机验证](local.md#后续补齐服务器证书-akiski2026-10-12)。

## 操作边界与保留诊断

首次SSH使用不匹配Administrator账号时连接被重置；改用原admin后成功。最初Git push在本机代理配置下无进展，取消精确自有Git进程后使用本进程无代理和非交互凭据配置成功推送，未改全局Git配置。远端交互PowerShell默认执行策略拒绝部署脚本，改用明确脚本的子进程ExecutionPolicy Bypass，未修改机器级策略。以上失败均发生在停止旧服务之前。

保留旧运行包、旧启动任务和私有TLS配置。部署证据/脚本位于两端.validation/deploy-011-20261012；脚本只用于本轮部署，不新增Python运行时或长期部署编排设施。

## 服务切换与当前配置

- 精确旧组协作shutdown返回200，五个旧Worker原OS句柄全部确认退出，再结束原ControlHost和Node；旧任务已停止，不把计划任务状态当退出证明。
- 数据库在原组退出、端口释放后冷备份到D:/SCP-cv/.validation/deploy-011-20261012/database-before-011，control.db及存在的wal/shm复制摘要一致；目录ACL仅admin/Administrators/SYSTEM。媒体和原DataRoot不迁移、不覆盖。
- 旧Web dist移动到本轮previous-frontend-dist后安装新Web；旧deploy-20261011-4f76a10包、旧任务及TLS配置完整保留。
- 新任务ScpCvRuntime-011-20261012、ScpCvFrontend-011-20261012使用D4\\admin的Interactive/Highest，不新增开机触发器。二者Running，原任务Ready不运行。
- 六个正式项目进程均在桌面会话1：ControlHost12848、Supervisor22852、Player19912/24452、Audio19468、MediaMTX9832；Node25488。新.NET进程路径均来自新包，未出现第二组。
- 原DataRoot/runtime-processes.json存在，四个成员role/PID/start/session与新Worker一致；ControlHost loopback /health/ready返回200。最终复核原进程身份仍未变。
- 网页监听0.0.0.0:5173/5174；ControlHost仅在127.0.0.1:18444提供明文上游，HTTPS绑定0.0.0.0:18443。前端加密上游为https://127.0.0.1:18443，私有receiver仍http://127.0.0.1:18444。
- 网页AllowedOrigins精确包含现场192.168.5.194、overlay10.88.50.194及必要loopback对应端口；CrossSiteCookies=false，不放宽为任意来源。HTTPS_HOSTS不填无SAN的localhost。

## 防火墙定位及用户授权

两入口本机监听且loopback发现接口正常，但本机192.168.1.99访问均连接超时。只读定位发现D4以太网为Public，原Node TCP规则明确Block/Any，优先于原5173 Allow；原5173 Allow也只含LocalSubnet及旧客户端192.168.1.109。不是证书或认证失败。

用户明确允许“仅开放这两个内网端口”后，只做以下变更，防火墙、网络类型及原UDP阻止保持：

- 原TCP阻止规则TCP Query User{97B2890A-186B-4E29-A172-629008CAC643}D:\\nodejs\\node.exe仍为Public/Block，LocalPort从Any缩为1-5172、5175-65535。
- 新规则SCP-cv-Web-011-LAN-5173-5174仅Allow TCP5173/5174，Program D:\\nodejs\\node.exe，LocalAddress192.168.5.194/10.88.50.194，RemoteAddress192.168.1.0/24、192.168.5.0/24、10.88.50.0/24。
- 原规则及过滤器备份至本轮firewall-before-011.xml；没有删除原5173 Allow或UDP Block，没有修改全局防火墙策略。范围外网络没有进行实体负向联机验证，仅核规则过滤器。

## 实际验收

最终ignored证据：.validation/deploy-011-20261012/verify-02c78bb2eb734d0fb0252d9d6f0f21c5/evidence.json及16张截图。自有QA退出0；无状态替身、无系统/浏览器根导入，无TLS或安全来源绕过。

- 本机跨网段实际HTTP5173与HTTPS5174均返回发现version1/httpsPort5174及no-store。curl明确提供公开CA、Python3.14默认严格SSL均正常，不用-k或关闭verify。
- D4真实TLS1.3叶SHA256 DER指纹1D:9E:72:8E:DC:2D:B1:1B:43:0E:45:0F:20:58:0C:B4:0F:8C:37:04:FD:C0:5E:B3:00:65:17:58:50:31:43:83，与本机原证书及候选相同；两端PEM文件摘要74F62C7F776862B348A7FEC96DE74EB6F5B0E44A97BC9C6FB6AE23C36424BA2F相同。原记录中52:BC…指纹已纠正，非本轮换证书。
- 同Cookie罐三轮HTTP/HTTPS登录及独立注销、32次并发实际认证读均通过；sessionid_http非Secure、sessionid Secure，二者均HttpOnly，四Cookie均Lax。只记录名称/属性，不存口令或Cookie值。
- 两协议真实SSE均收到id/event/data组成的playback_state，两个实际Player在线；HTTP/HTTPS各自API均为两窗online/idle、source/share四身份null、error_message空、cleanup_pending=false、pending_command空。
- 两窗desired_generation=846/579，observed_generation=844/577：协作shutdown和启动脚本的restart各递增一次，停止意图已退役；本轮未下发新播放，不能宣称代次对齐或新媒体后帧。新Worker空闲心跳有效，无待清理/排队命令。
- 实际拓扑仍仅DISPLAY2/3为big_left/big_right，DISPLAY1控制桌面和DISPLAY4/5均不是播放目标；未重新选输出。
- Chromium153.0.8010.12保持ignore_https_errors=false，窗口1/2 ×1440/390px×浅/深共8组实际SPA：每组3次按钮/Escape取消及焦点恢复、一次HTTP→正确HTTPS5174同页、重新登录、不自动采集、再主动点击后的原生拒绝通过。官方--auto-reject-capture只用于许可拒绝QA，不称人工系统选择器或实际出画。
- 24次取消均零采集，8次跳转不自动采集；8次HTTPS拒绝均无共享预约或其它非认证写，页面异常0。主代理检查桌面浅色和手机深色原图，提示/按钮在视口内、无横向溢出。

保留四个原失败run：b956…/2a19…误按大小写找HttpOnly（实际header是httponly），9e88…误把SSE首行id当event，4765…错误要求部署未播放时新旧代次一致；只修ignored QA判据，未改生产Cookie、SSE或代次围栏。error_message按真实合同核，不用不存在的last_error当无错。前述失败不计通过，不删证据。

本轮完整包发布及Web类型/构建均退出0；446前端/1446非Physical.NET全回归来自此前本机实现门禁，不冒称在D4本轮重跑。未进行媒体OPEN/CLOSE、共享预约/帧、墙面映射、电源、音量、PPT、长稳或扬声器听音复测；旧010出画证据保持独立。本机自有QA浏览器已关闭，D4正式两任务继续运行。

## 回退与运维

当前入口[HTTP5173](http://192.168.5.194:5173/)及[HTTPS5174](https://192.168.5.194:5174/)，升级后重新登录；其它设备的证书信任由用户管理，本机通过不等于所有设备通过。

需要回退时，先认证协作停止新组并按原OS身份确认退出，再精确停止新Node/任务，确保端口释放；不能同时启动两组。保留当前Web到另一个已验证的新备份目录，再从previous-frontend-dist恢复旧Web，启动原两个010任务，沿用原DataRoot。不要直接覆盖当前数据库；冷备份恢复会丢失部署后的写入，需另行评估并明确授权。旧任务使用旧TLS/HTTPS5173配置，访问设备仍需正常信任其证书。

如同时撤销本轮防火墙例外，仅移除新规则SCP-cv-Web-011-LAN-5173-5174，并把已核验的原TCP Block LocalPort恢复Any；原UDP/5173 Allow保持，不导入或重置整机规则。该恢复会重新阻止Public网络的Node网页访问，先评估现场影响；不是自动执行的回退动作。
