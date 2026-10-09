// 自有启动器必须在放行前保留OS句柄，退出后仍能读取准确终态，不执行Office。
using ScpCv.Runtime.Processes;
using System.Diagnostics;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationProcessJobTests
{
    /// <summary>:returns: 快速退出不会丢失进程句柄/退出码，清理只作用于本次job。</summary>
    [Fact]
    public void SuspendedLaunchRetainsExitEvidenceAfterProcessTerminates()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var job = new PresentationProcessJob();
        using var process = job.Launch(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c exit 7", Environment.SystemDirectory);

        Assert.True(process.WaitForExit(5000), "自有cmd应在预算内退出。");
        Assert.True(process.HasExited);
        Assert.Equal(7, process.ExitCode);
        // cmd可携带短寿命conhost；单个launcher退出不要求整个job同一瞬间消失。
        var budget = Stopwatch.StartNew();
        while (job.HasRunningMembers(diagnostics: true) && budget.Elapsed < TimeSpan.FromSeconds(5)) Thread.Sleep(10);
        Assert.False(job.HasRunningMembers(diagnostics: true), "自有job未在退出预算内结束：" + job.DescribeMembers());
    }
}
