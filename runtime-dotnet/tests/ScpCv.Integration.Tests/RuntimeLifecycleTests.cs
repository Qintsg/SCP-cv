// 运行组停机闩锁、进程归属和协作退出回归。
using System.Diagnostics;
using ScpCv.Domain.Model;
using ScpCv.Integration.Tests.Fixtures;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Supervisor.Processes;

namespace ScpCv.Integration.Tests;

public sealed class RuntimeLifecycleTests
{
    [Fact]
    public async Task CompleteStopIsIdempotentForTheCurrentStoppedEpoch()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var stopped = await fixture.RuntimeAuthority.GetGroupAsync();

        var confirmed = await fixture.RuntimeAuthority.CompleteStopAsync(stopped.GroupEpoch);

        Assert.Equal(RuntimeGroupState.Stopped, confirmed.State);
        Assert.Equal(stopped.GroupEpoch, confirmed.GroupEpoch);
    }

    [Fact]
    public async Task StopLatchRejectsNewWorkerAuthorityUntilExplicitStart()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(Guid.NewGuid());
        await fixture.RuntimeAuthority.ArmAsync(starting.ExplicitStartRequestId!.Value, starting.GroupEpoch);
        await fixture.RuntimeAuthority.BeginDrainAsync("test-stop");
        await Assert.ThrowsAsync<RuntimeAuthorityException>(() => fixture.RuntimeAuthority.RegisterWorkerAsync(new(
            CommandTargetKind.Display, 1, Guid.NewGuid(), 1, DateTimeOffset.UtcNow, 1, starting.GroupEpoch, "[]")));
    }

    [Fact]
    public void ProcessRegistryRequiresPidStartTimeAndSessionEvidence()
    {
        var registry = new ProcessRegistry();
        using var current = Process.GetCurrentProcess();
        var owned = registry.Register("test", current);
        Assert.True(ProcessRegistry.StillOwns(owned));
        Assert.False(ProcessRegistry.StillOwns(owned with { StartTime = owned.StartTime.AddMilliseconds(1) }));
    }

    [Fact]
    public async Task StopTerminatesOwnedPowerPointHostWithoutKillingItsProcessTree()
    {
        using var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -Command Start-Sleep -Seconds 60")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("无法启动测试子进程。");
        var registry = new ProcessRegistry();
        registry.Register("office", process);
        try
        {
            await new ScpCv.Supervisor.Runtime.ShutdownCoordinator(registry, TimeSpan.FromMilliseconds(50)).StopAsync();
            Assert.True(process.HasExited);
        }
        finally
        {
            if (!process.HasExited) process.Kill();
        }
    }
}
