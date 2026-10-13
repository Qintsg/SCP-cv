// MediaMTX协作退出深度模块：原创建证据/句柄/映像/会话核验，助手仅允许目标和自身两个控制台成员。
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ScpCv.Supervisor.Processes;

namespace ScpCv.Supervisor.Runtime;

public static class MediaMtxConsoleLifetime
{
    /// <summary>:param executable: 可信固定映像。:param arguments: 内部命令参数。:returns: 独立隐藏控制台，不共享Host/Worker控制信号。</summary>
    public static Process Start(string executable, string arguments) => IsolatedConsoleProcess.Start(executable, arguments);

    /// <summary>:param owned: 本Registry原生创建证明，不从状态文件或HTTP接受该布尔值。:returns: 是否完成协作退出；失败仍由Coordinator明确兜底。</summary>
    public static async Task<bool> TryStopAsync(OwnedProcess owned, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var self = Process.GetCurrentProcess();
        if (owned.Role != "mediamtx" || !owned.IsolatedConsole || !ProcessRegistry.StillOwns(owned) ||
            owned.SessionId != self.SessionId) return false;
        var image = owned.Process.MainModule?.FileName ?? string.Empty;
        if (!string.Equals(Path.GetFileName(image), "mediamtx.exe", StringComparison.OrdinalIgnoreCase)) return false;
        var helper = Path.ChangeExtension(typeof(MediaMtxConsoleLifetime).Assembly.Location, ".exe");
        if (!File.Exists(helper)) return false;
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var value in new[] { "console-stop", owned.ProcessId.ToString(CultureInfo.InvariantCulture),
            owned.StartTime.UtcTicks.ToString(CultureInfo.InvariantCulture), owned.SessionId.ToString(CultureInfo.InvariantCulture), image }) start.ArgumentList.Add(value);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("协作退出助手未启动。");
        _ = process.Handle;
        try
        {
            await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(4), token).ConfigureAwait(false);
            return process.ExitCode == 0 && owned.Process.HasExited && owned.Process.ExitCode == 0;
        }
        catch (TimeoutException) { return false; }
        finally
        {
            // 助手超时只定向清理该原句柄，不向未知控制台发送补偿信号。
            if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false); }
        }
    }

    /// <summary>:param arguments: 原PID/时刻/会话/映像的内部CLI纯值。:returns: 0仅表示目标原句柄正常退出；无效值/成员/超时非零。</summary>
    public static int Signal(string[] arguments)
    {
        if (arguments.Length != 5 || arguments[0] != "console-stop" ||
            !int.TryParse(arguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0 ||
            !long.TryParse(arguments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks <= 0 ||
            !int.TryParse(arguments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var session)) return 64;
        using var self = Process.GetCurrentProcess();
        if (pid == self.Id || session != self.SessionId || !Path.IsPathFullyQualified(arguments[4]) ||
            !string.Equals(Path.GetFileName(arguments[4]), "mediamtx.exe", StringComparison.OrdinalIgnoreCase)) return 65;
        using var target = Process.GetProcessById(pid);
        _ = target.Handle;
        bool Matches() => !target.HasExited && target.SessionId == session && target.StartTime.ToUniversalTime().Ticks == ticks &&
            string.Equals(target.MainModule?.FileName, Path.GetFullPath(arguments[4]), StringComparison.OrdinalIgnoreCase);
        if (!Matches()) return 66;
        _ = FreeConsole();
        if (!AttachConsole(checked((uint)pid))) return 67;
        ConsoleHandler handler = code => code is 0 or 1;
        try
        {
            if (!SetConsoleCtrlHandler(handler, true)) return 68;
            var processes = new uint[3];
            var count = GetConsoleProcessList(processes, checked((uint)processes.Length));
            if (count != 2 || !processes.Take(2).Contains(checked((uint)pid)) ||
                !processes.Take(2).Contains(checked((uint)self.Id)) || !Matches()) return 69;
            // 只在独立控制台恰有原目标与自有助手时发送BREAK；绝不在共享控制台广播。
            if (!GenerateConsoleCtrlEvent(1, 0)) return 70;
            return target.WaitForExit(2500) && target.ExitCode == 0 ? 0 : 71;
        }
        finally { _ = FreeConsole(); GC.KeepAlive(handler); }
    }

    private delegate bool ConsoleHandler(uint eventType);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint GetConsoleProcessList([Out] uint[] processes, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetConsoleCtrlHandler(ConsoleHandler handler, [MarshalAs(UnmanagedType.Bool)] bool add);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GenerateConsoleCtrlEvent(uint eventType, uint groupId);
}
