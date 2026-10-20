# 研究结论

- 原验证器以UserInfo.Length统一拒绝，创建/编辑共用，播放器已把完整URI交给LibVLC；修改应在验证器，不另造凭据字段或拆改播放器。
- 允许RTSP及已有自定义HTTP/HTTPS/RTMP内嵌凭据；UDP/RTP、SRT不引入新userinfo语义，保留UDP空userinfo组播和SRT查询。
- 自动名称原来截取完整URI，放行后会泄漏；先脱敏再截取。LogRedaction原只处理HTTP(S)，同步覆盖直播协议与passphrase等敏感查询键。
- 前端使用原URI进行保存/编辑；普通播放摘要使用独立纯展示函数。地址输入用真实Ant密码输入，默认隐藏、显式显示；不是更改媒体DTO或加密存储。
- 回归使用虚构凭据与回环，不连接用户现场地址。无需新增依赖或外部研究代理。
