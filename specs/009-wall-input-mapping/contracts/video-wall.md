# Contract: 大屏布局

复用认证/CSRF及四个布局端点；layout响应新增applied、applied_revision、dispatch_status、dispatch_detail，active_preset=single/double/custom。can_apply由服务器计划预检决定。

POST /api/video-wall/layout/apply/支持可选expected_revision；前端提交保存响应版本。旧调用省略时应用当前快照；不匹配409 layout_revision_conflict且零发包。未支持输入409 protocol_unavailable。发送中途失败可能改变墙面。

kind=window_1/window_2对应D4的192.168.5.63/64和224.1.1.63/64；kind=laptop在选择器展示“IP81”（完整记录为“笔记本 / 输入IP81”）。绑定不可由客户端覆盖；ip_stream仅配置明确的设备流描述可应用，非任意RTSP URL。单/双屏只是前端交互形式，继续提交既有fullscreen/left/right合同，不增加12种组合专用端点。
runtime.big_screen_mode新增custom，muted_windows由未使用播放器决定；IP81不增加播放器。
HTTP success不证明出画，dispatch_status明确模拟、已发送未确认或不确定；回读仅诊断，不杜撰ACK。
发送中重启标uncertain，不重放；数据库不可写时原发送失败必须保持，不以标记失败取代原错误。旧预案仍只写single/double，手动布局捕获时big_screen_mode_state=unset，激活保持当前墙面。
OpenAPI以docs/openapi.yaml为准，Redocly验证。
