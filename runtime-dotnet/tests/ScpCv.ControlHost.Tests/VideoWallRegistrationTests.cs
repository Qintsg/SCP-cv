// Hardware真实装配检查：D4窗口输入63/64，旧D2抓包不作为当前配置。
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Tests;

/// <summary>
/// 现场以 <c>-SafetyMode Hardware</c> 启动（见 docs/qa/003-workstation-runbook.md），而测试默认跑
/// Simulation。这里按 Hardware 组装一次容器，确认真实视频墙下发那条分支真的能被解析出来——
/// Simulation 下换成空实现，任何装配错误都只会在现场暴露。
/// </summary>
public sealed class VideoWallRegistrationTests
{
    [Fact]
    public void SimulationSafetyModeResolvesANonHardwareController()
    {
        using var factory = new ControlHostApplicationFactory();

        var controller = factory.Services.GetRequiredService<IVideoWallController>();

        Assert.IsType<SimulationVideoWallController>(controller);
        Assert.False(controller.IsHardware);
    }

    [Fact]
    public void HardwareSafetyModeResolvesTheTcpController()
    {
        using var factory = new ControlHostApplicationFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("SafetyMode", "Hardware"));

        var controller = factory.Services.GetRequiredService<IVideoWallController>();

        Assert.IsType<TcpVideoWallController>(controller);
        Assert.True(controller.IsHardware);
        Assert.IsType<TcpVideoWallTransport>(factory.Services.GetRequiredService<IVideoWallTransport>());
        // Hardware 分支必须能独立解析出下发参数：现场没有第二次机会试错。
        Assert.NotNull(factory.Services.GetRequiredService<VideoWallDispatchOptions>());
        var protocol = factory.Services.GetRequiredService<VideoWallProtocolOptions>();
        Assert.Equal("192.168.5.63", protocol.Inputs["window_1"].UnicastAddress);
        Assert.Equal("224.1.1.63", protocol.Inputs["window_1"].MulticastAddress);
        Assert.Equal("192.168.5.64", protocol.Inputs["window_2"].UnicastAddress);
        Assert.Equal("224.1.1.64", protocol.Inputs["window_2"].MulticastAddress);
        Assert.Equal("224.1.1.81", protocol.Inputs["laptop"].MulticastAddress);
        foreach (var kind in new[] { "window_1", "window_2", "window_3", "window_4" })
        {
            var input = SiteInputCatalog.ResolveDescriptor(kind, null, protocol)!;
            Assert.Equal(3840, input.Width); Assert.Equal(2160, input.Height);
        }
        Assert.Equal(1920, SiteInputCatalog.ResolveDescriptor("ip_stream", "192.168.5.85", protocol)!.Width);
        Assert.NotNull(factory.Services.GetRequiredService<VideoWallObservationStore>());
        Assert.NotNull(factory.Services.GetRequiredService<VideoWallOperationGate>());
    }

    /// <summary>:returns: 管理员配置仍高于现场默认表，两个入口不会各取不同尺寸。</summary>
    [Fact]
    public void ExplicitResolutionOverridesAreSharedByAliasAndAddress()
    {
        using var factory = new ControlHostApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("VideoWall:Inputs:window_1:Width", "1920");
            builder.UseSetting("VideoWall:Inputs:window_1:Height", "1080");
        });
        var protocol = factory.Services.GetRequiredService<VideoWallProtocolOptions>();
        foreach (var input in new[] { SiteInputCatalog.ResolveDescriptor("window_1", null, protocol),
            SiteInputCatalog.ResolveDescriptor("ip_stream", "192.168.5.63", protocol) })
        {
            Assert.NotNull(input); Assert.Equal(1920, input.Width); Assert.Equal(1080, input.Height);
        }
    }
}
