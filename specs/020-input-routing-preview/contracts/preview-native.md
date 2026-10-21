# 预览合同增量

Web POST /api/stream-previews/保留原body与租约，201新增sharing_key；server随机私钥HMAC绑定Owner和实际URI，非裸URI哈希。客户端在键返回后单飞合并，额外租约不能启动GET。

最终共享模型：每个不同target一个非消费验证lease，每个实际流另有一个消费lease；65同target为2lease，不是65个上游。heartbeat可选aliases最多64个同Owner／Origin／URI的仍有效验证lease；独立别名修订不误停其它仍有效别名，不用已修改或被删来源无限续旧URI。

POST /api/stream-previews/native/resolve/：原PreviewRequest；沿Cookie/CSRF/Origin，no-store，返回uri/sharing_key/source_id/source_revision/expires_at/quality。主进程从已提交主机取得，不把此响应交renderer；GET不创建任意代理。

NativePreviewRequest与Web描述一致；NativePreviewLease为{id,sharingKey,streamUrl,expiresAt:ISO}。adapter.preview提供acquire/heartbeat/release/invalidate，可信顶层所有者校验，rawURL/路径/命令行参数不可由renderer提供。app://scp-cv/__preview/<nonce>/stream.flv只读活跃本人租约，协议失败不回显URI。

原生回收先确认旧helper/FFmpeg退出，再允许同键兼容重开。无原生能力不伪装直连，普通Web不调用IPC。
