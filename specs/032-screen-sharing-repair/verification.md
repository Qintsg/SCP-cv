# 验证记录

日期：2026-11-05。

## 本机界面

真实 Vue、Pinia、Ant Design 和 Chromium 组件页面共 6 组：390/800/1440 宽度、亮暗主题。12 张截图、原生 open 状态、箭头旋转、焦点、边框、无横向溢出均通过；展开、收起前后 Panel 与 Store 所有者身份保持，页面没有发起 API 请求或产生错误。证据：`.validation/share-032/disclosure-1793867903613/`。此检查不声明活跃采集已经跨页面验证。

## 连接诊断及真实验证

- D4 管理连接全程使用 `10.88.50.194`，没有使用 computer use。
- 直连基线：正式 API 预约201、offer202、实际原生应答成功；ICE 检查失败，实际0帧；双方仅有 mDNS host 候选。
- 标准地址发现：官方 Pion STUN-only 服务，浏览器产生实际 `srflx 10.88.10.2` 候选，ICE connected，D4 实际接收72帧，发布者状态sharing且无错误。
- 使用真实视频 captureStream、生产 Publisher/Peer、正式 D4 API 和原生接收窗口；未模拟屏幕选择器或鼠标键盘。
- 临时策略与探测服务按身份清理；正式部署及更新后验证将在此补充。

## 编译与合同

- ControlHost Release构建通过，0警告、0错误。
- ICE配置、序列化及按访问网络选择服务地址的8项测试通过，无跳过；使用实际配置绑定与Registry，结果见本轮 `ice-configuration-final.trx`。
- 前端生产构建与类型检查通过；最终结果在交付时核验。
- OpenAPI校验0错误，4个既有WebSocket101/退役410告警。
- Go真实UDP Binding、正常关闭和同端口重新绑定测试通过；依赖校验和格式检查通过。
