# 工作区合同

- VideoWallMapping追加`region: grid`及`rect:{column,row,columns,rows}`；旧三种区域兼容。
- 输入追加`window_3/window_4`，只可保存/预览，can_apply=false。
- PUT /api/video-wall/layout/与POST /apply/沿用，客户端不暴露额外保存步骤；解码不调用任何写API。
- 预案请求/响应的advanced新增可选wall_layout_code；复用既有AdvancedJson，无需数据库码表或列迁移。高级布局使用同一解码/验证，激活不能绕过墙面预检。
- 本机预览桥：loopback绑定、允许Origin、随机启动token；创建请求body传RTSP/RTMP地址，返回不含凭据sessionID；授权GET帧与DELETE释放；CORS不使用任意通配，错误不得回显输入地址。
- 浏览器只向本机桥请求流，不向Host新增预览代理；无配置不是错误，失败单行展示。
- 预案YAML与SQLite状态保持明确失败，不静默双写不一致；若无法落盘不得返回保存成功。
