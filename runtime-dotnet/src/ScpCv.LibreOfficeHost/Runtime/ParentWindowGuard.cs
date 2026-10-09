// 在调用UNO承载前核真实父窗口PID/启动时间/会话/DPI，声明值不作为OS证据。
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ScpCv.Presentation.Protocol;

namespace ScpCv.LibreOfficeHost.Runtime;

internal static class ParentWindowGuard
{
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    /// <summary>:param parent: 可信父进程经控制面授权的窗口声明。:returns: 当前OS归属吻合。</summary>
    internal static void Verify(PresentationParentWindow parent)
    {
        var window = new IntPtr(parent.Handle);
        if (!IsWindow(window) || GetWindowThreadProcessId(window, out var pid) == 0 || pid != parent.ProcessId)
            throw new InvalidDataException("父窗口PID围栏不匹配");
        using var process = Process.GetProcessById(parent.ProcessId);
        var expected = DateTimeOffset.Parse(parent.ProcessStartTime, CultureInfo.InvariantCulture);
        if (process.StartTime.ToUniversalTime().Ticks != expected.UtcDateTime.Ticks || process.SessionId != parent.SessionId)
            throw new InvalidDataException("父窗口启动时间/会话围栏不匹配");
        using var current = Process.GetCurrentProcess();
        if (process.SessionId != current.SessionId || GetDpiForWindow(window) != parent.Dpi)
            throw new InvalidDataException("父窗口DPI或交互会话不匹配");
    }
}
