// 分享音频的认证、事务围栏和实际回读；纯值Worker报告不等于原生出声。SCP-cv 2026-10-11。
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.Ipc;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareAudioEndpointTests
{
    [Fact]
    public async Task AuthenticationAndCsrfPrecedeAudioMutation()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var path = $"/api/screen-shares/{Guid.NewGuid()}/audio/";
        using var anonymous = await ScreenShareEndpointTests.MutateAsync(client, "invalid", HttpMethod.Patch, path, new { volume = 29, muted = false });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await ScreenShareEndpointTests.LoginAsync(client);
        using var noCsrf = await client.PatchAsync(path, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        Assert.Equal("csrf_failed", (await ScreenShareEndpointTests.JsonAsync(noCsrf)).GetProperty("code").GetString());
        await AssertAudioCountAsync(factory, 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task QueuedAudioKeepsOriginalGenerationAndOnlyWorkerReceiptChangesActualValues(int window)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var (csrf, target, id, generation) = await ArrangeAsync(factory, client, window);
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Patch,
            $"/api/screen-shares/{id}/audio/", new { volume = 29, muted = false });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var submitted = (await ScreenShareEndpointTests.JsonAsync(response)).GetProperty("submitted_command_id").GetGuid();
        var before = await ReadSessionAsync(client, window);
        Assert.Equal(generation, before.GetProperty("desired_generation").GetInt64());
        Assert.Equal(100, before.GetProperty("actual_share_volume").GetInt32());
        Assert.True(before.GetProperty("actual_share_muted").GetBoolean());
        var lease = await ClaimAsync(factory, target);
        Assert.Equal(submitted, lease.CommandId);
        Assert.Equal("SCREEN_SHARE_AUDIO", lease.Command);
        Assert.Equal(generation, lease.SourceGeneration);
        Assert.Equal(4, lease.Args.Count);
        Assert.False(lease.Args.ContainsKey("receiver_uri"));
        Assert.Equal(id, lease.Args["share_id"].GetGuid());
        Assert.Equal(window, lease.Args["window_id"].GetInt32());
        Assert.Equal(29, lease.Args["volume"].GetInt32());
        await CompleteAsync(factory, target, lease, id, 29, false);
        var actual = await ReadSessionAsync(client, window);
        Assert.Equal(29, actual.GetProperty("actual_share_volume").GetInt32());
        Assert.False(actual.GetProperty("actual_share_muted").GetBoolean());
        Assert.Equal(generation, actual.GetProperty("observed_generation").GetInt64());
        using var shareResponse = await client.GetAsync($"/api/screen-shares/{id}/");
        var share = (await ScreenShareEndpointTests.JsonAsync(shareResponse)).GetProperty("share");
        Assert.Equal(29, share.GetProperty("actual_volume").GetInt32());
        Assert.False(share.GetProperty("actual_muted").GetBoolean());
        using var commandResponse = await client.GetAsync($"/api/playback/{window}/commands/{submitted}/");
        Assert.Equal("completed", (await ScreenShareEndpointTests.JsonAsync(commandResponse)).GetProperty("command").GetProperty("status").GetString());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var events = factory.Services.GetRequiredService<SseEventHub>().ReadAsync(0, timeout.Token).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());
        using var update = JsonDocument.Parse(events.Current.Data);
        var streamed = update.RootElement.GetProperty("sessions").EnumerateArray().Single(item => item.GetProperty("window_id").GetInt32() == window);
        Assert.Equal(29, streamed.GetProperty("actual_share_volume").GetInt32());
        Assert.False(streamed.GetProperty("actual_share_muted").GetBoolean());
        var other = await ReadSessionAsync(client, window == 1 ? 2 : 1);
        Assert.Equal(0, other.GetProperty("desired_generation").GetInt64());
        Assert.Equal(JsonValueKind.Null, other.GetProperty("actual_share_volume").ValueKind);
        await using var database = Context(factory);
        var command = await database.CommandRecords.SingleAsync(item => item.CommandId == submitted);
        Assert.Equal(TimeSpan.FromSeconds(3), command.Deadline!.Value - command.CreatedAt);
        Assert.DoesNotContain("receiver_uri", command.ArgsJson, StringComparison.Ordinal);
        // 启动已有六个内置摄像机；共享音频不能额外创建媒体源。
        Assert.Equal(6, await database.MediaSources.CountAsync());
        Assert.All(await database.MediaSources.ToArrayAsync(), source => Assert.StartsWith("camera:", source.SystemKey));
    }

    [Theory]
    [InlineData("{\"volume\":-1,\"muted\":false}")]
    [InlineData("{\"volume\":101,\"muted\":false}")]
    [InlineData("{\"volume\":1.5,\"muted\":false}")]
    [InlineData("{\"volume\":\"29\",\"muted\":false}")]
    [InlineData("{\"volume\":null,\"muted\":false}")]
    [InlineData("{\"volume\":29,\"muted\":\"false\"}")]
    [InlineData("{\"volume\":29}")]
    [InlineData("{\"muted\":false}")]
    [InlineData("{\"volume\":29,\"muted\":false,\"extra\":0}")]
    [InlineData("{\"volume\":29,\"volume\":80,\"muted\":false}")]
    public async Task InvalidAudioSchemaHasNoQueueOrIntentSideEffects(string json)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var (csrf, _, id, generation) = await ArrangeAsync(factory, client);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/screen-shares/{id}/audio/");
        request.Headers.Add("X-CSRFToken", csrf);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAudioCountAsync(factory, 0);
        Assert.Equal(generation, (await ReadSessionAsync(client, 1)).GetProperty("desired_generation").GetInt64());
    }

    [Theory]
    [InlineData("instance", false)]
    [InlineData("owner", false)]
    [InlineData("group", false)]
    [InlineData("offline", false)]
    [InlineData("instance", true)]
    [InlineData("owner", true)]
    [InlineData("group", true)]
    [InlineData("offline", true)]
    public async Task ChangedTargetCannotQueueAudio(string change, bool cleanupFirst)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var (csrf, _, id, _) = await ArrangeAsync(factory, client);
        await using (var database = Context(factory))
        {
            var worker = await database.WorkerOwnerships.SingleAsync(item => item.TargetId == 1);
            if (change == "instance") worker.WorkerInstanceId = Guid.NewGuid();
            if (change == "owner") worker.OwnerEpoch++;
            if (change == "offline") worker.Status = WorkerOwnershipState.Stale;
            if (change == "group") (await database.RuntimeGroupControls.SingleAsync()).GroupEpoch++;
            await database.SaveChangesAsync();
        }
        if (cleanupFirst) await factory.Services.GetRequiredService<ScreenShareService>().CleanupOnceAsync();
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Patch,
            $"/api/screen-shares/{id}/audio/", new { volume = 29, muted = false });
        // 原Worker失效先被事务发现是503；若真实后台先撤销分享则409。两者均严格核码和零排队。
        if (cleanupFirst) Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        else Assert.Contains(response.StatusCode, new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.Conflict });
        var code = (await ScreenShareEndpointTests.JsonAsync(response)).GetProperty("code").GetString();
        Assert.Equal(response.StatusCode == HttpStatusCode.ServiceUnavailable ? "share_player_offline" : "share_audio_unavailable", code);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            using var owned = await client.GetAsync($"/api/screen-shares/{id}/");
            Assert.Equal("stopping", (await ScreenShareEndpointTests.JsonAsync(owned)).GetProperty("share").GetProperty("state").GetString());
        }
        await AssertAudioCountAsync(factory, 0);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("allocated")]
    [InlineData("stopping")]
    [InlineData("replacement")]
    public async Task OwnershipAndCurrentShareAreRequired(string kind)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var (csrf, target, id, _) = await ArrangeAsync(factory, client);
        if (kind is "foreign" or "allocated")
        {
            var registry = factory.Services.GetRequiredService<ScreenShareRegistry>();
            var otherTarget = await factory.Services.GetRequiredService<RuntimeStateService>().GetScreenShareTargetAsync(2);
            if (kind == "foreign") id = registry.Create("other-owner", otherTarget, false).Id;
            else id = await ScreenShareEndpointTests.CreateAsync(client, csrf, otherTarget.WindowId);
        }
        if (kind == "stopping")
        {
            using var stopped = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Delete, $"/api/screen-shares/{id}/");
            stopped.EnsureSuccessStatusCode();
        }
        if (kind == "replacement") await factory.Services.GetRequiredService<RuntimeStateService>().CloseAsync(target.WindowId);
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Patch,
            $"/api/screen-shares/{id}/audio/", new { volume = 29, muted = false });
        Assert.Equal(kind == "foreign" ? HttpStatusCode.NotFound : HttpStatusCode.Conflict, response.StatusCode);
        await AssertAudioCountAsync(factory, 0);
    }

    [Fact]
    public async Task ReceiverFramesWithoutMatchingWorkerEvidenceCannotAuthorizeAudio()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var (csrf, target, id, generation) = await ArrangeAsync(factory, client);
        await factory.Services.GetRequiredService<RuntimeProjectionPublisher>().ApplyStateReportAsync(CommandTargetKind.Display,
            target.WindowId, target.WorkerInstanceId, target.OwnerEpoch, new StateReportDto
            {
                SourceGeneration = generation, State = JsonSerializer.SerializeToElement(new
                { source_id = (long?)null, adapter_kind = "screen_share", share_id = Guid.NewGuid(), actual_frames = 10L, actual_audio = true }),
            });
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Patch,
            $"/api/screen-shares/{id}/audio/", new { volume = 29, muted = false });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertAudioCountAsync(factory, 0);
    }

    [Fact]
    public async Task FailedNativeControlClearsOldReceiptInsteadOfKeepingRequestedOrPreviousAudio()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var (csrf, target, id, _) = await ArrangeAsync(factory, client);
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Patch,
            $"/api/screen-shares/{id}/audio/", new { volume = 29, muted = false });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var lease = await ClaimAsync(factory, target);
        await CompleteAsync(factory, target, lease, id, null, null, "failed");
        var actual = await ReadSessionAsync(client, 1);
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("actual_share_volume").ValueKind);
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("actual_share_muted").ValueKind);
        using var owned = await client.GetAsync($"/api/screen-shares/{id}/");
        var share = (await ScreenShareEndpointTests.JsonAsync(owned)).GetProperty("share");
        Assert.Equal(JsonValueKind.Null, share.GetProperty("actual_volume").ValueKind);
        Assert.Equal(JsonValueKind.Null, share.GetProperty("actual_muted").ValueKind);
    }

    /// <summary>:returns: 真实HTTP预约、原生同形接收报告和正式IPC完成；没有真实采集或声卡。</summary>
    private static async Task<(string Csrf, ScreenShareTargetIdentity Target, Guid Id, long Generation)> ArrangeAsync(
        ControlHostApplicationFactory factory, HttpClient client, int window = 1)
    {
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var target = await factory.Services.GetRequiredService<RuntimeStateService>().GetScreenShareTargetAsync(window);
        using var created = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post,
            "/api/screen-shares/", new { window_id = window, has_audio = true });
        var id = (await ScreenShareEndpointTests.JsonAsync(created)).GetProperty("share").GetProperty("id").GetGuid();
        using var offered = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post,
            $"/api/screen-shares/{id}/offer/", new { type = "offer", sdp = "v=0\r\n" });
        offered.EnsureSuccessStatusCode();
        var generation = (await ScreenShareEndpointTests.JsonAsync(offered)).GetProperty("share").GetProperty("source_generation").GetInt64();
        var registry = factory.Services.GetRequiredService<ScreenShareRegistry>();
        var grant = registry.GetReceiverGrant(id, target, generation);
        registry.SetAnswer(id, window, generation, grant.Token, "answer", "v=0\r\n");
        registry.ReportFrame(id, window, generation, grant.Token, 10, true);
        var open = await ClaimAsync(factory, target);
        await CompleteAsync(factory, target, open, id, 100, true);
        return (csrf, target, id, generation);
    }

    /// <summary>:returns: 原Worker领取的正式租约，不打印cap。</summary>
    private static async Task<CommandLeaseDto> ClaimAsync(ControlHostApplicationFactory factory, ScreenShareTargetIdentity target)
    {
        var frame = new IpcFrameDto
        {
            MessageType = "claim_request", InstanceId = target.WorkerInstanceId, OwnerEpoch = target.OwnerEpoch,
            Target = new IpcTargetDto { Kind = "display", Id = target.WindowId },
            Payload = JsonSerializer.SerializeToElement(new ClaimRequestDto { GroupEpoch = target.GroupEpoch }),
        };
        var response = await factory.Services.GetRequiredService<RuntimeMessageDispatcher>().DispatchAsync(frame);
        Assert.Equal("command_lease", response.MessageType);
        return response.Payload.Deserialize<CommandLeaseDto>()!;
    }

    /// <summary>:param volume: 原生同形实际值。:param muted: 实际静音值。:returns: 正式命令完成及已提交投影。</summary>
    private static async Task CompleteAsync(ControlHostApplicationFactory factory, ScreenShareTargetIdentity target,
        CommandLeaseDto lease, Guid id, int? volume, bool? muted, string status = "completed")
    {
        var frame = new IpcFrameDto
        {
            MessageType = "command_result", InstanceId = target.WorkerInstanceId, OwnerEpoch = target.OwnerEpoch,
            Target = new IpcTargetDto { Kind = "display", Id = target.WindowId },
            Payload = JsonSerializer.SerializeToElement(new CommandResultDto
            {
                CommandId = lease.CommandId, ClaimToken = lease.ClaimToken, OwnerEpoch = target.OwnerEpoch,
                Status = status, ResultCode = status == "completed" ? "ok" : "native_audio_failed",
                ActualState = JsonSerializer.SerializeToElement(new
                {
                    source_generation = lease.SourceGeneration, source_id = (long?)null, adapter_kind = "screen_share",
                    share_id = id, actual_frames = 10L, actual_audio = true, playback_state = "playing",
                    actual_share_volume = volume, actual_share_muted = muted,
                }),
            }),
        };
        var response = await factory.Services.GetRequiredService<RuntimeMessageDispatcher>().DispatchAsync(frame);
        Assert.Equal("result_accepted", response.MessageType);
        Assert.True(response.Payload.GetProperty("accepted").GetBoolean());
    }

    /// <summary>:returns: 正常认证HTTP的会话，不读取播放器对象。</summary>
    private static async Task<JsonElement> ReadSessionAsync(HttpClient client, int window)
    {
        using var response = await client.GetAsync($"/api/sessions/{window}/");
        response.EnsureSuccessStatusCode();
        return (await ScreenShareEndpointTests.JsonAsync(response)).GetProperty("session");
    }

    /// <summary>:returns: 自有测试数据库上下文。</summary>
    private static ControlDbContext Context(ControlHostApplicationFactory factory) =>
        factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();

    /// <summary>:param expected: 本轮实际持久命令数。:returns: 不以HTTP状态推断队列副作用。</summary>
    private static async Task AssertAudioCountAsync(ControlHostApplicationFactory factory, int expected)
    {
        await using var database = Context(factory);
        Assert.Equal(expected, await database.CommandRecords.CountAsync(item => item.Command == "SCREEN_SHARE_AUDIO"));
    }
}
