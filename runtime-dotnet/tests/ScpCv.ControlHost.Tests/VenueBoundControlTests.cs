// ControlHost真实装配与持久化合同；替身仅接收包，不证明机械动作或DSP实际增益。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Tests;

public sealed partial class VenueBoundControlTests
{
    [Fact]
    public async Task ThreeSlidersPersistWithoutScreenReadErasingAndWallReappliesEachOnce()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var join = Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>());
        using var response = await client.PutAsJsonAsync("/api/venue/audio/", new { headset_volume = 10, handheld_volume = 20, computer_volume = 30 });
        response.EnsureSuccessStatusCode();
        Assert.Equal(["FF06020A00010001FA", "FF06020A00020003FA", "FF06020A00030005FA"], join.Commands.SelectMany(command => command.Frames));
        using var read = await client.PostAsJsonAsync("/api/venue/audio/read/", new { }); read.EnsureSuccessStatusCode();
        using var state = JsonDocument.Parse(await client.GetStringAsync("/api/venue/"));
        var audio = state.RootElement.GetProperty("venue").GetProperty("audio");
        Assert.Equal(10, audio.GetProperty("headset_volume").GetInt32());
        Assert.Equal(20, audio.GetProperty("handheld_volume").GetInt32());
        Assert.Equal(30, audio.GetProperty("computer_volume").GetInt32());
        using var wall = await client.PostAsJsonAsync("/api/video-wall/presets/window_1_fullscreen/apply/", new { }); wall.EnsureSuccessStatusCode();
        Assert.Equal(6, join.Commands.Count);
        Assert.All(join.Commands, command => { Assert.Equal("192.168.5.10", command.Host); Assert.Equal(8889, command.Port); });
    }

    [Fact]
    public async Task InvalidCombinedRequestHasZeroWritesAndFixedCameraCannotBypassGate()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var join = Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>());
        using var bad = await client.PutAsJsonAsync("/api/venue/audio/", new { screen_volume = 25, handheld_volume = 25, computer_volume = 101 });
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        using var source = await client.PutAsJsonAsync("/api/venue/audio/", new { hanging_volume = 20, source = "scp_cv" });
        Assert.Equal(HttpStatusCode.Conflict, source.StatusCode);
        using var fixedCamera = await client.PostAsJsonAsync("/api/venue/cameras/fixed/motion/", new { direction = "up", phase = "start", motion_id = Guid.NewGuid(), issued_at_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        Assert.Equal(HttpStatusCode.Conflict, fixedCamera.StatusCode);
        Assert.Empty(join.Commands);
        var screen = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        Assert.Equal(0, screen.WriteCount);
    }

    [Fact]
    public async Task CameraHttpStartHoldStopUseConfirmedWhiteListAndNoRepeatPress()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var id = Guid.NewGuid();
        foreach (var phase in new[] { "start", "hold", "stop" })
        {
            using var response = await client.PostAsJsonAsync("/api/venue/cameras/center1/motion/", new
            { phase, direction = "left", motion_id = id, issued_at_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            response.EnsureSuccessStatusCode();
        }
        var join = Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>());
        Assert.Equal(["FF06010A00210001FA", "FF06010A00210000FA"], join.Commands.SelectMany(command => command.Frames));
        Assert.All(join.Commands, command => Assert.Equal("192.168.5.85", command.Host));
    }

    [Fact]
    public async Task MicrophoneOnlyAdvancedPresetIsNotSilentlySkipped()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "仅手持", advanced = new { audio = new { handheld_volume = 12 } } });
        create.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        using var activated = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { }); activated.EnsureSuccessStatusCode();
        var join = Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>());
        Assert.Equal("FF06020A00020002FA", Assert.Single(Assert.Single(join.Commands).Frames));
    }
}
