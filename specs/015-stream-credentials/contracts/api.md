# 接口合同

沿用OpenAPI3.0，无新字段：POST `/api/sources/streams/`的url和PATCH `/api/sources/{id}/`的uri支持已允许RTSP/HTTP(S)/RTMP的userinfo。完整URI仍返回已认证操作员；匿名、CSRF、固定源保护不放宽。

无效输入400，错误不回显原URL。SRT/UDP/RTP非空userinfo仍拒绝。保存metadata.stream_status=unverified，不探测设备；更新增加SourceRevision，仅下次OPEN使用。

前端地址默认密码型输入；脱敏值只展示，不代入更新/打开合同。
