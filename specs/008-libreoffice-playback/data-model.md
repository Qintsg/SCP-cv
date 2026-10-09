# 数据与生命周期

- PresentationSettings：固定默认LibreOffice、依赖路径/版本/就绪、经验证capabilities、显式静态回退模式。
- EngineIdentity：windowId或converter角色、group/instance、engineEpoch、profile、PID/start/session/endpoint；生命周期starting/ready/closing/stopped/faulted。
- DocumentIdentity：原件sourceId/digest/revision/generation、documentEpoch、打开时的fit_text布局选择、真实页码和media观测、cleanupPending；关闭证明前保留占用。布局仅改内存，不保存原件。
- PresentationOperation：protocol_version=3、operationId、parentCommand/claim、所有代次、deadline、参数/结果指纹；open/export_slides包含默认false的fit_text，true不得附着到其它动作。accepted≠succeeded，超时uncertain且禁止无证据重放；noop/presentation_at_last_slide只用于真实前向终端边界，保持当前画面身份，不冒充播放或关闭。
- PreparedArtifact：同源摘要/修订关联PNG/PDF/页数/notes及text_layout（original/fit），准备状态与动态原件可用性分开。原排版使用slide_images/static_pdf，适配使用fitted_slide_images/fitted_static_pdf，不覆盖默认制品。
- CommandReceipt：复用既有CommandRecord.CommandId，仅本次HTTP入队响应携带；查询原持久记录，不新增实体/迁移。客户端内存操作围栏只防旧反馈，不构成运行时租约或新持久命令。

请求/结果规范SHA分别使用`SCP-CV.Presentation.Request/v3`/`SCP-CV.Presentation.Response/v3`。
请求规范序列在readOnly/macros/links之后写fitText布尔位；它改变v1规范字节，即使默认false也不兼容。
旧版和未知版本不能授予执行或采用结果；历史v1/v2指纹仅保留审计，不重新计算为v3或据此重放操作。v2无新末页保护语义，必须与调用方同版更新宿主，不能只更新一边。

ControlHost唯一业务库写入者；Worker只发fenced观测。旧PowerPoint审计值仅可读，不可用于新启动/播放。

profile是引擎临时状态，不是上传媒体目录：`TMP/SCP-cv/presentations/<EngineInstanceId:N>/`，
播放与转换采用同一短路径生成器，不能按source/job复用旧profile。目录与退出日志保留用于开发诊断；
清理前仍需精确实例退出证明，不按Office名称或整个TMP目录删除。
