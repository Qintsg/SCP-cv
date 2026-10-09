// 仅测试自有子进程：限制继承为NUL，不让子进程持有父stdio而掩盖父先退出的场景。
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ScpCv.Presentation.TestHost;

internal static class RestrictedTestChild
{
    /// <summary>:param argument: 固定短/长寿命测试模式。:returns: 继承客户端job但不持有父stdio的本程序子进程。</summary>
    internal static Process Start(string argument)
    {
        if (argument is not "--short-child" and not "--long-child" &&
            !argument.StartsWith("--gated-child=Local\\SCP-cv-ShortChild-", StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(argument));
        var security = new SecurityAttributes { Size = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
        using var nul = CreateFile("NUL", 0xC0000000, 3, ref security, 3, 0, IntPtr.Zero);
        if (nul.IsInvalid) throw new Win32Exception();
        IntPtr size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var attributes = Marshal.AllocHGlobal(size);
        var handles = Marshal.AllocHGlobal(IntPtr.Size);
        var initialized = false;
        ProcessInformation process = default;
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)) throw new Win32Exception();
            initialized = true;
            Marshal.WriteIntPtr(handles, nul.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20002), handles, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            var start = new StartupInfoEx { Info = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                Input = nul.DangerousGetHandle(), Output = nul.DangerousGetHandle(), Error = nul.DangerousGetHandle() }, Attributes = attributes };
            var path = Environment.ProcessPath!;
            if (!CreateProcess(path, ("\"" + path + "\" " + argument + '\0').ToCharArray(), IntPtr.Zero, IntPtr.Zero, true,
                0x80000 | 0x08000000, IntPtr.Zero, Path.GetDirectoryName(path)!, ref start, out process)) throw new Win32Exception();
            var owned = Process.GetProcessById(checked((int)process.ProcessId));
            try { _ = owned.Handle; return owned; }
            catch { owned.Dispose(); throw; }
        }
        finally
        {
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
            if (initialized) DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(handles); Marshal.FreeHGlobal(attributes);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { internal int Size; internal IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] internal bool Inherit; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int Size; internal string? Reserved; internal string? Desktop; internal string? Title;
        internal int X, Y, Width, Height, XChars, YChars, Fill, Flags;
        internal short Show, ReservedCount; internal IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { internal StartupInfo Info; internal IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { internal IntPtr Process, Thread; internal uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, ref SecurityAttributes security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, [In, Out] char[] command, IntPtr processSecurity, IntPtr threadSecurity,
        bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation process);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
