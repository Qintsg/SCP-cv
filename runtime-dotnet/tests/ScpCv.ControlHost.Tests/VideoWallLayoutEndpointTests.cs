// 大屏布局 REST 合同：手动草稿可预览、未知帧不得假报应用成功。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class VideoWallLayoutEndpointTests
{
    [Fact]
    public async Task FixedPresetReturnsCurrentRuntimeAndTwoSessions()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var apply = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/video-wall/presets/window_1_left_window_2_right/apply/")
        {
            Content = JsonContent.Create(new { }),
        };
        apply.Headers.Add("X-CSRFToken", csrf);

        using var response = await client.SendAsync(apply);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("double", body.RootElement.GetProperty("runtime").GetProperty("big_screen_mode").GetString());
        Assert.Equal("double", body.RootElement.GetProperty("layout").GetProperty("active_preset").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("sessions").GetArrayLength());
    }

    [Fact]
    public async Task ManualDraftIsSavedButApplyReturnsProtocolUnavailable()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var save = new HttpRequestMessage(HttpMethod.Put, "/api/video-wall/layout/")
        {
            Content = JsonContent.Create(new
            {
                name = "笔记本左／直播右",
                mappings = new object[]
                {
                    new { region = "left", input = new { kind = "laptop" } },
                    new { region = "right", input = new { kind = "ip_stream", ip_address = "239.1.2.3" } },
                },
            }),
        };
        save.Headers.Add("X-CSRFToken", csrf);
        using var saved = await client.SendAsync(save);
        using var savedBody = await ReadJsonAsync(saved);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.False(savedBody.RootElement.GetProperty("layout").GetProperty("draft").GetProperty("can_apply").GetBoolean());

        using var apply = new HttpRequestMessage(HttpMethod.Post, "/api/video-wall/layout/apply/")
        {
            Content = JsonContent.Create(new { }),
        };
        apply.Headers.Add("X-CSRFToken", csrf);
        using var response = await client.SendAsync(apply);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("protocol_unavailable", body.RootElement.GetProperty("code").GetString());

        using var current = await client.GetAsync("/api/video-wall/layout/");
        using var currentBody = await ReadJsonAsync(current);
        Assert.Equal("single", currentBody.RootElement.GetProperty("layout").GetProperty("active_preset").GetString());
    }

    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrfBody = await ReadJsonAsync(csrfResponse);
        var csrf = csrfBody.RootElement.GetProperty("csrfToken").GetString()!;
        using var login = await client.PostAsJsonAsync(
            "/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return csrf;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
}
