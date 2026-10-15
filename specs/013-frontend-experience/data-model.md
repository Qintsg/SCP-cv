# 前端展示模型

- PageContext：title、description、可选meta和actions插槽，无业务请求。
- EmptyView：title、description、可选动作插槽；目录无内容与筛选无匹配分别提供原因。
- WindowSummary：复用session的window_id、source_id、actual_source_id、playback_state、代次及在线；源名来自已有sources，不产生新持久状态。
- DirectoryView：复用当前目录、category、searchKeyword/searchSubfolders、filtered及selected。清除筛选不改当前目录或触发设备动作。

不变更服务器数据模型、请求字段、认证、命令代次或音频设置。
