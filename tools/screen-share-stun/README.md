# 屏幕共享 STUN 服务

本工具使用官方 Pion TURN `v5.1.3` 的 STUN-only 配置，为浏览器提供真实的
server-reflexive（`srflx`）候选。共享视频保持 WebRTC 点对点传输。

监听参数为 IPv4 字面地址与端口，默认 `127.0.0.1:3478`：

```powershell
scp-cv-screen-share-stun.exe -listen 10.88.50.194:3478
```

服务持续运行，收到 Interrupt 或 SIGTERM 后正常释放 UDP 监听。启动错误返回非零退出码。
配置中没有 TURN RelayAddressGenerator、认证处理或媒体中继。

构建和验证：

```powershell
go mod download
go test ./...
go build -trimpath -o scp-cv-screen-share-stun.exe .
```

浏览器双方使用同一个可达地址：

```javascript
{ iceServers: [{ urls: 'stun:10.88.50.194:3478' }] }
```

部署时仅放行指定地址的 UDP 监听端口；可达性需要用浏览器实际生成的候选和接收帧验证。

代码遵循仓库 Artistic License 2.0。Pion 依赖采用 MIT，许可证保存在 `PION-LICENSE.txt`。

官方参考：

- https://github.com/pion/turn/tree/v5.1.3/examples/stun-only-server
- https://github.com/pion/turn/releases/tag/v5.1.3
