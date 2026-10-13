// 首页切墙一次重发一次屏幕音量；未知布局/绑定拒绝时零音量写出。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Tests;

public sealed class VenueAudioWallTests
{
    [Fact]
    public async Task AudioReadbackFailureDoesNotTurnSentWallIntoUnqualifiedSuccessOrRetry()
    {
        using var factory = new ControlHostApplicationFactory();
        var transport = new ReadbackFailureTransport();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IVenueAudioTransport>(transport)));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var response = await client.PostAsJsonAsync("/api/video-wall/presets/window_1_fullscreen/apply/", new { });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("sent_unconfirmed", body.RootElement.GetProperty("audio").GetProperty("status").GetString());
        Assert.Equal(1, transport.WriteCount);
        Assert.True(body.RootElement.TryGetProperty("layout", out _));
    }

    [Fact]
    public async Task SuccessfulWallDispatchReappliesExactlyOnceAndRejectedLayoutDoesNotWrite()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var transport = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        using var first = await client.PostAsJsonAsync("/api/video-wall/presets/window_1_fullscreen/apply/", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal("simulated", body.RootElement.GetProperty("audio").GetProperty("status").GetString());
        Assert.Equal(1, transport.WriteCount);
        using var bad = await client.PostAsJsonAsync("/api/video-wall/layout/apply/", new { expected_revision = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); // 尚无草稿，先返回layout_missing而不是版本冲突。
        Assert.Equal(1, transport.WriteCount);
        using var second = await client.PostAsJsonAsync("/api/video-wall/presets/window_1_left_window_2_right/apply/", new { });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, transport.WriteCount);
    }

    [Fact]
    public async Task UnboundSourceOrUnsupportedPositionIsRejectedBeforeAnyDeviceChange()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var transport = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        using var rejected = await client.PutAsJsonAsync("/api/venue/audio/", new { screen_volume = 20, source = "scp_cv" });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(0, transport.WriteCount);
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "未绑定",
            big_screen_mode_state = "set", big_screen_mode = "double",
            advanced = new { camera_positions = new { rear = 7 } } });
        create.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        using var activate = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { });
        Assert.Equal(HttpStatusCode.BadRequest, activate.StatusCode);
        using var state = JsonDocument.Parse(await client.GetStringAsync("/api/video-wall/layout/"));
        Assert.Equal("none", state.RootElement.GetProperty("layout").GetProperty("dispatch_status").GetString());
        Assert.Equal(0, transport.WriteCount);
    }

    private sealed class ReadbackFailureTransport : IVenueAudioTransport
    {
        public bool IsSimulation => false; // 有界故障替身，不访问网络。
        public int WriteCount { get; private set; }
        public Task<int> ReadAsync(CancellationToken token) => WriteCount == 0
            ? Task.FromResult(50) : Task.FromException<int>(new VenueDeviceException("测试读回失败"));
        public Task WriteAsync(int volume, CancellationToken token) { WriteCount++; return Task.CompletedTask; }
    }
}
