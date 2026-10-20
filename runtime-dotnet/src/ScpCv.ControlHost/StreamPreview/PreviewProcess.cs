// Windows预览转换器：暂停创建、只继承明确管道、先加入独占Job再放行；不启动可见窗口。
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ScpCv.ControlHost.StreamPreview;

[SupportedOSPlatform("windows")]
public sealed class PreviewProcess : IAsyncDisposable
{
    private readonly SafeFileHandle _job;
    private readonly Process _process;
    private int _disposed;
    public Stream Output { get; }
    public int ProcessId => _process.Id;

    private PreviewProcess(SafeFileHandle job, Process process, Stream output)
    { _job = job; _process = process; Output = output; }

    /// <summary>:returns: 取消时立即终止本Job以打断同步管道读取；最终Dispose仍核对退出，失败不冒称成功。</summary>
    public void RequestStop()
    { if (Volatile.Read(ref _disposed) == 0) _ = TerminateJobObject(_job, 0); }

    /// <summary>:param executable: 已解析绝对exe路径。:param arguments: 仅受控参数。:returns: 已加入自有Job的低CPU优先级进程，不代表网络QoS或已经出画。</summary>
    public static PreviewProcess Start(string executable, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) || IntPtr.Size != 8)
            throw new PreviewException("未找到可用的服务器FFmpeg", "preview_dependency_missing");
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid) { job.Dispose(); throw new Win32Exception(); }
        var info = Marshal.AllocHGlobal(144);
        SafeFileHandle? pipeRead = null;
        Process? owned = null;
        try
        {
            Marshal.Copy(new byte[144], 0, info, 144);
            // 独占Job总内存512MiB，限制异常源／解码器资源，不影响正式VLC或现有MediaMTX进程。
            Marshal.WriteInt32(info, 16, 0x2000 | 0x200); Marshal.WriteInt64(info, 120, 512L * 1024 * 1024);
            if (!SetInformationJobObject(job, 9, info, 144)) throw new Win32Exception();
            var security = new SecurityAttributes { Size = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
            if (!CreatePipe(out pipeRead, out var pipeWrite, ref security, 0)) throw new Win32Exception();
            using (pipeWrite)
            using (var nul = CreateFile("NUL", 0xc0000000, 3, ref security, 3, 0, IntPtr.Zero))
            {
                if (nul.IsInvalid || !SetHandleInformation(pipeRead, 1, 0)) throw new Win32Exception();
                owned = Launch(job, executable, arguments, pipeWrite, nul);
            }
            var stream = new FileStream(pipeRead, FileAccess.Read, 16384, false);
            pipeRead = null;
            return new(job, owned, stream);
        }
        catch { job.Dispose(); pipeRead?.Dispose(); owned?.Dispose(); throw; }
        finally { Marshal.FreeHGlobal(info); }
    }

    /// <summary>:returns: 仅终止本对象Job里的精确成员，父进程意外退出时由OS关闭Job兜底。</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            // 不发送q或复用PID，不允许残留编码器继续持有上游。
            if (!TerminateJobObject(_job, 0)) throw new Win32Exception();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw new PreviewException("预览转换器退出尚未确认", "preview_cleanup_pending"); }
        }
        finally
        {
            _job.Dispose();
            try { await Output.DisposeAsync().ConfigureAwait(false); }
            finally { _process.Dispose(); }
        }
    }

    private static Process Launch(SafeFileHandle job, string executable, IReadOnlyList<string> arguments, SafeFileHandle output, SafeFileHandle nul)
    {
        IntPtr size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var attributes = Marshal.AllocHGlobal(size); var handles = Marshal.AllocHGlobal(2 * IntPtr.Size);
        var initialized = false; ProcessInformation process = default;
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)) throw new Win32Exception();
            initialized = true;
            Marshal.WriteIntPtr(handles, output.DangerousGetHandle()); Marshal.WriteIntPtr(handles, IntPtr.Size, nul.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20002), handles, new IntPtr(2 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            var startup = new StartupInfoEx
            {
                Info = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                    Input = nul.DangerousGetHandle(), Output = output.DangerousGetHandle(), Error = nul.DangerousGetHandle() },
                Attributes = attributes,
            };
            var command = Quote(executable) + " " + string.Join(' ', arguments.Select(Quote));
            if (!CreateProcess(executable, (command + '\0').ToCharArray(), IntPtr.Zero, IntPtr.Zero, true,
                0x80000 | 0x4 | 0x08000000 | 0x40, IntPtr.Zero, Path.GetDirectoryName(executable), ref startup, out process)) throw new Win32Exception();
            if (!AssignProcessToJobObject(job, process.Process)) throw new Win32Exception();
            var owned = Process.GetProcessById((int)process.ProcessId);
            try
            {
                _ = owned.Handle;
                if (ResumeThread(process.Thread) == uint.MaxValue) throw new Win32Exception();
                return owned;
            }
            catch { owned.Dispose(); throw; }
        }
        catch { if (process.Process != IntPtr.Zero) TerminateProcess(process.Process, 2); throw; }
        finally
        {
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
            if (initialized) DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(handles); Marshal.FreeHGlobal(attributes);
        }
    }

    /// <summary>:param value: 单参数。:returns: Windows argv规则引用，不经cmd／PowerShell解释。</summary>
    private static string Quote(string value)
    {
        var result = new StringBuilder("\""); var slashes = 0;
        foreach (var letter in value)
        {
            if (letter == '\\') { slashes++; continue; }
            result.Append('\\', letter == '"' ? slashes * 2 + 1 : slashes); result.Append(letter); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int Size; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size; public IntPtr Reserved; public IntPtr Desktop; public IntPtr Title;
        public int X; public int Y; public int XSize; public int YSize; public int XChars; public int YChars; public int Fill;
        public int Flags; public short Show; public short ReservedSize; public IntPtr ReservedData;
        public IntPtr Input; public IntPtr Output; public IntPtr Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process; public IntPtr Thread; public uint ProcessId; public uint ThreadId; }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr security, string? name);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, IntPtr value, int length);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes security, int size);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetHandleInformation(SafeFileHandle handle, int mask, int flags);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, ref SecurityAttributes security, int disposition, int flags, IntPtr template);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(IntPtr list, int flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcess(string application, [In, Out] char[] command, IntPtr security, IntPtr threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, int flags, IntPtr environment, string? directory, ref StartupInfoEx startup, out ProcessInformation process);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(IntPtr process, uint code);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateJobObject(SafeFileHandle job, uint code);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
