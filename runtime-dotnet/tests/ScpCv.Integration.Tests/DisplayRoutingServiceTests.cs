// 真SQLite与假设备的输出记账/去重回归；所有设备发送均为内存替身。
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class DisplayRoutingServiceTests
{
    private static VideoWallInputDto Input(string kind = "window_1", string ip = "") => new() { Kind = kind, IpAddress = ip };

    /// <summary>:returns: 未登记旧库是unknown；成功记录重建服务仍可判等，手动强制不跳过。</summary>
    [Fact]
    public async Task CompleteSendPersistsAndOnlyNonForcedDuplicateSkips()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var service = Service(fixture, controller, gate);
        Assert.All((await service.GetAsync()).Values, output => { Assert.Equal("unknown", output.Status); Assert.Null(output.LastSent); });
        var first = await service.ApplyAsync("tv_left", Input(), false);
        Assert.Equal("sent_unconfirmed", first.Result); Assert.Equal("window_1", first.Output.LastSent!.Input!.Kind);
        var reloaded = Service(fixture, controller, gate);
        Assert.Equal("unchanged", (await reloaded.ApplyAsync("tv_left", Input("ip_stream", "192.168.5.63"), false)).Result);
        Assert.Single(controller.Calls);
        Assert.Equal("sent_unconfirmed", (await reloaded.ApplyAsync("tv_left", Input(), true)).Result);
        Assert.Equal(2, controller.Calls.Count);
        Assert.Equal("unknown", (await reloaded.GetAsync())["tv_right"].Status);
    }

    /// <summary>:returns: 门禁内重读，65并发相同意图仅一次完整下发。</summary>
    [Fact]
    public async Task ConcurrentSameInputOnlyDispatchesOncePerTarget()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var service = Service(fixture, controller, gate);
        var results = await Task.WhenAll(Enumerable.Range(0, 65).Select(_ => service.ApplyAsync("tv_left", Input(), false)));
        Assert.Single(controller.Calls); Assert.Equal(64, results.Count(result => result.Result == "unchanged"));
        await service.ApplyAsync("tv_right", Input(), false);
        Assert.Equal(2, controller.Calls.Count);
    }

    /// <summary>:param status: 所有不可信状态均不能复用旧成功记录。</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("dispatching")]
    [InlineData("uncertain")]
    public async Task UnknownOrIncompleteRecordsDoNotSkip(string status)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var service = Service(fixture, controller, gate); await service.ApplyAsync("tv_left", Input(), false);
        await fixture.Writes.ExecuteAsync(async (db, token) =>
        {
            var state = await db.RuntimeStates.SingleAsync(token);
            DisplayRoutingState.Invalidate(state, "tv_left", status, fixture.TimeProvider.GetUtcNow());
        });
        await service.ApplyAsync("tv_left", Input(), false); Assert.Equal(2, controller.Calls.Count);
    }

    /// <summary>:returns: 第二帧失败保留历史但失效指纹；恢复后的同源请求必须重发。</summary>
    [Fact]
    public async Task PartialFailureNeverKeepsPreviousReusableFingerprint()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var service = Service(fixture, controller, gate); await service.ApplyAsync("tv_left", Input(), false);
        controller.Fail = true;
        var error = await Assert.ThrowsAsync<DisplayRoutingException>(() => service.ApplyAsync("tv_left", Input("window_2"), false));
        Assert.Equal("display_routing_uncertain", error.Code);
        var failed = (await service.GetAsync())["tv_left"];
        Assert.Equal("uncertain", failed.Status); Assert.Equal("window_1", failed.LastSent!.Input!.Kind);
        controller.Fail = false;
        await service.ApplyAsync("tv_left", Input(), false); Assert.Equal(3, controller.Calls.Count);
    }

    /// <summary>:returns: 已知电源动作先使路由失效，动作期间不能交错保存另一布局。</summary>
    [Fact]
    public async Task PowerInvalidationRunsInsideSameOutputGate()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var service = Service(fixture, controller, gate); await service.ApplyAsync("tv_left", Input(), false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var power = service.WithInvalidatedOutputAsync("tv_left", async () => { entered.SetResult(); await release.Task; }, default);
        await entered.Task;
        Assert.Equal("unknown", (await service.GetAsync())["tv_left"].Status);
        var pending = service.ApplyAsync("tv_left", Input(), false);
        Assert.False(pending.IsCompleted); release.SetResult(); await power; await pending;
        Assert.Equal(2, controller.Calls.Count);
    }

    /// <summary>:returns: 输入尺寸/协议改变即使地址相同也必须重发。</summary>
    [Fact]
    public async Task SourceConfigurationChangesInvalidateFingerprint()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var options = new VideoWallProtocolOptions();
        var service = Service(fixture, controller, gate, options); await service.ApplyAsync("tv_left", Input(), false);
        options.Inputs["window_1"].Width = 1920; options.Inputs["window_1"].Height = 1080;
        await service.ApplyAsync("tv_left", Input(), false); Assert.Equal(2, controller.Calls.Count);
    }

    /// <summary>:returns: 未知来源/目标/通道结构在首包前拒绝，包含75。</summary>
    [Theory]
    [InlineData("tv_left", "ip_stream", "192.168.5.75")]
    [InlineData("tv_left", "custom", "192.168.5.63")]
    [InlineData("wall", "window_1", "")]
    [InlineData("tv_left", "window_1", "192.168.5.63")]
    public async Task InvalidRequestsHaveNoSendOrTrustedRecord(string target, string kind, string ip)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var service = Service(fixture, controller, gate);
        await Assert.ThrowsAsync<DisplayRoutingException>(() => service.ApplyAsync(target, Input(kind, ip), true));
        Assert.Empty(controller.Calls); Assert.All((await service.GetAsync()).Values, value => Assert.Equal("unknown", value.Status));
    }

    /// <summary>:returns: 模拟记录不能跨硬件边界成为去重证据。</summary>
    [Fact]
    public async Task SimulatedRecordDoesNotSuppressHardwareDispatch()
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var simulation = new DisplayRoutingService(fixture.Database, fixture.Writes, new SimulationTelevisionController(), gate, new());
        Assert.Equal("simulated", (await simulation.ApplyAsync("tv_left", Input(), false)).Result);
        var hardware = new RecordingTelevisions(); await Service(fixture, hardware, gate).ApplyAsync("tv_left", Input(), false);
        Assert.Single(hardware.Calls);
    }

    /// <summary>:returns: 真初始化器升级旧库，保留历史墙面字段且电视未知。</summary>
    [Fact]
    public async Task RestartRecoversInterruptedRecordsWithoutReplayingOrLosingLastSent()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var controller = new RecordingTelevisions(); using var gate = new VideoWallOperationGate();
        var service = Service(fixture, controller, gate); await service.ApplyAsync("tv_left", Input(), false);
        await fixture.Writes.ExecuteAsync(async (db, token) =>
        {
            var state = await db.RuntimeStates.SingleAsync(token);
            DisplayRoutingState.Invalidate(state, "tv_left", "dispatching", fixture.TimeProvider.GetUtcNow());
        });
        await new DatabaseInitializer(fixture.Database).InitializeAsync();
        Assert.Equal("uncertain", (await service.GetAsync())["tv_left"].Status);
        Assert.Equal("window_1", (await service.GetAsync())["tv_left"].LastSent!.Input!.Kind);
        Assert.Single(controller.Calls);
    }

    private static DisplayRoutingService Service(ControlHostFixture fixture, RecordingTelevisions controller, VideoWallOperationGate gate, VideoWallProtocolOptions? options = null) =>
        new(fixture.Database, fixture.Writes, controller, gate, options ?? new(), fixture.TimeProvider);

    internal sealed class RecordingTelevisions : ITelevisionController
    {
        public bool IsHardware => true;
        public bool Fail { get; set; }
        public string? FailTarget { get; set; }
        public List<(string Target, IReadOnlyList<byte[]> Frames)> Calls { get; } = [];
        public Task DispatchAsync(string target, IReadOnlyList<byte[]> frames, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls.Add((target, frames));
            return Fail || target == FailTarget ? Task.FromException(new DisplayRoutingException("模拟第二帧失败")) : Task.CompletedTask;
        }
    }
}
