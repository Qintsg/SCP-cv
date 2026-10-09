// 完整组合的持久投影、静音、草稿版本及发送失败边界；不发实体网络包。
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Scenarios;
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class VideoWallLayoutApplicationTests
{
    [Fact]
    public async Task CaptureKeepsCustomWallOutOfLegacyModeContract()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var commands = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = Runtime(fixture, new SimulationVideoWallController());
        await runtime.ApplyWallLayoutAsync(new WallLayout("IP81", [new(WallRegion.Fullscreen, new(WallInputKind.Laptop))]), 0);
        var scenarios = new ScenarioService(fixture.Database, fixture.Writes, commands);
        var captured = await scenarios.CaptureAsync("快照", "", null);
        Assert.Equal("unset", captured.BigScreenModeState);
        Assert.Equal("single", captured.BigScreenMode);
        await scenarios.ActivateAsync(captured.Id);
        Assert.Equal("custom", (await runtime.GetRuntimeAsync()).BigScreenMode);
    }

    [Fact]
    public async Task InterruptedDispatchBecomesUncertainOnRestartWithoutLosingAppliedLayout()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var runtime = Runtime(fixture, new SimulationVideoWallController());
        var service = new VideoWallLayoutService(fixture.Database, fixture.Writes, runtime);
        await runtime.ApplyWallLayoutAsync(new WallLayout("IP81", [new(WallRegion.Fullscreen, new(WallInputKind.Laptop))]), 0);
        var prior = (await service.GetAsync()).Applied!;
        await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var state = await database.RuntimeStates.SingleAsync(token);
            state.WallLayoutDispatchStatus = "dispatching";
        });
        await new DatabaseInitializer(fixture.Database).InitializeAsync();
        var restored = await service.GetAsync();
        Assert.Equal("uncertain", restored.DispatchStatus);
        Assert.Equal(prior.Mappings, restored.Applied!.Mappings);
        Assert.Contains("中断", restored.DispatchDetail);
    }

    [Fact]
    public async Task LegacyScenarioReplacesCustomAppliedProjection()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var commands = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = Runtime(fixture, new SimulationVideoWallController());
        await runtime.ApplyWallLayoutAsync(new WallLayout("IP81", [new(WallRegion.Fullscreen, new(WallInputKind.Laptop))]), 0);
        var scenarios = new ScenarioService(fixture.Database, fixture.Writes, commands);
        var scenario = await scenarios.CreateAsync(new ScenarioWriteModel("旧双屏", "", "set", "double", "unset", 100, []));
        await scenarios.ActivateAsync(scenario.Id);
        var service = new VideoWallLayoutService(fixture.Database, fixture.Writes, runtime);
        var applied = await service.GetAsync();
        Assert.Equal("double", applied.ActivePreset);
        Assert.Equal("window_1_left_window_2_right", applied.Applied!.Preset);
        Assert.Equal("simulated", applied.DispatchStatus);
        Assert.Empty((await runtime.GetRuntimeAsync()).MutedWindows);
    }

    public static IEnumerable<object[]> Combinations()
    {
        var kinds = new[] { WallInputKind.Window1, WallInputKind.Window2, WallInputKind.Laptop };
        foreach (var left in kinds)
        {
            yield return [left, null!];
            foreach (var right in kinds) yield return [left, right];
        }
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public async Task AllCombinationsApplyAndPersistWithExplicitResultAndCorrectMute(WallInputKind left, WallInputKind? right)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var wall = new SimulationVideoWallController();
        var runtime = Runtime(fixture, wall);
        var service = new VideoWallLayoutService(fixture.Database, fixture.Writes, runtime, fixture.TimeProvider);
        var layout = new WallLayout("组合", right is null
            ? [new(WallRegion.Fullscreen, new(left))]
            : [new(WallRegion.Left, new(left)), new(WallRegion.Right, new(right.Value))]);
        var saved = await service.SaveDraftAsync(layout);
        Assert.True(saved.Draft!.CanApply);
        var applied = await service.ApplyDraftAsync(expectedRevision: saved.DraftRevision);
        Assert.Equal("simulated", applied.DispatchStatus);
        Assert.Equal(saved.DraftRevision, applied.AppliedRevision);
        Assert.Equal(saved.Draft.Mappings, applied.Applied!.Mappings);
        var reconstructed = new VideoWallLayoutService(fixture.Database, fixture.Writes, Runtime(fixture, wall), fixture.TimeProvider);
        Assert.Equal(applied.Applied!.Mappings, (await reconstructed.GetAsync()).Applied!.Mappings);
        var used = layout.Mappings.Select(item => item.Input.Kind).ToHashSet();
        var muted = new[] { (1, WallInputKind.Window1), (2, WallInputKind.Window2) }
            .Where(item => !used.Contains(item.Item2)).Select(item => item.Item1);
        Assert.Equal(muted, (await runtime.GetRuntimeAsync()).MutedWindows);
    }

    [Fact]
    public async Task StaleDraftFailsWithoutDispatchAndFailedDispatchIsUncertain()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var wall = new FailingWall();
        var service = new VideoWallLayoutService(fixture.Database, fixture.Writes, Runtime(fixture, wall), fixture.TimeProvider);
        var layout = new WallLayout("窗口2", [new(WallRegion.Fullscreen, new(WallInputKind.Window2))]);
        var first = await service.SaveDraftAsync(layout);
        await service.SaveDraftAsync(layout with { Name = "新版本" });
        var stale = await Assert.ThrowsAsync<VideoWallLayoutException>(() => service.ApplyDraftAsync(expectedRevision: first.DraftRevision));
        Assert.Equal("layout_revision_conflict", stale.Code);
        Assert.Equal(0, wall.Dispatches);
        await Assert.ThrowsAsync<PlaybackServiceException>(() => service.ApplyDraftAsync());
        Assert.Equal(1, wall.Dispatches);
        var state = await service.GetAsync();
        Assert.Equal("uncertain", state.DispatchStatus);
        Assert.Null(state.Applied);
    }

    private static RuntimeStateService Runtime(ControlHostFixture fixture, IVideoWallController wall) => new(
        fixture.Database, fixture.Writes, new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
        fixture.TimeProvider, videoWall: wall);

    private sealed class FailingWall : IVideoWallController
    {
        public bool IsHardware => true;
        public int Dispatches { get; private set; }
        public Task DispatchAsync(string mode, CancellationToken cancellationToken = default) => throw new VideoWallException("受控失败");
        public Task<VideoWallDispatchReceipt> DispatchLayoutAsync(WallLayout layout, CancellationToken cancellationToken = default)
        {
            Dispatches++;
            throw new VideoWallException("受控中途失败");
        }
    }
}
