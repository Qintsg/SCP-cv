/*
 * 确认停机事务的旧意图退役、投影清理、历史证据与代次围栏回归。
 * 真实 SQLite 及公开持久化入口；不创建原生进程，不伪造关闭完成或实际画面。
 * @Project : SCP-cv
 * @File : ShutdownPendingIntentTests.StopTransaction.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class ShutdownPendingIntentTests
{
    /// <summary>
    /// 两窗停机后兼容 pending 投影应清空，最后实际源与观测代次仍保留。
    /// :param windowId: 当前大屏窗口号。
    /// :returns: 清理不冒充旧 CLOSE 的完成或实际出画观测。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ConfirmedStopClearsCurrentDisplayProjectionWithoutChangingObservation(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var stopped = await CreateStoppedCloseAsync(fixture, windowId);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId);
        Assert.Equal(string.Empty, session.PendingCommand);
        Assert.Equal("{}", session.CommandArgsJson);
        Assert.Null(session.MediaSourceId);
        Assert.Equal(stopped.PreviousSourceId, session.ActualSourceId);
        Assert.Equal(LastObservedGeneration, session.ObservedGeneration);
        Assert.Equal(LastObservedGeneration + 1, session.DesiredGeneration);
        var close = await database.CommandRecords.SingleAsync(item => item.CommandId == stopped.CloseId);
        Assert.Equal(CommandStatus.Superseded, close.Status);
        Assert.Equal("runtime_group_stopped", close.ResultCode);
        Assert.Equal(0, close.AttemptCount);
    }

    /// <summary>
    /// 音频未领取 STOP 不得转投新 owner，投影清理不改变最后位置和 observed。
    /// :returns: 新组无旧音频租约，源与观测证据未被退队列事务改写。
    /// </summary>
    [Fact]
    public async Task ConfirmedStopRetiresPendingAudioBeforeNewOwnerCanClaim()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var started = await StartOwnerAsync(fixture, 1);
        await RegisterAudioOwnerAsync(fixture, started.Group.GroupEpoch);
        var sourceId = await SeedAudioObservationAsync(fixture);
        await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var audio = await database.BackgroundAudioStates.SingleAsync(token);
            audio.CurrentSourceId = sourceId;
            audio.PlaybackState = PlaybackState.Playing;
            audio.PositionMs = 1234;
            audio.DurationMs = 5678;
            audio.ObservedGeneration = LastObservedGeneration;
            audio.DesiredGeneration = LastObservedGeneration;
        });
        var draining = await fixture.RuntimeAuthority.BeginDrainAsync("audio_shutdown");
        var stop = await fixture.Commands.EnqueueAsync(new EnqueueCommand(
            CommandTargetKind.Audio, 1, "STOP", "{\"reason\":\"shutdown\"}",
            LastObservedGeneration + 1, 1), async (database, command, token) =>
        {
            var audio = await database.BackgroundAudioStates.SingleAsync(token);
            audio.PendingCommand = command.Command;
            audio.CommandArgsJson = command.ArgsJson;
            audio.DesiredGeneration = command.SourceGeneration;
        });
        await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        var replacement = await StartOwnerAsync(fixture, 1);
        var newOwner = await RegisterAudioOwnerAsync(fixture, replacement.Group.GroupEpoch);
        var unexpected = await CreateLeases(fixture).ClaimAsync(new LeaseClaimRequest(
            CommandTargetKind.Audio, 1, newOwner.WorkerInstanceId, newOwner.OwnerEpoch,
            replacement.Group.GroupEpoch, TimeSpan.FromSeconds(30)));
        Assert.Null(unexpected);
        await using var verification = fixture.Database.CreateDbContext();
        var retired = await verification.CommandRecords.SingleAsync(item => item.CommandId == stop.CommandId);
        Assert.Equal(CommandStatus.Superseded, retired.Status);
        Assert.Null(retired.ConsumerInstanceId);
        Assert.Null(retired.ClaimToken);
        Assert.Null(retired.LeaseExpiresAt);
        var persistedAudio = await verification.BackgroundAudioStates.SingleAsync();
        Assert.Equal(string.Empty, persistedAudio.PendingCommand);
        Assert.Equal("{}", persistedAudio.CommandArgsJson);
        Assert.Equal(sourceId, persistedAudio.CurrentSourceId);
        Assert.Equal(1234, persistedAudio.PositionMs);
        Assert.Equal(5678, persistedAudio.DurationMs);
        Assert.Equal(LastObservedGeneration, persistedAudio.ObservedGeneration);
        Assert.Equal(LastObservedGeneration + 1, persistedAudio.DesiredGeneration);
    }

    /// <summary>
    /// 重复确认同一 stopped epoch 仍清理未领取意图，首次终态审计时间不得重写。
    /// :returns: 新注入的遗留意图退役，首次退役证据保持幂等。
    /// </summary>
    [Fact]
    public async Task RepeatedConfirmationRetiresPendingIntentAndPreservesEarlierTerminalEvidence()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var stopped = await CreateStoppedCloseAsync(fixture, 1);
        CommandRecord original;
        await using (var database = fixture.Database.CreateDbContext())
            original = await database.CommandRecords.AsNoTracking().SingleAsync(item => item.CommandId == stopped.CloseId);
        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(3));
        var pending = await fixture.Commands.EnqueueAsync(new EnqueueCommand(
            CommandTargetKind.Display, 1, "PAUSE", "{\"legacy\":true}", LastObservedGeneration + 1, 1),
            async (database, command, token) =>
            {
                var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1, token);
                session.PendingCommand = command.Command;
                session.CommandArgsJson = command.ArgsJson;
            });
        await fixture.RuntimeAuthority.CompleteStopAsync(stopped.StoppedGroupEpoch);
        await using var verification = fixture.Database.CreateDbContext();
        var retired = await verification.CommandRecords.SingleAsync(item => item.CommandId == pending.CommandId);
        Assert.Equal(CommandStatus.Superseded, retired.Status);
        var unchanged = await verification.CommandRecords.SingleAsync(item => item.CommandId == original.CommandId);
        Assert.Equal(CommandStatus.Superseded, unchanged.Status);
        Assert.Equal(original.CompletedAt, unchanged.CompletedAt);
        Assert.Equal(original.ResultCode, unchanged.ResultCode);
        Assert.Equal(original.LastError, unchanged.LastError);
        var session = await verification.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(string.Empty, session.PendingCommand);
        Assert.Equal("{}", session.CommandArgsJson);
    }

    /// <summary>
    /// 已有终态是审计证据，停机确认不能将其改成新退役状态或抹去原租约身份。
    /// :param status: 历史完成、失败、不确定或已退役状态。
    /// :returns: 所有历史结果字段保持原值。
    /// </summary>
    [Theory]
    [InlineData(CommandStatus.Completed)]
    [InlineData(CommandStatus.Failed)]
    [InlineData(CommandStatus.Uncertain)]
    [InlineData(CommandStatus.Superseded)]
    public async Task ConfirmedStopPreservesPreviouslyTerminalCommandEvidence(CommandStatus status)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        await StartOwnerAsync(fixture, 1);
        var command = await fixture.Commands.EnqueueAsync(new EnqueueCommand(
            CommandTargetKind.Display, 1, "PAUSE", "{}", LastObservedGeneration, 1));
        var consumer = Guid.NewGuid();
        var claim = Guid.NewGuid();
        var completedAt = fixture.TimeProvider.GetUtcNow().AddSeconds(-2);
        await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var audit = await database.CommandRecords.SingleAsync(item => item.CommandId == command.CommandId, token);
            audit.Status = status;
            audit.ConsumerInstanceId = consumer;
            audit.ClaimToken = claim;
            audit.OwnerEpoch = 17;
            audit.CompletedAt = completedAt;
            audit.ResultCode = "original_terminal_code";
            audit.ResultHash = "original_fingerprint";
            audit.ResultEvidenceJson = "{\"original\":true}";
            audit.LastError = "original_diagnostic";
        });
        var draining = await fixture.RuntimeAuthority.BeginDrainAsync("history_preservation");
        await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        await using var verification = fixture.Database.CreateDbContext();
        var persisted = await verification.CommandRecords.SingleAsync(item => item.CommandId == command.CommandId);
        Assert.Equal(status, persisted.Status);
        Assert.Equal(consumer, persisted.ConsumerInstanceId);
        Assert.Equal(claim, persisted.ClaimToken);
        Assert.Equal(17, persisted.OwnerEpoch);
        Assert.Equal(completedAt, persisted.CompletedAt);
        Assert.Equal("original_terminal_code", persisted.ResultCode);
        Assert.Equal("original_fingerprint", persisted.ResultHash);
        Assert.Equal("{\"original\":true}", persisted.ResultEvidenceJson);
        Assert.Equal("original_diagnostic", persisted.LastError);
    }

    /// <summary>
    /// 旧 epoch 的迟到停止确认不能退役下一组正在 drain 的命令或清除其投影。
    /// :returns: 旧确认失败，下一组命令、意图和组状态保持原样。
    /// </summary>
    [Fact]
    public async Task StaleStopEpochCannotRetireNextDrainingGroupPendingIntent()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var stopped = await CreateStoppedCloseAsync(fixture, 1);
        await StartOwnerAsync(fixture, 1);
        var sourceId = await SeedImageAsync(fixture, "next-group-explicit-open");
        await CreateRuntime(fixture).OpenSourceAsync(1, sourceId, autoplay: true, targetSlide: 0);
        var newDrain = await fixture.RuntimeAuthority.BeginDrainAsync("next_group_shutdown");
        await Assert.ThrowsAsync<RuntimeAuthorityException>(() =>
            fixture.RuntimeAuthority.CompleteStopAsync(stopped.StoppedGroupEpoch));
        await using var verification = fixture.Database.CreateDbContext();
        var group = await verification.RuntimeGroupControls.SingleAsync();
        Assert.Equal(newDrain.GroupEpoch, group.GroupEpoch);
        Assert.Equal(RuntimeGroupState.Draining, group.State);
        var newIntent = await verification.CommandRecords.SingleAsync(item => item.Command == "OPEN");
        Assert.Equal(CommandStatus.Pending, newIntent.Status);
        Assert.Null(newIntent.CompletedAt);
        var session = await verification.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal("OPEN", session.PendingCommand);
        Assert.Equal(newIntent.ArgsJson, session.CommandArgsJson);
        Assert.Equal(sourceId, session.MediaSourceId);
    }

    /// <summary>
    /// 已确认停机后迟到的旧 Processing 结果必须被租约围栏拒绝。
    /// :returns: 旧命令仍为退役，结果指纹和最后实际观测不被迟到回执改写。
    /// </summary>
    [Fact]
    public async Task ConfirmedStopRejectsLateResultFromRetiredInFlightOwner()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var started = await StartOwnerAsync(fixture, 1);
        var sourceId = await SeedImageAsync(fixture, "old-inflight-open");
        await CreateRuntime(fixture).OpenSourceAsync(1, sourceId, autoplay: true, targetSlide: 0);
        var claimed = await CreateLeases(fixture).ClaimAsync(new LeaseClaimRequest(
            CommandTargetKind.Display, 1, started.Owner.WorkerInstanceId, started.Owner.OwnerEpoch,
            started.Group.GroupEpoch, TimeSpan.FromSeconds(30)));
        Assert.NotNull(claimed);
        var draining = await fixture.RuntimeAuthority.BeginDrainAsync("late_result_shutdown");
        await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        await Assert.ThrowsAsync<CommandFenceException>(() => fixture.Commands.CompleteAsync(new CompleteCommand(
            claimed.CommandId, claimed.ClaimToken!.Value, claimed.OwnerEpoch, CommandStatus.Completed,
            "late_fake_success", "late_fingerprint", "{\"late\":true}")));
        await using var verification = fixture.Database.CreateDbContext();
        var retired = await verification.CommandRecords.SingleAsync(item => item.CommandId == claimed.CommandId);
        Assert.Equal(CommandStatus.Superseded, retired.Status);
        Assert.Equal(string.Empty, retired.ResultHash);
        Assert.Equal("{}", retired.ResultEvidenceJson);
        var session = await verification.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Null(session.ActualSourceId);
        Assert.Equal(0, session.ObservedGeneration);
    }

    /// <summary>
    /// 当前两窗投影清理不能删除或改写退役窗口的历史投影行。
    /// :param windowId: 只用于持久审计的旧窗口 3 或 4。
    /// :returns: 旧投影和历史代次仍可读取，且不是新播放目标。
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConfirmedStopDoesNotClearRetiredWindowHistoryProjection(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        await StartOwnerAsync(fixture, 1);
        var timestamp = fixture.TimeProvider.GetUtcNow().AddSeconds(-1);
        await fixture.Writes.ExecuteAsync((database, _) =>
        {
            database.PlaybackSessions.Add(new PlaybackSession
            {
                WindowId = windowId, PendingCommand = "HISTORICAL_PENDING_LABEL",
                CommandArgsJson = "{\"historical\":true}", DesiredGeneration = 37,
                ObservedGeneration = 34, PlaybackState = PlaybackState.Error,
                ErrorMessage = "retired_window", LastUpdatedAt = timestamp,
            });
            return Task.CompletedTask;
        });
        var draining = await fixture.RuntimeAuthority.BeginDrainAsync("legacy_projection_preservation");
        await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        await using var verification = fixture.Database.CreateDbContext();
        var legacy = await verification.PlaybackSessions.SingleAsync(item => item.WindowId == windowId);
        Assert.Equal("HISTORICAL_PENDING_LABEL", legacy.PendingCommand);
        Assert.Equal("{\"historical\":true}", legacy.CommandArgsJson);
        Assert.Equal(37, legacy.DesiredGeneration);
        Assert.Equal(34, legacy.ObservedGeneration);
        Assert.Equal(PlaybackState.Error, legacy.PlaybackState);
        Assert.Equal("retired_window", legacy.ErrorMessage);
        Assert.Equal(timestamp, legacy.LastUpdatedAt);
    }

    /// <summary>
    /// 投影已有未来毫秒时间时，清理仍须严格推进；清理后重复确认不能制造新变化。
    /// :returns: 两窗兼容时间及音频更新时间严格单调，重复无变更确认保持时间。
    /// </summary>
    [Fact]
    public async Task StopProjectionTimestampsAdvanceOnlyForChangedCompatibilityIntent()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        await StartOwnerAsync(fixture, 1);
        var previousTimestamp = fixture.TimeProvider.GetUtcNow().AddSeconds(10);
        await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var sessions = await database.PlaybackSessions.Where(item => item.WindowId >= 1 && item.WindowId <= 2)
                .ToListAsync(token);
            foreach (var session in sessions)
            {
                session.PendingCommand = "LEGACY_INTENT";
                session.CommandArgsJson = "{\"stale\":true}";
                session.LastUpdatedAt = previousTimestamp;
            }
            var audio = await database.BackgroundAudioStates.SingleAsync(token);
            audio.PendingCommand = "LEGACY_AUDIO_INTENT";
            audio.CommandArgsJson = "{\"stale\":true}";
            audio.UpdatedAt = previousTimestamp;
        });
        var draining = await fixture.RuntimeAuthority.BeginDrainAsync("timestamp_confirmation");
        await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        var expectedTimestamp = previousTimestamp.AddMilliseconds(1);
        await using (var first = fixture.Database.CreateDbContext())
        {
            var sessions = await first.PlaybackSessions.ToListAsync();
            Assert.All(sessions, session => Assert.Equal(expectedTimestamp, session.LastUpdatedAt));
            Assert.All(sessions, session => Assert.Equal(string.Empty, session.PendingCommand));
            Assert.All(sessions, session => Assert.Equal("{}", session.CommandArgsJson));
            Assert.Equal(expectedTimestamp, (await first.BackgroundAudioStates.SingleAsync()).UpdatedAt);
        }
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        await fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        await using var verification = fixture.Database.CreateDbContext();
        Assert.All(await verification.PlaybackSessions.ToListAsync(),
            session => Assert.Equal(expectedTimestamp, session.LastUpdatedAt));
        Assert.Equal(expectedTimestamp, (await verification.BackgroundAudioStates.SingleAsync()).UpdatedAt);
    }

    /// <summary>
    /// 登记测试用音频所有者；精确进程退出证明仅作为持久化门禁输入。
    /// :param fixture: 独立 SQLite 测试夹具。
    /// :param groupEpoch: 当前已授权组代次。
    /// :returns: 音频唯一目标的合法新 owner。
    /// </summary>
    private static Task<WorkerOwnership> RegisterAudioOwnerAsync(ControlHostFixture fixture, long groupEpoch) =>
        fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Audio, 1, Guid.NewGuid(), 202, fixture.TimeProvider.GetUtcNow(),
            1, groupEpoch, "[\"audio\"]", PreviousOwnerExitConfirmed: true));

    /// <summary>
    /// 保存符合音频领域类型的最后观测源，仅登记元数据，不创建或播放实体音频。
    /// :param fixture: 独立 SQLite 测试夹具。
    /// :returns: 用于历史观测断言的音频主键。
    /// </summary>
    private static async Task<long> SeedAudioObservationAsync(ControlHostFixture fixture)
    {
        await using var database = fixture.Database.CreateDbContext();
        var source = new MediaSource
        {
            SourceType = MediaSourceType.Audio, Name = "last-audio-observation-fixture",
            Uri = "last-audio-observation.mp3", IsAvailable = true, SourceRevision = 1,
            CreatedAt = fixture.TimeProvider.GetUtcNow(),
        };
        database.MediaSources.Add(source);
        await database.SaveChangesAsync();
        return source.Id;
    }
}
