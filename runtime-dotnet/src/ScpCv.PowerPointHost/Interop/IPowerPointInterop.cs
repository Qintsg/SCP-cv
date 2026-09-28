// PowerPoint COM 创建与 Win32 窗口身份证据的外部互操作契约。
namespace ScpCv.PowerPointHost.Interop;

/// <summary>窗口与其当前所属进程的精确证据；启动时间用于防止 PID 重用。</summary>
public sealed record PowerPointWindowEvidence(nint Handle, int ProcessId, DateTimeOffset ProcessStart);

/// <summary>只封装外部 COM/Win32 访问；文稿归属决策由实际 Adapter 执行。</summary>
public interface IPowerPointInterop
{
    /// <summary>
    /// 创建 PowerPoint Automation 对象；调用方仍须检查是否混入用户文稿。
    /// :returns: 当前 STA 拥有的外部 Application 对象。
    /// </summary>
    object CreateApplication();

    /// <summary>
    /// 读取指定 Application 的窗口身份，不按进程名搜索。
    /// :param application: 当前 STA 拥有的 PowerPoint Application。
    /// :returns: 窗口与进程身份；无法证明时返回 null。
    /// </summary>
    PowerPointWindowEvidence? GetApplicationWindow(object application);

    /// <summary>
    /// 读取 Run 返回的指定 SlideShowWindow 的窗口身份，不枚举其他放映。
    /// :param slideShowWindow: 当前文稿 Run 返回的 COM 放映窗口。
    /// :returns: 窗口与进程身份；无法证明时返回 null。
    /// </summary>
    PowerPointWindowEvidence? GetSlideShowWindow(object slideShowWindow);
}
