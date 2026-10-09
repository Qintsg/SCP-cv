// 大屏布局 REST 合同：手动草稿可预览、未知帧不得假报应用成功。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class VideoWallLayoutEndpointTests
{
    [Theory]
    [InlineData("\"7\"")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("0")]
    public async Task InvalidRevisionReturnsBadRequestWithoutChangingDispatchState(string revision)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await AuthenticateAsync(client));
        using var body = new StringContent("{\"expected_revision\":" + revision + "}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/video-wall/layout/apply/", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var current = await client.GetAsync("/api/video-wall/layout/");
        using var state = await ReadJsonAsync(current);
        Assert.Equal("none", state.RootElement.GetProperty("layout").GetProperty("dispatch_status").GetString());
    }

    [Fact]
    public async Task EveryInputCombinationAppliesAndStaleRevisionIsRejected()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Add("X-CSRFToken", csrf);
        var kinds = new[] { "window_1", "window_2", "laptop" };
        string?[] rightChoices = [null, .. kinds];
        foreach (var left in kinds)
        {
            foreach (var right in rightChoices)
            {
                var mappings = right is null
                    ? new[] { new { region = "fullscreen", input = new { kind = left } } }
                    : [new { region = "left", input = new { kind = left } }, new { region = "right", input = new { kind = right } }];
                using var saved = await client.PutAsJsonAsync("/api/video-wall/layout/", new { name = "组合", mappings });
                saved.EnsureSuccessStatusCode();
                using var body = await ReadJsonAsync(saved);
                var revision = body.RootElement.GetProperty("layout").GetProperty("draft_revision").GetInt64();
                using var applied = await client.PostAsJsonAsync("/api/video-wall/layout/apply/", new { expected_revision = revision });
                applied.EnsureSuccessStatusCode();
                using var result = await ReadJsonAsync(applied);
                var state = result.RootElement.GetProperty("layout");
                Assert.Equal("simulated", state.GetProperty("dispatch_status").GetString());
                Assert.Equal(revision, state.GetProperty("applied_revision").GetInt64());
                Assert.Equal(mappings.Length, state.GetProperty("applied").GetProperty("mappings").GetArrayLength());
            }
        }
        using var stale = await client.PostAsJsonAsync("/api/video-wall/layout/apply/", new { expected_revision = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var staleBody = await ReadJsonAsync(stale);
        Assert.Equal("layout_revision_conflict", staleBody.RootElement.GetProperty("code").GetString());
    }

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
