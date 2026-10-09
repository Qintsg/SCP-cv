# Presentation.Protocol v3

本 module 在 `net48` / `net10.0` 分别编译同源合同，不引用 UNO、WPF、业务数据库或第三方 JSON 包。
它提供请求校验/能力 gate、有界 wire、规范指纹和结果采用策略；协议库本身不实现原生播放器。

## 版本与文字适配

当前只接受 `protocol_version=3`，请求和结果 DTO 的默认值均来自 `PresentationRequestPolicy.ProtocolVersion`。
v2 新增 `fit_text`：省略或 false 表示原排版，true 只允许用于 `open` / `export_slides`，无关动作返回
`unexpected_text_layout`。选项进入独立请求快照及规范指纹；同身份下原排版的结果不能用于适配请求。

在 v1 的规范字节中加入一个 false 布尔位也会改变 SHA，因此本次不是无需升级版本的未知 JSON 字段扩展。
v3新增明确`noop/presentation_at_last_slide`边界结果，表示已匹配当前文稿、无播放动作而保持当前观测；不是failed或虚构动画播放成功。只有next_effect/next_slide可采用此结果，宿主必须另证实际终端及动画耗尽，纯合同不按current==total猜隐藏页/自定义放映末页。
请求/结果使用新的`/v3` tag；v1/v2及未知版本明确拒绝，不协商降级、重盖历史指纹或混用旧宿主。调用方与侧车须一起重新编译；历史v1/v2回环仅保留审计。实际末页保护/Player及前端反馈仍由T069实现和实测，协议通过不等于该功能已交付。

## Interface 与调用约束

1. `PresentationRequestPolicy.Validate(request, now)` 是纯校验，不授予副作用执行。
2. `PresentationExecutionGate.Execute(request, capabilities, currentScope, now, execute)` 必须取得可信宿主提供的当前授权操作 scope；
   **不能用请求自身的 scope 冒充授权**。`OPEN` 的新文稿/source generation scope 先预留，不能等同最后实际画面。
3. gate **先有限捕获独立纯值快照，再对该快照校验结构/期限/scope/能力**；`execute` 收到的就是该快照。
   此 module 不执行原生操作，不验证 OS PID/HWND/进程开始时间，也不验证业务库父租约。
   实际宿主必须在串行执行线程验证这些归属、路径根目录白名单和原件摘要。
4. 收到结果后用 `PresentationResultPolicy.Match(originalRequestSnapshot, response, latestScope, now, previousResultFingerprint)`。
   对当前 engine/document/source 已变化的晚结果，返回拒绝，不采用观测。
5. `accepted` 仅受理；`uncertain` 不可盲目重放；`failed` / `unsupported` 都不授权画面更新。
   `succeeded` 必须携带匹配的确认观测才能更新文稿状态；`noop`同样需要当前源/代次/指纹及有效页观测，仅保留画面，不授权其它操作/制品发布或关闭。
6. `CanRecordLifecycleCompletion` 只代表当前身份的生命周期回执可记录，**不是 OS 进程退出证明**。
   `shutdown` 没有 observation 时不能伪造实际画面清空；converter 只授权确认产物元数据，不授权 Display 出画。

所有 DTO 是可变普通类，不能据此宣称 immutable 或 OS proof。调用方要保存请求快照，不能在等待期间改写其值。

## 身份与参数

- `impress-1/2` 与 window 1/2 必须成对；普通文稿动作必须有非空 `parent_command_id` + `claim_token`，无 parent job。
- `converter` 的 window=0，`export_slides` 用 `parent_job_id`，无 Display claim/父窗口。
- `shutdown` 仅用 `parent_shutdown_id`（已认证 shutdown frame 的 message_id）；与 command/job/claim 互斥。
  group/owner/engine epoch 和 engine instance 仍必须有效，文稿及 source scope 必须全 0/空，父窗口为空。
- 文稿动作的 document/source generation/revision/id 都须为正、digest 恰为 64 位 SHA256 文本。
- goto/初始页码为 1 起始，范围 1..500；UNO 0 起始转换由实际 adapter 完成。
- 只读必须 true，宏与外链更新必须 false；没有编辑/保存操作。
- 只支持本机绝对Windows ppt/pps/pot、pptx/pptm/ppsx/ppsm/potx/potm、odp原件路径，拒绝UNC、相对父目录、ADS、控制字符、通配符。
  路径根目录白名单和磁盘文件可用性仍由宿主验证。
- 转换输出不得是原件或包含原件的目录；比较统一斜线，但规范指纹保留请求原始路径。
- deadline 为带显式时区的 ISO 时间，执行时必须在未来且不超过 5 分钟；具体原生动作可使用更短预算。
  取消等待/超时**不能证明原生动作未发生或已停止**。

## 能力

全部能力默认 false。当前 `media_pause` / `media_resume` / `media_seek` 在 gate 中硬返回 `unsupported_media_control`，
即使伪造对应能力为 true，也不会进入 executor。音量/静音及其余能力仅由可信宿主在实际验证后启用。
Protocol 的允许判断本身不证明原生动画、媒体、窗口或进程已经完成。

## Wire

- 单帧：4 字节 little-endian 有符号长度 + UTF-8 JSON payload。
- payload 长度范围 1..65536；长度校验先于 payload 分配/读取。
- `EncodeRequest/DecodeRequest` 与 `EncodeResponse/DecodeResponse` 使用固定 DTO 类型。
- `ReadRequestAsync/ReadResponseAsync` 支持真实短读；完整 EOF 返回 null，半 header/payload EOF 抛 `EndOfStreamException`。
- 单帧 byte-array decode 拒绝多余 bytes 或第二个 JSON 根；小型 byte scanner 仅定位 outer object/string/escape 终点，
  不解析字段或值，完整语法/深度仍由官方 reader 校验。
- DataContractJsonSerializer 最大 object graph=256；JSON reader depth=16；严格 UTF-8，mandatory 字段必须存在。
- 未知字段不成为执行参数；更改已知字段语义须升级 `protocol_version`。JSON 编解码不授权实际执行。
- stream 所有权属于调用方；write 会 flush，codec 不关闭流。有限读取/写入预算由调用方 cancellation token 施加。

## 规范 SHA256

不是对 JSON 文本 hash，而是对固定纯值序列 hash，因此两个框架无需依赖 JSON 排版完全相同。

- UTF-8 严格编码、不带 BOM；string 用 BinaryWriter 的 7-bit 编码字节长度及其 UTF-8 字节。
- int32/int64 使用 little-endian；bool 为单字节；nullable 值先写存在 bool，再写值。
- Guid 用固定小写 D 格式；时间先解析为 UTC ticks；SHA256 输入摘要/请求摘要统一十六进制大写。
- **请求顺序**：tag `SCP-CV.Presentation.Request/v3`、version、operationId、parentCommand/claim/job/shutdown、scope、deadline、
  operation/sourcePath/outputDirectory/targetSlide/position/volume/muted、readOnly/macros/links/fitText、parentWindow 存在位及 handle/pid/start/session/dpi/width/height。
- **scope 顺序**：role/window/engineInstance/group/owner/engine/document/sourceId/sourceRevision/sourceGeneration/sourceDigest。
- **结果顺序**：tag `SCP-CV.Presentation.Response/v3`、version、operationId/requestFingerprint、scope、status/completedAt/errorCode/errorDetail、
  observation 存在位及 confirmed/sourceId/currentSlide/totalSlides/documentClosed/engineExited/position/duration/artifactDigest。
  结果自身的 `result_fingerprint` 不进入计算。
- 同一 operation 的终态结果若指纹不同，matcher 返回 `result_conflict`；相同已接受指纹只幂等确认，不重复采用观测。

指纹用于内容一致性，不是 HMAC、OS 身份证明或父命令授权。实际本机连接认证/归属和持久幂等由宿主/ControlHost 负责。

## 项目引用与验证

`ScpCv.Contracts` 的进程客户端与 `ScpCv.LibreOfficeHost` 官方 UNO 侧车已引用此项目，Contracts.Tests 验证纯值合同。
这些引用说明协议已进入调用链，不代表动态媒体、多开或有界原生退出已经验收；相关实测结果以008 verification为准。
solution/项目引用由 root 统一维护。
Net48 用现有 targeting pack（`AutomaticallyUseReferenceAssemblyPackages=false`）；不隐式下载新 ReferenceAssembly 包。
Framework 条件引用 `System.Runtime.Serialization` / `System.Net.Http`；现代 Hash/Memory API 使用按 TFM 分支，wire 不变。

回归类：`ScpCv.Contracts.Tests.PresentationProtocolTests`。全部测试是内存流/纯值/外部 callback 计数，无 UNO/Office/GUI 副作用。
`VersionThreeCanonicalFingerprintsHaveStableKnownVectors`固定v3中文路径夹具的请求/结果tag与字段顺序；
原排版请求SHA为`4F7215958DD1327CCCF7DBF9F41690477AD39CEF4796D136F03550468A6A066B`，
适配请求SHA为`761BD999D1EB94AFF64B880F03A808A5A4596458CD9ADFF113F085974866F256`。
双 TFM 编译成功不等于实际跨运行时 wire 已验证；需要真实 Net48 框架客户端与 Net10 codec 对照，结果由主验证记录承接。
新版回环应覆盖打开/导出各两种布局、末页noop、未支持媒体控制、shutdown及v1/v2拒绝；不得复用旧版二进制而宣称新版已验证。
