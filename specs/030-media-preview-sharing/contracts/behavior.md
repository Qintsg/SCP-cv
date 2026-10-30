# 接口与交互合同

- `POST /api/playback/{windowId}/control/` 增加`action: refresh`；仅Web有效，其它类型400；200及submitted_command_id沿用现有合同，受理后查询命令终态。
- `GET /api/sources/{id}/preview/` 支持image、video、audio和PDF，保留认证、Range及路径检查。
- 文稿继续使用`ppt-resources/`、`slides/{page}/`和明确的`prepare/`；预览不调用播放器open。
- 流预览提交source_id，复用租约和解码，不展示凭据。
- 点击名称打开详情，关闭卸载内容。网页遵守嵌入限制，提供新标签入口。
- 共享离开控制页面持续；其它页面提供紧凑停止入口。
