// 模拟丢失ControlHost内存授权而保留真实SQLite审计；只核条件清理，不声称进程或采集已退出。
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareRestartCleanupTests
{
    [Fact]
    public async Task LostRegistryQueuesOneConditionalCloseWithoutRestoringOrReplayingOldShare()
    {
        using var host = new ControlHostApplicationFactory();
        await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var original = host.Services.GetRequiredService<ScreenShareService>();
        var share = await original.CreateAsync("owner", 1, false);
        share = await original.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        var registry = new ScreenShareRegistry(host.Services.GetRequiredService<TimeProvider>());
        using var restarted = new ScreenShareService(registry, host.Services.GetRequiredService<RuntimeStateService>());
        await restarted.CleanupOnceAsync(); await restarted.CleanupOnceAsync();
        Assert.Equal(0, registry.ActiveCount);
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Equal(1, await database.CommandRecords.CountAsync(item => item.Command == "OPEN"));
        var close = Assert.Single(await database.CommandRecords.Where(item => item.Command == "CLOSE").ToArrayAsync());
        Assert.Equal(share.SourceGeneration + 1, close.SourceGeneration);
        Assert.Contains(share.Id.ToString("D"), close.ArgsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("receiver_uri", close.ArgsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LostRegistryDoesNotCloseLaterOrdinaryMediaIntent()
    {
        using var host = new ControlHostApplicationFactory();
        await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var shares = host.Services.GetRequiredService<ScreenShareService>();
        var share = await shares.CreateAsync("owner", 1, false);
        await shares.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        long sourceId;
        await using (var seed = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext())
        {
            var source = new MediaSource { Name = "新图", SourceType = MediaSourceType.Image, Uri = "fixture.png" };
            seed.MediaSources.Add(source); await seed.SaveChangesAsync(); sourceId = source.Id;
        }
        var runtime = host.Services.GetRequiredService<RuntimeStateService>();
        await runtime.OpenSourceAsync(1, sourceId, true, 1);
        using var restarted = new ScreenShareService(new(TimeProvider.System), runtime);
        await restarted.CleanupOnceAsync();
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.False(await database.CommandRecords.AnyAsync(item => item.Command == "CLOSE"));
        Assert.Equal(sourceId, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).MediaSourceId);
        Assert.Equal("OPEN", (await database.CommandRecords.SingleAsync(item => item.Status == CommandStatus.Pending)).Command);
    }
}
