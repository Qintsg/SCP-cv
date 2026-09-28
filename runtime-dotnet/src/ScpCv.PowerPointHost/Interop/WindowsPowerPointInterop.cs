// 通过 Office 类型库的固定 DispId 读取指定 COM 窗口，拒绝全局猜测 HWND。
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScpCv.PowerPointHost.Interop;

/// <summary>PowerPoint COM 与 Win32 的真实外部 Adapter；只在 Office STA 上调用。</summary>
internal sealed class WindowsPowerPointInterop : IPowerPointInterop
{
    /// <summary>
    /// 创建 PowerPoint COM Application。
    /// :returns: 外部 Application；缺少 Office 或创建失败时抛出异常。
    /// </summary>
    public object CreateApplication()
    {
        var applicationType = Type.GetTypeFromProgID("PowerPoint.Application")
            ?? throw new InvalidOperationException("未安装 PowerPoint COM Automation 类型。");
        return Activator.CreateInstance(applicationType)
            ?? throw new InvalidOperationException("无法创建 PowerPoint COM Application。");
    }

    /// <summary>
    /// 按类型库中 _Application.HWND 的 DispId 2031 读取当前 Application 窗口。
    /// :param application: PowerPoint Application COM 对象。
    /// :returns: 当前精确进程身份；无法读取时返回 null。
    /// </summary>
    public PowerPointWindowEvidence? GetApplicationWindow(object application)
    {
        try { return InspectWindow(((IApplicationWindow)application).HWND, false); }
        catch (Exception exception) when (IsUnavailable(exception)) { return null; }
    }

    /// <summary>
    /// 按类型库中 SlideShowWindow.HWND 的 DispId 2010 读取 Run 返回的窗口。
    /// :param slideShowWindow: 当前文稿 Run 返回的 COM 窗口。
    /// :returns: 可见放映窗口的精确进程身份；无法读取时返回 null。
    /// </summary>
    public PowerPointWindowEvidence? GetSlideShowWindow(object slideShowWindow)
    {
        try { return InspectWindow(((ISlideShowWindow)slideShowWindow).HWND, true); }
        catch (Exception exception) when (IsUnavailable(exception)) { return null; }
    }

    /// <summary>
    /// 联合检查指定 HWND、放映窗口类及当前进程启动时间。
    /// :param rawHandle: Office 返回的 32 位 HWND 位模式。
    /// :param isSlideShow: 是否要求为可见 screenClass 放映窗口。
    /// :returns: 当前窗口证据；窗口或进程消失时返回 null。
    /// </summary>
    private static PowerPointWindowEvidence? InspectWindow(int rawHandle, bool isSlideShow)
    {
        // HWND 在 Office 类型库中是有符号 long；高位为 1 仍是有效的句柄位模式。
        var handle = (nint)unchecked((uint)rawHandle);
        if (handle == 0 || !IsWindow(handle)) return null;
        if (isSlideShow)
        {
            if (!IsWindowVisible(handle)) return null;
            var className = new char[64];
            var length = GetClassName(handle, className, className.Length);
            if (length <= 0 || !string.Equals(
                    new string(className, 0, length), "screenClass", StringComparison.Ordinal)) return null;
        }
        if (GetWindowThreadProcessId(handle, out var processId) == 0 || processId == 0) return null;
        using var process = Process.GetProcessById(checked((int)processId));
        if (process.HasExited) return null;
        var start = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        return new(handle, (int)processId, start);
    }

    /// <summary>
    /// 外部对象消失、拒绝访问或 Office 不暴露对应接口时按无法证明归属处理。
    /// :param exception: 外部互操作异常。
    /// :returns: 是否为可诊断的证据不可用。
    /// </summary>
    private static bool IsUnavailable(Exception exception) =>
        exception is COMException or InvalidCastException or ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or OverflowException;

    // HWND 是 PowerPoint 类型库的隐藏成员；固定 DispId 绕开 GetIDsOfNames 的名称缺失。
    [ComImport, Guid("91493442-5A91-11CF-8700-00AA0060263B"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    private interface IApplicationWindow
    {
        [DispId(2031)] int HWND { get; }
    }

    [ComImport, Guid("91493453-5A91-11CF-8700-00AA0060263B"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    private interface ISlideShowWindow
    {
        [DispId(2010)] int HWND { get; }
    }

    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, char[] className, int maximum);
}
