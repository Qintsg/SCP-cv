// 宿主仅接受OS实际父进程；父进程消失或PID复用时退出，从而释放独占原生job。
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ScpCv.LibreOfficeHost.Runtime;

internal sealed class ParentProcessLifetime : IDisposable
{
    private readonly Process _parent;
    private readonly Timer _watch;
    private readonly long _start;

    /// <summary>:param pid: 声明父PID。:param start: UTC启动ticks。:param session: 交互会话。:returns: 验证实际父归属并持续监测。</summary>
    internal ParentProcessLifetime(int pid, long start, int session)
    {
        using var self = Process.GetCurrentProcess();
        var basic = new IntPtr[6];
        if (NtQueryInformationProcess(self.Handle, 0, basic, IntPtr.Size * basic.Length, out _) != 0 || basic[5].ToInt64() != pid)
            throw new InvalidDataException("OS实际父进程不匹配");
        _parent = Process.GetProcessById(pid); _start = start;
        try
        {
            if (_parent.SessionId != session || session != self.SessionId || _parent.StartTime.ToUniversalTime().Ticks != start)
                throw new InvalidDataException("父进程启动时间/会话不匹配");
            _watch = new Timer(_ => Check(), null, 500, 500);
        }
        catch { _parent.Dispose(); throw; }
    }

    /// <summary>:returns: 不阻塞UNO线程；死亡时由OS关闭侧车job句柄，绝不访问用户Office。</summary>
    private void Check()
    {
        try { if (!_parent.HasExited && _parent.StartTime.ToUniversalTime().Ticks == _start) return; }
        catch (Exception error) when (error is InvalidOperationException || error is System.ComponentModel.Win32Exception) { }
        Console.Error.WriteLine("libreoffice_parent_lost: owned_job_cleanup");
        Environment.Exit(2);
    }

    /// <summary>:returns: 结束仅自有父监测。</summary>
    public void Dispose() { _watch.Dispose(); _parent.Dispose(); }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int infoClass, [Out] IntPtr[] data, int length, out int returned);
}
