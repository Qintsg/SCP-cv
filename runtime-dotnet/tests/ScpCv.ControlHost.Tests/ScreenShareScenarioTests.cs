// 真实预案HTTP与SQLite回归：临时共享不可静默存成空窗口，也不可作为永久源恢复。
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Http;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Scenarios;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareScenarioTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CaptureRejectsDesiredOrStillActualShareWithoutChangingSavedScenario(bool replace, bool actualOnly)
    {
        using var host = new ControlHostApplicationFactory();
        using var client = host.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var original = await scenarios.CreateAsync(new("保留原预案", "不能覆盖", "unset", "single", "unset", 50, []));
        var shares = host.Services.GetRequiredService<ScreenShareService>();
        var share = await shares.CreateAsync("owner", 1, false);
        await shares.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        if (actualOnly)
        {
            await using var seed = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
            var session = await seed.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
            session.ActualAdapterKind = "screen_share";
            session.DesiredGeneration++;
            await seed.SaveChangesAsync();
        }
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/scenarios/capture/",
            new { name = "不可保存的共享", description = "临时", scenario_id = replace ? original.Id : (long?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ScreenShareEndpointTests.JsonAsync(response);
        Assert.Equal("scenario_screen_share_not_persistable", error.GetProperty("code").GetString());
        Assert.Contains("屏幕共享", error.GetProperty("detail").GetString(), StringComparison.Ordinal);
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        var saved = Assert.Single(await database.Scenarios.ToArrayAsync());
        Assert.Equal(original.Id, saved.Id); Assert.Equal("保留原预案", saved.Name); Assert.Equal("不能覆盖", saved.Description);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task SetTargetCannotPersistDynamicZeroOrInvalidMediaIdentity(long sourceId)
    {
        using var host = new ControlHostApplicationFactory();
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var error = await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.CreateAsync(new("临时源", "", "unset", "single", "unset", 50,
            [new ScenarioTargetDto { WindowId = 1, SourceState = "set", SourceId = sourceId }])));
        Assert.Equal("scenario_source_invalid", error.Code);
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Empty(await database.Scenarios.ToArrayAsync());
    }

    [Fact]
    public async Task UnsetActivationPreservesLiveShareAndNeverReopensIt()
    {
        using var host = new ControlHostApplicationFactory();
        await ScreenShareEndpointTests.SeedOnlineAsync(host);
        var shares = host.Services.GetRequiredService<ScreenShareService>();
        var share = await shares.CreateAsync("owner", 1, false);
        share = await shares.OfferAsync("owner", share.Id, "offer", "v=0\r\n");
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var scenario = await scenarios.CreateAsync(new("保持", "", "unset", "single", "unset", 50,
            [new ScenarioTargetDto { WindowId = 1, SourceState = "unset" }]));
        await scenarios.ActivateAsync(scenario.Id);
        await using var database = host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(share.SourceGeneration, session.DesiredGeneration);
        Assert.Equal("OPEN", Assert.Single(await database.CommandRecords.ToArrayAsync()).Command);
        using var args = JsonDocument.Parse((await database.CommandRecords.SingleAsync()).ArgsJson);
        Assert.Equal(share.Id, args.RootElement.GetProperty("share_id").GetGuid());
    }
}
