using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.VideoWall;

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
    }
}
