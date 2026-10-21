// 目录HTTP门禁与只读GET；默认Simulation绝不连接现场或启动媒体。
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class InputCatalogEndpointTests
{
    [Fact]
    public async Task AuthenticationAndCsrfProtectDiscovery()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        using var anonymous = await client.GetAsync("/api/inputs/"); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        _ = await ScreenShareEndpointTests.LoginAsync(client);
        using var noCsrf = await client.PostAsJsonAsync("/api/inputs/refresh/", new { force = true });
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<InputDiscovery>().Snapshot);
    }

    [Fact]
    public async Task GetDoesNotDiscoverOrWriteAndSimulationRefreshIsUnknown()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        var discovery = factory.Services.GetRequiredService<InputDiscovery>();
        Assert.IsType<SimulationInputReachabilityProbe>(factory.Services.GetRequiredService<IInputReachabilityProbe>());
        await using var db = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        var before = await db.MediaSources.CountAsync();
        using var get = await client.GetAsync("/api/inputs/"); Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var inputs = (await ScreenShareEndpointTests.JsonAsync(get)).GetProperty("inputs").EnumerateArray().ToArray();
        Assert.Equal(31, inputs.Length); Assert.DoesNotContain(inputs, item => item.GetProperty("ip_suffix").GetInt32() == 75);
        Assert.Equal("监控01", inputs.Single(item => item.GetProperty("ip_suffix").GetInt32() == 76).GetProperty("name").GetString());
        Assert.Equal("window_3", inputs.Single(item => item.GetProperty("ip_suffix").GetInt32() == 65).GetProperty("preview_input").GetProperty("kind").GetString());
        Assert.Empty(discovery.Snapshot); Assert.Equal(before, await db.MediaSources.CountAsync());
        using var refresh = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/inputs/refresh/", new { force = true });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = (await ScreenShareEndpointTests.JsonAsync(refresh)).GetProperty("inputs").EnumerateArray().ToArray();
        Assert.All(refreshed, item => { Assert.Equal("unknown", item.GetProperty("availability").GetString()); Assert.False(item.GetProperty("is_available").GetBoolean()); });
        Assert.Equal(24, await db.MediaSources.CountAsync(source => source.SystemKey.StartsWith("workstation:")));
        using var sources = await client.GetAsync("/api/sources/");
        Assert.DoesNotContain((await ScreenShareEndpointTests.JsonAsync(sources)).GetProperty("sources").EnumerateArray(), item => item.GetProperty("path").GetString()!.StartsWith("/工作站/", StringComparison.Ordinal));
    }
}
