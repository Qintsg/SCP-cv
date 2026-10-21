# 原型取舍

- Decision：入口放wall功能旁。Rationale：生产Dashboard／Preview初始化鉴权和API，不能把demo接到现场。Alternative：真实/dashboard变体被拒绝，避免真实订阅，不加生产路由。
- Decision：沿用Vue／Ant／Vite，无新增技术选择；已核对WallLayoutComposer、WallGridEditor和ClientStreamPreview。没有需要外派研究的未知依赖，主agent直接完成本机原型。
- Decision：Canvas.captureStream作为明确的模拟视频，video.srcObject复用MediaStream。Rationale：能观察媒体对象寿命／时间连续，不仅显示静态“复用”字样；不冒称验证网络优化。[captureStream](https://developer.mozilla.org/en-US/docs/Web/API/HTMLCanvasElement/captureStream)、[srcObject](https://developer.mozilla.org/en-US/docs/Web/API/HTMLMediaElement/srcObject)。
- Decision：51–74共24工作站；75不存在，76监控01，81现场，84后／85中1／86中2／87左／88右。用户最新表格覆盖旧口头映射及参考图文字。
- Decision：组播沿224.1.1.末段惯例仅展示示意。正式控制接线需核对源参数和几何，本轮不发包。
