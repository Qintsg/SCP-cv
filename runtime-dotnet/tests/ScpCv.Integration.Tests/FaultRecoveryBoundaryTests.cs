// 两块大屏的失联观测、旧代次/所有权隔离及确认停机后的迟到结果回归；不启动实体 Worker。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class FaultRecoveryBoundaryTests
{
    /// <summary>当前 Worker 的旧页或未来页状态均不得覆盖已确认的新画面。</summary>
    [Theory]
    [InlineData(1, -1)]
    [InlineData(1, 1)]
    [InlineData(2, -1)]
    [InlineData(2, 1)]
    public async Task DisplayStateRejectsGenerationMismatchWithoutChangingCurrentPicture(int windowId, int offset)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var ownership = await ArmAndRegisterAsync(fixture, windowId);
        await SetCurrentPictureAsync(fixture, windowId);
        var publisher = CreatePublisher(fixture);

        var rejected = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, windowId, ownership.WorkerInstanceId, ownership.OwnerEpoch,
            PictureReport(generation: 2 + offset, sourceId: 99, slide: 1, state: "error"));

        Assert.False(rejected.Accepted);
        Assert.Equal("stale_generation", rejected.Reason);
        await AssertCurrentPictureAsync(fixture, windowId);
    }

    /// <summary>证明旧进程退出并更换所有权后，旧实例即使携带当前源代次也不能落库。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReplacedWorkerCannotOverwriteCurrentPictureWithMatchingSourceGeneration(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var previous = await ArmAndRegisterAsync(fixture, windowId);
        var group = await fixture.RuntimeAuthority.GetGroupAsync();
        var replacement = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Display, windowId, Guid.NewGuid(), 202, fixture.TimeProvider.GetUtcNow(),
            1, group.GroupEpoch, "[\"image\"]", PreviousOwnerExitConfirmed: true));
        await SetCurrentPictureAsync(fixture, windowId);
        var publisher = CreatePublisher(fixture);

        var rejected = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, windowId, previous.WorkerInstanceId, previous.OwnerEpoch,
            PictureReport(generation: 2, sourceId: 99, slide: 1, state: "error"));

        Assert.False(rejected.Accepted);
        Assert.Equal("worker_fenced", rejected.Reason);
        Assert.Equal(previous.OwnerEpoch + 1, replacement.OwnerEpoch);
        await AssertCurrentPictureAsync(fixture, windowId);
        var accepted = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, windowId, replacement.WorkerInstanceId, replacement.OwnerEpoch,
            PictureReport(generation: 2, sourceId: 41, slide: 8, state: "playing"));
        Assert.True(accepted.Accepted);
        Assert.Equal(8, (await CreateRuntime(fixture).GetSessionAsync(windowId)).CurrentSlide);
    }

    /// <summary>确认整组退出后，旧 claim 的结果在重启前后均拒绝且不阻塞新命令。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ConfirmedStopFencesLateResultAndAllowsOnlyNewClaimAfterExplicitStart(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var previous = await ArmAndRegisterAsync(fixture, windowId);
        var group = await fixture.RuntimeAuthority.GetGroupAsync();
        await fixture.Commands.EnqueueAsync(new EnqueueCommand(
            CommandTargetKind.Display, windowId, "OPEN", "{}", 1, 1));
        var oldClaim = await fixture.Commands.ClaimAsync(new ClaimCommand(
            CommandTargetKind.Display, windowId, previous.WorkerInstanceId, previous.OwnerEpoch,
            group.GroupEpoch, TimeSpan.FromSeconds(30)));
        Assert.NotNull(oldClaim);
        var lateResult = new CompleteCommand(oldClaim.CommandId, oldClaim.ClaimToken!.Value,
            previous.OwnerEpoch, CommandStatus.Completed, "ok", "old-result", "{}");

        var draining = await fixture.RuntimeAuthority.BeginDrainAsync("test_confirmed_exit");
        await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        await Assert.ThrowsAsync<CommandFenceException>(() => fixture.Commands.CompleteAsync(lateResult));

        var requestId = Guid.NewGuid();
        var restarting = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        await fixture.RuntimeAuthority.ArmAsync(requestId, restarting.GroupEpoch);
        var replacement = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Display, windowId, Guid.NewGuid(), 202, fixture.TimeProvider.GetUtcNow(),
            1, restarting.GroupEpoch, "[\"image\"]", PreviousOwnerExitConfirmed: true));
        var newCommand = await fixture.Commands.EnqueueAsync(new EnqueueCommand(
            CommandTargetKind.Display, windowId, "OPEN", "{}", 2, 1));
        var newClaim = await fixture.Commands.ClaimAsync(new ClaimCommand(
            CommandTargetKind.Display, windowId, replacement.WorkerInstanceId, replacement.OwnerEpoch,
            restarting.GroupEpoch, TimeSpan.FromSeconds(30)));

        Assert.NotNull(newClaim);
        Assert.Equal(newCommand.CommandId, newClaim.CommandId);
        Assert.NotEqual(oldClaim.ClaimToken, newClaim.ClaimToken);
        await Assert.ThrowsAsync<CommandFenceException>(() => fixture.Commands.CompleteAsync(lateResult));
        await using var database = fixture.Database.CreateDbContext();
        var retired = await database.CommandRecords.SingleAsync(command => command.CommandId == oldClaim.CommandId);
        Assert.Equal(CommandStatus.Superseded, retired.Status);
        Assert.Equal("runtime_group_stopped", retired.ResultCode);
        Assert.Null(retired.ClaimToken);
        Assert.Equal(CommandStatus.Processing,
            (await database.CommandRecords.SingleAsync(command => command.CommandId == newClaim.CommandId)).Status);
    }

    /// <summary>不把进程存活当作在线；五秒未见心跳后离线，同一合法所有者恢复可见。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task HeartbeatExpiryAndRecoveryAreVisibleWithoutChangingPicture(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var ownership = await ArmAndRegisterAsync(fixture, windowId);
        await SetCurrentPictureAsync(fixture, windowId);
        var runtime = CreateRuntime(fixture);
        Assert.True(await fixture.RuntimeAuthority.RecordHeartbeatAsync(
            CommandTargetKind.Display, windowId, ownership.WorkerInstanceId, ownership.OwnerEpoch, uiProgress: false));
        Assert.True((await runtime.GetSessionAsync(windowId)).PlayerOnline);

        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(6));
        var offline = await runtime.GetSessionAsync(windowId);
        Assert.False(offline.PlayerOnline);
        Assert.Equal("playing", offline.PlaybackState);
        await AssertCurrentPictureAsync(fixture, windowId);

        Assert.True(await fixture.RuntimeAuthority.RecordHeartbeatAsync(
            CommandTargetKind.Display, windowId, ownership.WorkerInstanceId, ownership.OwnerEpoch, uiProgress: false));
        Assert.True((await runtime.GetSessionAsync(windowId)).PlayerOnline);
        await AssertCurrentPictureAsync(fixture, windowId);
    }

    /// <summary>登记只用受控进程身份数据，不创建 Windows 播放进程。</summary>
    /// <remarks>:param fixture: 独立 SQLite 测试实例。:param windowId: 大屏窗口号。:returns: 当前 Worker 所有权。</remarks>
    private static async Task<WorkerOwnership> ArmAndRegisterAsync(ControlHostFixture fixture, int windowId)
    {
        var requestId = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        await fixture.RuntimeAuthority.ArmAsync(requestId, starting.GroupEpoch);
        return await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Display, windowId, Guid.NewGuid(), 101, fixture.TimeProvider.GetUtcNow(),
            1, starting.GroupEpoch, "[\"image\"]"));
    }

    /// <summary>保存独立的新画面样本，以检测被拒绝报告是否仍有副作用。</summary>
    /// <remarks>:param fixture: 独立 SQLite 测试实例。:param windowId: 大屏窗口号。</remarks>
    private static Task SetCurrentPictureAsync(ControlHostFixture fixture, int windowId) =>
        fixture.Writes.ExecuteAsync(async (database, cancellationToken) =>
        {
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId, cancellationToken);
            session.DesiredGeneration = 2;
            session.ObservedGeneration = 2;
            session.ActualSourceId = 41;
            session.PlaybackState = PlaybackState.Playing;
            session.CurrentSlide = 7;
            session.ErrorMessage = string.Empty;
        });

    /// <summary>检查画面关键字段，不以单个 Accepted 标记代替持久化断言。</summary>
    /// <remarks>:param fixture: 独立 SQLite 测试实例。:param windowId: 大屏窗口号。</remarks>
    private static async Task AssertCurrentPictureAsync(ControlHostFixture fixture, int windowId)
    {
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId);
        Assert.Equal(2, session.DesiredGeneration);
        Assert.Equal(2, session.ObservedGeneration);
        Assert.Equal(41, session.ActualSourceId);
        Assert.Equal(PlaybackState.Playing, session.PlaybackState);
        Assert.Equal(7, session.CurrentSlide);
        Assert.Empty(session.ErrorMessage);
    }

    /// <summary>构造明确带源代次的状态合同。</summary>
    /// <remarks>:param generation: 源代次。:param sourceId: 实际源。:param slide: 实际页。:param state: 实际状态。:returns: 状态报告。</remarks>
    private static StateReportDto PictureReport(long generation, long sourceId, int slide, string state) => new()
    {
        SourceGeneration = generation,
        State = JsonSerializer.SerializeToElement(new
        {
            source_id = sourceId,
            current_slide = slide,
            playback_state = state,
            error_message = state == "error" ? "旧 Worker 的迟到错误" : string.Empty,
        }),
    };

    /// <summary>复用真实状态投影和 SSE 服务，但外部命令唤醒无副作用。</summary>
    /// <remarks>:param fixture: 独立 SQLite 测试实例。:returns: 真实状态投影服务。</remarks>
    private static RuntimeProjectionPublisher CreatePublisher(ControlHostFixture fixture)
    {
        var coordinator = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = new RuntimeStateService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
        var audio = new BackgroundAudioService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
        return new RuntimeProjectionPublisher(
            fixture.Writes, new SseEventHub(runtime, audio, new SseEventStreamOptions()), fixture.TimeProvider);
    }

    /// <summary>复用对外会话的真实在线判据。</summary>
    /// <remarks>:param fixture: 独立 SQLite 测试实例。:returns: 无硬件副作用的运行态服务。</remarks>
    private static RuntimeStateService CreateRuntime(ControlHostFixture fixture) => new(
        fixture.Database, fixture.Writes, new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
}
