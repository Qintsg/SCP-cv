// 动态共享经正式Worker身份门禁到认证HTTP/SSE；不启动原生播放器，不当实体出画证明。SCP-cv 2026-10-11。
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenSharePlaybackProjectionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DesiredSharingIsTemporaryAndOnlyMatchingWorkerFramesBecomeActual(int window)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var target = await factory.Services.GetRequiredService<RuntimeStateService>().GetScreenShareTargetAsync(window);
        var id = await ScreenShareEndpointTests.CreateAsync(client, csrf, window);
        Assert.Equal(JsonValueKind.Null, (await ReadAsync(client, window)).GetProperty("share_id").ValueKind);
        var generation = await OfferAsync(client, csrf, id);
        var pending = await ReadAsync(client, window);
        Assert.Equal(id, pending.GetProperty("share_id").GetGuid());
        Assert.Equal("screen_share", pending.GetProperty("source_type").GetString());
        Assert.Equal(JsonValueKind.Null, pending.GetProperty("source_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, pending.GetProperty("actual_share_id").ValueKind);
        Assert.Equal(0, pending.GetProperty("actual_frames").GetInt64());
        Assert.True((await ReportAsync(factory, target, generation, ActualState(id, 73))).Accepted);
        var actual = await ReadAsync(client, window);
        AssertActual(actual, id, 73);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var updates = factory.Services.GetRequiredService<SseEventHub>().ReadAsync(0, cancellation.Token).GetAsyncEnumerator();
        Assert.True(await updates.MoveNextAsync());
        using var snapshot = JsonDocument.Parse(updates.Current.Data);
        AssertActual(snapshot.RootElement.GetProperty("sessions").EnumerateArray().Single(item => item.GetProperty("window_id").GetInt32() == window), id, 73);
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        // 内置摄像机是永久目录项；临时共享不得增加任何普通/临时媒体项。
        var catalog = await database.MediaSources.ToArrayAsync();
        Assert.Equal(6, catalog.Length);
        Assert.All(catalog, source => Assert.StartsWith("camera:", source.SystemKey));
        Assert.DoesNotContain(catalog, source => source.IsTemporary || source.StreamIdentifier.Length > 0);
        Assert.DoesNotContain("receiver_uri", actual.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("sdp", actual.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wrong-id")]
    [InlineData("negative-frames")]
    [InlineData("missing-audio")]
    [InlineData("permanent-source")]
    public async Task MalformedOrWrongSharingEvidenceCannotInventActualIdentity(string kind)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var id = await ScreenShareEndpointTests.CreateAsync(client, csrf, 1);
        var generation = await OfferAsync(client, csrf, id);
        var state = new Dictionary<string, object?>
        {
            ["source_id"] = kind == "permanent-source" ? 42L : null,
            ["adapter_kind"] = "screen_share", ["playback_state"] = "playing",
            ["share_id"] = kind == "wrong-id" ? Guid.NewGuid() : id,
            ["actual_frames"] = kind == "negative-frames" ? -1L : 20L,
        };
        if (kind != "missing-audio") state["actual_audio"] = false;
        Assert.True((await ReportAsync(factory, target, generation, state)).Accepted);
        var actual = await ReadAsync(client, 1);
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("actual_share_id").ValueKind);
        Assert.Equal(0, actual.GetProperty("actual_frames").GetInt64());
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("actual_audio").ValueKind);
    }

    [Fact]
    public async Task FencedWorkerAndGenerationCannotOverwriteFramesAndOrdinaryReplacementClearsSharing()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await ScreenShareEndpointTests.LoginAsync(client);
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var id = await ScreenShareEndpointTests.CreateAsync(client, csrf, 1);
        var generation = await OfferAsync(client, csrf, id);
        Assert.True((await ReportAsync(factory, target, generation, ActualState(id, 73))).Accepted);
        Assert.False((await ReportAsync(factory, target with { WorkerInstanceId = Guid.NewGuid() }, generation, ActualState(id, 900))).Accepted);
        Assert.False((await ReportAsync(factory, target, generation - 1, ActualState(id, 900))).Accepted);
        AssertActual(await ReadAsync(client, 1), id, 73);
        await factory.Services.GetRequiredService<RuntimeStateService>().CloseAsync(1);
        var closing = await ReadAsync(client, 1);
        Assert.Equal(JsonValueKind.Null, closing.GetProperty("share_id").ValueKind);
        Assert.Equal(id, closing.GetProperty("actual_share_id").GetGuid());
        Assert.True((await ReportAsync(factory, target, generation + 1, new
        { source_id = (long?)null, adapter_kind = "", playback_state = "idle", share_id = (Guid?)null, actual_frames = 0L, actual_audio = false })).Accepted);
        var closed = await ReadAsync(client, 1);
        Assert.Equal(JsonValueKind.Null, closed.GetProperty("actual_share_id").ValueKind);
        Assert.Equal(0, closed.GetProperty("actual_frames").GetInt64());
    }

    /// <summary>:param id: 自有预约。:returns: 动态OPEN分配代次，不代替实际Worker帧。</summary>
    private static async Task<long> OfferAsync(HttpClient client, string csrf, Guid id)
    {
        using var response = await ScreenShareEndpointTests.MutateAsync(client, csrf, HttpMethod.Post,
            $"/api/screen-shares/{id}/offer/", new { type = "offer", sdp = "v=0\r\n" });
        response.EnsureSuccessStatusCode();
        return (await ScreenShareEndpointTests.JsonAsync(response)).GetProperty("share").GetProperty("source_generation").GetInt64();
    }

    /// <summary>:returns: 已认证正式状态报告入口的结果；不直接设置实际数据库字段。</summary>
    private static Task<StateReportAcceptance> ReportAsync(ControlHostApplicationFactory factory, ScreenShareTargetIdentity target,
        long generation, object state) => factory.Services.GetRequiredService<RuntimeProjectionPublisher>()
        .ApplyStateReportAsync(CommandTargetKind.Display, target.WindowId, target.WorkerInstanceId, target.OwnerEpoch,
            new StateReportDto { SourceGeneration = generation, State = JsonSerializer.SerializeToElement(state) });

    /// <summary>:returns: 原生合同同形的纯值观测，不含cap或媒体。</summary>
    private static object ActualState(Guid id, long frames) => new
    {
        source_id = (long?)null, adapter_kind = "screen_share", playback_state = "playing", share_id = id,
        actual_frames = frames, actual_audio = false, position_ms = (long?)null, duration_ms = (long?)null,
    };

    /// <summary>:param window: 明确逻辑目标。:returns: 正常认证HTTP合同字段。</summary>
    private static async Task<JsonElement> ReadAsync(HttpClient client, int window)
    {
        using var response = await client.GetAsync($"/api/sessions/{window}/");
        response.EnsureSuccessStatusCode();
        return (await ScreenShareEndpointTests.JsonAsync(response)).GetProperty("session").Clone();
    }

    /// <summary>:returns: 断言临时身份与真实帧，同时拒绝文件时钟伪投影。</summary>
    private static void AssertActual(JsonElement session, Guid id, long frames)
    {
        Assert.Equal(id, session.GetProperty("actual_share_id").GetGuid());
        Assert.Equal(frames, session.GetProperty("actual_frames").GetInt64());
        Assert.False(session.GetProperty("actual_audio").GetBoolean());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("actual_source_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, session.GetProperty("position_ms").ValueKind);
        Assert.Equal(JsonValueKind.Null, session.GetProperty("duration_ms").ValueKind);
    }
}
