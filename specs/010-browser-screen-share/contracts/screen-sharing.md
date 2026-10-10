# Contract: 屏幕共享

最终合同同步docs/openapi.yaml（OpenAPI3.0）；发布者/接收HTTP及正式Player专用资源已实现，前端/清理确认等仍待后续任务，不代表完整功能可使用。

## 发布者（登录及CSRF）

- POST `/api/screen-shares/`：window_id=1/2、has_audio实际轨道；返回id/状态/目标，无receiver token；仅预约不OPEN。
- POST `/api/screen-shares/{id}/offer/`：type=offer、有界SDP，提交动态OPEN；受理不是出画。
- GET `/api/screen-shares/{id}/`：本人状态、answer/未准备及实际接收信息；no-store。
- POST `/api/screen-shares/{id}/heartbeat/`：续本人寿命，不授予实际帧。
- DELETE `/api/screen-shares/{id}/`：撤销及条件CLOSE，重复幂等；受理/实际释放分别表示。

401未登录；400输入/CSRF；404非本人或不存在；409占用/协商冲突；413超大信令；503播放器/运行组离线。取消、过期、重复、迟到均须测试。

## 本机接收者

- GET `/api/screen-share/receiver/{id}/?window_id=...&source_generation=...`：loopback且Authorization Bearer有效cap，no-store/no-referrer；cap禁止放查询串。私有receiver_uri仅通过fragment给原Worker，WPF首次导航设置header；页面脚本内存读取后立即移除fragment。
- 同路径`offer/`、`status/`用GET；`answer/`、`frames/`、`end/`用POST。frames要求正递增frames、实际has_audio及activated=true，预备帧不能授予实际播出。结束受理仍需原生释放证明。
- 读取offer、提交answer和结束只限同cap、身份、正确阶段；拒绝外部Origin，有界body。localhost代理不代替cap；普通用户cookie不授予接收者权利。
- HTML/JS为本仓库资源，无用户脚本拼接或外链；WPF禁止其他导航、新窗和采集权限，WebMessage核来源/schema/资源身份。

## Player

持久OPEN：source_type=screen_share、source_id=0、share_id；不含token。已认证且原instance/owner/group均匹配的Worker领取时内存注入receiver_uri；失效授权的已领取OPEN记为Failed，不能返回无授权OPEN或卡住Processing。SourceGeneration事务分配；真实视频帧前loading，激活后也需观测。CLOSE/换源/退出Dispose，不保温。未实现暂停/定位/页码/循环明确禁用；音量/静音桥只在实际接通时呈现。

Player实际状态新增非秘密`share_id`、递增`actual_frames`及实际音轨存在`actual_audio`；共享的source_id/position_ms/duration_ms为null。仅协商ready/预备帧不能完成OPEN；过期租约及后帧任务已经完成但资源已结束仍须失败。HTTP/SSE动态身份接线留T009，不能把IPC字段当作已开放HTTP字段。当前默认静音且普通媒体控制明确拒绝，音量/静音桥留T013。

## UI

紧凑目标选择/共享/停止，原生采集在点击调用，选择器期间不排播放；明确不支持、离线、正在连接、实际接收、停止待确认。共享临时，不存媒体库/场景，不重启恢复。
