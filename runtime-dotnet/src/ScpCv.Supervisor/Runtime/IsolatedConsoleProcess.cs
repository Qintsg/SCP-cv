// 隐藏独立控制台的原生启动；只用于MediaMTX，不脱离父job、不改其它进程或全局控制台。
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScpCv.Supervisor.Runtime;

internal static class IsolatedConsoleProcess
{
    /// <summary>:param executable: 可信MediaMTX绝对路径。:param arguments: 内部配置路径。:returns: 已持有原句柄的独立控制台进程。</summary>
    internal static Process Start(string executable, string arguments)
    {
        var path = Path.GetFullPath(executable);
        if (!File.Exists(path) || !string.Equals(Path.GetFileName(path), "mediamtx.exe", StringComparison.OrdinalIgnoreCase) || path.Contains('"'))
            throw new InvalidOperationException("协作控制台只允许显式MediaMTX映像。");
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Flags = 1, ShowWindow = 0 };
        var command = ($"\"{path}\" {arguments}\0").ToCharArray();
        if (!CreateProcess(path, command, IntPtr.Zero, IntPtr.Zero, false, 0x10u | 0x400u, IntPtr.Zero,
                Path.GetDirectoryName(path), ref startup, out var information)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var process = Process.GetProcessById(checked((int)information.ProcessId));
            try { _ = process.Handle; return process; }
            catch { process.Dispose(); throw; }
        }
        catch { _ = TerminateProcess(information.Process, 1); throw; }
        finally { _ = CloseHandle(information.Thread); _ = CloseHandle(information.Process); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, Width, Height, CharacterWidth, CharacterHeight, FillAttribute, Flags;
        public ushort ShowWindow, ReservedLength;
        public IntPtr ReservedBytes, StandardInput, StandardOutput, StandardError;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] char[] command, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment,
        string? directory, ref StartupInfo startup, out ProcessInformation process);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);
}
