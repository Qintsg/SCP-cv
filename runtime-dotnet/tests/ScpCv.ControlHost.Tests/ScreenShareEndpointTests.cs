// 分享HTTP认证、预约和信令回归；Simulation不代表真实播放器出画。SCP-cv 2026-10-10。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareEndpointTests
{
    [Fact]
    public async Task AuthenticationAndCsrfFailBeforeReservationOrPlayback()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var anonymous = await client.PostAsJsonAsync("/api/screen-shares/", new { window_id = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await LoginAsync(client);
        using var missingCsrf = await client.PostAsJsonAsync("/api/screen-shares/", new { window_id = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        Assert.Equal("csrf_failed", await CodeAsync(missingCsrf));
        Assert.Equal(0, factory.Services.GetRequiredService<ScreenShareRegistry>().ActiveCount);
        await AssertNoCommandsAsync(factory);
    }

    [Fact]
    public async Task OfflineAndInvalidTargetsHaveNoSideEffects()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await LoginAsync(client);
        using var offline = await MutateAsync(client, csrf, HttpMethod.Post, "/api/screen-shares/", new { window_id = 1, has_audio = false });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, offline.StatusCode);
        Assert.Equal("share_player_offline", await CodeAsync(offline));
        using var invalid = await MutateAsync(client, csrf, HttpMethod.Post, "/api/screen-shares/", new { window_id = 3 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("share_window_invalid", await CodeAsync(invalid));
        await AssertNoCommandsAsync(factory);
    }

    [Fact]
    public async Task ReservationAndUnopenedCancellationNeverQueueOpenOrClose()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await LoginAsync(client);
        await SeedOnlineAsync(factory);
        var id = await CreateAsync(client, csrf, 1);
        await AssertNoCommandsAsync(factory);
        using var conflict = await MutateAsync(client, csrf, HttpMethod.Post, "/api/screen-shares/", new { window_id = 1 });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var stop = await MutateAsync(client, csrf, HttpMethod.Delete, $"/api/screen-shares/{id}/");
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        Assert.Equal("closed", (await JsonAsync(stop)).GetProperty("share").GetProperty("state").GetString());
        using var repeated = await MutateAsync(client, csrf, HttpMethod.Delete, $"/api/screen-shares/{id}/");
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        await AssertNoCommandsAsync(factory);
        await CreateAsync(client, csrf, 1);
        await CreateAsync(client, csrf, 2);
        Assert.Equal(2, factory.Services.GetRequiredService<ScreenShareRegistry>().ActiveCount);
    }

    [Fact]
    public async Task OfferQueuesOnlyDynamicIdentityAndResponseIsNotFrameEvidence()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await LoginAsync(client);
        var target = await SeedOnlineAsync(factory);
        var id = await CreateAsync(client, csrf, 1);
        using var offered = await MutateAsync(client, csrf, HttpMethod.Post, $"/api/screen-shares/{id}/offer/", new { type = "offer", sdp = "v=0\r\n" });
        Assert.Equal(HttpStatusCode.Accepted, offered.StatusCode);
        var share = (await JsonAsync(offered)).GetProperty("share");
        Assert.Equal("negotiating", share.GetProperty("state").GetString());
        Assert.Equal(0, share.GetProperty("actual_frames").GetInt64());
        Assert.Equal(JsonValueKind.Null, share.GetProperty("answer").ValueKind);
        var generation = share.GetProperty("source_generation").GetInt64();
        var grant = factory.Services.GetRequiredService<ScreenShareRegistry>().GetReceiverGrant(id, target, generation);
        Assert.DoesNotContain(grant.Token, share.GetRawText(), StringComparison.Ordinal);
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        var command = await database.CommandRecords.SingleAsync();
        Assert.Equal("OPEN", command.Command);
        Assert.Equal(generation, command.SourceGeneration);
        Assert.DoesNotContain(grant.Token, command.ArgsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("receiver_uri", command.ArgsJson, StringComparison.Ordinal);
        Assert.Empty(await database.MediaSources.ToArrayAsync());
        using var stop = await MutateAsync(client, csrf, HttpMethod.Delete, $"/api/screen-shares/{id}/");
        Assert.Equal("stopping", (await JsonAsync(stop)).GetProperty("share").GetProperty("state").GetString());
        Assert.Throws<ScreenShareException>(() => factory.Services.GetRequiredService<ScreenShareRegistry>().GetReceiverGrant(id, target, generation));
        Assert.Equal(1, await database.CommandRecords.CountAsync(item => item.Command == "CLOSE"));
    }

    [Fact]
    public async Task RestartBetweenReservationAndOfferCannotOpenNewWorker()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await LoginAsync(client);
        await SeedOnlineAsync(factory);
        var id = await CreateAsync(client, csrf, 1);
        await using (var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext())
        {
            var worker = await database.WorkerOwnerships.SingleAsync(item => item.TargetId == 1);
            worker.WorkerInstanceId = Guid.NewGuid();
            worker.OwnerEpoch++;
            await database.SaveChangesAsync();
        }
        using var offered = await MutateAsync(client, csrf, HttpMethod.Post, $"/api/screen-shares/{id}/offer/", new { type = "offer", sdp = "v=0\r\n" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, offered.StatusCode);
        await AssertNoCommandsAsync(factory);
        Assert.Equal(0, factory.Services.GetRequiredService<ScreenShareRegistry>().ActiveCount);
    }

    [Fact]
    public async Task InvalidOrOversizedOfferDoesNotQueueOpenAndOtherOwnerIsHidden()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await LoginAsync(client);
        var target = await SeedOnlineAsync(factory);
        var id = await CreateAsync(client, csrf, 1);
        using var invalid = await MutateAsync(client, csrf, HttpMethod.Post, $"/api/screen-shares/{id}/offer/", new { type = "answer", sdp = "v=0\r\n" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var large = await MutateAsync(client, csrf, HttpMethod.Post, $"/api/screen-shares/{id}/offer/", new { type = "offer", sdp = "v=0\r\n" + new string('中', 22000) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        await AssertNoCommandsAsync(factory);
        using var stop = await MutateAsync(client, csrf, HttpMethod.Delete, $"/api/screen-shares/{id}/");
        var other = factory.Services.GetRequiredService<ScreenShareRegistry>().Create("other-owner", target, false);
        using var hidden = await client.GetAsync($"/api/screen-shares/{other.Id}/");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var hiddenStop = await MutateAsync(client, csrf, HttpMethod.Delete, $"/api/screen-shares/{other.Id}/");
        Assert.Equal(HttpStatusCode.NotFound, hiddenStop.StatusCode);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task WrongWindowJsonTypeIsBadRequestNotUnhandledException(string rawWindow)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await LoginAsync(client);
        using var document = JsonDocument.Parse("{\"window_id\":" + rawWindow + "}");
        using var response = await MutateAsync(client, csrf, HttpMethod.Post, "/api/screen-shares/", document.RootElement);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoCommandsAsync(factory);
    }

    internal static async Task<ScreenShareTargetIdentity> SeedOnlineAsync(WebApplicationFactory<Program> factory)
    {
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        var group = await database.RuntimeGroupControls.SingleAsync();
        group.State = RuntimeGroupState.Armed;
        group.GroupEpoch = 19;
        var first = new ScreenShareTargetIdentity(1, Guid.NewGuid(), 7, group.GroupEpoch);
        foreach (var window in new[] { 1, 2 })
        {
            database.WorkerOwnerships.Add(new WorkerOwnership
            {
                TargetKind = CommandTargetKind.Display, TargetId = window,
                WorkerInstanceId = window == 1 ? first.WorkerInstanceId : Guid.NewGuid(), OwnerEpoch = 7,
                Status = WorkerOwnershipState.Online, LastTransportHeartbeat = DateTimeOffset.UtcNow,
            });
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == window);
            session.PlayerLastSeenAt = DateTimeOffset.UtcNow;
            session.TargetDisplayLabel = $"模拟大屏{window}";
        }
        await database.SaveChangesAsync();
        return first;
    }

    internal static async Task<string> LoginAsync(HttpClient client)
    {
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        using var csrf = await client.GetAsync("/api/auth/csrf/");
        return (await JsonAsync(csrf)).GetProperty("csrfToken").GetString()!;
    }

    internal static async Task<Guid> CreateAsync(HttpClient client, string csrf, int window)
    {
        using var response = await MutateAsync(client, csrf, HttpMethod.Post, "/api/screen-shares/", new { window_id = window, has_audio = false });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        return (await JsonAsync(response)).GetProperty("share").GetProperty("id").GetGuid();
    }

    internal static async Task<HttpResponseMessage> MutateAsync(HttpClient client, string csrf, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add("X-CSRFToken", csrf);
        return await client.SendAsync(request);
    }

    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) => (await JsonAsync(response)).GetProperty("code").GetString();

    private static async Task AssertNoCommandsAsync(ControlHostApplicationFactory factory)
    {
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        Assert.Empty(await database.CommandRecords.ToArrayAsync());
    }
}
