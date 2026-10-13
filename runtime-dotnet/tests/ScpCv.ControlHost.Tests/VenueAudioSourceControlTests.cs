// 二选一来源的真实HTTP/持久/重发与全项门禁；只用明确传输替身，不冒充实体路由读回。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Tests;

public sealed class VenueAudioSourceControlTests
{
    [Fact]
    public async Task KnownIp81CanSendWithoutSoftwareOutputAndReappliesExactlyOnce()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var transport = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        using var response = await client.PutAsJsonAsync("/api/venue/audio/", new { source = "ip81" }); response.EnsureSuccessStatusCode();
        Assert.Equal(1, transport.SourceWriteCount); Assert.Equal("ip81", transport.SelectedSource); Assert.Equal(0, transport.WriteCount);
        using var read = JsonDocument.Parse(await client.GetStringAsync("/api/venue/"));
        Assert.Equal("ip81", read.RootElement.GetProperty("venue").GetProperty("audio").GetProperty("source").GetString());
        using var wall = await client.PostAsJsonAsync("/api/video-wall/presets/window_1_fullscreen/apply/", new { }); wall.EnsureSuccessStatusCode();
        Assert.Equal(2, transport.SourceWriteCount); Assert.Equal(1, transport.WriteCount);
    }

    [Fact]
    public async Task ValidSoftwareBindingAllowsSelectionButLossRejectsWholeRequestBeforeWrites()
    {
        using var factory = new ControlHostApplicationFactory(); var binding = new MutableBinding();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IVenueSoftwareAudioBinding>(binding)));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var transport = Assert.IsType<SimulationVenueAudioTransport>(configured.Services.GetRequiredService<IVenueAudioTransport>());
        using var first = await client.PutAsJsonAsync("/api/venue/audio/", new { source = "scp_cv", screen_volume = 31 }); first.EnsureSuccessStatusCode();
        Assert.Equal(1, transport.SourceWriteCount); Assert.Equal(1, transport.WriteCount);
        binding.Reason = "测试端点已移除";
        using var rejected = await client.PutAsJsonAsync("/api/venue/audio/", new { source = "scp_cv", screen_volume = 42, handheld_volume = 50 });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(1, transport.SourceWriteCount); Assert.Equal(1, transport.WriteCount);
        var join = Assert.IsType<SimulationDeviceCommandTransport>(configured.Services.GetRequiredService<IDeviceCommandTransport>()); Assert.Empty(join.Commands);
        using var state = JsonDocument.Parse(await client.GetStringAsync("/api/venue/"));
        Assert.False(state.RootElement.GetProperty("venue").GetProperty("capabilities").GetProperty("source_scp_cv").GetBoolean());
        Assert.Equal("scp_cv", state.RootElement.GetProperty("venue").GetProperty("audio").GetProperty("source").GetString());
    }

    [Fact]
    public async Task UnboundSoftwareAndUnknownSourceHaveZeroSideEffects()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        foreach (var source in new[] { "scp_cv", "arbitrary" })
        { using var response = await client.PutAsJsonAsync("/api/venue/audio/", new { source, screen_volume = 20 }); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); }
        var transport = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        Assert.Equal(0, transport.SourceWriteCount); Assert.Equal(0, transport.WriteCount);
    }

    [Fact]
    public async Task SourceRequestRemainsReportedWhenLaterSliderFailsAfterScreenReadback()
    {
        using var factory = new ControlHostApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IDeviceCommandTransport>(new FailingJoin())));
        _ = configured.CreateClient();
        var result = await configured.Services.GetRequiredService<VenueDeviceService>().ApplyAsync(new VenueAudioSettings
        { Source = "ip81", ScreenVolume = 31, HandheldVolume = 50 });
        Assert.Equal("uncertain", result.Status); Assert.Equal("handheld_volume", result.FailedField);
        Assert.Equal("ip81", result.Source); Assert.Equal(31, result.ScreenVolume);
        Assert.Equal(["source", "screen_volume"], result.SentFields);
        var saved = await configured.Services.GetRequiredService<VenueDeviceService>().SettingsAsync();
        Assert.Equal("ip81", saved.Source); Assert.Equal(31, saved.ScreenVolume); Assert.Null(saved.HandheldVolume);
    }

    [Fact]
    public async Task PresetSourceIsSentOnceAndUnavailableSoftwareRejectsBeforeWallOrCamera()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var audio = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "来源预设", big_screen_mode_state = "set", big_screen_mode = "double",
            advanced = new { camera_positions = new { center1 = 2 }, audio = new { source = "scp_cv", screen_volume = 30 } } });
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        using var rejected = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { }); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var state = JsonDocument.Parse(await client.GetStringAsync("/api/video-wall/layout/"));
        Assert.Equal("none", state.RootElement.GetProperty("layout").GetProperty("dispatch_status").GetString());
        Assert.Equal(0, audio.WriteCount); Assert.Equal(0, audio.SourceWriteCount);
        Assert.Empty(Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>()).Commands);
        using var update = await client.PatchAsJsonAsync($"/api/scenarios/{id}/", new { name = "来源预设", big_screen_mode_state = "set", big_screen_mode = "double",
            advanced = new { audio = new { source = "ip81", screen_volume = 30 } } }); update.EnsureSuccessStatusCode();
        using var activated = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { }); activated.EnsureSuccessStatusCode();
        Assert.Equal(1, audio.SourceWriteCount); Assert.Equal(1, audio.WriteCount);
    }

    private sealed class FailingJoin : IDeviceCommandTransport
    {
        public bool IsSimulation => false;
        public Task SendAsync(DeviceCommand command, CancellationToken cancellationToken = default) =>
            Task.FromException(new DeviceServiceException("明确失败，不重试。"));
    }

    private sealed class MutableBinding : IVenueSoftwareAudioBinding
    {
        public string Reason { get; set; } = "";
        public Task<string> UnavailableReasonAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(Reason); }
    }
}
