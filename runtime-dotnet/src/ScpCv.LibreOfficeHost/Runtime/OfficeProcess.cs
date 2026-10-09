// 独占profile和原生job，端口只接受属于该job的监听者；正常退出需核所有成员。
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using ScpCv.Runtime.Processes;

namespace ScpCv.LibreOfficeHost.Runtime;

internal sealed class OfficeProcess : IDisposable
{
    private readonly Process _launcher;
    private readonly PresentationProcessJob _job;
    private bool _confirmedStopped;
    internal string ProgramDirectory { get; }
    internal string Profile { get; }
    internal int Port { get; }

    /// <summary>:param program: 官方目录。:param profile: 本次全新自有目录。:param headless: 转换无需窗口。</summary>
    internal OfficeProcess(string program, string profile, bool headless)
    {
        ProgramDirectory = Path.GetFullPath(program);
        Profile = Path.GetFullPath(profile);
        if (!File.Exists(Path.Combine(ProgramDirectory, "uno.ini")) || !File.Exists(Path.Combine(ProgramDirectory, "soffice.exe")))
            throw new FileNotFoundException("LibreOffice安装目录不完整");
        OfficeEngineProfile.Create(Profile, Environment.GetEnvironmentVariable("LIBO_UPDATER_TEST_ENABLE") is not null);
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try { Port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
        _job = new PresentationProcessJob();
        Process? launcher = null;
        try
        {
            var arguments = "\"-env:UserInstallation=" + new Uri(Profile).AbsoluteUri +
                "\" --nodefault --nologo --norestore --nofirststartwizard " +
                (headless ? "--headless " : string.Empty) + "\"--accept=socket,host=127.0.0.1,port=" +
                Port + ";urp;StarOffice.ComponentContext\"";
            launcher = _job.Launch(Path.Combine(ProgramDirectory, "soffice.exe"), arguments, ProgramDirectory);
            _launcher = launcher;
            Console.Error.WriteLine("libreoffice_launch: launcher=" + _launcher.Id + ";port=" + Port + ";profile=" + Profile);
            var budget = Stopwatch.StartNew();
            while (budget.Elapsed.TotalSeconds < 30)
            {
                if (_launcher.HasExited) throw new InvalidOperationException("LibreOffice在ready前退出；launcher_exit=" +
                    _launcher.ExitCode + ";port=" + Port + ";members=" + _job.DescribeMembers());
                var portOwner = NativeTcpOwnership.ListenerOwner(Port);
                if (portOwner != 0)
                {
                    if (!_job.Members().Contains(portOwner)) throw new InvalidOperationException("监听端口归属其它进程，拒绝连接");
                    using var socket = new TcpClient();
                    socket.Connect("127.0.0.1", Port);
                    Console.Error.WriteLine("libreoffice_owned: launcher=" + _launcher.Id + ";listener=" + portOwner + ";port=" + Port);
                    return;
                }
                Thread.Sleep(100);
            }
            throw new TimeoutException("LibreOffice未在启动预算内监听；自有job成员=" + string.Join(",", _job.Members()));
        }
        catch { _job.Dispose(); launcher?.Dispose(); throw; }
    }

    /// <summary>:returns: UNO退出后核job所有成员及监听，不将launcher早退等同引擎退出。</summary>
    internal void ConfirmStopped()
    {
        var budget = Stopwatch.StartNew();
        var sampled = false;
        while (budget.Elapsed.TotalSeconds < 5)
        {
            if (!_job.HasRunningMembers() && NativeTcpOwnership.ListenerOwner(Port) == 0)
            {
                _confirmedStopped = true;
                return;
            }
            if (!sampled && budget.Elapsed.TotalSeconds >= 2)
            {
                Console.Error.WriteLine("libreoffice_exit_pending: launcher=" + _launcher.Id + ";listener=" + NativeTcpOwnership.ListenerOwner(Port) + ";members=" + _job.DescribeMembers());
                sampled = true;
            }
            Thread.Sleep(50);
        }
        throw new TimeoutException("LibreOffice job成员或监听退出未确认；launcher=" + _launcher.Id + ";listener=" + NativeTcpOwnership.ListenerOwner(Port) + ";members=" + _job.DescribeMembers());
    }

    /// <summary>:returns: 仅自有job兜底；失败记录不冒充正常退出。</summary>
    public void Dispose()
    {
        if (!_confirmedStopped) Console.Error.WriteLine("libreoffice_forced_cleanup: normal_exit_unconfirmed");
        _job.Dispose(); _launcher.Dispose();
    }
}
