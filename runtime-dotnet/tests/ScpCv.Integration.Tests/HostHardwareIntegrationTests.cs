using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Scenarios;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class HostHardwareIntegrationTests
{
    [Fact]
    public async Task PhysicalDisplayTopologyPreservesNegativeCoordinatesAndRejectsUnknownTarget()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var topology = new StubDisplayTopologyProvider(new DisplayTopologySnapshot(
            true,
            [
                Display(1, @"\\.\DISPLAY1", 0, 0, true),
                Display(2, @"\\.\DISPLAY2", -1920, 120, false),
            ],
            "windows_display_topology"));
        var runtime = CreateRuntime(fixture, topology, new SimulationSystemAudioController());

        var targets = runtime.ListDisplays();
        Assert.Equal(2, targets.Count);
        Assert.Equal(-1920, targets[1].X);
        Assert.Equal(120, targets[1].Y);

        var sessions = await runtime.SelectDisplayAsync(1, "single", @"\\.\DISPLAY2");
        Assert.Equal(@"\\.\DISPLAY2", sessions.Single(item => item.WindowId == 1).TargetDisplayLabel);
        var error = await Assert.ThrowsAsync<PlaybackServiceException>(
            () => runtime.SelectDisplayAsync(1, "single", @"\\.\DISPLAY9"));
        Assert.Equal("display_target_unavailable", error.Code);
    }

    [Fact]
    public async Task HardwareVolumePersistsOnlyObservedCoreAudioState()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var audio = new StubSystemAudioController(
            new SystemAudioSnapshot(true, 64, false, "windows_core_audio"),
            new SystemAudioSnapshot(true, 41, true, "windows_core_audio"));
        var runtime = CreateRuntime(
            fixture,
            new SimulationDisplayTopologyProvider(),
            audio);

        var initial = await runtime.GetSystemVolumeAsync();
        Assert.Equal(64, initial.Level);
        Assert.True(initial.SystemSynced);
        Assert.Equal("windows_core_audio", initial.Backend);

        var applied = await runtime.SetSystemVolumeAsync(42, true);
        Assert.Equal((42, true), audio.LastSet);
        Assert.Equal(41, applied.Level);
        Assert.True(applied.Muted);
        Assert.True(applied.SystemSynced);

        await using var database = fixture.Database.CreateDbContext();
        var persisted = await database.RuntimeStates.AsNoTracking().SingleAsync();
        Assert.Equal(41, persisted.VolumeLevel);
        Assert.True(persisted.VolumeMuted);
    }

    [Fact]
    public async Task UnavailableCoreAudioFailsClosedWithoutChangingPersistedIntent()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var audio = new StubSystemAudioController(
            new SystemAudioSnapshot(false, 0, false, "windows_core_audio_unavailable", "没有默认渲染设备。"),
            new SystemAudioSnapshot(false, 0, false, "windows_core_audio_unavailable", "没有默认渲染设备。"));
        var runtime = CreateRuntime(
            fixture,
            new SimulationDisplayTopologyProvider(),
            audio);

        var current = await runtime.GetSystemVolumeAsync();
        Assert.False(current.SystemSynced);
        Assert.Equal("windows_core_audio_unavailable", current.Backend);

        var error = await Assert.ThrowsAsync<PlaybackServiceException>(
            () => runtime.SetSystemVolumeAsync(22, false));
        Assert.Equal("system_audio_unavailable", error.Code);

        await using var database = fixture.Database.CreateDbContext();
        var persisted = await database.RuntimeStates.AsNoTracking().SingleAsync();
        Assert.NotEqual(22, persisted.VolumeLevel);
    }

    [Fact]
    public async Task RuntimeModeSwitchDispatchesVideoWallBeforePersisting()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var observedDuringDispatch = BigScreenMode.Double;
        var videoWall = new StubVideoWallController(async _ =>
        {
            observedDuringDispatch = await ReadBigScreenModeAsync(fixture);
        });
        var runtime = CreateRuntime(fixture, new SimulationDisplayTopologyProvider(), new SimulationSystemAudioController(), videoWall);

        await runtime.SetRuntimeModeAsync("double");

        Assert.Equal(["double"], videoWall.Modes);
        // 下发发生在落库之前：节点不可达时不应该留下“已切到双屏”的运行态。
        Assert.Equal(BigScreenMode.Single, observedDuringDispatch);
        Assert.Equal(BigScreenMode.Double, await ReadBigScreenModeAsync(fixture));
    }

    [Fact]
    public async Task VideoWallFailureKeepsPersistedRuntimeModeAndMutePolicy()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var videoWall = new StubVideoWallController
        {
            Failure = new VideoWallException("发送视频墙控制包失败：192.168.5.101:4830 连接超时"),
        };
        var runtime = CreateRuntime(fixture, new SimulationDisplayTopologyProvider(), new SimulationSystemAudioController(), videoWall);

        var error = await Assert.ThrowsAsync<PlaybackServiceException>(() => runtime.SetRuntimeModeAsync("double"));

        Assert.Equal("video_wall_error", error.Code);
        Assert.Contains("192.168.5.101:4830", error.Message);
        Assert.Equal(BigScreenMode.Single, await ReadBigScreenModeAsync(fixture));
        // 静音策略也必须留在旧模式：否则会得到“双屏运行态 + 单屏静音”的半套状态。
        await using var database = fixture.Database.CreateDbContext();
        var sessions = await database.PlaybackSessions.AsNoTracking().ToListAsync();
        Assert.All(sessions, session => Assert.False(session.IsMuted));
    }

    [Fact]
    public async Task RepeatingTheCurrentModeStillDispatchesTheWholeSequence()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var videoWall = new StubVideoWallController();
        var runtime = CreateRuntime(fixture, new SimulationDisplayTopologyProvider(), new SimulationSystemAudioController(), videoWall);

        // 运行态初始就是 single，这里两次请求的都是「当前已生效的模式」。
        // 现场补救路径：切换报成功但墙面没动时重来一次，服务层不得因为模式没变就跳过下发。
        await runtime.SetRuntimeModeAsync("single");
        await runtime.SetRuntimeModeAsync("single");

        Assert.Equal(["single", "single"], videoWall.Modes);
        Assert.Equal(BigScreenMode.Single, await ReadBigScreenModeAsync(fixture));
    }

    [Fact]
    public async Task ScenarioActivationDispatchesVideoWallForItsBigScreenMode()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var videoWall = new StubVideoWallController();
        var scenarios = CreateScenarios(fixture, videoWall);
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("双屏预案", "", "set", "double", "unset", 100, []));

        await scenarios.ActivateAsync(scenario.Id);

        Assert.Equal(["double"], videoWall.Modes);
        Assert.Equal(BigScreenMode.Double, await ReadBigScreenModeAsync(fixture));
    }

    [Fact]
    public async Task ScenarioActivationFailureLeavesRuntimeModeUnchanged()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var videoWall = new StubVideoWallController { Failure = new VideoWallException("发送视频墙控制包失败：节点不可达") };
        var scenarios = CreateScenarios(fixture, videoWall);
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("双屏预案", "", "set", "double", "unset", 100, []));

        var error = await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.ActivateAsync(scenario.Id));

        Assert.Contains("节点不可达", error.Message);
        Assert.Equal(BigScreenMode.Single, await ReadBigScreenModeAsync(fixture));
    }

    private static RuntimeStateService CreateRuntime(
        ControlHostFixture fixture,
        IDisplayTopologyProvider displays,
        ISystemAudioController audio,
        IVideoWallController? videoWall = null)
    {
        var coordinator = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        return new RuntimeStateService(
            fixture.Database,
            fixture.Writes,
            coordinator,
            fixture.TimeProvider,
            displays,
            audio,
            videoWall);
    }

    private static ScenarioService CreateScenarios(ControlHostFixture fixture, IVideoWallController videoWall) =>
        new(
            fixture.Database,
            fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            fixture.TimeProvider,
            videoWall);

    private static async Task<BigScreenMode> ReadBigScreenModeAsync(ControlHostFixture fixture)
    {
        await using var database = fixture.Database.CreateDbContext();
        return (await database.RuntimeStates.AsNoTracking().SingleAsync()).BigScreenMode;
    }

    private sealed class StubVideoWallController(Func<string, Task>? onDispatch = null) : IVideoWallController
    {
        public bool IsHardware => true;

        public List<string> Modes { get; } = [];

        public VideoWallException? Failure { get; init; }

        public async Task DispatchAsync(string bigScreenMode, CancellationToken cancellationToken = default)
        {
            Modes.Add(bigScreenMode);
            if (onDispatch is not null)
            {
                await onDispatch(bigScreenMode);
            }

            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    private static DisplayTargetDto Display(int index, string name, int x, int y, bool primary) => new()
    {
        Index = index,
        Name = name,
        Width = 1920,
        Height = 1080,
        X = x,
        Y = y,
        IsPrimary = primary,
    };

    private sealed class StubDisplayTopologyProvider(DisplayTopologySnapshot snapshot) : IDisplayTopologyProvider
    {
        public DisplayTopologySnapshot GetCurrent() => snapshot;
    }

    private sealed class StubSystemAudioController(
        SystemAudioSnapshot current,
        SystemAudioSnapshot applied) : ISystemAudioController
    {
        public bool IsHardware => true;
        public (int? Level, bool? Muted) LastSet { get; private set; }

        public SystemAudioSnapshot GetCurrent() => current;

        public SystemAudioSnapshot Apply(int? level, bool? muted)
        {
            LastSet = (level, muted);
            return applied;
        }
    }
}
