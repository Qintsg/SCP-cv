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
- NativeReleased只来自原Worker的实际Dispose回执或原进程确证退出；CLOSE排队、offline、换Worker及源意图变化不能设置。终态释放每窗预约，活跃最多两条。

## 不变量

1. 取消权限不创建会话；服务失败关闭轨道/撤销接收授权。
2. 终止在同事务内核SourceGeneration并条件CLOSE，不先读后无条件关闭。
3. 重复停止幂等但CLOSE入队不当实际确认；失败待确认不重试副作用。
4. 服务重启无内存授权，旧OPEN不能重新接收，不恢复录制或采集。
5. 页面/主机/账号更换销毁旧采集，旧回调不污染新页面或会话。
6. 两窗媒体资源隔离，争抢409；只暴露本人发布状态。
7. 周期清理独立于发布者查询。重启仅条件清除孤立动态意图，不恢复cap/SDP；预案不能保存共享，捕获失败不得覆盖旧预案。

## 现有运行时

动态OPEN/CLOSE仍走队列及源代次。actual source_id=null、adapter_kind=screen_share不能投影成“无输出”；共享身份字段需显式接入HTTP/SSE/前端观察，不赋予文件定位或循环能力。

## ScreenSharePlaybackProjection（内存公共投影）

只保存两个窗口各自的非秘密分享ID、OPEN代次、原Worker实例/owner及最新已提交报告版本、实际帧和音轨存在。请求`share_id`与实际`actual_share_id`分离；持久会话的desired/observed代次分别匹配才补字段。帧数不倒退，旧post-commit回调不恢复后来已清除的共享；缺失或畸形实际字段保守清空实际身份。重启不能凭数据库中的null源/adapter还原分享，不保存SDP、cap、媒体URI、画面或永久媒体库条目。

## 原生释放回执（仅内部IPC）

`released_screen_shares`最多32条，每条仅`share_id`非空GUID和`source_generation`正int64。Player保留有界去重历史并重复采样；只有完整原生Dispose完成才追加，不因JS closed/轨道ended或取消追加。ControlHost在原Worker身份事务提交后核窗口/instance/owner/原分享/原代次；整个数组schema不合法即不采信。不是公共HTTP/SSE字段，不存数据库、cap或receiver URI。原Player观察最多两条且绑定原身份，取消只结束观察、不确认死亡。
