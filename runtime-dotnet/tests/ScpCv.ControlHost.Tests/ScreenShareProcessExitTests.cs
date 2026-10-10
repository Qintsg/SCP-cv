// 原进程观察的身份/取消/退出回归；Simulation只替代OS观察，使用真实Program与SQLite清理路径。
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using ScpCv.ControlHost.Ipc;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareProcessExitTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GroupStopAndWorkerReplacementWaitForOriginalVerifiedProcessExit(bool confirmed)
    {
        var observer = new ControlledExitObserver();
        using var root = new ControlHostApplicationFactory();
        using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IRuntimeProcessExitObserver>(observer);
            foreach (var item in services.Where(item => item.ServiceType == typeof(IHostedService) && item.ImplementationType == typeof(ScreenShareCleanupService)).ToArray())
                services.Remove(item);
        }));
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var identity = new RegisteredProcessIdentity(41021, DateTimeOffset.UtcNow, 1, "player-1", target.WorkerInstanceId);
        host.Services.GetRequiredService<IRegisteredProcessRegistry>().Register(identity);
        await using (var seed = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext())
        {
            var worker = await seed.WorkerOwnerships.SingleAsync(item => item.TargetId == 1);
            worker.ProcessId = identity.ProcessId; worker.ProcessStartTime = identity.ProcessStartTime; worker.LogonSessionId = identity.LogonSessionId;
            await seed.SaveChangesAsync();
        }
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var share = await service.CreateAsync("owner", 1, false);
        share = await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        Assert.Equal(identity, Assert.Single(observer.Identities));
        await using (var seed = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext())
        {
            var group = await seed.RuntimeGroupControls.SingleAsync(); group.State = RuntimeGroupState.Draining; group.GroupEpoch++;
            var worker = await seed.WorkerOwnerships.SingleAsync(item => item.TargetId == 1); worker.WorkerInstanceId = Guid.NewGuid(); worker.OwnerEpoch++;
            await seed.SaveChangesAsync();
        }
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("owner", share.Id).State);
        observer.Complete(new(confirmed, confirmed ? "process_exited" : "process_identity_unverifiable"));
        await observer.Returned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        // 等待观察器完成后的实际闭环，而不是用group/worker变化作死亡证明。
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        do
        {
            await service.CleanupOnceAsync();
            if (!confirmed || registry.GetOwned("owner", share.Id).State == ScreenShareState.Closed) break;
            await Task.Delay(10);
        } while (DateTimeOffset.UtcNow < deadline);
        Assert.Equal(confirmed ? ScreenShareState.Closed : ScreenShareState.Stopping, registry.GetOwned("owner", share.Id).State);
        Assert.Equal(confirmed ? 0 : 1, registry.ActiveCount);
    }

    [Theory]
    [InlineData("unregistered")]
    [InlineData("instance")]
    [InlineData("start")]
    [InlineData("session")]
    [InlineData("role")]
    public async Task UnregisteredOrWrongProcessIdentityCannotSupplyExitEvidence(string mismatch)
    {
        var observer = new ControlledExitObserver();
        using var root = new ControlHostApplicationFactory();
        using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IRuntimeProcessExitObserver>(observer)));
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var start = DateTimeOffset.UtcNow;
        await using (var seed = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext())
        {
            var worker = await seed.WorkerOwnerships.SingleAsync(item => item.TargetId == 1); worker.ProcessId = 41022; worker.ProcessStartTime = start; worker.LogonSessionId = 1;
            await seed.SaveChangesAsync();
        }
        if (mismatch != "unregistered") host.Services.GetRequiredService<IRegisteredProcessRegistry>().Register(new(41022,
            mismatch == "start" ? start.AddDays(-1) : start, mismatch == "session" ? 2 : 1, mismatch == "role" ? "player-2" : "player-1",
            mismatch == "instance" ? Guid.NewGuid() : target.WorkerInstanceId));
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var share = await service.CreateAsync("owner", 1, false); await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        await service.StopAsync("owner", share.Id); await service.CleanupOnceAsync();
        Assert.Empty(observer.Identities);
        Assert.Equal(ScreenShareState.Stopping, host.Services.GetRequiredService<ScreenShareRegistry>().GetOwned("owner", share.Id).State);
    }

    [Fact]
    public async Task CancellingOriginalProcessWatchNeverConfirmsNativeRelease()
    {
        var observer = new ControlledExitObserver();
        using var root = new ControlHostApplicationFactory();
        using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IRuntimeProcessExitObserver>(observer)));
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var identity = new RegisteredProcessIdentity(41023, DateTimeOffset.UtcNow, 1, "player-1", target.WorkerInstanceId);
        host.Services.GetRequiredService<IRegisteredProcessRegistry>().Register(identity);
        await using (var seed = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext())
        {
            var worker = await seed.WorkerOwnerships.SingleAsync(item => item.TargetId == 1);
            worker.ProcessId = identity.ProcessId; worker.ProcessStartTime = identity.ProcessStartTime; worker.LogonSessionId = identity.LogonSessionId;
            await seed.SaveChangesAsync();
        }
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var share = await service.CreateAsync("owner", 1, false); await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        Assert.Single(observer.Identities);
        await service.StopAsync("owner", share.Id);
        host.Services.GetRequiredService<ScreenShareProcessTracker>().Dispose();
        await observer.Returned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        observer.Complete(new(true, "late_result_after_cancel"));
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Stopping, host.Services.GetRequiredService<ScreenShareRegistry>().GetOwned("owner", share.Id).State);
    }

    private sealed class ControlledExitObserver : IRuntimeProcessExitObserver
    {
        private readonly TaskCompletionSource<RuntimeProcessExitEvidence> _evidence = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<RegisteredProcessIdentity> Identities { get; } = new();
        internal TaskCompletionSource Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(RuntimeProcessExitEvidence evidence) => _evidence.TrySetResult(evidence);
        /// <summary>:returns: 只在明确完成OS替身证据时返回；取消不返回confirmed。</summary>
        public async Task<RuntimeProcessExitEvidence> WaitForExitAsync(RegisteredProcessIdentity identity, CancellationToken cancellationToken)
        {
            Identities.Enqueue(identity);
            try { return await _evidence.Task.WaitAsync(cancellationToken); }
            finally { Returned.TrySetResult(); }
        }
    }
}
