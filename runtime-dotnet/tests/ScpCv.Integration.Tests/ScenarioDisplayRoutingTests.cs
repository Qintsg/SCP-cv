// 预案真实库/YAML接线与逐输出判重；保留声卡/媒体流程，全部设备使用替身。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Scenarios;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class ScenarioDisplayRoutingTests
{
    /// <summary>:returns: 右电视失败不抹去左电视完整写出记录；重试只补右侧且中止本轮媒体。</summary>
    [Fact]
    public async Task FailedTelevisionKeepsOtherTargetRecordAndRetryOnlySendsFailedTarget()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var controller = new DisplayRoutingServiceTests.RecordingTelevisions { FailTarget = "tv_right" };
        var routing = new DisplayRoutingService(fixture.Database, fixture.Writes, controller, gate, new());
        var scenarios = Create(fixture, gate, new RecordingWall(), routing);
        var scenario = await scenarios.CreateAsync(new("独立电视阶段", "", "unset", "single", "unset", 100,
            [new() { WindowId = 1, SourceState = "empty" }], Advanced: new() { Televisions = Both() }));
        Assert.Equal("scenario_display_routing_uncertain", (await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.ActivateAsync(scenario.Id))).Code);
        var state = await routing.GetAsync(); Assert.Equal("sent_unconfirmed", state["tv_left"].Status); Assert.Equal("uncertain", state["tv_right"].Status);
        await using (var db = fixture.Database.CreateDbContext()) Assert.Empty(await db.CommandRecords.ToListAsync());
        controller.FailTarget = null; await scenarios.ActivateAsync(scenario.Id);
        Assert.Equal(["tv_left", "tv_right", "tv_right"], controller.Calls.Select(call => call.Target));
    }

    /// <summary>:returns: 墙面本轮确实跳过时，后续声卡失败不把未操作的墙面误标为失效。</summary>
    [Fact]
    public async Task AudioFailureAfterSkippedWallDoesNotInvalidateUnchangedOutput()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var wall = new RecordingWall(); var audio = new RecordingAudio();
        var scenarios = Create(fixture, gate, wall, audio: audio);
        var scenario = await scenarios.CreateAsync(new("未变墙面", "", "set", "single", "unset", 100, []));
        await scenarios.ActivateAsync(scenario.Id);
        await scenarios.UpdateAsync(scenario.Id, new(null, null, null, null, "set", 20, null));
        audio.Available = false;
        Assert.Equal("system_audio_unavailable", (await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.ActivateAsync(scenario.Id))).Code);
        Assert.Equal(1, wall.Calls);
        await using var db = fixture.Database.CreateDbContext(); var runtime = await db.RuntimeStates.SingleAsync();
        Assert.Equal("sent_unconfirmed", runtime.WallLayoutDispatchStatus);
        Assert.Equal("sent_unconfirmed", DisplayRoutingState.Read(runtime, "wall")!.Status);
        audio.Available = true; await scenarios.ActivateAsync(scenario.Id); Assert.Equal(1, wall.Calls);
    }
    /// <summary>:returns: 相同预案第二轮三输出均不重发；系统音量和媒体命令仍执行。</summary>
    [Fact]
    public async Task RepeatedScenarioSkipsOnlyUnchangedOutputs()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var tv = new DisplayRoutingServiceTests.RecordingTelevisions(); var wall = new RecordingWall(); var audio = new RecordingAudio();
        var routing = new DisplayRoutingService(fixture.Database, fixture.Writes, tv, gate, new());
        var scenarios = Create(fixture, gate, wall, routing, audio);
        var scenario = await scenarios.CreateAsync(new("三个输出", "", "set", "single", "set", 20,
            [new() { WindowId = 1, SourceState = "empty" }], Advanced: new() { Televisions = Both() }));
        await scenarios.ActivateAsync(scenario.Id); await scenarios.ActivateAsync(scenario.Id);
        Assert.Equal(1, wall.Calls); Assert.Equal(2, tv.Calls.Count); Assert.Equal(2, audio.Calls);
        await using var db = fixture.Database.CreateDbContext();
        Assert.Equal(2, await db.CommandRecords.CountAsync(command => command.Command == "CLOSE"));
        Assert.All((await routing.GetAsync()).Values, output => Assert.Equal("sent_unconfirmed", output.Status));
    }

    /// <summary>:returns: 一个来源未变但目标几何改变仍会切墙，名称变化则不切。</summary>
    [Fact]
    public async Task GeometryChangesDispatchButNamesDoNot()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var wall = new RecordingWall(); var scenarios = Create(fixture, gate, wall);
        WallLayout Layout(string name, WallRegion region) => new(name, [new(region, new(WallInputKind.Window1))]);
        var full = Layout("第一次", WallRegion.Fullscreen);
        var scenario = await scenarios.CreateAsync(new("几何", "", "unset", "single", "unset", 100, [],
            Advanced: new() { WallLayoutCode = WallLayoutCode.Encode(full) }));
        await scenarios.ActivateAsync(scenario.Id);
        await scenarios.UpdateAsync(scenario.Id, new(null, null, null, null, null, null, null,
            Advanced: new() { WallLayoutCode = WallLayoutCode.Encode(Layout("改名", WallRegion.Fullscreen)) }));
        await scenarios.ActivateAsync(scenario.Id); Assert.Equal(1, wall.Calls);
        await scenarios.UpdateAsync(scenario.Id, new(null, null, null, null, null, null, null,
            Advanced: new() { WallLayoutCode = WallLayoutCode.Encode(Layout("同源左半", WallRegion.Left)) }));
        await scenarios.ActivateAsync(scenario.Id); Assert.Equal(2, wall.Calls);
    }

    /// <summary>:returns: 用户手动应用同布局始终发送；随后预案能看到同一持久记账。</summary>
    [Fact]
    public async Task ManualWallForceFeedsScenarioStateWithoutChangingItsForceSemantics()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var wall = new RecordingWall(); var commands = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = new RuntimeStateService(fixture.Database, fixture.Writes, commands, videoWall: wall, wallGate: gate);
        await runtime.SetRuntimeModeAsync("single"); await runtime.SetRuntimeModeAsync("single"); Assert.Equal(2, wall.Calls);
        var scenarios = Create(fixture, gate, wall);
        var scenario = await scenarios.CreateAsync(new("手动后预案", "", "set", "single", "unset", 100, []));
        await scenarios.ActivateAsync(scenario.Id); Assert.Equal(2, wall.Calls);
    }

    /// <summary>:returns: 任意电视不可用必须在墙面首包之前拒绝；旧预案不含电视则保持。</summary>
    [Fact]
    public async Task TelevisionPreflightRejectsAllEffectsBeforeWallAndLegacyDefaultsKeep()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var tv = new DisplayRoutingServiceTests.RecordingTelevisions(); var wall = new RecordingWall();
        var routing = new DisplayRoutingService(fixture.Database, fixture.Writes, tv, gate, new());
        var scenarios = Create(fixture, gate, wall, routing);
        var bad = await scenarios.CreateAsync(new("未登记", "", "set", "double", "unset", 100, [],
            Advanced: new() { Televisions = new() { TvLeft = new() { State = "set", Input = new() { Kind = "ip_stream", IpAddress = "192.168.5.75" } } } }));
        Assert.Equal("scenario_advanced_unavailable", (await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.ActivateAsync(bad.Id))).Code);
        Assert.Equal(0, wall.Calls); Assert.Empty(tv.Calls);
        await fixture.Writes.ExecuteAsync(async (db, token) => (await db.Scenarios.SingleAsync(item => item.Id == bad.Id, token)).AdvancedJson = "{}");
        await scenarios.ActivateAsync(bad.Id); Assert.Equal(1, wall.Calls); Assert.Empty(tv.Calls);
    }

    /// <summary>:returns: TV新字段往返YAML及旧文件缺省保持；不能在文件层丢弃电视输入。</summary>
    [Fact]
    public async Task TelevisionYamlRoundTripsAndLegacyMissingSectionKeeps()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var files = new ScenarioFileStore(fixture.TemporaryRoot);
        var scenarios = Create(fixture, gate, new RecordingWall(), files: files);
        var item = await scenarios.CreateAsync(new("电视文档", "", "unset", "single", "unset", 100, [], Advanced: new() { Televisions = Both() }));
        var yaml = await File.ReadAllTextAsync(Path.Combine(fixture.TemporaryRoot, "scenarios", "电视文档.yaml"));
        Assert.Contains("tv_left:", yaml); Assert.Contains("tv_right:", yaml); files.Verify(item);
        var parsed = ScenarioFileStore.ParseDocument(yaml).Scenario;
        Assert.Equal("window_1", parsed.Advanced.Televisions.TvLeft.Input!.Kind);
        Assert.Equal("window_2", parsed.Advanced.Televisions.TvRight.Input!.Kind);
        var legacy = ScenarioFileStore.ParseDocument("version: 1\nscenario:\n  id: 1\n  advanced:\n    camera_positions: {}\n  targets: []\n").Scenario;
        Assert.Equal("keep", legacy.Advanced.Televisions.TvLeft.State); Assert.Equal("keep", legacy.Advanced.Televisions.TvRight.State);
        var edited = await scenarios.UpdateAsync(item.Id, new(null, "更新", null, null, null, null, null,
            Advanced: item.Advanced with { Televisions = new() { TvLeft = item.Advanced.Televisions.TvLeft } }));
        files.Verify(edited); Assert.Equal("keep", edited.Advanced.Televisions.TvRight.State);
    }

    /// <summary>:returns: 从当前逻辑下发生成预案，保留两电视输入；未知状态不假造默认来源。</summary>
    [Fact]
    public async Task CaptureUsesOnlySuccessfulOutputIntents()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var routing = new DisplayRoutingService(fixture.Database, fixture.Writes, new DisplayRoutingServiceTests.RecordingTelevisions(), gate, new());
        await routing.ApplyAsync("tv_left", new() { Kind = "window_4" }, true);
        var captured = await Create(fixture, gate, new RecordingWall(), routing).CaptureAsync("当前输出", "", null);
        Assert.Equal("window_4", captured.Advanced.Televisions.TvLeft.Input!.Kind);
        Assert.Equal("keep", captured.Advanced.Televisions.TvRight.State);
        Assert.Equal("unset", captured.BigScreenModeState);
        await routing.WithInvalidatedOutputAsync("tv_left", () => Task.CompletedTask, default);
        captured = await Create(fixture, gate, new RecordingWall(), routing).CaptureAsync("状态失效后", "", null);
        Assert.Equal("keep", captured.Advanced.Televisions.TvLeft.State);
    }

    private static ScenarioTelevisionsDto Both() => new()
    {
        TvLeft = new() { State = "set", Input = new() { Kind = "window_1" } },
        TvRight = new() { State = "set", Input = new() { Kind = "window_2" } },
    };
    private static ScenarioService Create(ControlHostFixture fixture, VideoWallOperationGate gate, RecordingWall wall,
        DisplayRoutingService? routing = null, RecordingAudio? audio = null, ScenarioFileStore? files = null) =>
        new(fixture.Database, fixture.Writes, new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            videoWall: wall, wallGate: gate, systemAudio: audio, displayRouting: routing, scenarioFiles: files);
    private sealed class RecordingWall : IVideoWallController
    {
        public bool IsHardware => true;
        public int Calls { get; private set; }
        public Task DispatchAsync(string mode, CancellationToken cancellationToken = default) { Calls++; return Task.CompletedTask; }
        public Task<VideoWallDispatchReceipt> DispatchLayoutAsync(WallLayout layout, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new VideoWallDispatchReceipt(true, 1)); }
    }
    private sealed class RecordingAudio : ISystemAudioController
    {
        public bool IsHardware => true;
        public bool Available { get; set; } = true;
        public int Calls { get; private set; }
        public SystemAudioSnapshot GetCurrent() => new(Available, 20, false, "test");
        public SystemAudioSnapshot Apply(int? level, bool? muted) { Calls++; return GetCurrent(); }
    }
}
