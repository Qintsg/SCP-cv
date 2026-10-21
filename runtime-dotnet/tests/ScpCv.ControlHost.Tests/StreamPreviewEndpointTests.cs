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
        foreach (var input in new[] { new { kind = "ip_stream", ip_address = "192.168.5.75" }, new { kind = "ip_stream", ip_address = "127.0.0.1" } })
        {
            using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { input });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
        Assert.Equal(0, factory.Services.GetRequiredService<StreamPreviewRegistry>().LeaseCount);
    }

    [Fact]
    public async Task AliasesReceiveOpaqueSharedIdentityAndNativeResolutionNeverCreatesLeaseAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var created = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { input = new { kind = "window_3" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ScreenShareEndpointTests.JsonAsync(created);
        var key = body.GetProperty("sharing_key").GetString(); Assert.Matches("^[0-9a-f]{64}$", key!);
        using var native = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/native/resolve/", new { input = new { kind = "ip_stream", ip_address = "192.168.5.65" } });
        Assert.Equal(HttpStatusCode.OK, native.StatusCode); Assert.Equal("no-store", native.Headers.CacheControl!.ToString());
        var descriptor = await ScreenShareEndpointTests.JsonAsync(native);
        Assert.Equal(key, descriptor.GetProperty("sharing_key").GetString());
        Assert.Equal("rtmp://192.168.5.65:1935/live/chn1", descriptor.GetProperty("uri").GetString());
        Assert.True(descriptor.GetProperty("expires_at").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        var registry = factory.Services.GetRequiredService<StreamPreviewRegistry>(); Assert.Equal(1, registry.LeaseCount); Assert.Equal(0, registry.FeedCount);
        var nextCsrf = await ScreenShareEndpointTests.LoginAsync(client);
        using var next = await ScreenShareEndpointTests.MutateAsync(client, nextCsrf, HttpMethod.Post, "/api/stream-previews/native/resolve/", new { input = new { kind = "window_3" } });
        Assert.NotEqual(key, (await ScreenShareEndpointTests.JsonAsync(next)).GetProperty("sharing_key").GetString());
    }

    [Fact]
    public async Task SharedHeartbeatMovesValidationToStillCurrentAliasWithoutChangingActualStreamAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = contexts.CreateDbContext();
        var source = new MediaSource { Name = "共享修订合成来源", EntryName = "shared-revision-fixture", SourceType = MediaSourceType.CustomStream,
            Uri = "rtmp://192.168.5.63:1935/live/chn1", SourceRevision = 1, CreatedAt = DateTimeOffset.UtcNow };
        database.MediaSources.Add(source); await database.SaveChangesAsync();
        async Task<Guid> Create(object body)
        {
            using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await ScreenShareEndpointTests.JsonAsync(response)).GetProperty("id").GetGuid();
        }
        var consumer = await Create(new { source_id = source.Id });
        var originalAlias = await Create(new { source_id = source.Id });
        var windowAlias = await Create(new { input = new { kind = "window_1" } });
        var other = await Create(new { input = new { kind = "window_2" } });
        source.SourceRevision++; source.Uri = "rtmp://192.0.2.1/live/new"; await database.SaveChangesAsync();
        using var stale = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{consumer}/heartbeat/", new { aliases = new[] { originalAlias, windowAlias } });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var different = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{consumer}/heartbeat/", new { aliases = new[] { other } });
        Assert.Equal(HttpStatusCode.BadRequest, different.StatusCode);
        using var renewed = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{consumer}/heartbeat/", new { aliases = new[] { windowAlias } });
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        using var ordinary = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{consumer}/heartbeat/");
        Assert.Equal(HttpStatusCode.OK, ordinary.StatusCode);
        Assert.Equal(0, factory.Services.GetRequiredService<StreamPreviewRegistry>().FeedCount);
    }

    [Fact]
    public async Task SharedHeartbeatRejectsUnboundedDuplicateForeignAndMalformedAliasesAsync()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient(); using var second = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client); var otherCsrf = await ScreenShareEndpointTests.LoginAsync(second);
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, "/api/stream-previews/", new { input = new { kind = "window_1" } });
        var id = (await ScreenShareEndpointTests.JsonAsync(response)).GetProperty("id").GetGuid();
        using var foreignResponse = await ScreenShareEndpointTests.MutateAsync(second, otherCsrf, HttpMethod.Post, "/api/stream-previews/", new { input = new { kind = "window_1" } });
        var foreignId = (await ScreenShareEndpointTests.JsonAsync(foreignResponse)).GetProperty("id").GetGuid();
        foreach (var aliases in new[] { Array.Empty<Guid>(), new[] { id, id }, Enumerable.Range(0, 65).Select(_ => Guid.NewGuid()).ToArray(), new[] { id } })
        {
            using var invalid = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{id}/heartbeat/", new { aliases });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var foreign = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{id}/heartbeat/", new { aliases = new[] { foreignId } });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var unknown = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post, $"/api/stream-previews/{id}/heartbeat/", new { aliases = new[] { Guid.NewGuid() }, extra = true });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(0, factory.Services.GetRequiredService<StreamPreviewRegistry>().FeedCount);
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
