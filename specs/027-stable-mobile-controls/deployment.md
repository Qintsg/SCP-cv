# D4部署与客户端制品

日期：2026-10-27。

## 运行部署

GitHub和GitLab均已推送，D4从GitLab快进。运行制品对应提交`16f62df39a8312fcc70e7e132cdd8aa646e6275e`，目录为`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261027-027-r2/`；981个文件与本机构建清单逐项SHA256一致。网页81个文件经跨机正常TLS访问后逐项匹配。

旧运行组五个成员协作退出0，后端和网页随后退出。数据库三文件冷备位于`D:/SCP-cv/.validation/release-027/database-before/`，复制摘要一致；旧运行包、网页和任务定义保留。新任务`ScpCvRuntime-027-20261027`与`ScpCvFrontend-027-20261027`均使用PowerShell 7，运行于admin交互会话1。桌面一键启动入口指向本轮`startup.json`。

部署时窗口1实际播放来源144，PDF模式、第1页；更新后只提交一次恢复请求，实际来源与代次匹配。窗口2空闲。七个核心进程及两播放器在线，当前窗口没有待执行命令或错误；再次调用正式启动入口后，七个进程的PID与启动时间均保持，文稿仍在第1页。

11个媒体登记、3个目录及6份原件保持；原件SHA256逐项匹配。系统音量、窗口配置和背景音乐设置保持。五屏从左向右排列，DISPLAY1为主屏；DISPLAY16–19的桌面尺寸均为3840×2160，窗口1/2仍绑定DISPLAY16/17。部署后的显示配置与部署前快照一致。

HTTP 80返回307至HTTPS 443。通过`10.88.50.194`访问时正常验证证书、主机名和全部81个网页文件。Capacitor来源`https://localhost`的真实HTTPS预检与CSRF读取通过，允许凭据的响应头正确。现场墙面、电视、电源、云台和音源控制不属于本轮部署验证。

## Windows便携ZIP

本机桌面：`C:/Users/qintsg/Desktop/SCP-cv-Windows-x64-Portable-20261027.zip`。

- 大小275831228字节，约263 MiB。
- SHA256：`91B508CDC8929610819BF67230914F14468E96EF648BD41098ECCB549689CEBE`。
- Electron 44.2.0、Windows x64；解压后运行`SCP-cv Control.exe`。
- 包内101个应用代码文件和195个原生预览资产逐项匹配当前构建；ASAR摘要为`c99d8c0f87f2976615673b8dbf1680ef23ad73db7582d228f546f05b03b74df8`。
- ZIP完整性校验通过，共269个文件。沿用本机使用的媒体组件和许可证文件。

## Android APK

本机桌面：`C:/Users/qintsg/Desktop/SCP-cv-Android-arm64-v8a-20261027.apk`。

- 大小4553539字节，约4.34 MiB。
- SHA256：`3B82015C977A9F6A89F3319BABF2115BDD9992ADEE6D12C6FA6C8D4EE1F02586`。
- 包名`cn.edu.sspu.scpcv.control`，Release、versionCode 1；最低API24，target/compile API36，WebView最低111。
- Java21、Gradle8.14.3、SDK36；输出元数据的ABI过滤器为`arm64-v8a`。Gradle使用南京大学镜像和官方SHA256校验。
- Release构建及Lint通过；APK v2/v3签名和16 KiB排列校验通过，包内81个网页文件逐项匹配。
- 签名证书SHA256：`fd53edbf1413e827214a971d47752162c94a76e2997ec15021d785834656e88b`。后续更新须复用同一密钥；本机密钥和口令文件保存在忽略目录`.validation/private/android-signing-027/`。
- 资源表、网络配置和证书字节已核验。公开根证书只向D4的`192.168.5.194`及`10.88.50.194`提供附加信任，HTTPS继续验证主机名和证书链。首次连接填写`https://192.168.5.194`或对应组网地址。

本轮没有连接Android真机进行安装或触摸验证。原始发布、签名、归档与远端核验记录保存在本机及D4的`.validation/release-027/`。
