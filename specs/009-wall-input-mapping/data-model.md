# Data Model: 大屏映射

- 复用WallInput/WallMapping/WallLayout。ip_stream缺少服务器配置描述时仍可保存但不可应用。
- VideoWallProtocolOptions：SendType(1..3)、CutSync、IGMPv3、UseNewProtocol、DesKeyHex、PreserveLegacyPresets、Inputs。输入包含UnicastAddress、MulticastAddress、Port、Width、Height、UnicastPort。
- VideoWallPlan：布局、阶段及发送项；只支持均匀10×5墙。
- RuntimeState新增WallLayoutAppliedJson、WallLayoutAppliedRevision、WallLayoutDispatchStatus、WallLayoutDispatchDetail；空JSON为历史未记录。
- 状态none→dispatching→simulated/sent_unconfirmed；中途失败/取消或提交异常→uncertain。失败保留上次发送意图，不证明墙面未变化。
- 非旧预设BigScreenMode=Custom；旧模式入口和预案必须同步重置自定义投影。
- VideoWallWindowReply：节点连接身份、输入及矩形，不携带操作revision，不能自动标确认。
