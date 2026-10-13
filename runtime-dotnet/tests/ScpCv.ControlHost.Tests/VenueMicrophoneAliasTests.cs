// 用户已确认悬挂与原头戴1/2为同一组：旧配置兼容、去重及冲突首包前拒绝。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class VenueMicrophoneAliasTests
{
    [Theory]
    [InlineData(null, 20)]
    [InlineData(20, null)]
    [InlineData(20, 20)]
    public async Task EitherAliasOrBothEqualWriteJoinOneOnceAndPersistOneValue(int? headset, int? hanging)
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var response = await client.PutAsJsonAsync("/api/venue/audio/", new { headset_volume = headset, hanging_volume = hanging });
        response.EnsureSuccessStatusCode();
        var joins = Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>());
        Assert.Equal("FF06020A00010003FA", Assert.Single(Assert.Single(joins.Commands).Frames));
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(["headset_volume"], result.RootElement.GetProperty("audio").GetProperty("sent_fields").EnumerateArray().Select(field => field.GetString()));
        using var read = await client.PostAsJsonAsync("/api/venue/audio/read/", new { }); read.EnsureSuccessStatusCode();
        var stored = await factory.Services.GetRequiredService<VenueDeviceService>().SettingsAsync();
        Assert.Equal(20, stored.HeadsetVolume); Assert.Null(stored.HangingVolume);
        using var state = JsonDocument.Parse(await client.GetStringAsync("/api/venue/"));
        var capabilities = state.RootElement.GetProperty("venue").GetProperty("capabilities");
        Assert.True(capabilities.GetProperty("hanging_volume").GetBoolean());
        Assert.True(capabilities.GetProperty("headset_volume").GetBoolean());
        using var wall = await client.PostAsJsonAsync("/api/video-wall/presets/window_1_fullscreen/apply/", new { }); wall.EnsureSuccessStatusCode();
        Assert.Equal(2, joins.Commands.Count);
    }

    [Fact]
    public async Task LegacyRememberedHangingIsReadableAndReappliedOnceWithoutDestroyingOtherValues()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (db, token) =>
        {
            var state = await db.RuntimeStates.SingleAsync(token);
            state.VenueAudioJson = "{\"hanging_volume\":40,\"handheld_volume\":60}";
        });
        var settings = await factory.Services.GetRequiredService<VenueDeviceService>().SettingsAsync();
        Assert.Equal(40, settings.HeadsetVolume); Assert.Null(settings.HangingVolume); Assert.Equal(60, settings.HandheldVolume);
        using var response = await client.PostAsJsonAsync("/api/video-wall/presets/window_1_fullscreen/apply/", new { }); response.EnsureSuccessStatusCode();
        var joins = Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>());
        Assert.Equal(["FF06020A00010007FA", "FF06020A0002000BFA"], joins.Commands.SelectMany(command => command.Frames));
        using var read = await client.PostAsJsonAsync("/api/venue/audio/read/", new { }); read.EnsureSuccessStatusCode();
        settings = await factory.Services.GetRequiredService<VenueDeviceService>().SettingsAsync();
        Assert.Equal(40, settings.HeadsetVolume); Assert.Equal(60, settings.HandheldVolume);
    }

    [Fact]
    public async Task LegacyPresetHangingActivatesJoinOneAndNewSaveCanonicalizes()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "悬挂旧预设", advanced = new { audio = new { hanging_volume = 60 } } });
        create.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var scenario = body.RootElement.GetProperty("scenario");
        Assert.Equal(60, scenario.GetProperty("advanced").GetProperty("audio").GetProperty("headset_volume").GetInt32());
        var id = scenario.GetProperty("id").GetInt64();
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (db, token) =>
        {
            var legacy = await db.Scenarios.SingleAsync(item => item.Id == id, token);
            legacy.AdvancedJson = "{\"audio\":{\"hanging_volume\":60}}";
        });
        using var activate = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { }); activate.EnsureSuccessStatusCode();
        var joins = Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>());
        Assert.Equal("FF06020A0001000BFA", Assert.Single(Assert.Single(joins.Commands).Frames));
    }

    [Fact]
    public async Task ConflictingAliasPresetSaveAndLegacyActivationRejectBeforeWallCameraOrAudio()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var bad = await client.PostAsJsonAsync("/api/scenarios/", new { name = "冲突", advanced = new { audio = new { hanging_volume = 30, headset_volume = 40 } } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "历史冲突", big_screen_mode_state = "set", big_screen_mode = "double" });
        create.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (db, token) =>
        {
            var legacy = await db.Scenarios.SingleAsync(item => item.Id == id, token);
            legacy.AdvancedJson = "{\"camera_positions\":{\"center1\":1},\"audio\":{\"hanging_volume\":30,\"headset_volume\":40,\"screen_volume\":50}}";
        });
        using var activate = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { });
        Assert.Equal(HttpStatusCode.BadRequest, activate.StatusCode);
        Assert.Empty(Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>()).Commands);
        Assert.Equal(0, Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>()).WriteCount);
        using var wall = JsonDocument.Parse(await client.GetStringAsync("/api/video-wall/layout/"));
        Assert.Equal("none", wall.RootElement.GetProperty("layout").GetProperty("dispatch_status").GetString());
    }

    [Fact]
    public async Task SavedAliasConflictRejectsDirectAndPresetWritesBeforeAnyEffect()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "持久冲突", big_screen_mode_state = "set", big_screen_mode = "double",
            advanced = new { audio = new { headset_volume = 25, screen_volume = 50 } } });
        create.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (db, token) =>
        {
            var state = await db.RuntimeStates.SingleAsync(token);
            state.VenueAudioJson = "{\"headset_volume\":20,\"hanging_volume\":30}";
        });
        using var direct = await client.PutAsJsonAsync("/api/venue/audio/", new { screen_volume = 40 });
        Assert.Equal(HttpStatusCode.Conflict, direct.StatusCode);
        using var activate = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { });
        Assert.Equal(HttpStatusCode.BadRequest, activate.StatusCode);
        Assert.Empty(Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>()).Commands);
        Assert.Equal(0, Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>()).WriteCount);
        using var wall = JsonDocument.Parse(await client.GetStringAsync("/api/video-wall/layout/"));
        Assert.Equal("none", wall.RootElement.GetProperty("layout").GetProperty("dispatch_status").GetString());
    }

    [Fact]
    public async Task LegacyAliasWithoutJoinAssemblyCannotWriteScreenFirst()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        using var service = new VenueDeviceService(factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>(),
            factory.Services.GetRequiredService<WriteCoordinator>(), factory.Services.GetRequiredService<IVenueAudioTransport>(),
            factory.Services.GetRequiredService<CameraCatalog>());
        await Assert.ThrowsAsync<VenueDeviceException>(() => service.ApplyAsync(new ScpCv.Contracts.Http.VenueAudioSettings { HangingVolume = 20, ScreenVolume = 40 }));
        Assert.Equal(0, Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>()).WriteCount);
    }
}
