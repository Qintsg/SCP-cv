// 手动布局只保存/预览，未知帧不得下发；固定预设复用已验证墙面序列。
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class VideoWallLayoutServiceTests
{
    [Fact]
    public async Task ManualLayoutPersistsButCannotApplyWithoutCapturedFrames()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var wall = new RecordingWall();
        var service = CreateService(fixture, wall);
        var layout = new WallLayout("笔记本左／直播右",
        [
            new WallMapping(WallRegion.Left, new WallInput(WallInputKind.Laptop)),
            new WallMapping(WallRegion.Right, new WallInput(WallInputKind.IpStream, "239.1.2.3")),
        ]);

        var saved = await service.SaveDraftAsync(layout);
        Assert.Equal(1, saved.DraftRevision);
        Assert.False(saved.Draft!.CanApply);
        Assert.Equal("single", saved.ActivePreset);
        Assert.Empty(wall.Modes);

        var reloaded = await CreateService(fixture, wall).GetAsync();
        Assert.Equal("笔记本左／直播右", reloaded.Draft!.Name);
        var error = await Assert.ThrowsAsync<VideoWallLayoutException>(() => service.ApplyDraftAsync());
        Assert.Equal("protocol_unavailable", error.Code);
        Assert.Empty(wall.Modes);
        Assert.Equal("single", (await service.GetAsync()).ActivePreset);
    }

    [Fact]
    public async Task FixedPresetAppliesThroughExistingVideoWallController()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var wall = new RecordingWall();
        var service = CreateService(fixture, wall);

        var applied = await service.ApplyPresetAsync("window_1_left_window_2_right");

        Assert.Equal("double", applied.ActivePreset);
        Assert.Equal(["double"], wall.Modes);
    }

    private static VideoWallLayoutService CreateService(ControlHostFixture fixture, IVideoWallController wall)
    {
        var runtime = new RuntimeStateService(
            fixture.Database,
            fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            fixture.TimeProvider,
            videoWall: wall);
        return new VideoWallLayoutService(fixture.Database, fixture.Writes, runtime, fixture.TimeProvider);
    }

    private sealed class RecordingWall : IVideoWallController
    {
        public bool IsHardware => true;
        public List<string> Modes { get; } = [];

        public Task DispatchAsync(string bigScreenMode, CancellationToken cancellationToken = default)
        {
            Modes.Add(bigScreenMode);
            return Task.CompletedTask;
        }
    }
}
