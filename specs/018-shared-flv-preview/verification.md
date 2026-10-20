# 018 验证进度

2026-10-20，仅本机，未推送／部署／连接D4；无computer use。**功能尚未完成，不可当作已可用。**

## 基础阶段 T001–T003

- 规范16/16质量检查与设计已完成；speckit-plan要求的一名只读研究agent已返回，不负责实施。
- 新增PreviewOptions／PreviewSourceResolver、FlvReader／FlvGopBuffer／FlvTimeline；尚未装配进Program、没有可调用的预览生产端点、未启动原生进程。
- FLV测试先在缺少模块时编译红灯，再实现转绿；`dotnet test runtime-dotnet/tests/ScpCv.ControlHost.Tests/ScpCv.ControlHost.Tests.csproj -c Release --no-restore -m:1 -nr:false --filter FullyQualifiedName~StreamPreview` **11/11**，0跳过，编译0警告／错误。
- 覆盖完整tag／尾长度校验、异常长度先拒绝、GOP带初始化与关键帧、重编码时间线、代次清尾及缓存上限；共享键区分密码／通道，不因输出质量／媒体别名重复；已知63／64通道、3预留拒绝、任意未登记IP／双输入拒绝、管理API回环及资源上限。
- 目前Custom IP仅允许VideoWall已配置输入或StreamPreview.AllowedWallInputAddresses显式许可项；不把任意网页输入直接变成网络代理。媒体ID只读取登记的RTSP／RTMP，内置Camera低清可按已确认chn1调整。
- 未完成：T004–T013，详见[tasks.md](tasks.md)。这些基础测试不证明MediaMTX按需启动、65并发、双出口解码、浏览器播放、拥塞或系统QoS已实现。

## 必须继续验证

实际FFmpeg FLV元数据／header与本解析器兼容、编码器调档后连续解码、同URI并发共享／最终释放、正式会话门禁／Origin、Windows Job父死亡清理、HTTP／WS与代理接线、前端可见性和认证／主机围栏、旧本机桥彻底替换、全量测试与文档。

不把CPU优先级误称网络优先级；输出转码无法降低原始入站流量。真实D4摄像机、网络拥塞、设备QoS与4K实体出画仍不在本机证据范围。
