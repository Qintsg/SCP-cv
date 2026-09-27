// 默认页图模式及实验性 Office 放映设置的 REST 安全边界。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class PowerPointSettingsEndpointTests
{
    [Fact]
    public async Task SimulationKeepsNativePowerPointDisabledAndCannotEnableIt()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var anonymous = await client.GetAsync("/api/settings/powerpoint/");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var csrf = await AuthenticateAsync(client);

        using var current = await client.GetAsync("/api/settings/powerpoint/");
        using var currentBody = await ReadJsonAsync(current);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var settings = currentBody.RootElement.GetProperty("settings");
        Assert.False(settings.GetProperty("experimental_enabled").GetBoolean());
        Assert.False(settings.GetProperty("available").GetBoolean());

        using var enable = new HttpRequestMessage(HttpMethod.Patch, "/api/settings/powerpoint/")
        {
            Content = JsonContent.Create(new { experimental_enabled = true }),
        };
        enable.Headers.Add("X-CSRFToken", csrf);
        using var response = await client.SendAsync(enable);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("powerpoint_unavailable", body.RootElement.GetProperty("code").GetString());
    }

    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrfBody = await ReadJsonAsync(csrfResponse);
        var csrf = csrfBody.RootElement.GetProperty("csrfToken").GetString()!;
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new
        {
            username = "operator",
            password = "Old-password-123",
        });
        login.EnsureSuccessStatusCode();
        return csrf;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
}
