# Implementation Plan: 目录式现场控制台、清晰共享与摄像机音量

**Branch**: `main` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: `specs/012-directory-camera-console/spec.md`

## Summary

沿用.NET控制主机、SQLite与双播放器，先修复1080P裁剪和共享编码，再将媒体/预设统一为目录优先的操作台。实体控制独立成摄像机与现场音频服务，只有有证据的绑定才能下发。网页入口切到80/443，保留内部18444/18443及原证书。

## Technical Context

**Language/Version**: C#/.NET10、TypeScript/Vue3。

**Primary Dependencies**: ASP.NET Core、EF Core SQLite、VLC、WebView2、Vue Router、Pinia、naive-ui、Vite、浏览器WebRTC；pnpm管理前端依赖。

**Storage**: 既有SQLite；受管media实体目录与原件；内置摄像机固定配置；现场音频与预设高级配置由ControlHost持久化。不引入MongoDB。

**Testing**: xUnit、Node单元/组件测试、真实浏览器布局和交互、D4只读及有界实体验证；OpenAPI3.0/Redocly、Spec Kit检查。

**Target Platform**: Windows x64交互桌面D4，现代Chromium网页控制端。

**Project Type**: 分进程播放器与共享网页控制台。

**Performance Goals**: 两路1080P保留捕获尺寸；清晰文字优先30fps共享，发送码率按实际分辨率建议并以统计验证；摄像机运动有停止超时；目录操作不占用播放器线程。

**Constraints**: 两块大屏；不启动PowerPoint；不覆盖存量媒体/数据库；不猜测设备通道；无读回协议只报告发送，不假称执行；文件模块默认500行以内。

**Scale/Scope**: 50墙面节点、两个播放器、六路摄像机、三种现场音量、两个声音来源；六个用户故事。

## Constitution Check

*Phase 0与Phase 1共用门禁，设计后复核。*

| 原则 | 检查与结论 |
| --- | --- |
| I 运行安全 | PASS：路径/内置项服务端保护；运动超时停止；音频绑定未知则禁用；部分失败明确报告，不自动重放。 |
| II 规范驱动 | PASS：先spec，再研究/方案/数据合同/任务；现有012指针不提交。 |
| III 分层验证 | PASS：每故事独立测试；浏览器和实体结果分开；不能完成的实体项目不勾完成。 |
| IV 合同边界 | PASS：只有ControlHost写DB；前端仅REST；设备控制独立服务，不让网页直接UDP。 |
| V 最小复杂度 | PASS：复用MediaFolder、SQLite、双窗及设计令牌；仅预设增加必要目录/高级字段。 |

## Project Structure

```text
specs/012-directory-camera-console/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/api.md
└── tasks.md
runtime-dotnet/src/
├── ScpCv.Contracts/Http/
├── ScpCv.Infrastructure/{Media,Scenarios,VideoWall,Devices}/
└── ScpCv.ControlHost/{Endpoints,Configuration}/
frontend/src/
├── features/{sources,display,screen-share,scenarios,devices}/
├── services/
└── stores/
runtime-dotnet/tests/
frontend/scripts/
docs/
```

**Structure Decision**: 保持现有边界；目录共用轻量路径/工具栏，打开到窗口以小卡片呈现。媒体库侧栏显示目录而非类型，类型改紧凑筛选。预设高级项折叠，摄像机与音量用可用状态明确的设备卡片；沿用现有字体、色彩和间距，不引入新视觉库。

## Complexity Tracking

无宪章例外。摄像机实际RTSP/PTZ与音频通道仍在只读研究中；以研究结论补齐绑定，对未确认项不提供虚假可用入口。

## 软件输出接线补充（2026-10-12）

实机低增益窄带测试确认D4输入63对应端点`{0.0.0.00000000}.{eb7e6029-ae6b-4d15-b79a-268521bd9abd}`，64对应`{0.0.0.00000000}.{d5ff475c-6792-4d13-bc2b-2e601d097be4}`。不按同名HDMI的顺序或系统默认值选择。

先为两个PlayerWorker的LibVLC资源及AudioWorker实现显式端点绑定。可信启动环境`SCP_CV_AUDIO_OUTPUT_DEVICE_ID`提供Windows Render端点ID；未设置保留既有行为，已设置但不存在/不可用则拒绝播放，不回退系统默认设备。创建/重播前预检并设定绑定；健康同源切换和暂停恢复仅核验、不重置正在使用的输出模块。播放观测检查实际输出；不调用OS主音量/系统默认设备写入。新增模块链接给两个使用LibVLC的工程，纯策略置于Contracts，避免重复实现。

这一步仅覆盖LibVLC。WebView2共享/网页和LibreOffice必须单独验证并接线，在它们闭环前不把整个SCP-cv声音来源称为已完成，也不启用未核实的实体选源/话筒/云台。旧播放器列表的237监控地址是不可达历史候选，不能作为默认固定监控。

规划表与用户最新更正确认固定监控输入76，默认RTSP配置同步至76；音频处理器5.9接中控5.10 COM4，话筒与分布式输入接线见[绑定证据](verification/device-bindings.md)。表格只证明接线，不能据此选择Dante协议或把摄像机编码输入当云台地址。

## 触控Join控制补充（2026-10-13）

General及图片已恢复五路云台和中控三路滑杆绑定。独立`VenueJoinProtocol`、`VenueJoinDispatcher`和`VenueCameraController`承接包构造、白名单发送、每摄像机串行短租约/停止看门狗；复用现有有界设备TCP传输，不把运动状态写为已确认。ControlHost托管看门狗，停机以独立预算释放活动动作。HTTP保留认证/CSRF，新增motion_id、phase和issued_at_ms以防迟到请求重新启动运动。

原电脑音频滑杆新增computer_volume，155旧screen_volume保持兼容且仍读回。读155不能清空已记忆的其他字段；每次切墙与高级预设调用均覆盖所有已配置音频项，未配置保持原状。未知来源绑定仍在首个副作用前拒绝。实体位置保存不可在测试中覆盖现场已有位置；静态证据、回环包级证据和真实动作证据分开记录。

用户于2026-10-13确认悬挂即原头戴1/2分组。统一Join1，headset_volume为唯一保存/响应字段，hanging_volume为旧输入别名；单一或同值规范化并仅发一次，不同值在任何设备动作前拒绝，页面只保留一个话筒项。三路滑杆量程由Type7实际SQL加载与三行Lock=19确认，百分比换算floor(percent*19/100)，不以其他类相同字段偏移的255当作量程。

## 网页与LibreOffice输出方案补充（2026-10-13）

D4 Win10 18363不支持进程loopback，WebView2亦无宿主输出端点设置API。隔离副本上的Win32应用输出策略已通过两个实际WebView声音会话确认：修改宿主PID无效，需要真正发声的WebView工具进程；AUMID不隔离同一路径，独立可执行路径才隔离。LibreOffice的独立XManager暖机不创建可用声音会话，离屏自有GUI MediaShape零采样暖机可完成身份登记；两个Office实例的实际活动会话均在63。旧原生引擎退出与测试退出ACK竞态另验，不能用活动会话绿色代替退出。

生产配置采用项目专用引擎副本和明确Windows Render端点；未配置保持旧行为，已明确配置则缺副本/端点/原生确认必须拒绝，不回退默认设备。网页/共享在用户内容加载前用零采样自有媒体登记实际声音进程，配置应用输出并重新建流核对WASAPI活动端点，之后才导航用户内容或私有receiver。共享的认证、同源导航、首帧及退出门禁保持。每个窗口使用独立私有引擎路径，避免路径级策略影响另一个窗口或非项目软件；不改原安装、系统默认设备或总音量。Windows内部接口只支持已验证ABI，无法识别版本/接口时失败关闭，不写注册表或清空全局应用策略。

可信启动环境SCP_CV_PRIVATE_AUDIO_ENGINE_ROOT指向由项目初始化脚本生成标记、经签名及源/副本摘要核验的引擎目录，分别包含window-1/window-2下的webview与libreoffice。物理SCP-cv公共声音总线最终选63；VLC、网页/共享及LibreOffice分别验证后才启用来源能力，不把配置值或测试探针当生产接线。

可信启动环境`SCP_CV_WEBVIEW_USER_DATA_ROOT`可独立设置当前安装/验证运行组的本地资料根目录，按player-1/player-2与screen-share/player-1/player-2区分。未设置保留原LocalAppData/SCP-cv/WebView2路径；显式错误路径拒绝而不回退，禁止相对、UNC、根盘与重解析目录。共享仍用原InPrivate随机share profile，不把它改成普通网页缓存。此项不是用户API字段，不改变捕获许可、认证或声音门禁。

整组音频验证r39/r40发现正常停机误判：Worker与浏览器全部退出0，Supervisor因未获停机意图而退出1。采用原认证连接上的nonce准备确认，先立Supervisor围栏、最多三秒确认后才通知Worker；错误身份/nonce/缺确认不放行，非零成员退出仍失败。BeginDrain恰下一代心跳只维持等待准备，不代替准备授权。协议补充于003/runtime-ipc，不改用户HTTP合同或系统音量。

LibreOffice正式接线复用同一纯端点选择策略，Framework侧车链接纯值源文件，不将.NET10或UNO代理跨边界加载。Player在候选容器创建前选择对应窗口私有Office，侧车启动前核私有路径及唯一活动端点；converter不参与声音绑定。用户原件加载前，两份自有内存文稿依次播放有限PCM零音：第一份登记实际原生PID并绑定，第二份独立URI建流核WASAPI确实只在目标端点，均自然结束后异步结束暖机放映。r22实测用户文稿加载后关闭暖机Frame会令引擎正常退出0，因此已停止的暖机资源保留到独占shutdown统一清理，不在放映期间关闭。观测/控制检查实际会话，失配只静音自有实例并报错，不改系统默认/主音量。正常停机纳入暖机资源，只有launcher/native退出0、job空与监听关闭才接受最后UNO回复丢失；故障与缺失门禁不放宽。

2026-10-13接续：IP81另行读取实际SPS为1080P，独立默认值更新但可显式覆盖。原选源构造参数已闭环并实现编码/有界TCP单次写出，HTTP/界面门禁继续等待完整软件声音与退出。安装阶段登记方案出现新实例实际默认端点反例，不能仅靠持久应用策略；每实例有限零音方案音频通过但退出失败，生产设计仍须解决生命周期，不能把该实验作为已稳定实现。

2026-10-14接续：所有引擎已完成各自实际声音、切换/关闭及共享/PPT认证IPC完整组验证，T038实现验收完成，正式常驻激活另追踪。来源传输单独声明能力；IP81不依赖软件就绪。SCP-cv每次发送前要求启动环境完整RenderID与可信Venue:SoftwareAudioOutputDeviceId（D4已确认63）相同、活动端点唯一、两窗私有网页/Office路径及已知声音策略ABI只读检查通过；不写系统策略来查询能力，不接受HTTP就绪布尔绕过。GET新增source_ip81/source_scp_cv，界面显式应用草稿。发送顺序来源→155→头戴/手持/电脑，每个写出请求单独持久化，失败返回source/sent_fields/failed_field，不自动回滚或假称实体路由已读回。预设完整预检先于墙面/机位；首页墙面成功后重发当前配置一次，软件失效报声音阶段不确定，不重放墙面。

## T042：窗口音频保持（2026-10-14）

普通与预设OPEN传递当前volume/muted，Player严格预检字段并按窗口记忆成功配置；候选首次播放先静音，绑定确认后应用音频。健康同源更新和活动音量/静音用两秒条件确认，不用固定睡眠或setter返回假报完成；错误保持静音，失败候选不能污染旧配置。重播/自然循环在停止前捕获当前意图，重建后复核。仅准备/loading无输出时允许原生-1，但不将其保存为用户音量或称为实际声音。每窗进程隔离，共享/LibreOffice能力限制保持，D4原始WASAPI及新一小时长稳单列验收。

## T040：MediaMTX协作退出（2026-10-14）

012h完整组实测暴露控制台服务CloseMainWindow无效，Kill导致MediaMTX-1和Supervisor1。新增小接口MediaMtxConsoleLifetime：仅MediaMTX使用隐藏独立控制台创建，保留父job和环境；Registry在原生创建时记录内存证明，状态文件仍为原四字段，附着旧状态的停止CLI不获得该证明。原Supervisor收到已认证nonce准备后请求协作退出；短寿命同包助手核原PID/启动时刻/会话/精确映像和控制台成员恰为原目标+自身，再发送BREAK并确认原句柄退出0。额外成员、未知身份或无创建证明拒绝信号，不在共享Host/Worker控制台广播。助手失败/超时及强制兜底仍不被最终退出码检查当作正常。实际MediaMTX回环测试先失败-1再正常0，旧身份/额外成员独立验证；D4含真实MediaMTX的完整认证组与固定提交再发布仍需补证据，不提前勾T040。

后续实际实施：固定d67ce53发布012i并核967文件，D4隔离两轮及正式实际配置完整空闲组均正常退出0；正式组状态文件删除、API恢复及原媒体/双输出/网页/系统声音基线通过，T040完成。此前“待再发布”是设计阶段边界，当前证据见verification/20261014-mediamtx-stop.md；不扩大为空闲以外的动态媒体或实体声音完成。
