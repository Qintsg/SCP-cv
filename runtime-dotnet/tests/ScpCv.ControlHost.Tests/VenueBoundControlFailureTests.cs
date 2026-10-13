// 现场音频的完整预检和部分失败回归；只使用明确故障替身，不发实体控制。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Tests;

public sealed partial class VenueBoundControlTests
{
    [Fact]
    public async Task ConflictingMicrophoneAliasesRejectAllOtherValidFieldsBeforeAnyWrite()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var response = await client.PutAsJsonAsync("/api/venue/audio/", new
        { hanging_volume = 20, headset_volume = 30, handheld_volume = 40, computer_volume = 50, screen_volume = 60 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(Assert.IsType<SimulationDeviceCommandTransport>(factory.Services.GetRequiredService<IDeviceCommandTransport>()).Commands);
        Assert.Equal(0, Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>()).WriteCount);
    }

    [Fact]
    public async Task SecondSliderFailurePreservesFirstIntentReportsStageAndNeverSendsThirdOrRetries()
    {
        using var factory = new ControlHostApplicationFactory();
        var control = new FailingJoinTransport();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IDeviceCommandTransport>(control)));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var response = await client.PutAsJsonAsync("/api/venue/audio/", new { headset_volume = 10, handheld_volume = 20, computer_volume = 30 });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = body.RootElement.GetProperty("audio");
        Assert.Equal("uncertain", result.GetProperty("status").GetString());
        Assert.Equal("handheld_volume", result.GetProperty("failed_field").GetString());
        Assert.Equal(["headset_volume"], result.GetProperty("sent_fields").EnumerateArray().Select(field => field.GetString()));
        Assert.Equal(2, control.Commands.Count);
        Assert.Equal("FF06020A00010001FA", Assert.Single(control.Commands[0].Frames));
        Assert.Equal("FF06020A00020003FA", Assert.Single(control.Commands[1].Frames));
        var settings = await configured.Services.GetRequiredService<VenueDeviceService>().SettingsAsync();
        Assert.Equal(10, settings.HeadsetVolume); Assert.Null(settings.HandheldVolume); Assert.Null(settings.ComputerVolume);
    }

    [Fact]
    public async Task CancellationAfterWriteDoesNotClaimPersistenceOrReplay()
    {
        using var factory = new ControlHostApplicationFactory(); using var cancellation = new CancellationTokenSource();
        var control = new FailingJoinTransport(cancellation);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IDeviceCommandTransport>(control)));
        _ = configured.CreateClient();
        var service = configured.Services.GetRequiredService<VenueDeviceService>();
        var result = await service.ApplyAsync(new VenueAudioSettings { HeadsetVolume = 10, HandheldVolume = 20 }, cancellation.Token);
        Assert.Equal("uncertain", result.Status); Assert.Equal("headset_volume", result.FailedField);
        Assert.Equal(["headset_volume"], result.SentFields); Assert.Single(control.Commands);
        Assert.Null((await service.SettingsAsync()).HeadsetVolume);
    }

    private sealed class FailingJoinTransport(CancellationTokenSource? cancellation = null) : IDeviceCommandTransport
    {
        public bool IsSimulation => false;
        internal List<DeviceCommand> Commands { get; } = [];
        public Task SendAsync(DeviceCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Commands.Add(command);
            if (Commands.Count == 2) throw new DeviceServiceException("第二滑杆显式发送故障；不能改第三滑杆或重试。");
            cancellation?.Cancel();
            return Task.CompletedTask;
        }
    }
}
