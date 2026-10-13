// 测试专用额外控制台成员，验证生产信号助手拒绝共享控制台；不发送信号，不进入发布包。
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ScpCv.Presentation.TestHost;

internal static class ConsoleHolder
{
    /// <summary>:param args: 自有MediaMTX原PID/时刻和全新就绪文件。:returns: 附着后有限等待，不访问Office或控制设备。</summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ||
            !long.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
            !Path.IsPathFullyQualified(args[3]) || File.Exists(args[3])) return 64;
        using var target = Process.GetProcessById(pid);
        using var self = Process.GetCurrentProcess();
        _ = target.Handle;
        if (target.HasExited || target.SessionId != self.SessionId || target.StartTime.ToUniversalTime().Ticks != ticks ||
            !string.Equals(Path.GetFileName(target.MainModule?.FileName), "mediamtx.exe", StringComparison.OrdinalIgnoreCase)) return 65;
        _ = FreeConsole();
        if (!AttachConsole(checked((uint)pid))) return 66;
        try { await File.WriteAllTextAsync(args[3], "attached"); await Task.Delay(TimeSpan.FromSeconds(30)); return 0; }
        finally { _ = FreeConsole(); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AttachConsole(uint processId);
}
