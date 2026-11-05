# 调查决策

- summary 使用 flex 后原生标记不显示；使用独立装饰箭头并保持原生 details 键盘语义。
- 不以窗口宽度判断屏幕采集能力；入口保持可见，现有 availability 决定能否开始共享。
- store 已持有共享寿命，折叠操作不得改变组件身份或重新采集。
- D4 的共享登记是短期内存状态；终态会清理，需结合持久命令和用户重试诊断。
- 本机历史真实测试使用独立 Publisher；本轮若改动 store，需要测试实际 store 所有者。

## 跨机故障复现

2026-11-05，本机真实视频 captureStream 经生产 Publisher、正式 D4 API 和原生接收播放器测试：预约 201、offer 202；约 2.75 秒收到并实际应用 answer，原生 signalingState 为 stable。ICE 检查约 15 秒后失败，实际接收始终为零帧。心跳请求正常返回 200。这轮证据排除了协商总预算不足作为该次故障原因，后续检查 host 候选和实际网络路径。

只读防火墙核对：D4 两个私有接收浏览器已有 TCP/UDP 入站 Allow，覆盖当前 Private/Public 网卡；不能以 Worker 的阻止规则直接认定接收媒体被阻止。

## 地址发现验证

双方原始协商仅含 mDNS host 候选。使用官方 Pion STUN-only 服务及原生浏览器的标准 ICE 配置后，发布端产生真实 `srflx 10.88.10.2` 候选，ICE 进入 connected，正式 D4 播放器报告 72 个实际视频帧。HTTP、原生 Peer 和接收资源均为实际实现；测试视频通过原生 captureStream 取得，没有自动操作系统屏幕选择器。

采用独立、低流量的 STUN 服务，不转发共享媒体。`ScreenSharing:LocalStunPort` 启用时，发布端采用其实际访问主机名，私有接收端采用本机地址；现场和组网不混用不可达网卡。显式 `IceServers` 仍支持标准外部 STUN/TURN 配置。服务使用官方 Pion `v5.1.3`，依赖和许可证独立保存。
