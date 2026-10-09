// 经认证HTTP与真实持久队列验证控制回执；不启动Office或现场设备。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class PlaybackCommandReceiptTests
{
    /// <summary>:param status: 原有三种终态。:returns: 只读查询保留精确结果，不把入队或失败当末页成功。</summary>
    [Theory]
    [InlineData(CommandStatus.Completed)]
    [InlineData(CommandStatus.Failed)]
    [InlineData(CommandStatus.Uncertain)]
    public async Task ResultQueryPreservesPersistedTerminalAndDoesNotLeakWorkerCredentials(CommandStatus status)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticateAsync(factory);
        await SeedSourceAsync(factory);
        var id = await SubmitAsync(client);
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using (var database = await contexts.CreateDbContextAsync())
        {
            var group = await database.RuntimeGroupControls.SingleAsync();
            group.State = RuntimeGroupState.Armed;
            group.GroupEpoch = 1;
            await database.SaveChangesAsync();
        }
        var repository = factory.Services.GetRequiredService<CommandRepository>();
        var claimed = await repository.ClaimAsync(new ClaimCommand(CommandTargetKind.Display, 1,
            Guid.NewGuid(), 1, 1, TimeSpan.FromSeconds(30)));
        Assert.NotNull(claimed);
        var code = status == CommandStatus.Completed ? "presentation_at_last_slide" : "presentation_transport_uncertain";
        Assert.True((await factory.Services.GetRequiredService<CommandResultService>().AcceptAsync(
            new CompleteCommand(id, claimed.ClaimToken!.Value, 1, status, code, string.Empty, "{}"))).Accepted);
        using var response = await client.GetAsync($"/api/playback/1/commands/{id}/");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = body.RootElement.GetProperty("command");
        Assert.Equal(status.ToString().ToLowerInvariant(), result.GetProperty("status").GetString());
        Assert.Equal(code, result.GetProperty("result_code").GetString());
        Assert.Equal(JsonValueKind.String, result.GetProperty("completed_at").ValueKind);
        Assert.False(result.TryGetProperty("claim_token", out _));
        using var ordinary = await client.GetAsync("/api/sessions/1/");
        using var ordinaryBody = await JsonDocument.ParseAsync(await ordinary.Content.ReadAsStreamAsync());
        Assert.False(ordinaryBody.RootElement.GetProperty("session").TryGetProperty("submitted_command_id", out _));
    }

    /// <summary>:returns: 未认证请求不可查询持久记录。</summary>
    [Fact]
    public async Task CommandQueryRequiresAuthenticatedSession()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync($"/api/playback/1/commands/{Guid.NewGuid()}/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>:returns: 两次提交各自得到已持久化命令身份，而非全局最新命令。</summary>
    [Fact]
    public async Task EachControlResponseIdentifiesItsOwnPersistedCommand()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticateAsync(factory);
        await SeedSourceAsync(factory);
        var first = await SubmitAsync(client);
        var second = await SubmitAsync(client);
        Assert.NotEqual(first, second);
        using var response = await client.GetAsync($"/api/playback/1/commands/{first}/");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = body.RootElement.GetProperty("command");
        Assert.Equal(first, result.GetProperty("command_id").GetGuid());
        Assert.Equal("pending", result.GetProperty("status").GetString());
        Assert.Equal(3, result.GetProperty("source_generation").GetInt64());
        Assert.False(result.TryGetProperty("claim_token", out _));
        Assert.False(result.TryGetProperty("args_json", out _));
        using var wrongWindow = await client.GetAsync($"/api/playback/2/commands/{first}/");
        Assert.Equal(HttpStatusCode.NotFound, wrongWindow.StatusCode);
    }

    /// <summary>:param client: 已认证客户端。:returns: 本次HTTP受理命令身份。</summary>
    private static async Task<Guid> SubmitAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrf = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/playback/1/control/")
        { Content = JsonContent.Create(new { action = "next_effect" }) };
        request.Headers.Add("X-CSRFToken", csrf.RootElement.GetProperty("csrfToken").GetString());
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var session = body.RootElement.GetProperty("sessions").EnumerateArray()
            .Single(item => item.GetProperty("window_id").GetInt32() == 1);
        return session.GetProperty("submitted_command_id").GetGuid();
    }

    /// <summary>:param factory: 独立主机。:returns: 预置媒体与组身份，不制造原生完成观测。</summary>
    private static async Task SeedSourceAsync(ControlHostApplicationFactory factory)
    {
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        var source = new MediaSource { Name = "回执样例", Uri = "receipt.pptx", SourceType = MediaSourceType.Presentation };
        database.MediaSources.Add(source);
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        session.MediaSource = source;
        session.DesiredGeneration = 3;
        await database.SaveChangesAsync();
    }

    /// <summary>:param factory: 独立主机。:returns: 无现场副作用的认证客户端。</summary>
    private static async Task<HttpClient> AuthenticateAsync(ControlHostApplicationFactory factory)
    {
        var client = factory.CreateHttpsClient();
        using var csrf = await client.GetAsync("/api/auth/csrf/");
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
