# 研究：窗口维护

**Decision**: PlayerWindow在所属Dispatcher内设置Topmost，并通过SetWindowPos使用HWND_BOTTOM或HWND_TOPMOST，保留原尺寸、位置与焦点。状态读取WS_EX_TOPMOST。
**Rationale**: [SetWindowPos官方文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)确认底部排序会取消置顶。窗口保持可见视觉树，现有WebView2与文稿资源无需重建。
**Alternatives considered**: Window.Hide会改变控件可见性，影响现有依赖可见视觉树的预备流程；本功能使用窗口顺序控制。

**Decision**: 复用持久命令队列、Worker快照、Session和SSE，窗口观测独立于媒体代次。
**Rationale**: 只读研究确认OPEN会取消所有待执行命令，需要保留SET_WINDOW_FOREGROUND；周期采样需覆盖无源、图片和网页。缓存命令结果可重发，因此窗口观测必须携带独立递增序列，并在新所有者登记时清空。
**Alternatives considered**: 只在Web保存布尔状态无法证明实际窗口顺序；直接跨会话操作HWND不符合现有进程边界。
