// LibreOffice退出的纯值门禁：通信桥断开不是证据，须两个原句柄退出0、job空且监听关闭。
namespace ScpCv.Presentation.Protocol;

public static class PresentationNativeExitPolicy
{
    /// <summary>:param launcherExit: 原launcher未退出为null。:param engineExit: 原监听引擎未退出为null。:param jobEmpty: 自有job没有存活成员。:param listenerClosed: 原端口已释放。:returns: 正常退出是否已被独立OS证据证明。</summary>
    public static bool Confirmed(int? launcherExit, int? engineExit, bool jobEmpty, bool listenerClosed) =>
        launcherExit == 0 && engineExit == 0 && jobEmpty && listenerClosed;
}
