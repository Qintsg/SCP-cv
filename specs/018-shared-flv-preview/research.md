# 018 Phase 0 调研

按speckit-plan明确研究要求分派一名只读agent；主agent核对仓库认证、MediaMTX配置与官方文档。未修改／访问D4。

## 传输

- Decision：MediaMTX sourceOnDemand输入→本机RTSP→一份FFmpeg→ControlHost HTTP-FLV／WS-FLV。
- Rationale：本机MediaMTX v1.17.1不输出FLV；现有配置仅API／RTSP／SRT启用，RTMP listener=false不妨碍主动拉外部RTMP。
- Alternatives：WebRTC／HLS可直接复用MediaMTX但用户已明确FLV；Jessibuca开源版部分能力受限，不需另引大型播放器壳。
- Sources：[MediaMTX v1.17.1](https://github.com/bluenviron/mediamtx/blob/v1.17.1/README.md)、[Control API](https://raw.githubusercontent.com/bluenviron/mediamtx/v1.17.1/api/openapi.yaml)、[mpegts.js官方直播](https://github.com/xqq/mpegts.js/blob/master/docs/livestream.md)。

## 可复用边界

- Supervisor/Runtime/MediaMtxProcess.cs仅负责现有进程组，不应在预览失败时重启它。
- CameraCatalog已确认84–88及76；墙面输入63／64／81；3/4仍预留。
- RequireSessionAndCsrf只验证写入，故GET不可直接创建拉流；先POST短租约并验证精确Origin。
- 当前没有UseWebSockets，Vite协议代理也未ws:true，需明确接通两端。
- PresentationProcessJob可借鉴挂job再放行，但原Launch重定向NUL，不可直接用于FLV stdout。

## 连续性与资源

- Decision：完整GOP有界扇出，全局限速，动态降输出质量，后台CPU优先级；不能以DropOldest随机丢FLV字节。
- Rationale：新订阅需文件头、AVC配置及关键帧；重编码调档需要时间戳重映射。慢客户端不能阻塞共享生产者。
- Limit：输出降码率只省D4出站；入站需低清源。CPU BelowNormal不是网络QoS，全机严格优先级需要系统和交换机策略配合。
- Source：[Windows策略QoS](https://learn.microsoft.com/en-us/windows-server/networking/technologies/qos/qos-policy-top)。

本机FFmpeg 9.0.2含libx264（Scoop），MediaMTX为tools/third_party/mediamtx/mediamtx.exe。此事实不证明D4已安装FFmpeg。
