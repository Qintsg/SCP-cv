# Contract: 屏幕共享

最终合同同步docs/openapi.yaml（OpenAPI3.0）；本文件为设计，不代表端点已存在。

## 发布者（登录及CSRF）

- POST `/api/screen-shares/`：window_id=1/2、has_audio实际轨道；返回id/状态/目标，无receiver token；仅预约不OPEN。
- POST `/api/screen-shares/{id}/offer/`：type=offer、有界SDP，提交动态OPEN；受理不是出画。
- GET `/api/screen-shares/{id}/`：本人状态、answer/未准备及实际接收信息；no-store。
- POST `/api/screen-shares/{id}/heartbeat/`：续本人寿命，不授予实际帧。
- DELETE `/api/screen-shares/{id}/`：撤销及条件CLOSE，重复幂等；受理/实际释放分别表示。

401未登录；400输入/CSRF；404非本人或不存在；409占用/协商冲突；413超大信令；503播放器/运行组离线。取消、过期、重复、迟到均须测试。

## 本机接收者

- GET `/api/screen-share/receiver/{id}/?token=...`：loopback且有效cap，no-store/no-referrer。
- 读取offer、提交answer和结束只限同cap、身份、正确阶段；拒绝外部Origin，有界body。localhost代理不代替cap；普通用户cookie不授予接收者权利。
- HTML/JS为本仓库资源，无用户脚本拼接或外链；WPF禁止其他导航、新窗和采集权限，WebMessage核来源/schema/资源身份。

## Player

持久OPEN：source_type=screen_share、source_id=0、share_id；不含token。已认证匹配Worker领取时内存注入receiver_uri。SourceGeneration事务分配；真实视频帧前loading，激活后也需观测。CLOSE/换源/退出Dispose，不保温。未实现暂停/定位/页码/循环明确禁用；音量/静音桥只在实际接通时呈现。

## UI

紧凑目标选择/共享/停止，原生采集在点击调用，选择器期间不排播放；明确不支持、离线、正在连接、实际接收、停止待确认。共享临时，不存媒体库/场景，不重启恢复。
