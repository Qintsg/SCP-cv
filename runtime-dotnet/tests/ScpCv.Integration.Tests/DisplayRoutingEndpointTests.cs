// 正式Host/认证/CSRF与模拟装配的电视API；不连接现场或启动播放器。
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScpCv.Infrastructure.Auth;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Integration.Tests;

public sealed class DisplayRoutingEndpointTests
{
    /// <summary>:returns: Hardware分支确实装配TCP电视控制器；其它硬件全部替换，不调用Dispatch或连接设备。</summary>
    [Fact]
    public void HardwareRegistrationResolvesRealTelevisionControllerWithoutDispatch()
    {
        using var root = new SecurityApplicationFactory();
        using var host = root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SafetyMode", "Hardware");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDisplayTopologyProvider>(); services.AddSingleton<IDisplayTopologyProvider, SimulationDisplayTopologyProvider>();
                services.RemoveAll<ISystemAudioController>(); services.AddSingleton<ISystemAudioController, SimulationSystemAudioController>();
                services.RemoveAll<IVideoWallController>(); services.AddSingleton<IVideoWallController, SimulationVideoWallController>();
                services.RemoveAll<IDeviceCommandTransport>(); services.AddSingleton<IDeviceCommandTransport, SimulationDeviceCommandTransport>();
            });
        });
        Assert.IsType<TcpTelevisionController>(host.Services.GetRequiredService<ITelevisionController>());
        Assert.NotNull(host.Services.GetRequiredService<DisplayRoutingService>());
    }

    /// <summary>:returns: 两个路由均要求登录，不通过未知输入触碰设备。</summary>
    [Fact]
    public async Task ReadAndWriteRequireAuthentication()
    {
        using var host = new SecurityApplicationFactory(); using var client = Client(host);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/display-routing/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/display-routing/televisions/tv_left/", new { input = new { kind = "window_1" } })).StatusCode);
        Assert.IsType<SimulationTelevisionController>(host.Services.GetRequiredService<ITelevisionController>());
    }

    /// <summary>:returns: 真实Host装配左右来源/last_sent和force；3/4只作为源，不增加Worker。</summary>
    [Fact]
    public async Task SimulationHostPersistsSingleSourceAndHonorsManualForce()
    {
        using var host = new SecurityApplicationFactory(); using var client = Client(host); await Login(client);
        var initial = await client.GetFromJsonAsync<JsonElement>("/api/display-routing/");
        Assert.Equal(["tv_left", "tv_right", "wall"], initial.GetProperty("outputs").EnumerateObject().Select(item => item.Name).Order().ToArray());
        using var request = await client.PutAsJsonAsync("/api/display-routing/televisions/tv_left/", new { input = new { kind = "window_3" }, force = true });
        request.EnsureSuccessStatusCode(); var sent = await request.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("simulated", sent.GetProperty("result").GetString());
        Assert.Equal("window_3", sent.GetProperty("output").GetProperty("last_sent").GetProperty("input").GetProperty("kind").GetString());
        using var skipped = await client.PutAsJsonAsync("/api/display-routing/televisions/tv_left/", new { input = new { kind = "window_3" }, force = false });
        skipped.EnsureSuccessStatusCode(); Assert.Equal("unchanged", (await skipped.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetString());
        using var manual = await client.PutAsJsonAsync("/api/display-routing/televisions/tv_left/", new { input = new { kind = "window_3" } });
        manual.EnsureSuccessStatusCode(); Assert.Equal("simulated", (await manual.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetString());
        var current = await client.GetFromJsonAsync<JsonElement>("/api/display-routing/");
        Assert.Equal("unknown", current.GetProperty("outputs").GetProperty("tv_right").GetProperty("status").GetString());
    }

    /// <summary>:returns: CSRF、固定目标和受信输入都在任何发送之前校验。</summary>
    [Fact]
    public async Task InvalidOrUntrustedRequestsRejectWithoutChangingOutput()
    {
        using var host = new SecurityApplicationFactory(); using var client = Client(host); await Login(client);
        client.DefaultRequestHeaders.Remove(CsrfTokenService.HeaderName);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/display-routing/televisions/tv_left/", new { input = new { kind = "window_1" } })).StatusCode);
        await AddCsrf(client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/display-routing/televisions/tv_left/", new { input = new { kind = "ip_stream", ip_address = "192.168.5.75" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/display-routing/televisions/wall/", new { input = new { kind = "window_1" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/display-routing/televisions/tv_left/", new StringContent("[]", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.All((await host.Services.GetRequiredService<DisplayRoutingService>().GetAsync()).Values, output => Assert.Equal("unknown", output.Status));
    }

    private static HttpClient Client(SecurityApplicationFactory host) => host.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
    private static async Task Login(HttpClient client)
    {
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Security-password-123" });
        login.EnsureSuccessStatusCode(); await AddCsrf(client);
    }
    private static async Task AddCsrf(HttpClient client)
    {
        var value = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf/");
        client.DefaultRequestHeaders.Remove(CsrfTokenService.HeaderName);
        client.DefaultRequestHeaders.Add(CsrfTokenService.HeaderName, value.GetProperty("csrfToken").GetString());
    }
}
