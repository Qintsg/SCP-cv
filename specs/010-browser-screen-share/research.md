# Research: 网页屏幕共享

## 1. 原生采集

**Decision**: getDisplayMedia在用户点击时直接调用，视频及用户选择的可选内容音轨，不获取麦克风或静默许可。

**Rationale**: API要求secure context及用户激活，LAN HTTP不能像localhost使用，内容音频并非始终可用。参见[MDN](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getDisplayMedia)与[W3C屏幕采集](https://w3c.github.io/mediacapture-screen-share/)。

**Alternatives considered**: JPEG帧不满足实时媒体/音频；MediaRecorder分块增加延迟与格式风险；不绕过浏览器安全flag。

## 2. 传输与接收

**Decision**: 直接WebRTC至专用WebView2资源，真实采集、激活后帧和关闭实验先于生产接入。

**Rationale**: 两端Chromium可协商VP8/H264、Opus，无跨协议转码。现有普通Web资源隐藏保温、导航即playing且音量只到VLC，不能直接复用。

**Alternatives considered**: 当前MediaMTX webrtc=false；WHIP→RTSP/VLC需编码限制/新端口并仍需首帧诊断，WHIP→WHEP仍需专用接收寿命。[官方发布](https://mediamtx.org/docs/features/publish)与[读取](https://mediamtx.org/docs/features/read)支持协议转换；真实ICE门禁失败时有依据修正，不提前改MediaMTX。

## 3. 网络与资源

**Decision**: 同局域网无外部STUN/TURN默认依赖；有限ICE与端到端20秒期限，实际跨机candidate pair和后帧为门禁。

**Rationale**: mDNS .local、跨VLAN及AP隔离可能影响候选，但RFC8445的peer-reflexive检查也可能建立连接，不能仅静态推断。HTTP成功不代表媒体可达；候选WebView遮挡可能节流，须单独核激活后帧。

## 4. 动态身份

**Decision**: source_id=0/actual null、专用screen_share与shareId/generation，不造永久源。

**Rationale**: 现有IPC/实际投影允许null，但OpenSourceAsync拒绝非正ID，普通Control查永久源，前端也依赖正ID。独立入口及所有相关分支都需补齐。RuntimeProjectionPublisher只核实例/owner/代次，不识别shareId，分享服务须自行匹配。

## 5. 安全与停止

**Decision**: cap仅内存、注入已认证匹配Worker的lease，发布者沿用cookie/CSRF；原生接收页面限定cap/来源/身份，媒体不录制。

**Rationale**: Vite等代理可能令远程请求RemoteIP变loopback，该检查不是唯一授权。持久命令日志或公开Session URI不能含cap。关闭按原子generation条件，不关闭后来的源；普通web保温不得保留采集。

**Alternatives considered**: URI内持久cap容易泄漏；把分享存入媒体库使过期资源被再次打开。均不采用。
