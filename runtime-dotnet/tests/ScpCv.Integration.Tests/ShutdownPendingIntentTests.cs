/*
 * 确认停机后的未领取 CLOSE 隔离回归：真实 SQLite 与公开运行组、重置、租约链。
 * 不创建真实 Worker，不以整组退出伪造命令 Completed 或最后实际画面观测。
 * @Project : SCP-cv
 * @File : ShutdownPendingIntentTests.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class ShutdownPendingIntentTests
{
    private const long LastObservedGeneration = 9;

    /// <summary>
    /// Drain 后排队且未领取的 CLOSE，确认停机后不得转投新运行组的合法所有者。
    /// :param windowId: 大屏窗口 1 或 2。
    /// :returns: 旧命令无新租约；测试不会为其伪造完成回执。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ConfirmedStopCannotReissueUnclaimedCloseToNewOwner(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var stopped = await CreateStoppedCloseAsync(fixture, windowId);
        var leases = CreateLeases(fixture);
        await Assert.ThrowsAsync<CommandFenceException>(() => leases.ClaimAsync(new LeaseClaimRequest(
            CommandTargetKind.Display, windowId, stopped.PreviousOwner.WorkerInstanceId,
            stopped.PreviousOwner.OwnerEpoch, stopped.PreviousGroupEpoch, TimeSpan.FromSeconds(30))));

        var replacement = await StartOwnerAsync(fixture, windowId);
        Assert.True(replacement.Group.GroupEpoch > stopped.StoppedGroupEpoch);
        Assert.True(replacement.Owner.OwnerEpoch > stopped.PreviousOwner.OwnerEpoch);

        // 故意不先发 OPEN：OPEN 会替换全部 Pending，掩盖旧 CLOSE 仍可领取的问题。
        var unexpected = await leases.ClaimAsync(new LeaseClaimRequest(
            CommandTargetKind.Display, windowId, replacement.Owner.WorkerInstanceId,
            replacement.Owner.OwnerEpoch, replacement.Group.GroupEpoch, TimeSpan.FromSeconds(30)));

        Assert.Null(unexpected);
        await using var database = fixture.Database.CreateDbContext();
        var retired = await database.CommandRecords.SingleAsync(command => command.CommandId == stopped.CloseId);
        Assert.Equal(CommandStatus.Superseded, retired.Status);
        Assert.Null(retired.ConsumerInstanceId);
        Assert.Null(retired.ClaimToken);
    }

    /// <summary>
    /// 新组收到明确的新 OPEN 后仍可正常领取；领取不等于实际画面或命令已完成。
    /// :param windowId: 大屏窗口 1 或 2。
    /// :returns: 仅新 OPEN 获得租约，旧实际源及 observed 保持最后已知证据。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NewExplicitOpenRemainsClaimableWithoutRewritingLastObservation(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var stopped = await CreateStoppedCloseAsync(fixture, windowId);
        var replacement = await StartOwnerAsync(fixture, windowId);
        var newSourceId = await SeedImageAsync(fixture, "new-explicit-open");
        await CreateRuntime(fixture).OpenSourceAsync(windowId, newSourceId, autoplay: true, targetSlide: 0);
        Guid newOpenId;
        await using (var beforeClaim = fixture.Database.CreateDbContext())
        {
            newOpenId = (await beforeClaim.CommandRecords.SingleAsync(command =>
                command.TargetKind == CommandTargetKind.Display && command.TargetId == windowId &&
                command.Command == "OPEN")).CommandId;
        }

        var claimed = await CreateLeases(fixture).ClaimAsync(new LeaseClaimRequest(
            CommandTargetKind.Display, windowId, replacement.Owner.WorkerInstanceId,
            replacement.Owner.OwnerEpoch, replacement.Group.GroupEpoch, TimeSpan.FromSeconds(30)));

        Assert.NotNull(claimed);
        Assert.Equal(newOpenId, claimed.CommandId);
        Assert.NotEqual(stopped.CloseId, claimed.CommandId);
        Assert.Equal("OPEN", claimed.Command);
        Assert.Equal(CommandStatus.Processing, claimed.Status);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId);
        Assert.Equal(newSourceId, session.MediaSourceId);
        Assert.Equal(stopped.PreviousSourceId, session.ActualSourceId);
        Assert.Equal(LastObservedGeneration, session.ObservedGeneration);
        Assert.Equal(LastObservedGeneration + 2, session.DesiredGeneration);
        Assert.NotEqual(CommandStatus.Completed,
            (await database.CommandRecords.SingleAsync(command => command.CommandId == stopped.CloseId)).Status);
    }

    /// <summary>
    /// 终止运行组不能冒充旧 CLOSE 已执行，也不能把最后实际源和 observed 改成期望值。
    /// :param windowId: 大屏窗口 1 或 2。
    /// :returns: Stopped 与 Idle 意图成立，最后实际画面证据仍保留。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ConfirmedStopDoesNotFabricateCloseCompletionOrObservedPicture(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var stopped = await CreateStoppedCloseAsync(fixture, windowId);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId);
        var close = await database.CommandRecords.SingleAsync(command => command.CommandId == stopped.CloseId);

        Assert.Null(session.MediaSourceId);
        Assert.Equal(PlaybackState.Idle, session.PlaybackState);
        Assert.Equal(stopped.PreviousSourceId, session.ActualSourceId);
        Assert.Equal(LastObservedGeneration, session.ObservedGeneration);
        Assert.Equal(LastObservedGeneration + 1, session.DesiredGeneration);
        Assert.NotEqual(CommandStatus.Completed, close.Status);
        Assert.Equal(0, close.AttemptCount);
        Assert.Null(close.ConsumerInstanceId);
        Assert.Null(close.ClaimToken);
    }

    /// <summary>
    /// 复现公开 Drain→ResetAll→CompleteStop 链；不发送 Worker 完成或状态报告。
    /// :param fixture: 独立真实 SQLite 测试实例。
    /// :param windowId: 当前需要核对的大屏窗口。
    /// :returns: 旧 CLOSE 与前后运行组身份，用于不经过新 OPEN 的领取检查。
    /// </summary>
    private static async Task<StoppedClose> CreateStoppedCloseAsync(ControlHostFixture fixture, int windowId)
    {
        var previous = await StartOwnerAsync(fixture, windowId);
        var sourceId = await SeedImageAsync(fixture, "last-observed-image");
        await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId, token);
            session.MediaSourceId = sourceId;
            session.ActualSourceId = sourceId;
            session.PlaybackState = PlaybackState.Playing;
            session.DesiredGeneration = LastObservedGeneration;
            session.ObservedGeneration = LastObservedGeneration;
            session.PlayerLastSeenAt = fixture.TimeProvider.GetUtcNow();
        });

        var draining = await fixture.RuntimeAuthority.BeginDrainAsync("system_shutdown");
        await CreateRuntime(fixture).ResetAllAsync();
        Guid closeId;
        await using (var queued = fixture.Database.CreateDbContext())
        {
            var close = await queued.CommandRecords.SingleAsync(command =>
                command.TargetKind == CommandTargetKind.Display && command.TargetId == windowId &&
                command.Command == "CLOSE");
            Assert.Equal(CommandStatus.Pending, close.Status);
            Assert.Equal(LastObservedGeneration + 1, close.SourceGeneration);
            closeId = close.CommandId;
        }
        var confirmed = await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        Assert.Equal(RuntimeGroupState.Stopped, confirmed.State);
        Assert.Equal("system_shutdown", confirmed.StopReason);
        return new StoppedClose(closeId, sourceId, previous.Owner, previous.Group.GroupEpoch, confirmed.GroupEpoch);
    }

    /// <summary>
    /// 明确启动授权与新所有者登记；退出证明只是测试输入，不操作或伪装真实进程退出。
    /// :param fixture: 独立真实 SQLite 测试实例。
    /// :param windowId: 大屏窗口号。
    /// :returns: 新 Armed 运行组和匹配该组的所有权。
    /// </summary>
    private static async Task<(RuntimeGroupControl Group, WorkerOwnership Owner)> StartOwnerAsync(
        ControlHostFixture fixture, int windowId)
    {
        var requestId = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        var armed = await fixture.RuntimeAuthority.ArmAsync(requestId, starting.GroupEpoch);
        var owner = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Display, windowId, Guid.NewGuid(), 101, fixture.TimeProvider.GetUtcNow(),
            1, armed.GroupEpoch, "[\"image\"]", PreviousOwnerExitConfirmed: true));
        return (armed, owner);
    }

    /// <summary>
    /// 保存有限媒体元数据供真实 OPEN/CLOSE 入队，不创建文件或播放器。
    /// :param fixture: 独立 SQLite 测试实例。
    /// :param name: 夹具媒体名称。
    /// :returns: 持久媒体主键。
    /// </summary>
    private static async Task<long> SeedImageAsync(ControlHostFixture fixture, string name)
    {
        await using var database = fixture.Database.CreateDbContext();
        var source = new MediaSource
        {
            SourceType = MediaSourceType.Image, Name = name, Uri = $"{name}.png",
            IsAvailable = true, SourceRevision = 1, CreatedAt = fixture.TimeProvider.GetUtcNow(),
        };
        database.MediaSources.Add(source);
        await database.SaveChangesAsync();
        return source.Id;
    }

    /// <summary>
    /// 复用真实持久化重置模块，唤醒 seam 不创建任何外部 Worker。
    /// :param fixture: 独立 SQLite 测试实例。
    /// :returns: 公开运行状态入口。
    /// </summary>
    private static RuntimeStateService CreateRuntime(ControlHostFixture fixture) => new(
        fixture.Database, fixture.Writes,
        new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);

    /// <summary>
    /// 复用真实当前组、所有权和 durable 命令领取校验。
    /// :param fixture: 独立 SQLite 测试实例。
    /// :returns: 公开租约入口。
    /// </summary>
    private static CommandLeaseService CreateLeases(ControlHostFixture fixture) => new(
        fixture.Commands, fixture.RuntimeAuthority, fixture.Database, fixture.Writes, fixture.TimeProvider);

    private sealed record StoppedClose(
        Guid CloseId, long PreviousSourceId, WorkerOwnership PreviousOwner,
        long PreviousGroupEpoch, long StoppedGroupEpoch);
}
