# T016跨机预检与本机测试包准备（尚未实测）

2026-10-11，页面门禁已签名保存a19e1b1。T016保持未勾选；下面不代表D4已经更新、跨机ICE可用或两个正式Worker已共享。

## 已执行

- `.validation/qa-screen-share-010/t016-d4-preflight.ps1`只读SSH核查实际退出0。D4源码fd64de6c8a8d11b4c3adcea0937534e8c4fa32b3、工作区干净，admin console会话1 Active。原ControlHost10416、Supervisor22724、Player9460/3760、Audio21636、MediaMTX19728保持，应用来自deploy-20261009-c824924；原运行组/前端计划任务仍运行。
- 原DataRoot的control.db、原私有开发口令文件及MediaMTX存在；没有读取/输出口令或更改文件。D4具有.NET/ASP.NET/WindowsDesktop10.0.11。本机IPv4以太网192.168.1.99/24，D4以太网192.168.5.194/24；SSH可用不能证明WebRTC/UDP或mDNS跨网段可用。
- 发布依赖只读检查通过。原包准备在`t016-package-a19e1b1/`因Player还原NuGet漏洞数据NU1900失败，部分产物及`t016-publish-a19e1b1.log`保留，没有覆盖或发布半包。
- 仅本次发布命令清空HTTP/HTTPS/ALL代理，使用全新`.validation/qa-screen-share-010/t016-package-a19e1b1-direct/`，保持锁定还原、漏洞检查和错误门禁，五个组件及隔离Framework宿主/官方VLC插件索引生成成功，实际退出0。原始`t016-publish-a19e1b1-direct.log`保留。正常直连官方服务索引经其HTTPS重定向返回200；未修改全局代理、NuGet源或依赖。
- 最终正式Web产物复制到上述本轮包的web目录，没有采集测试HTML或TLS密钥。主要摘要见下表；准备前本机工作区干净，无010推送/远端写入。

| 文件 | SHA256 |
| --- | --- |
| ScpCv.ControlHost.dll | 993CF85FE563675A0F7B4E4244E28E775E5C0571611D526FDAC009025F9975B3 |
| ScpCv.Supervisor.dll | F4C29056B24C2E1731D554FF1E00C8D655DD31EBB6E2BFB41FFA8CDF2D00E4EB |
| ScpCv.PlayerWorker.dll | 7223818865F4E0B167B18303C8658483872CB47835DAE6FC69F8969C9F894018 |
| ScpCv.AudioWorker.dll | AC20A9DC4FF8C27DD5DD36D43D29C8F43986310671D80E138B27C13024D83EBE |
| libreoffice/ScpCv.LibreOfficeHost.exe | 8707C00022DFB73393827F3B8D6DC199BF4710168C574D1570DEC742A683CFBE |
| web/index.html | 57327FFE3F2FC77628B6526F9FD53506DEE43E153C21689912E7A42E37576B9B |

## TLS准备与下一步约束

已核本轮公开CA指纹9C71B09C2EF04F3FC66815AB0A667F4C2B3C49A6，正常信任仍在。mkcert仅签发D4/localhost/127.0.0.1的叶证书，保存于忽略目录`t016-private-a19e1b1/`；叶证书指纹CB1301B671F3B708BDF2518018C073ECA606D0FF，私钥ACL仅当前用户及SYSTEM。没有执行再次CA安装、传输任何私钥或更改D4证书库。

准备接收地址前已只读核正式`ScreenShareOptions`与`ScreenShareReceiverIdentity`：二者允许严格loopback HTTP(S)，仍须cap、原Worker/窗口/代次、来源及路径。可使用独立仅127.0.0.1监听的HTTP接收端点，以及正常可信HTTPS网页/API，避免为私有接收流程关闭证书验证或给D4导入QA根；这是待部署验证的配置选择，不是已完成设置。原headless启动脚本存在旧HTTPS就绪探测放宽逻辑，不能据此当正常TLS证据；下一步需明确避开该路径并独立正常校验HTTPS。

实际切换前仍须核源/包摘要、精确旧组协作停机及原进程退出、冷备份原数据库、两目标DISPLAY2/3和控制桌面保持；新包不得携带用户数据或CA私钥。跨机首帧、连续双共享五分钟、十轮、普通换源/断线/迟到隔离、原生和发布资源释放、非目标运动及现场声音都尚未执行。不发墙面/电源，不将预检或HTTP200升级为实际出画。
