# Implementation Plan: 按需共享的免安装直播预览

**Branch**: `main` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)

## Summary

ControlHost新增经过认证／CSRF的短租约与同源FLV出口。每个规范化上游URI只注册一条现有MediaMTX动态sourceOnDemand路径，一份自有FFmpeg从本机RTSP读入，转H.264无声FLV。Vue使用mpegts.js，按可见性订阅／续租，按完整GOP共享缓存、扇出与追赶。替代016用户本机桥；不另启MediaMTX，不改变D4部署。

## Technical Context

**Language/Version**: C# net10.0、Vue3／TypeScript、Node24构建。
**Primary Dependencies**: 现有MediaMTX v1.17.1，服务端FFmpeg；浏览器增加Apache-2.0的mpegts.js精确版本，pnpm镜像安装。
**Storage**: 预览只在内存，媒体索引仅只读；凭据不发客户端。
**Testing**: xUnit默认假生产者／假API；单独合成回环真实FFmpeg与MediaMTX，Node／HappyDOM及只读浏览器。
**Target Platform**: Windows x64播放Host；共享Web／Electron／Android控制台（实际解码仅验证支持MSE的本机浏览器）。
**Project Type**: ASP.NET Core服务内薄预览模块＋共享Vue组件。
**Performance Goals**: 默认8Mbps预览总出站、最多6上游、128租约、每客户端2个完整GOP；实际按人数和拥塞降档，默认无音频。
**Constraints**: 原始入站码率不能靠输出转码降低；严格全机QoS需额外系统／网络支持；仅本机，无computer use，不连接现场。
**Scale/Scope**: 认证端点、私有MediaMTX API、独占转换子进程、有界FLV共享、Vue生命周期、维护文档。

## Constitution Check

- 安全：默认Simulation不启动真实进程；只有明确租约启动，所有路径／PID归属本实例；无任意URL入口。
- 追溯：018规范／设计／任务／验证同步，017已独立本地提交。
- 分层：默认合同测试不证明真实解码，合成RTMP＋两种出口另验；不冒称现场通过。
- 边界：ControlHost唯一业务写入者；预览不写数据库或操纵播放器，沿用认证与协议会话隔离。
- 最小复杂度：一个服务模块，不保留本机桥双栈，单文件小于500行。

设计前后均通过。参考已读取docs/known-pitfalls.md §1 MediaMTX／进程组路径；新增路径在子文档追加，不挤超500行入口文档。

## Project Structure

### Documentation (this feature)

```text
specs/018-shared-flv-preview/
  spec.md plan.md research.md data-model.md quickstart.md tasks.md verification.md
  contracts/preview.md checklists/requirements.md
```

### Source Code (repository root)

```text
runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/
  PreviewOptions.cs PreviewSourceResolver.cs MediaMtxPreviewClient.cs
  FlvReader.cs FlvGopBuffer.cs PreviewBandwidth.cs PreviewProcess.cs
  SharedPreviewFeed.cs StreamPreviewRegistry.cs StreamPreviewRegistration.cs
runtime-dotnet/src/ScpCv.ControlHost/Endpoints/StreamPreviewEndpoints.cs
runtime-dotnet/tests/ScpCv.ControlHost.Tests/StreamPreview*.cs
frontend/src/features/preview/
  ClientStreamPreview.vue previewClient.ts previewPlayer.ts previewTarget.ts
frontend/src/features/settings/tabs/ClientPreviewSettings.vue
frontend/config/dualProtocol.ts
docs/openapi.yaml
```

**Structure Decision**: 技术适配与短期注册位于ControlHost的独立功能目录，源数据从既有DbContext读取，不新增业务持久化。

## Runtime Design

- POST请求仅含source_id或墙面input及quality，不接受URL。租约绑定用户＋入口协议＋加密会话票据摘要，避免同账号重登复用旧租约；不能只用可能未轮换的CSRF。
- 创建只登记需求，实际FLV消费连接才启动；单租约仅一消费连接。续租经CSRF每10秒一次，30秒到期；最后读者宽限≤3秒。
- 上游键含完整规范化URI（含凭据，仅服务端摘要）；固定墙面流优先RTMP1935/live/chn1，小窗／高清为不同实际通道；自定义地址受现场网段和输入规则限制，预留3/4返回无流。
- 动态路径命名含随机Host实例前缀，只增删已拥有路径；sourceOnDemand=true，读者消失即停止设备拉流；不改all_others或writeQueueSize。
- FFmpeg后台低CPU优先级、单转码线程、无声音；Windows独占Job确保Host死亡回收；拒绝无依赖，不下载／启动第二个MediaMTX。
- 解析FLV文件头和完整tag，校验长度／类型／尾长度；缓存metadata、AVC sequence header、完整关键帧起始GOP。每个GOP带初始化tag，订阅队列仅丢完整GOP，不丢任意字节。
- 编码降档可能重启精确自有转换器；输出时间戳按单调代次重映射，不关闭HTTP／WS。新编码以sequence header＋关键帧接入；不能承诺任何编码参数都无缝。
- 出站全局令牌桶，16KiB公平分块，独立慢客户端有界队列。预算按有效消费者分摊；拥塞／正式直播活跃使用保守档，降档快、恢复慢。
- NIC统计为尽力检测而非全机绝对优先级：高利用率压低预览预算；未知链路不假报QoS。输入高码率仍可能占链路，默认选chn1。
- HTTP／WS沿用Cookie认证，POST／续租／删除验证CSRF；WS额外精确Origin并绑定建立时Origin，GET不能创建任意流。长连接仅在有效续租期间生存，退出最多30秒回收。
- WS代理显式ws:true，HTTPS校验证书不放宽；不把令牌／摄像机密码放URL或异常。
- 页面隐藏／离屏／换主机／退出销毁播放器并取消租约；无流普通空态，失败卡内小字，保留最后画面时标明非实时。

## Complexity Tracking

没有新增数据库或常驻网关进程；FFmpeg仅预览需求存活。系统级QoS不自动改策略，作为部署后的可选维护措施，不能据此声称本轮已设置。

