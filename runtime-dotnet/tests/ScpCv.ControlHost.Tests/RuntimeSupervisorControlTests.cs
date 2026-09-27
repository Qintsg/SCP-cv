// Supervisor 调用必须先通知当前受管 Worker 协作退出，再执行精确进程兜底。
using System.Diagnostics;
using ScpCv.ControlHost.Ipc;
using ScpCv.ControlHost.Runtime;

namespace ScpCv.ControlHost.Tests;

public sealed class RuntimeSupervisorControlTests
{
    [Theory]
    [InlineData("stop", 1)]
    [InlineData("restart", 1)]
    [InlineData("status", 0)]
    public async Task StopAndRestartNotifyAuthenticatedWorkers(string action, int expectedNotices)
    {
        if (!OperatingSystem.IsWindows()) return;
        var notifier = new RecordingShutdownNotifier();
        var control = new RuntimeSupervisorControl(new RuntimeSupervisorOptions
        {
            ExecutablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "where.exe"),
            RuntimeRoot = AppContext.BaseDirectory,
            StatePath = Path.Combine(Path.GetTempPath(), $"scp-cv-stop-{Guid.NewGuid():N}.json"),
        }, shutdownNotifier: notifier);

        await control.LaunchAsync(action);

        Assert.Equal(expectedNotices, notifier.Reasons.Count);
        if (expectedNotices > 0) Assert.Equal($"supervisor_{action}", notifier.Reasons[0]);
    }

    [Fact]
    public async Task StartFailsImmediatelyWhenSupervisorExitsBeforeWorkersAreReady()
    {
        if (!OperatingSystem.IsWindows()) return;

        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "where.exe");
        var control = new RuntimeSupervisorControl(
            new RuntimeSupervisorOptions
            {
                ExecutablePath = executable,
                RuntimeRoot = AppContext.BaseDirectory,
                StatePath = Path.Combine(Path.GetTempPath(), $"scp-cv-{Guid.NewGuid():N}.json"),
                StartupTimeoutSeconds = 10,
            },
            readinessGate: new NeverReadyGate());

        var stopwatch = Stopwatch.StartNew();
        var result = await control.LaunchAsync("start", 1, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("supervisor_exited", result.Code);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private sealed class NeverReadyGate : IRuntimeReadinessGate
    {
        public async Task<RuntimeReadinessResult> WaitForRuntimeReadyAsync(
            long groupEpoch,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new RuntimeReadinessResult(false, ["player-1"]);
        }
    }

    private sealed class RecordingShutdownNotifier : IRuntimeShutdownNotifier
    {
        public List<string> Reasons { get; } = [];
        public Task NotifyRuntimeShutdownAsync(string reason, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reasons.Add(reason);
            return Task.CompletedTask;
        }
    }
}
