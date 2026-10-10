// 重复停止、两目标隔离与提交后唤醒失败回归；真实SQLite但不启动Player，不当原生释放证明。SCP-cv 2026-10-11。
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareLifecycleTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ConcurrentRepeatedStopsRevokeOnlyOriginalGrantAndQueueOneClose(int window)
    {
        using var factory = new ControlHostApplicationFactory();
        await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var runtime = factory.Services.GetRequiredService<RuntimeStateService>();
        var service = factory.Services.GetRequiredService<ScreenShareService>();
        var registry = factory.Services.GetRequiredService<ScreenShareRegistry>();
        var target = await runtime.GetScreenShareTargetAsync(window);
        var otherWindow = window == 1 ? 2 : 1;
        var otherTarget = await runtime.GetScreenShareTargetAsync(otherWindow);
        var first = await service.CreateAsync("original-owner", window, false);
        first = await service.OfferAsync("original-owner", first.Id, "offer", "v=0\r\n");
        var other = await service.CreateAsync("other-owner", otherWindow, false);
        other = await service.OfferAsync("other-owner", other.Id, "offer", "v=0\r\n");
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => service.StopAsync("original-owner", first.Id)));
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("original-owner", first.Id).State);
        Assert.Equal(ScreenShareState.Negotiating, registry.GetOwned("other-owner", other.Id).State);
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(first.Id, target, first.SourceGeneration!.Value));
        Assert.NotNull(registry.GetReceiverGrant(other.Id, otherTarget, other.SourceGeneration!.Value));
        var busy = Assert.Throws<ScreenShareException>(() => registry.Create("new-owner", target, false));
        Assert.Equal("share_window_busy", busy.Code);
        Assert.Equal(2, registry.ActiveCount);
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Equal(1, await database.CommandRecords.CountAsync(item => item.Command == "CLOSE" && item.TargetId == window));
        Assert.False(await database.CommandRecords.AnyAsync(item => item.Command == "CLOSE" && item.TargetId == otherWindow));
        Assert.Equal(first.SourceGeneration + 1, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == window)).DesiredGeneration);
        Assert.Equal(other.SourceGeneration, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == otherWindow)).DesiredGeneration);
    }

    [Fact]
    public async Task StopWakeFailureAfterCommitRemainsUnconfirmedAndRetryDoesNotDuplicateSideEffect()
    {
        var wake = new ControlledWakeNotifier();
        using var root = new ControlHostApplicationFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICommandWakeNotifier>();
            services.AddSingleton<ICommandWakeNotifier>(wake);
        }));
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var service = factory.Services.GetRequiredService<ScreenShareService>();
        var registry = factory.Services.GetRequiredService<ScreenShareRegistry>();
        var first = await service.CreateAsync("owner", 1, false);
        first = await service.OfferAsync("owner", first.Id, "offer", "v=0\r\n");
        wake.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => service.StopAsync("owner", first.Id));
        var stopped = registry.GetOwned("owner", first.Id);
        Assert.Equal(ScreenShareState.Stopping, stopped.State);
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(first.Id, target, first.SourceGeneration!.Value));
        var calls = wake.Calls;
        Assert.Equal(ScreenShareState.Stopping, (await service.StopAsync("owner", first.Id)).State);
        Assert.Equal(calls, wake.Calls);
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Equal(1, await database.CommandRecords.CountAsync(item => item.Command == "CLOSE"));
        Assert.Equal(first.SourceGeneration + 1, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).DesiredGeneration);
        Assert.Equal(0, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).ObservedGeneration);
    }

    private sealed class ControlledWakeNotifier : ICommandWakeNotifier
    {
        public bool Fail { get; set; }
        public int Calls { get; private set; }

        /// <summary>:returns: 可控外部唤醒失败，仅模拟提交之后的边界，不更改SQLite事务。</summary>
        public ValueTask WakeAsync(CommandWakeSignal signal, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (Fail) throw new IOException("fixture_stop_wake_failure");
            return ValueTask.CompletedTask;
        }
    }
}
