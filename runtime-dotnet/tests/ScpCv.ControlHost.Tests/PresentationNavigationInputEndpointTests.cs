// 导航输入真实HTTP回归；畸形goto不能产生持久意图或改变公开会话，不启动Office/Worker。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationNavigationInputEndpointTests
{
    [Fact]
    public async Task GotoWithoutTargetReturnsInvalidNavigationAndPreservesCommandsAndSessions()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await UploadAndOpenAsync(client);
        var beforeSessions = await ReadSessionsAsync(client);
        var beforeCommands = await ReadCommandsAsync(factory);
        using var gotoRequest = JsonRequest(HttpMethod.Post, "/api/playback/1/navigate/", csrf, new { action = "goto" });

        using var response = await client.SendAsync(gotoRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_navigation", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(beforeSessions, await ReadSessionsAsync(client));
        Assert.Equal(beforeCommands, await ReadCommandsAsync(factory));
    }

    /// <summary>:param client: 自有Simulation客户端。:returns: 正式认证/上传/打开受理后的CSRF，不执行播放器。</summary>
    private static async Task<string> UploadAndOpenAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        csrfResponse.EnsureSuccessStatusCode();
        using var csrfBody = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        var csrf = csrfBody.RootElement.GetProperty("csrfToken").GetString()!;
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent("navigation-input-owned"u8.ToArray()), "file", "navigation.pptx");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = upload };
        uploadRequest.Headers.Add("X-CSRFToken", csrf);
        using var uploaded = await client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var uploadBody = await JsonDocument.ParseAsync(await uploaded.Content.ReadAsStreamAsync());
        var sourceId = uploadBody.RootElement.GetProperty("source").GetProperty("id").GetInt64();
        using var openRequest = JsonRequest(HttpMethod.Post, "/api/playback/1/open/", csrf,
            new { source_id = sourceId, autoplay = true });
        using var opened = await client.SendAsync(openRequest);
        opened.EnsureSuccessStatusCode();
        return csrf;
    }

    /// <summary>:param method: HTTP动词。:param path: 正式端点。:param csrf: 会话token。:param body: 业务参数。:returns: 认证写入请求。</summary>
    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string csrf, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRFToken", csrf);
        return request;
    }

    /// <summary>:param client: 认证客户端。:returns: 全部公开会话快照，包括pending/desired/actual。</summary>
    private static async Task<string> ReadSessionsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/sessions/");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("sessions").GetRawText();
    }

    /// <summary>:param factory: 自有Simulation主机。:returns: 既有公开EF命令集合只读投影，不调用private队列实现。</summary>
    private static async Task<string> ReadCommandsAsync(ControlHostApplicationFactory factory)
    {
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        var commands = await database.CommandRecords.AsNoTracking().OrderBy(command => command.Id)
            .Select(command => new
            {
                command.CommandId, command.TargetId, command.TargetSequence, command.Command, command.ArgsJson,
                command.SourceGeneration, command.SourceRevision, command.Status, command.ResultCode,
            }).ToArrayAsync();
        return JsonSerializer.Serialize(commands);
    }
}
