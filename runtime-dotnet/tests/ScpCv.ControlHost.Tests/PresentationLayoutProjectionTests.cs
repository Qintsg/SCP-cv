// 真实状态报告经身份/代次围栏、持久投影及认证HTTP验证实际文字布局；不启动原生引擎。
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class PresentationLayoutProjectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualLayoutIsReportedByHttpOnlyAfterWorkerObservation(bool fitText)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory);
        Assert.Equal(JsonValueKind.Null, (await ReadLayoutAsync(client)).ValueKind);
        Assert.True((await ReportAsync(factory, worker, 3, new
        { source_id = 48, adapter_kind = "libreoffice", playback_mode = "libreoffice", actual_fit_text = fitText })).Accepted);
        Assert.Equal(fitText, (await ReadLayoutAsync(client)).GetBoolean());
    }

    [Fact]
    public async Task FailedReplacementKeepsOldObservedLayoutAndLateGenerationCannotOverwriteIt()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory);
        Assert.True((await ReportAsync(factory, worker, 3, new
        { source_id = 48, adapter_kind = "libreoffice", actual_fit_text = true })).Accepted);
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using (var database = await contexts.CreateDbContextAsync())
        {
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
            session.DesiredGeneration = 4;
            session.CommandArgsJson = "{\"source_id\":49,\"fit_text\":false}";
            await database.SaveChangesAsync();
        }
        Assert.True((await ReportAsync(factory, worker, 4, new
        { source_id = 48, adapter_kind = "libreoffice", actual_fit_text = true, playback_state = "error" })).Accepted);
        Assert.True((await ReadLayoutAsync(client)).GetBoolean());
        var late = await ReportAsync(factory, worker, 3, new
        { source_id = 49, adapter_kind = "libreoffice", actual_fit_text = false });
        Assert.False(late.Accepted);
        Assert.Equal("stale_generation", late.Reason);
        Assert.True((await ReadLayoutAsync(client)).GetBoolean());
    }

    [Theory]
    [InlineData("{\"source_id\":null,\"actual_fit_text\":true}")]
    [InlineData("{\"source_id\":49,\"adapter_kind\":\"image\",\"actual_fit_text\":true}")]
    [InlineData("{\"source_id\":49,\"adapter_kind\":\"libreoffice\"}")]
    public async Task ClosedNonPresentationOrNewUnknownResourceClearsPreviouslyObservedLayout(string replacement)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory);
        await ReportAsync(factory, worker, 3, new { source_id = 48, adapter_kind = "libreoffice", actual_fit_text = true });
        Assert.True((await ReportAsync(factory, worker, 3, JsonSerializer.Deserialize<JsonElement>(replacement))).Accepted);
        Assert.Equal(JsonValueKind.Null, (await ReadLayoutAsync(client)).ValueKind);
    }

    [Theory]
    [InlineData("{\"source_id\":48,\"adapter_kind\":\"libreoffice\"}")]
    [InlineData("{\"source_id\":48,\"adapter_kind\":\"libreoffice\",\"actual_fit_text\":\"false\"}")]
    public async Task MissingOrMalformedLayoutForSameResourceDoesNotInventNewLayout(string observation)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory);
        await ReportAsync(factory, worker, 3, new { source_id = 48, adapter_kind = "libreoffice", actual_fit_text = true });
        Assert.True((await ReportAsync(factory, worker, 3, JsonSerializer.Deserialize<JsonElement>(observation))).Accepted);
        Assert.True((await ReadLayoutAsync(client)).GetBoolean());
    }

    /// <summary>独立测试库只预置归属和desired，fit_text意图不能代替原生观测。</summary>
    private static async Task<Guid> SeedOwnerAsync(ControlHostApplicationFactory factory)
    {
        var worker = Guid.NewGuid();
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        session.DesiredGeneration = 3;
        session.CommandArgsJson = "{\"fit_text\":true}";
        database.WorkerOwnerships.Add(new WorkerOwnership
        {
            TargetKind = CommandTargetKind.Display, TargetId = 1, WorkerInstanceId = worker,
            OwnerEpoch = 2, ProcessId = 12345, ProcessStartTime = DateTimeOffset.UtcNow,
            LastTransportHeartbeat = DateTimeOffset.UtcNow, Status = WorkerOwnershipState.Online,
        });
        await database.SaveChangesAsync();
        return worker;
    }

    /// <summary>经生产状态报告入口提交纯值观测，不直接修改实际布局字段。</summary>
    private static Task<StateReportAcceptance> ReportAsync(ControlHostApplicationFactory factory, Guid worker,
        long generation, object state) => factory.Services.GetRequiredService<RuntimeProjectionPublisher>()
        .ApplyStateReportAsync(CommandTargetKind.Display, 1, worker, 2, new StateReportDto
        { SourceGeneration = generation, State = JsonSerializer.SerializeToElement(state) });

    /// <summary>读取认证HTTP会话字段，确保不是仅数据库中有值。</summary>
    private static async Task<JsonElement> ReadLayoutAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/sessions/1/");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("session").GetProperty("actual_fit_text").Clone();
    }

    /// <summary>创建自有认证测试客户端，不连接现场ControlHost。</summary>
    private static async Task<HttpClient> AuthenticatedClientAsync(ControlHostApplicationFactory factory)
    {
        var client = factory.CreateHttpsClient();
        using var csrf = await client.GetAsync("/api/auth/csrf/");
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
