// 两种安全模式均经过Program真实装配，失败后撤销授权；不调用实体输出。SCP-cv 2026-10-10。
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ScpCv.ControlHost.Ipc;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareRegistrationTests
{
    [Theory]
    [InlineData("Simulation")]
    [InlineData("Hardware")]
    public void BothModesResolveRealShareServiceAndLeaseAuthority(string mode)
    {
        using var root = new ControlHostApplicationFactory();
        using var host = root.WithWebHostBuilder(builder => builder.UseSetting("SafetyMode", mode));
        Assert.NotNull(host.Services.GetRequiredService<ScreenShareService>());
        Assert.NotNull(host.Services.GetRequiredService<ScreenShareProcessTracker>());
        Assert.NotNull(host.Services.GetRequiredService<ScreenShareLeaseAuthority>());
        Assert.NotNull(host.Services.GetRequiredService<RuntimeMessageDispatcher>());
        Assert.Same(host.Services.GetRequiredService<ScreenShareRegistry>(), host.Services.GetRequiredService<ScreenShareRegistry>());
        Assert.True(host.Services.GetRequiredService<ScreenShareOptions>().ReceiverBaseUri.IsLoopback);
        Assert.Single(host.Services.GetServices<IHostedService>().OfType<ScreenShareCleanupService>());
    }

    [Fact]
    public async Task WakeFailureAfterCommitRevokesCapWithoutPretendingDatabaseWasRolledBack()
    {
        using var root = new ControlHostApplicationFactory();
        using var host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICommandWakeNotifier>();
            services.AddSingleton<ICommandWakeNotifier, FailingWakeNotifier>();
        }));
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var service = host.Services.GetRequiredService<ScreenShareService>();
        var registry = host.Services.GetRequiredService<ScreenShareRegistry>();
        var share = await service.CreateAsync("test-owner", 1, false);
        await Assert.ThrowsAsync<IOException>(() => service.OfferAsync("test-owner", share.Id, "offer", "v=0\r\n"));
        var stopped = registry.GetOwned("test-owner", share.Id);
        Assert.Equal(ScreenShareState.Stopping, stopped.State);
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(share.Id, target, stopped.SourceGeneration!.Value));
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Equal(1, await database.CommandRecords.CountAsync(item => item.Command == "OPEN"));
    }

    private sealed class FailingWakeNotifier : ICommandWakeNotifier
    {
        public ValueTask WakeAsync(CommandWakeSignal signal, CancellationToken cancellationToken = default) =>
            throw new IOException("fixture_wake_failure");
    }
}
