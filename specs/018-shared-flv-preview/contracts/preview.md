# 018 REST／流合同

所有端点在`/api/stream-previews`，沿用有效Cookie会话，所有写入要求CSRF。

- `POST /`：body为`{source_id,quality}`或`{input:{kind,ip_address},quality}`，字段互斥。201返回随机id、相对HTTP／WS路径及租约到期时间，不返回实际设备地址。登记不等于首帧成功。
- `POST /{id}/heartbeat/`：本人同登录代次续租，200返回到期时间；无效租约404／410，不重建流。
- `DELETE /{id}/`：关闭本人租约，204；不能关闭他人。
- `GET /{id}/stream.flv`：先验本人／Origin／未过期／单消费者，再开始流；`video/x-flv`、no-store、不缓存。HTTP取消释放消费者；创建后未消费不会拉流。
- `GET /{id}/stream.ws`：同门禁，额外要求WebSocket升级与精确Origin。binary承载FLV文件头＋完整tag；无业务控制文本消息。
- `GET /status/`：只读依赖与预算摘要，无真实源地址、路径凭据或外部探测。

错误JSON包含固定detail／code，依赖不可用503、容量429、非法输入400／无流422、无认证401、Owner／Origin不符403／404。流开始后的故障不能改写HTTP200为成功证据，关闭连接且前端内联标明重连／最后画面。

WebSocket不携带额外URL令牌；Cookie与租约绑定。OpenAPI3.0仅描述升级101与二进制流，不将它们称JSON。
