// 正式认证HTTP门禁回归：默认Simulation，无真实转换器、设备连接或播放命令。
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ScpCv.ControlHost.StreamPreview;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewEndpointTests
{
    [Fact]
    public async Task AuthenticationCsrfAndOriginRejectBeforeUpstreamAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        using var anonymous = await client.PostAsJsonAsync("/api/stream-previews/", new { input = new { kind = "window_1" } });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var missing = await client.PostAsJsonAsync("/api/stream-previews/", new { input = new { kind = "window_1" } });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var forged = new HttpRequestMessage(HttpMethod.Post, "/api/stream-previews/") { Content = JsonContent.Create(new { input = new { kind = "window_1" } }) };
        forged.Headers.Add("Origin", "https://evil.test"); forged.Headers.Add("X-CSRFToken", csrf);
        using var rejected = await client.SendAsync(forged); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var registry = factory.Services.GetRequiredService<StreamPreviewRegistry>(); Assert.Equal(0, registry.FeedCount); Assert.Equal(0, registry.LeaseCount);
    }

    [Fact]
    public async Task CreatingLeaseNeverPullsAndSimulationCannotPretendFirstFrameAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var created = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { input = new { kind = "window_1" }, quality = "low" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ScreenShareEndpointTests.JsonAsync(created); var id = body.GetProperty("id").GetGuid();
        Assert.DoesNotContain("192.168", body.ToString(), StringComparison.Ordinal);
        var registry = factory.Services.GetRequiredService<StreamPreviewRegistry>(); Assert.Equal(0, registry.FeedCount);
        using var stream = await client.GetAsync($"/api/stream-previews/{id}/stream.flv");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, stream.StatusCode); Assert.Equal(0, registry.LeaseCount);
        using var foreign = await client.GetAsync($"/api/stream-previews/{Guid.NewGuid()}/stream.flv");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task SameAccountReloginChangesLeaseAuthorityEvenWhenCsrfIsUnchangedAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var created = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { input = new { kind = "window_1" } });
        var id = (await ScreenShareEndpointTests.JsonAsync(created)).GetProperty("id").GetGuid();
        var nextCsrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var heartbeat = await ScreenShareEndpointTests.MutateAsync(client, nextCsrf, HttpMethod.Post, $"/api/stream-previews/{id}/heartbeat/");
        Assert.Equal(HttpStatusCode.NotFound, heartbeat.StatusCode);
        Assert.Equal(0, factory.Services.GetRequiredService<StreamPreviewRegistry>().FeedCount);
    }

    [Fact]
    public async Task UnknownUrlAndReservedOrUnlistedInputNeverBecomeProxyAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var url = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { url = "rtmp://192.0.2.1/private" });
        Assert.Equal(HttpStatusCode.BadRequest, url.StatusCode);
        foreach (var input in new[] { new { kind = "window_3", ip_address = "" }, new { kind = "ip_stream", ip_address = "127.0.0.1" } })
        {
            using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { input });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
        Assert.Equal(0, factory.Services.GetRequiredService<StreamPreviewRegistry>().LeaseCount);
    }

    [Fact]
    public async Task WebSocketOriginAndUpgradeAreRequiredBeforeConsumingLeaseAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var created = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { input = new { kind = "window_1" } });
        var id = (await ScreenShareEndpointTests.JsonAsync(created)).GetProperty("id").GetGuid();
        using var ordinary = await client.GetAsync($"/api/stream-previews/{id}/stream.ws"); Assert.Equal(HttpStatusCode.Forbidden, ordinary.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/stream-previews/{id}/stream.ws"); request.Headers.Add("Origin", "https://evil.test");
        using var rejected = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(0, factory.Services.GetRequiredService<StreamPreviewRegistry>().FeedCount);
    }

    [Fact]
    public async Task SourceRevisionChangeInvalidatesHeartbeatWithoutOpeningAnyStreamAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        var source = new MediaSource { Name = "合成来源", EntryName = "preview-fixture", SourceType = MediaSourceType.CustomStream,
            Uri = "rtmp://192.0.2.1/live/one", SourceRevision = 1, CreatedAt = DateTimeOffset.UtcNow };
        database.MediaSources.Add(source); await database.SaveChangesAsync();
        using var created = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { source_id = source.Id });
        var id = (await ScreenShareEndpointTests.JsonAsync(created)).GetProperty("id").GetGuid();
        source.SourceRevision++; await database.SaveChangesAsync();
        using var heartbeat = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{id}/heartbeat/");
        Assert.Equal(HttpStatusCode.Gone, heartbeat.StatusCode);
        var registry = factory.Services.GetRequiredService<StreamPreviewRegistry>(); Assert.Equal(0, registry.FeedCount); Assert.Equal(0, registry.LeaseCount);
    }
}
