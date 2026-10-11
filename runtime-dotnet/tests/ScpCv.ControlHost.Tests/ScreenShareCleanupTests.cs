// 周期共享回收及原Worker释放证据回归；真实Program/SQLite，不启动Player、不假报实体释放。
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareCleanupTests
{
    [Fact]
    public async Task ConfirmedOriginalReleaseClearsActualIdentityEvenWhenSourceReportIsFenced()
    {
        using var root = new ControlHostApplicationFactory();
        using var host = Host(root, new CleanupClock());
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var runtime = host.Services.GetRequiredService<RuntimeStateService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var share = await service.CreateAsync("owner", 1, false);
        share = await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        var generation = share.SourceGeneration!.Value;
        Assert.True((await ReportAsync(host, target, generation, new
        {
            source_id = (long?)null, adapter_kind = "screen_share", playback_state = "playing",
            share_id = share.Id, actual_frames = 30, actual_audio = false,
        })).Accepted);
        Assert.Equal(share.Id, (await runtime.GetSessionAsync(1)).ActualShareId);
        await service.StopAsync("owner", share.Id);
        await service.CleanupOnceAsync();
        Assert.Equal(share.Id, (await runtime.GetSessionAsync(1)).ActualShareId);
        // 原资源Dispose回执可采信，旧源状态必须仍被围栏拒绝。
        Assert.False((await ReportAsync(host, target, generation, Proof(share.Id, generation))).Accepted);
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Closed, registry.GetOwned("owner", share.Id).State);
        var released = await runtime.GetSessionAsync(1);
        Assert.Null(released.ActualShareId);
        Assert.Equal(0, released.ActualFrames);
        Assert.Null(released.ActualAudio);
    }

    [Fact]
    public async Task RegisteredBackgroundTimerExpiresShareWithoutAnyPublisherGetOrManualCycle()
    {
        var clock = new CleanupClock();
        using var root = new ControlHostApplicationFactory();
        using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var share = await service.CreateAsync("owner", 1, false);
        share = await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        clock.Advance(TimeSpan.FromSeconds(21));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        var closedCommands = 0;
        do
        {
            await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
            closedCommands = await database.CommandRecords.CountAsync(item => item.Command == "CLOSE");
            if (closedCommands > 0) break;
            await Task.Delay(20);
        } while (DateTimeOffset.UtcNow < deadline);
        Assert.Equal(1, closedCommands);
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("owner", share.Id).State);
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(share.Id, target, share.SourceGeneration!.Value));
    }

    [Fact]
    public async Task ExpirationCycleQueuesOneCloseAndWaitsForActualReleaseWithoutPublisherPolling()
    {
        var clock = new CleanupClock();
        using var root = new ControlHostApplicationFactory();
        using var host = Host(root, clock);
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var share = await service.CreateAsync("owner", 1, false);
        share = await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        clock.Advance(TimeSpan.FromSeconds(21));
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("owner", share.Id).State);
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(share.Id, target, share.SourceGeneration!.Value));
        await service.CleanupOnceAsync();
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Equal(1, await database.CommandRecords.CountAsync(item => item.Command == "CLOSE"));
        Assert.Equal(0, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).ObservedGeneration);
        var stale = await ReportAsync(host, target, share.SourceGeneration!.Value, Proof(share.Id, share.SourceGeneration.Value));
        Assert.False(stale.Accepted, "源状态仍旧代次，不允许覆盖持久新意图；释放回执是另一证据");
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Closed, registry.GetOwned("owner", share.Id).State);
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public async Task ReplacementStateAloneIsNotReleaseProofAndOldCleanupNeverCancelsNewOpen()
    {
        using var root = new ControlHostApplicationFactory();
        using var host = Host(root, new CleanupClock());
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var runtime = host.Services.GetRequiredService<RuntimeStateService>();
        var share = await service.CreateAsync("owner", 1, false);
        share = await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        long sourceId;
        await using (var seed = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext())
        {
            var source = new MediaSource { Name = "新图", SourceType = MediaSourceType.Image, Uri = "fixture.png", CreatedAt = DateTimeOffset.UtcNow };
            seed.MediaSources.Add(source); await seed.SaveChangesAsync(); sourceId = source.Id;
        }
        await runtime.OpenSourceAsync(1, sourceId, true, 1);
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("owner", share.Id).State);
        var generation = share.SourceGeneration!.Value + 1;
        Assert.True((await ReportAsync(host, target, generation, new { source_id = sourceId, adapter_kind = "image", playback_state = "playing" })).Accepted);
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("owner", share.Id).State);
        Assert.True((await ReportAsync(host, target, generation, Proof(share.Id, share.SourceGeneration.Value))).Accepted);
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Closed, registry.GetOwned("owner", share.Id).State);
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.False(await database.CommandRecords.AnyAsync(item => item.Command == "CLOSE"));
        Assert.Equal(sourceId, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).MediaSourceId);
        Assert.Equal("OPEN", (await database.CommandRecords.SingleAsync(item => item.Status == CommandStatus.Pending)).Command);
    }

    [Theory]
    [InlineData("worker")]
    [InlineData("window")]
    [InlineData("share")]
    [InlineData("generation")]
    [InlineData("extra-field")]
    [InlineData("too-many")]
    [InlineData("invalid-type")]
    public async Task FencedOrMalformedReleaseCannotFreeAnotherResource(string mismatch)
    {
        using var root = new ControlHostApplicationFactory();
        using var host = Host(root, new CleanupClock());
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var share = await service.CreateAsync("owner", 1, false);
        share = await service.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        await service.StopAsync("owner", share.Id);
        object proof = new { share_id = mismatch == "share" ? Guid.NewGuid() : share.Id,
                             source_generation = mismatch == "generation" ? 999L : share.SourceGeneration!.Value };
        if (mismatch == "extra-field") proof = new { share_id = share.Id, source_generation = share.SourceGeneration!.Value, released = true };
        object state = mismatch == "invalid-type" ? new { released_screen_shares = "closed" } :
            new { released_screen_shares = Enumerable.Repeat(proof, mismatch == "too-many" ? 33 : 1).ToArray() };
        if (mismatch == "worker") target = target with { WorkerInstanceId = Guid.NewGuid() };
        if (mismatch == "window") target = await host.Services.GetRequiredService<RuntimeStateService>().GetScreenShareTargetAsync(2);
        await ReportAsync(host, target, 0, state);
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("owner", share.Id).State);
        Assert.Equal(1, registry.ActiveCount);
    }

    [Fact]
    public async Task RolledBackOpenBindingCanCloseWithoutInventingAReceiverExit()
    {
        using var root = new ControlHostApplicationFactory();
        using var host = Host(root, new CleanupClock());
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var share = await service.CreateAsync("owner", 1, false);
        registry.SetOffer("owner", share.Id, "offer", "v=0\r\n");
        await Assert.ThrowsAsync<IOException>(() => host.Services.GetRequiredService<RuntimeStateService>().OpenScreenShareAsync(target, share.Id,
            generation => { registry.BindReceiver("owner", share.Id, generation); throw new IOException("fixture_transaction_rollback"); }));
        registry.BeginStop("owner", share.Id, "open_failed");
        await service.CleanupOnceAsync();
        Assert.Equal(ScreenShareState.Closed, registry.GetOwned("owner", share.Id).State);
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Empty(await database.CommandRecords.ToArrayAsync());
    }

    /// <summary>:returns: 真实装配，仅移除周期调度以便显式控制每轮，业务和持久化不替换。</summary>
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(ControlHostApplicationFactory root, CleanupClock clock) =>
        root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            foreach (var descriptor in services.Where(item => item.ServiceType == typeof(IHostedService) && item.ImplementationType?.Name == "ScreenShareCleanupService").ToArray())
                services.Remove(descriptor);
        }));

    /// <summary>:returns: 原Worker管道报告形状；不包含cap、SDP或任何采集画面。</summary>
    private static object Proof(Guid id, long generation) => new { released_screen_shares = new[] { new { share_id = id, source_generation = generation } } };

    /// <summary>:returns: 经过正式持久归属检查的报告，不直接修改释放标志。</summary>
    private static Task<StateReportAcceptance> ReportAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host,
        ScreenShareTargetIdentity target, long generation, object state) => host.Services.GetRequiredService<RuntimeProjectionPublisher>()
        .ApplyStateReportAsync(CommandTargetKind.Display, target.WindowId, target.WorkerInstanceId, target.OwnerEpoch,
            new StateReportDto { SourceGeneration = generation, State = JsonSerializer.SerializeToElement(state) });

    private sealed class CleanupClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan duration) => _now += duration;
    }
}
