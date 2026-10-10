# Contract: 屏幕共享

最终合同同步docs/openapi.yaml（OpenAPI3.0）；发布者/接收HTTP、正式Player专用资源、网页原生采集面板、动态HTTP/SSE身份及后台释放确认已接线，音频/HTTPS/两窗稳定性及D4验收仍待后续任务，不代表完整功能可使用。

## 发布者（登录及CSRF）

- POST `/api/screen-shares/`：window_id=1/2、has_audio实际轨道；返回id/状态/目标，无receiver token；仅预约不OPEN。
- POST `/api/screen-shares/{id}/offer/`：type=offer、有界SDP，提交动态OPEN；受理不是出画。
- GET `/api/screen-shares/{id}/`：本人状态、answer/未准备及实际接收信息；no-store。
- POST `/api/screen-shares/{id}/heartbeat/`：续本人寿命，不授予实际帧。
- DELETE `/api/screen-shares/{id}/`：撤销及条件CLOSE，重复幂等；受理/实际释放分别表示。
- PATCH `/api/screen-shares/{id}/audio/`：严格仅volume整数0..100及muted布尔，登录/CSRF和本人receiving分享；事务重核原Worker/组、当前OPEN代次及已提交后帧。202返回submitted_command_id/window_id/share_id/source_generation，源代次不变；精确命令查询与实际属性回读分开，受理不代表应用成功。

401未登录；400输入/CSRF；404非本人或不存在；409占用/协商冲突；413超大信令；503播放器/运行组离线。取消、过期、重复、迟到均须测试。

## 本机接收者

- GET `/api/screen-share/receiver/{id}/?window_id=...&source_generation=...`：loopback且Authorization Bearer有效cap，no-store/no-referrer；cap禁止放查询串。私有receiver_uri仅通过fragment给原Worker，WPF首次导航设置header；页面脚本内存读取后立即移除fragment。
- 同路径`offer/`、`status/`用GET；`answer/`、`frames/`、`end/`用POST。frames要求正递增frames、实际has_audio及activated=true，预备帧不能授予实际播出。结束受理仍需原生释放证明。
- 读取offer、提交answer和结束只限同cap、身份、正确阶段；拒绝外部Origin，有界body。localhost代理不代替cap；普通用户cookie不授予接收者权利。
- HTML/JS为本仓库资源，无用户脚本拼接或外链；WPF禁止其他导航、新窗和采集权限，WebMessage核来源/schema/资源身份。

## Player

持久OPEN：source_type=screen_share、source_id=0、share_id；不含token。已认证且原instance/owner/group均匹配的Worker领取时内存注入receiver_uri；失效授权的已领取OPEN记为Failed，不能返回无授权OPEN或卡住Processing。SourceGeneration事务分配；真实视频帧前loading，激活后也需观测。CLOSE/换源/退出Dispose，不保温。未实现暂停/定位/页码/循环明确禁用；音量/静音桥只在实际接通时呈现。

Player实际状态新增非秘密`share_id`、递增`actual_frames`及实际音轨存在`actual_audio`；共享的source_id/position_ms/duration_ms为null。仅协商ready/预备帧不能完成OPEN；过期租约及后帧任务已经完成但资源已结束仍须失败。当前默认静音且普通媒体控制明确拒绝，音量/静音桥留T013。

T013已实现原生专用`SCREEN_SHARE_AUDIO`，args严格仅`share_id`字符串GUID、`window_id`数值1/2、`volume`整数0..100、`muted`布尔；lease.source_generation须等于当前资源的原OPEN代次。错误身份/类型/范围/额外字段、过期或资源结束均拒绝，不能推进源代次或调用VLC。命令从CreatedAt起三秒截止，原生控制预算也不超过deadline；真实接收属性与请求一致才返回completed。领取时再次核内存授权及原Worker，撤销/更换/缺装配则终结已领取命令，不下发控制、不注入receiver_uri。实际快照可空`actual_share_volume/actual_share_muted`，外部动作开始即失效旧值，失败保持未知。无活跃音轨时实际muted固定true；轨道存在不代表声音已播出。发布者HTTP和公共投影已接线，网页控件与真实内容声音未验证，普通SET_VOLUME/SET_MUTE仍未开放。

内部状态另有`released_screen_shares`，最多32项，每项严格仅非空GUID `share_id`和正int64 `source_generation`，不包含接收授权；只在原生控制器Dispose及Surface移除完成后产生，并随空闲/新源状态重复回报。原Worker身份检查及事务提交后才采信；当前源报告被围栏不妨碍采信其原资源释放，但不能更新新源公共状态。JS closed、CLOSE排队、offline、新Worker和取消不能代替释放证据。原Player已验证OS身份的确证退出可以释放原分享，取证失败保持stopping。

后台每秒撤销过期/换源/组失效共享并条件停止，不自动重采集或重播。重启仅通过当前OPEN审计清除孤立意图、不还原cap/SDP。预案捕获当前请求或实际共享返回400 `scenario_screen_share_not_persistable`，不新增/覆盖预案；创建/编辑预案的set目标必须是正媒体库ID，否则400 `scenario_source_invalid`。unset保持当前共享，empty或有效固定源是明确替换，不恢复共享。

## 公共会话HTTP/SSE

`SessionSnapshot`新增可空`share_id`（请求）与`actual_share_id`（实际），`actual_frames`为非负int64，`actual_audio`为可空boolean。普通源为null/null/0/null；共享不造永久媒体ID，source_uri保持空字符串。只有通过原Worker实例/owner/源代次门禁的已提交报告能更新实际身份，字段缺失或身份不匹配不能由adapter或playing补齐。两窗口内存投影有版本围栏、重启清空，不公开cap/SDP/receiver URI。

`actual_share_volume/actual_share_muted`是成对的可空实际接收页属性，不是普通volume/is_muted或202请求值；缺失/畸形报告清除旧值，不丢弃仍有效的视频帧。本人GET分享增加`actual_volume/actual_muted`，只在当前请求/实际身份、原代次、正帧、在线及receiving/playing匹配时提供，否则null。停止/离线或未知null不能当声音已停止。

意图切走后实际旧共享可暂留，不能把CLOSE入队当原生释放。网页确认共享须请求/实际GUID及源代次相同、正帧、online且playing；停止待确认不显示成功色。控制条仅DELETE原共享ID后GET刷新，明确临时、不支持普通媒体控制及自动重开；两逻辑窗口导航与墙面单/双/自定义布局解耦。

## UI

紧凑目标选择/共享/停止，原生采集在点击调用，选择器期间不排播放；明确不支持、离线、正在连接、实际接收、停止待确认。共享临时，不存媒体库/场景，不重启恢复。

每个当前目标面板独占一授权尝试/轨道/peer；取得视频后create，完成有界ICE再offer。实际视频帧为正、receiving且answer应用完成才呈现已收到画面。停止优先关闭本地资源，原账号/主机可达时只发一次DELETE；stopping/停止失败不宣称已关闭，查询只发GET，不自动重采。迟到分配只撤销原ID，取消选择器不预约；调用方取消与原cookie/CSRF、主机代次仍适用。当前音频只显示真实轨道存在、接收端静音，音频控制未开放。
