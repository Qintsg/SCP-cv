# 数据与生命周期

- PresentationSettings：固定默认LibreOffice、依赖路径/版本/就绪、经验证capabilities、显式静态回退模式。
- EngineIdentity：windowId或converter角色、group/instance、engineEpoch、profile、PID/start/session/endpoint；生命周期starting/ready/closing/stopped/faulted。
- DocumentIdentity：原件sourceId/digest/revision/generation、documentEpoch、真实页码和media观测、cleanupPending；关闭证明前保留占用。
- PresentationOperation：operationId、parentCommand/claim、所有代次、deadline、参数/结果指纹；accepted≠succeeded，超时uncertain且禁止无证据重放。
- PreparedArtifact：同源摘要/修订关联PNG/PDF/页数/notes，准备状态与动态原件可用性分开。

ControlHost唯一业务库写入者；Worker只发fenced观测。旧PowerPoint审计值仅可读，不可用于新启动/播放。
