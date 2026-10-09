// 暂停创建后先加入独占job再运行，消除Office子进程逃逸的启动竞态。
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ScpCv.Runtime.Processes;

internal sealed class PresentationProcessJob : IDisposable
{
    private readonly SafeFileHandle _job;

    /// <summary>:returns: 创建不继承的job，最后一个句柄关闭时清理所有成员。</summary>
    internal PresentationProcessJob()
    {
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job.IsInvalid) { _job.Dispose(); throw new Win32Exception(); }
        var limits = Marshal.AllocHGlobal(144);
        try
        {
            Marshal.Copy(new byte[144], 0, limits, 144);
            Marshal.WriteInt32(limits, 16, 0x2000);
            if (!SetInformationJobObject(_job, 9, limits, 144)) throw new Win32Exception();
        }
        catch { _job.Dispose(); throw; }
        finally { Marshal.FreeHGlobal(limits); }
    }

    /// <summary>:param process: 仍在启动门禁等待的精确自有子进程。:returns: 加入独占job后才可放行。</summary>
    internal void Attach(Process process)
    {
        if (!AssignProcessToJobObject(_job, process.Handle)) throw new Win32Exception();
    }

    /// <summary>:param path: 官方launcher。:param arguments: 已引用参数。:param directory: 安装目录。:returns: 已加入job的精确进程。</summary>
    internal Process Launch(string path, string arguments, string directory)
    {
        var security = new SecurityAttributes { Size = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
        using var nul = CreateFile("NUL", 0xC0000000, 3, ref security, 3, 0, IntPtr.Zero);
        if (nul.IsInvalid) throw new Win32Exception();
        IntPtr listSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
        var attributes = Marshal.AllocHGlobal(listSize);
        var handles = Marshal.AllocHGlobal(IntPtr.Size);
        var initialized = false;
        ProcessInformation process = default;
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref listSize)) throw new Win32Exception();
            initialized = true;
            Marshal.WriteIntPtr(handles, nul.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20002), handles, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception();
            var start = new StartupInfoEx
            {
                Info = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                    Input = nul.DangerousGetHandle(), Output = nul.DangerousGetHandle(), Error = nul.DangerousGetHandle() },
                Attributes = attributes,
            };
            if (!CreateProcess(path, ("\"" + path + "\" " + arguments + '\0').ToCharArray(), IntPtr.Zero, IntPtr.Zero, true,
                0x80000 | 0x4 | 0x08000000, IntPtr.Zero, directory, ref start, out process)) throw new Win32Exception();
            if (!AssignProcessToJobObject(_job, process.Process)) throw new Win32Exception();
            var owned = Process.GetProcessById((int)process.ProcessId);
            // Framework的GetProcessById只记PID；在放行前缓存精确OS句柄，退出后才能读终态且避免PID复用。
            try { _ = owned.Handle; }
            catch { owned.Dispose(); throw; }
            if (ResumeThread(process.Thread) == uint.MaxValue) { owned.Dispose(); throw new Win32Exception(); }
            return owned;
        }
        catch
        {
            // 仅终止本次仍暂停的精确句柄，失败不触碰其它Office。
            if (process.Process != IntPtr.Zero) TerminateProcess(process.Process, 2);
            throw;
        }
        finally
        {
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
            if (initialized) DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(attributes); Marshal.FreeHGlobal(handles);
        }
    }

    /// <summary>:returns: OS实际job成员；用于启动归属与退出证明。</summary>
    internal long[] Members()
    {
        var buffer = Marshal.AllocHGlobal(65536);
        try
        {
            if (!QueryInformationJobObject(_job, 3, buffer, 65536, IntPtr.Zero)) throw new Win32Exception();
            var count = Marshal.ReadInt32(buffer, 4);
            if (count < 0 || count > 8191) throw new InvalidOperationException("Office job成员数量无效");
            var ids = new long[count];
            for (var index = 0; index < count; index++) ids[index] = Marshal.ReadInt64(buffer, 8 + index * 8);
            return ids;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal int ActiveCount()
    {
        var buffer = Marshal.AllocHGlobal(48);
        try
        {
            if (!QueryInformationJobObject(_job, 1, buffer, 48, IntPtr.Zero)) throw new Win32Exception();
            return Marshal.ReadInt32(buffer, 40);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    /// <summary>:param diagnostics: 仅诊断同一次采样、同一持有句柄，不改变判断。:returns: 是否仍有未退出的OS成员。</summary>
    internal bool HasRunningMembers(bool diagnostics = false)
    {
        var members = Members();
        foreach (var id in members)
        {
            try
            {
                using var process = Process.GetProcessById(checked((int)id));
                if (!process.HasExited)
                {
                    if (diagnostics) DiagnoseRunningMember(process, members);
                    return true;
                }
            }
            catch (ArgumentException) { }
        }
        return false;
    }

    /// <summary>:param process: 当前查询对象。:param members: 原采样列表。:returns: 原始OS读数，不以第二次采样掩盖首次存活。</summary>
    private void DiagnoseRunningMember(Process process, long[] members)
    {
        try
        {
            var handle = process.Handle;
            var wait = WaitForSingleObject(handle, 0);
            var exitRead = GetExitCodeProcess(handle, out var exitCode);
            var exitError = Marshal.GetLastWin32Error();
            var membershipRead = IsProcessInJob(handle, _job, out var member);
            var membershipError = Marshal.GetLastWin32Error();
            // 先写原始读数，避免进程在名称查询前退出而掩盖同句柄证据。
            Console.Error.WriteLine("presentation_live_member: pid=" + process.Id +
                ";members=" + string.Join(",", members) + ";wait=" + wait + ";exit_read=" + exitRead + ";exit=" + exitCode +
                ";exit_error=" + exitError + ";job_read=" + membershipRead + ";in_job=" + member + ";job_error=" + membershipError);
        }
        catch (Exception error) when (error is Win32Exception || error is InvalidOperationException)
        { Console.Error.WriteLine("presentation_live_member_query_failed: pid=" + process.Id + ";error=" + error.Message); }
    }

    /// <summary>:returns: 自有job成员诊断，不由进程名称授予控制权，不将PID缺失伪称正常退出。</summary>
    internal string DescribeMembers()
    {
        var descriptions = new System.Collections.Generic.List<string>();
        foreach (var id in Members())
        {
            try
            {
                using var process = Process.GetProcessById(checked((int)id));
                descriptions.Add(id + ":" + process.ProcessName + ":exited=" + process.HasExited);
            }
            catch (ArgumentException) { descriptions.Add(id + ":no_live_process"); }
            catch (Win32Exception error) { descriptions.Add(id + ":query_failed=" + error.NativeErrorCode); }
        }
        return string.Join(",", descriptions);
    }

    /// <summary>:returns: 仅关闭自有job句柄。</summary>
    public void Dispose() => _job.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { internal int Size; internal IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] internal bool Inherit; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int Size; internal string? Reserved; internal string? Desktop; internal string? Title;
        internal int X, Y, Width, Height, XChars, YChars, Fill, Flags;
        internal short Show, ReservedCount; internal IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { internal StartupInfo Info; internal IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { internal IntPtr Process, Thread; internal uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int info, IntPtr data, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int info, IntPtr data, uint size, IntPtr returned);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
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
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool member);
}
