// 预案通过 Host 真实装配调用外部替身；不访问声卡、墙面或播放器。
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Scenarios;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Integration.Tests;

public sealed class ScenarioVolumeActivationTests
{
    /// <summary>
    /// 预案设置音量应经过硬件边界，保存观测值并保留实际静音。
    /// </summary>
    [Fact]
    public async Task HardwareScenarioPersistsObservedVolumeAndPreservesActualMute()
    {
        var audio = new FakeSystemAudioController(new SystemAudioSnapshot(true, 41, true, "fake_core_audio"));
        using var root = new SecurityApplicationFactory();
        using var host = CreateHost(root, audio, new FakeVideoWallController());
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var runtime = host.Services.GetRequiredService<RuntimeStateService>();
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("实体音量预案", "", "unset", "single", "set", 42, []));

        await scenarios.ActivateAsync(scenario.Id);

        Assert.Equal([(42, (bool?)null)], audio.Requests);
        Assert.Equal(41, (await runtime.GetRuntimeAsync()).VolumeLevel);
        var captured = await scenarios.CaptureAsync("实际音量快照", "", null);
        Assert.Equal(41, captured.VolumeLevel);
        Assert.True((await runtime.GetSystemVolumeAsync()).Muted);
        Assert.True((await PersistedRuntime(host).GetSystemVolumeAsync()).Muted);
    }

    /// <summary>墙面已下发而声卡不可用时，应说明部分副作用并停止媒体命令。</summary>
    [Fact]
    public async Task AudioFailureAfterWallDispatchReportsPartialEffectsAndKeepsRuntime()
    {
        var audio = new FakeSystemAudioController(new SystemAudioSnapshot(false, 0, false, "fake_core_audio", "声卡写入失败"));
        var wall = new FakeVideoWallController();
        using var root = new SecurityApplicationFactory();
        using var host = CreateHost(root, audio, wall);
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var runtime = host.Services.GetRequiredService<RuntimeStateService>();
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("音量失败预案", "", "set", "double", "set", 42,
            [new ScenarioTargetDto { WindowId = 1, SourceState = "empty" }]));

        var error = await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.ActivateAsync(scenario.Id));

        Assert.Equal("system_audio_unavailable", error.Code);
        Assert.Contains("墙面已下发", error.Message);
        Assert.Contains("声卡写入失败", error.Message);
        Assert.Equal(["double"], wall.Modes);
        var state = await runtime.GetRuntimeAsync();
        Assert.Equal("single", state.BigScreenMode);
        Assert.Equal(100, state.VolumeLevel);
        Assert.All(await runtime.GetSessionsAsync(), session =>
        {
            Assert.False(session.IsMuted);
            Assert.Empty(session.PendingCommand);
        });
    }

    /// <summary>未设置和置空仍表示保持当前系统音量，不调用声卡。</summary>
    [Theory]
    [InlineData("unset")]
    [InlineData("empty")]
    public async Task NoChangeVolumeStatesDoNotWriteAudioOrRuntime(string volumeState)
    {
        var audio = new FakeSystemAudioController(new SystemAudioSnapshot(true, 41, true, "fake_core_audio"));
        var wall = new FakeVideoWallController();
        using var root = new SecurityApplicationFactory();
        using var host = CreateHost(root, audio, wall);
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("音量不变更", "", "unset", "single", volumeState, 42, []));

        await scenarios.ActivateAsync(scenario.Id);

        Assert.Empty(audio.Requests);
        Assert.Empty(wall.Modes);
        Assert.Equal(100, (await PersistedRuntime(host).GetSystemVolumeAsync()).Level);
    }

    /// <summary>墙面失败必须先于声卡和状态写入中止预案。</summary>
    [Fact]
    public async Task WallFailureStopsBeforeAudioWriteOrPlaybackCommands()
    {
        var audio = new FakeSystemAudioController(new SystemAudioSnapshot(true, 41, true, "fake_core_audio"));
        var wall = new FakeVideoWallController { Failure = new VideoWallException("假的墙面不可达") };
        using var root = new SecurityApplicationFactory();
        using var host = CreateHost(root, audio, wall);
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("墙面失败预案", "", "set", "double", "set", 42,
            [new ScenarioTargetDto { WindowId = 1, SourceState = "empty" }]));

        var error = await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.ActivateAsync(scenario.Id));

        Assert.Contains("墙面不可达", error.Message);
        Assert.Empty(audio.Requests);
        var runtime = PersistedRuntime(host);
        Assert.Equal(100, (await runtime.GetSystemVolumeAsync()).Level);
        Assert.Equal("single", (await runtime.GetRuntimeAsync()).BigScreenMode);
        Assert.All(await runtime.GetSessionsAsync(), session => Assert.Empty(session.PendingCommand));
    }

    /// <summary>相同音量仍可重新下发，零音量也保存控制器实际静音。</summary>
    [Theory]
    [InlineData(100, false)]
    [InlineData(0, true)]
    public async Task RepeatedVolumeRequestsApplyAgainAndPersistObservedMute(int level, bool muted)
    {
        var audio = new FakeSystemAudioController(new SystemAudioSnapshot(true, level, muted, "fake_core_audio"));
        using var root = new SecurityApplicationFactory();
        using var host = CreateHost(root, audio, new FakeVideoWallController());
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("重复音量预案", "", "unset", "single", "set", level, []));

        await scenarios.ActivateAsync(scenario.Id);
        await scenarios.ActivateAsync(scenario.Id);

        Assert.Equal([(level, (bool?)null), (level, (bool?)null)], audio.Requests);
        var observed = await PersistedRuntime(host).GetSystemVolumeAsync();
        Assert.Equal(level, observed.Level);
        Assert.Equal(muted, observed.Muted);
    }

    /// <summary>仿真音量预案仍只保存意图，不要求外部音频设备可用。</summary>
    [Fact]
    public async Task SimulationVolumeRetainsIntentWithoutCallingAudioAdapter()
    {
        var audio = new FakeSystemAudioController(new SystemAudioSnapshot(false, 0, false, "runtime_state"), isHardware: false);
        using var root = new SecurityApplicationFactory();
        using var host = CreateHost(root, audio, new FakeVideoWallController(), "Simulation");
        var scenarios = host.Services.GetRequiredService<ScenarioService>();
        var runtime = PersistedRuntime(host);
        await runtime.SetSystemVolumeAsync(68, true);
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("仿真音量预案", "", "unset", "single", "set", 42, []));

        await scenarios.ActivateAsync(scenario.Id);

        Assert.Empty(audio.Requests);
        var observed = await runtime.GetSystemVolumeAsync();
        Assert.Equal(42, observed.Level);
        Assert.True(observed.Muted);
        Assert.False(observed.SystemSynced);
    }

    /// <summary>从真实 Host 装配解析预案，只替换外部硬件边界。</summary>
    private static WebApplicationFactory<Program> CreateHost(
        SecurityApplicationFactory root, ISystemAudioController audio, IVideoWallController wall, string safetyMode = "Hardware") =>
        root.WithWebHostBuilder(builder => builder.UseSetting("SafetyMode", safetyMode).ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAudioController>();
            services.AddSingleton(audio);
            services.RemoveAll<IVideoWallController>();
            services.AddSingleton(wall);
        }));

    /// <summary>通过运行态公共读接口查看已持久化数据，避免声卡替身遮蔽数据库状态。</summary>
    private static RuntimeStateService PersistedRuntime(WebApplicationFactory<Program> host) => new(
        host.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>(),
        host.Services.GetRequiredService<WriteCoordinator>(),
        host.Services.GetRequiredService<CommandCoordinator>());

    private sealed class FakeSystemAudioController(SystemAudioSnapshot applied, bool isHardware = true) : ISystemAudioController
    {
        public bool IsHardware => isHardware;
        public List<(int? Level, bool? Muted)> Requests { get; } = [];

        /// <summary>返回假的系统音量；不访问 Windows API。</summary>
        public SystemAudioSnapshot GetCurrent() => applied;

        /// <summary>记录外部边界请求并返回假的实际读回值。</summary>
        public SystemAudioSnapshot Apply(int? level, bool? muted)
        {
            Requests.Add((level, muted));
            return applied;
        }
    }

    private sealed class FakeVideoWallController : IVideoWallController
    {
        public bool IsHardware => true;
        public List<string> Modes { get; } = [];
        public VideoWallException? Failure { get; init; }

        /// <summary>禁止真实传输；本回归仅记录预案是否到达墙面边界。</summary>
        public Task DispatchAsync(string bigScreenMode, CancellationToken cancellationToken = default)
        {
            Modes.Add(bigScreenMode);
            if (Failure is not null) throw Failure;
            return Task.CompletedTask;
        }
    }
}
