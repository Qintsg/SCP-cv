# Implementation Plan: 输入路由与共享低延迟预览

**Branch**: `main` | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

## 追加部署阶段（2026-10-21用户授权）

沿用现有D4 RemoteSigned和admin交互会话，先确认干净仓库／实际进程／媒体与音量配置；推送两个既有远端，再由D4既有内网origin正常pull --ff-only。新运行包及网页先独立构建并校验全清单，协作停机确认后按原Host／Node身份精确停止，冷备control.db及WAL/SHM、完整旧网页及任务XML。不覆盖旧包、媒体、证书或私有声音引擎。

新建本轮任务／启动脚本，更新既有桌面一键入口的固定路径和摘要；只启动软件，恢复原显示器绑定和配置，不打开历史源或调用新实体控制。新服务需真实双播放器在线、状态文件匹配、HTTPS全文件摘要与只读API通过；重启原软件可能短暂中断现有播放，部署前如有在途动作或新活动状态先重新评估，不盲目覆盖。最终记录软件制品commit、两仓／D4源码commit、冷备与未验实体边界。

部署链路补充：D4启动配置精确添加app://scp-cv及HTTPS跨站Cookie，不用通配CORS。Vite dev/preview关闭自身默认CORS中间件，让/api的预检转发至ControlHost；不允许代理层提前返回缺Allow-Credentials的204。以实际两层OPTIONS和回环红绿回归核验，不通过关闭浏览器webSecurity或TLS绕过。

## Summary

以019已确认A为输入组件，正式Dashboard仍保留音量/电源右栏，电视源置于电源下；不搬入原型媒体目录卡。统一31路受信输入，工作站目录受保护且只列服务可达项，窗口3/4仅来源与预览。Web应用级单传输/单decoder+多canvas，Electron改走主进程专用app协议与随包媒体helper。电视独立矩阵帧，同操作门禁内持久记账并对预案判重。

## Technical Context

**Language/Version**: C# net10.0、Vue3/TypeScript、Electron44.2.0、Node24。
**Primary Dependencies**: 既有MediaMTX/mpegts.js1.8.2/Ant/Vite，客户端媒体组件FFmpeg与自包含Windows helper；不引入Python运行服务。
**Storage**: 既有SQLite/EF、预案YAML；新增每输出逻辑下发记录。预览租约/decoder只在内存。
**Testing**: 默认非Physical、组件/协议/伪时钟；另显式合成回环与实际Electron/Chromium，无computer use/输入模拟。
**Target Platform**: Windows x64 Electron直连，普通Web/Android仍服务端预览；原生播放器仍两窗。
**Project Type**: 既有分层控制台/运行时的增量实现。
**Performance Goals**: 65同源订阅1传输1decoder，几何不重连；冷/热首帧至少5轮p50/p95对照。
**Constraints**: 含凭据URI不进renderer或日志，正常TLS、沙箱和认证寿命不放宽；无真实设备控制测试/不部署D4。
**Scale/Scope**: 输入31路、墙1+电视2逻辑目标、客户端短期预览池。

## Constitution Check

- 安全：统一受信目录，不接受前端原始帧/任意spawn；模拟装配不探测现场。预案失败/未知不跳过，不把写出当实体反馈。
- 追溯：019已保存为19356ee，A及用户修正进入020，研究已按skill分派三名只读agent，技术未知已解决。
- 分层：真实媒体、原构造纯CPU黄金、回环和实体边界分别报告；不以假decoder代验首帧。
- 合同：业务唯一写库仍ControlHost，Native只经认证固定描述符/IPC；YAML与API同步。
- 最小复杂度：一个输入目录、一套共享hub，Native是transport adapter；不复制网页/播放器双栈。新增文件小于500行，既有超长文件先拆分。

设计前后通过。已核docs/known-pitfalls.md §1；新增路径及失误沉淀到相关子文档。

## Research Decisions

详见research.md。TV为22字节610D，不是50屏映射；6231满屏+610D是明确的项目单路组合，同一连接按序写。原工程对51–74、76、81、84–88声明3840×2160；目录采用此工程声明，但81沿用此前实测1080。工程声明不是SPS实时确认，显式配置优先，不自动改系统分辨率。未知源/协议选择拒发。

Web先enableStashBuffer:false与0.5秒输出GOP，不改完整GOP广播语义。锁定mpegts1.8.2源码实际默认64KiB，在线文档384KB不是本机版本事实。几何不自动变高清/低清通道，实质不同URI不合并。

## Shared Contracts / Ownership

根agent拥有SiteInputCatalog/发现与工作站目录、输入API、正式A组件/仪表盘/摄像机、Program注册/OpenAPI总入口、最终集成。
预览agent拥有frontend/features/preview及后端StreamPreview/其端点与测试，不改platform/apiTypes/package.json。
Native agent拥有electron/平台桥、新ClientPreviewHost/构建脚本及相应测试，不改package.json，由根合并资源命令。
TV agent拥有新DisplayRouting、场景C#/YAML/RuntimeState迁移、矩阵/去重测试及独立API/schema文件，后续接T011预案前端apiTypes/clone/capture/编辑与DOM回归；不改Program、Dashboard或OpenAPI总入口。

SiteInputCatalog位于Infrastructure.Devices，静态All/ByAddress和ResolveDescriptor(string kind,string? ip,VideoWallProtocolOptions)返回VideoWallInputDescriptor?；Root实现该基础后其它后端任务使用。

Web POST /api/stream-previews/新增sharing_key：随机私钥HMAC(完整Owner+实际URI)，不是URI/凭据SHA裸hash；别名请求在key返回后原子合并，额外租约不启动GET。
Native POST /api/stream-previews/native/resolve/只供Main客户端使用，返回uri,sharing_key,source_id,source_revision,expires_at,quality，沿用Cookie/CSRF与no-store；URI不传renderer。
platform/previewTypes.ts约定NativePreviewRequest为source_id或input与quality；NativePreviewLease为id/sharingKey/streamUrl/expiresAt(ISO字符串)，adapter.preview的acquire/heartbeat/release/invalidate接口供hub选择；Web不调用IPC。

GET /api/inputs/返回受限目录（id/name/group/ip_suffix/preview_input/source_id?/availability/checked_at）；POST /api/inputs/refresh/仅有界端口探测，不拉媒体、缓存合并。
GET /api/display-routing/返回各目标last_sent逻辑状态；PUT /api/display-routing/televisions/{target}/输入{input,force}，目标仅tv_left/tv_right。
advanced.televisions为{tv_left:{state:'keep'|'set',input?},tv_right:...}；缺省keep。状态不是硬件实时读回。

## Project Structure

- Infrastructure/Devices/SiteInputCatalog及WorkstationCatalog/InputDiscovery，ControlHost/Endpoints/InputCatalogEndpoints。
- Infrastructure/DisplayRouting、Domain RuntimeState/ScenarioAdvanced与EF迁移；ControlHost/Endpoints/DisplayRoutingEndpoints。
- frontend/features/runtime/inputs、features/preview、features/dashboard、features/devices、features/scenarios。
- frontend/electron/preview、src/platform/previewTypes.ts，runtime-dotnet/src/ScpCv.ClientPreviewHost。
- specs/020-input-routing-preview/{spec,plan,tasks,research,data-model,quickstart,verification}.md及contracts。
- docs OpenAPI、使用/维护、第三方声明与构建清单。

## Runtime Design

共享池键不含坐标/标签/布局码，由独立应用认证scope拥有，不能借首个组件的页面寿命。单video真正帧回调广播到有界DPR canvas；最后引用约1秒宽限吸收DOM替换，隐藏/认证失效立即清需求。新订阅复用最近有效帧，旧账号帧不得跨scope保留。

Native可信顶层IPC固定描述符，无任意URL/路径/参数；app://scp-cv/__preview/<256bit nonce>/stream.flv仅活跃所属租约可读。主机/完整Cookie票据/来源修订/所有者寿命围栏，异步到达再次核查。helper stdin私有首配置+心跳，EOF/超时≤5秒取消并关闭自有Job；FFmpeg先挂Job再运行，不继承helper stdin。H264优先copy，不兼容只在确认旧进程清理后兼容重开，无声。发布禁止PATH回退，extraResources与来源/许可清单完整。

每个目标记录fingerprint/输入/状态/时间；门禁内重读后预案判等，dispatching/uncertain/unknown无效，几何与源参数纳入fingerprint。手动force不去重；电源/配置等已知失效应撤销可信记录。外部写完再记录，持久化失败报告不确定不伪装回滚，原媒体与音量流程不因跳过输出而省略。

电源按钮使用请求进行中的非数值进度（无伪造硬件百分比），实际完成才结束；保持归属、防重及危险确认。只有未来具备可靠服务端分阶段事件时才显示数值。

## Validation and Limits

所有实体入口只在模拟/假传输/独立回环验证，不连接D4。默认全量回归与显式合成媒体/实际Electron分开。若平台执行策略阻止原生探针，不绕过，保留可运行产物和未验状态；无法据此关闭Native就绪门槛。公开分发前需匹配媒体组件源码/声明，非商业不豁免，不在本轮自动发布安装器。
