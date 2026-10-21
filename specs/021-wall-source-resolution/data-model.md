# 数据模型

无数据库迁移。

- SiteInputDefinition：固定身份/地址；Width/Height为本轮匹配611A的默认值，0/0表示未知。
- VideoWallInputDescriptor：受信完整流描述；同UnicastAddress的组播、端口、宽高及单播端口必须一致。
- ResolutionObservation：suffix、status、group、port、width、height、reply_hex、时间，不含凭据或画面。
- WallFingerprint/TelevisionFingerprint：继续按完整帧计算，尺寸变化失配，名称变化不影响。
