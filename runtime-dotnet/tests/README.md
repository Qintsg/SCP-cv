# 测试分层

## 动画末页事实

`PresentationTerminalEvidenceTests`Compile Link正式owner纯值模块，验证匹配主序/原末组生命周期、
旧轮/其它与未知模型、丢失/倒退/过渡等12项；不替代UNO绑定或实际播放。
D4实际Player相同父测试旧候选图保持但ok真红→当前单页三轮30noop与原PNG/退出绿，
完整页序/重开/两窗等未验收，见[记录](../../specs/008-libreoffice-playback/verification/animated-terminal-evidence-20261005.md)。

## 背景音频定位（非Physical与D4分层）

`BackgroundAudioCompatibilityEndpointTests.Seek`经正式认证HTTP检查seek受理/目标验证与空源拒绝，
`BackgroundAudioPersistenceTests.Seek`经公开服务/实际SQLite/租约核目标int64、合并与源代次不变。
受理不改实际位置；原生媒体时钟与真实SSE另由独立D4夹具核验，不把单元/HTTP200当播放完成，
见[定位记录](../../specs/008-libreoffice-playback/verification/audio-seek-http-20261005.md)。

## 主序重复安全保护

`PresentationAnimationSafetyTests`核主序第二轮/无限的已知危险边界及void/单轮/分数保护，不启动Office。
公开`PlayerRuntimeHostLifecycleTests.RejectedNativeOpenAndFailedExitPreserveBothCreationCauses`通过实际外部stdio
核具体拒绝原因与双清理失败保持。原生模型接线、旧PNG/普通与PAR重复及D4正常退出另见
[本轮记录](../../specs/008-libreoffice-playback/verification/repeat-safety-and-source-picker-20261005.md)，
纯值绿色不当全部重复语义或动画末页完成证明。

## Windows PDF缓存（Physical）

`PdfPlaybackAdapterPhysicalTests`读取显式SCP_CV_QA_PDF多页只读夹具，测试相邻预渲染缓存和回退均更新CurrentPage，
另测非法页号/释放后访问；缺夹具明确失败，不启动Office或用户窗口。通过
`dotnet test runtime-dotnet/tests/ScpCv.Windows.Tests/ScpCv.Windows.Tests.csproj -c Release --filter FullyQualifiedName~PdfPlaybackAdapterPhysicalTests`
执行，环境变量仅本轮进程设置并恢复。默认Category!=Physical排除，不以默认绿色代替真实PDF测试。
红绿、D4多源画面与边界见008的[非PPT实机记录](../../specs/008-libreoffice-playback/verification/native-media-d4-20261004.md)。

## 分层与常规门禁

- `ScpCv.Domain.Tests`：纯业务规则，不访问文件、网络、数据库或 Windows API。
- `ScpCv.Contracts.Tests`：HTTP/SSE 与 IPC 序列化、OpenAPI 外观。
- `ScpCv.Infrastructure.Tests`：SQLite、文件边界和基础设施适配。
- `ScpCv.ControlHost.Tests`：ASP.NET Core 端点、认证与授权。
- `ScpCv.Integration.Tests`：simulation 模式下的跨层故障与恢复。
- `ScpCv.Windows.Tests`：需要 Windows API 的可自动化测试；真实 Office、显示器和播放器测试
  仍须在验证记录中单独标明。

默认测试不得启动真实播放器、Office、MediaMTX 或设备控制。需要物理副作用的测试使用
`Physical` trait，常规命令通过 `--filter "Category!=Physical"` 排除。

`ScpCv.Presentation.TestHost`是合同测试的外部stdio夹具，不是生产宿主。测试项目构建时将当前配置的
EXE/协议依赖复制到自己的`PresentationTestHost/`，不会进入正式五组件发布包。
`PresentationProcessClientFaultTests`验证排队期限、半帧/迟到/身份错误及在途关闭；不启动LibreOffice。
失败清理只作用于本次已缓存句柄，并有外部预算，强清不能标为通过。

`LibreOfficeConversionCleanupTests`与`PlayerRuntimeHostCreationCleanupTests`使用该外部stdio夹具，
分别走真实上传/Claim/Converter及公开Player→正式adapter，覆盖动作拒绝/制品缺失与后退出0/2。
Player仅创建自有隐藏HWND，不Show；正常非Physical即可运行，不启动Office。环境变量在finally恢复并按测试组隔离。
OLE头占位不是实际PPT格式证据；本地双原因诊断不等于持久API完整堆栈，真实格式/出画另需Physical或受控本机验证。
`PlayerRuntimeHostCloseCleanupTests`另覆盖已打开文稿的公开CLOSE/重复CLOSE/Host退出，正常、单失败及shutdown/exit/自有父窗销毁三失败都需保留；同样仅隐藏HWND和外部stdio，不是实际Office关闭证明。

`ScpCv.ParentGuard.TestHost`为Framework4.8父身份/Timer回归夹具，直接Compile Link正式监测逻辑。
`PresentationParentGuardTests`运行真实observer→parent→guard，拒绝错误父PID/start/session并验证父死亡诊断；
不用自有job、不读父EOF，不访问UNO/Office。构建需要已有Framework targeting pack，缺失明确失败。
同样仅复制到测试输出，绝不进入生产发布包。

## LibreOffice真实加载拒绝（Physical）

`LibreOfficeLoadRejectionPhysicalTests`通过公开媒体登记/Converter验证自有加密ODP无密码终止、正常引擎退出和原件保护，另有旧stage输出仍uncertain负例。默认非Physical不执行。
显式配置`SCP_CV_TEST_ENCRYPTED_ODP`为已验证的自有加密ODP路径、`SCP_CV_LIBREOFFICE_HOST`为同版已构建宿主；缺夹具明确失败，不生成或保存密码。运行 `dotnet test runtime-dotnet/tests/ScpCv.Integration.Tests/ScpCv.Integration.Tests.csproj -c Release --filter FullyQualifiedName~LibreOfficeLoadRejectionPhysicalTests`。
用独立新SQLite/profile/job，不触用户默认Office、数据库或D4；已有输出/强清不记正常。边界与红绿见008的load-rejection验证记录。

## 视频墙回环实测（`VideoWallLoopbackTests`）

视频墙节点是 `192.168.5.101~150:4830`，开发机不在这个网段，所以默认只做包级与替身时序测试。
要看传输层的真实行为（2 秒连接超时、重试退避、失败即中止、取消原样抛出），用管理员 PowerShell
把这些地址挂成本机别名，测试里的假节点就会在真实端口上收包：

```powershell
runtime-dotnet/scripts/videowall-loopback.ps1 -Apply     # 或 -Status / -Clear；别名 store=active，重启即消失
dotnet test runtime-dotnet/ScpCv.sln -c Debug --filter "Requires=VideoWallLoopback"
```

别名没挂时这 5 个用例按各自前置条件**跳过**（跳过原因里写着上面的准备命令），不会让常规测试变红。
它们的边界——能证明什么、不能证明什么（帧内容、TCP_NODELAY、真实网段行为都测不到）——见
`specs/004-video-wall-control/verification.md` 与 `docs/known-pitfalls.md` 坑 9。
