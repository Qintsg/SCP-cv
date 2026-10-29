# 窗口维护合同

`POST /api/playback/window-foreground/`，已登录及CSRF保护，JSON必须提供布尔字段foreground。

- foreground=false：窗口1与2取消置顶并移到底部；true：恢复置顶。
- 202：返回sessions及各窗口submitted_command_id，只代表入队。
- 400：字段缺失或类型错误；409：窗口离线或未取得实际窗口观测。
- GET sessions及SSE增加nullable window_foreground，读取实际结果。
- IPC命令SET_WINDOW_FOREGROUND，参数foreground；同类Pending可合并，媒体替换保留该命令。
- 快照window_observation_sequence只接受当前所有者严格递增的值；窗口观测不推进媒体代次。
