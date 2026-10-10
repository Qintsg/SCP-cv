# Data Model: 分享会话

## ScreenShareSession（内存）

- Id随机GUID，不代替账号认证；OwnerId绑定发布者。
- WindowId严格1/2，每目标一个活动预约。
- SourceGeneration由OPEN事务分配，null表示尚未打开。
- 原Target身份包含WorkerInstanceId/OwnerEpoch/GroupEpoch；OPEN事务与租约注入均须匹配，不允许重启后的新Worker接收旧共享。
- ReceiverToken随机256位，常量时间核验、绑定share/window/generation及原Target；不公开DTO/日志/数据库，URI仅fragment携带、HTTP用Authorization Bearer。
- Offer/Answer各64KiB，严格type、不可替换已经使用的协商；不缓存无限ICE列表。
- State：allocated→negotiating→receiving→stopping→closed/failed；终态不回退，重试新身份。
- CreatedAt/LastPublisherSeen/ExpiresAt使用TimeProvider；预约20秒、活跃30秒心跳期限。
- ActualFrames/LastFrameAt/ReceiverConnected来自匹配的实际Worker或限定接收资源，发布者心跳不授予playing。
- HasAudio以实际轨道为准，请求选项不当能力。

## 不变量

1. 取消权限不创建会话；服务失败关闭轨道/撤销接收授权。
2. 终止在同事务内核SourceGeneration并条件CLOSE，不先读后无条件关闭。
3. 重复停止幂等但CLOSE入队不当实际确认；失败待确认不重试副作用。
4. 服务重启无内存授权，旧OPEN不能重新接收，不恢复录制或采集。
5. 页面/主机/账号更换销毁旧采集，旧回调不污染新页面或会话。
6. 两窗媒体资源隔离，争抢409；只暴露本人发布状态。

## 现有运行时

动态OPEN/CLOSE仍走队列及源代次。actual source_id=null、adapter_kind=screen_share不能投影成“无输出”；共享身份字段需显式接入HTTP/SSE/前端观察，不赋予文件定位或循环能力。
