# 数据与状态

PlaybackSession新增：

- WindowForeground: bool?，实际置顶状态；未知及新所有者初始为null。
- WindowObservationSequence: long，当前所有者接受的最新窗口观测序列，初始0。

Worker每次实际窗口快照递增window_observation_sequence；所有权登记变更后服务端清空。HTTP/SSE输出window_foreground，播放器离线时为null。

状态：置顶→隐藏请求入队→实际窗口移到底部→隐藏观测；恢复请求逆向执行。两个窗口不一致时，菜单提供显示窗口。请求受理只更新队列，不改变实际观测。
