# 020数据模型

- SiteInputDefinition：31个受信地址、类别、名称/别名、已确认组播/源参数、预览规范身份。24工作站仅51–74，75不存在；63–66为D4四路，76/81/84–88按用户表。
- InputReachability：仅服务端口可达、检查时间、有限TTL；不等于出画。Simulation无网络探测。
- WorkstationMedia：内置受保护目录/源，仅由服务同步，隐藏不可达项但不删除已被预案/会话引用的ID。
- DisplayRoutingState：每目标逻辑输入/布局、fingerprint、dispatching/sent_unconfirmed/simulated/uncertain/unknown、更新时间。进行中先失效旧成功，完整发完才记录。
- ScenarioAdvanced.televisions：tv_left/tv_right各keep或set+输入；旧数据缺省keep，YAML/索引规范化一致。
- PreviewPoolEntry：应用主机/认证scope、opaque实际流键、单video/decoder/transport、引用集、有界最近帧、重试/续租/空闲时限。几何不入键。
- PreviewTargetLease：每个不同来源身份一验证租约，不消费媒体；合并后的实际流另有消费租约，最多64别名参加同归属续期。
- NativePreviewLease：创建者webContents/top-frame、主机代次/Cookie摘要/源修订、随机nonce、期限、唯一消费与独占helper/FFmpeg身份。

源密钥和摄像机凭据不入公开UI/日志；native描述符敏感URI只在认证Main和私有helper内存。失败不自动重放已发生物理动作。
