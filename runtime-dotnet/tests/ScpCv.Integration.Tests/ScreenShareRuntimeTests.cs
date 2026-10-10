// 共享动态OPEN及条件CLOSE使用真实SQLite事务，不启动播放器。SCP-cv 2026-10-10。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class ScreenShareRuntimeTests
{
    [Fact]
    public async Task DynamicOpenHasNullMediaIdentityAndNoPersistentReceiverCredential()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var identity = await SeedOnlineAsync(fixture);
        var runtime = Runtime(fixture);
        var shareId = Guid.NewGuid();
        long bound = 0;
        var receipt = await runtime.OpenScreenShareAsync(identity, shareId, generation => bound = generation);
        await using var database = fixture.Database.CreateDbContext();
        var command = await database.CommandRecords.SingleAsync();
        using var args = JsonDocument.Parse(command.ArgsJson);
        Assert.Equal(shareId, args.RootElement.GetProperty("share_id").GetGuid());
        Assert.Equal("screen_share", args.RootElement.GetProperty("source_type").GetString());
        Assert.Equal(0, args.RootElement.GetProperty("source_id").GetInt64());
        Assert.False(args.RootElement.TryGetProperty("receiver_uri", out _));
        Assert.False(args.RootElement.TryGetProperty("token", out _));
        Assert.Equal(bound, receipt.SourceGeneration);
        Assert.Equal(bound, command.SourceGeneration);
        Assert.Equal(receipt.CommandId, command.CommandId);
        Assert.Equal(CommandStatus.Pending, command.Status);
        Assert.Empty(await database.MediaSources.ToArrayAsync());
        var first = await database.PlaybackSessions.SingleAsync(session => session.WindowId == 1);
        var second = await database.PlaybackSessions.SingleAsync(session => session.WindowId == 2);
        Assert.Null(first.MediaSourceId);
        Assert.Equal(PlaybackState.Loading, first.PlaybackState);
        Assert.Equal(PlaybackState.Idle, second.PlaybackState);
        Assert.Equal(0, second.DesiredGeneration);
    }

    [Fact]
    public async Task OfflineOrStaleWorkerIdentityCannotQueueOrBind()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var runtime = Runtime(fixture);
        var invalid = new ScreenShareTargetIdentity(1, Guid.NewGuid(), 7, 19);
        var bound = false;
        var offline = await Assert.ThrowsAsync<PlaybackServiceException>(
            () => runtime.OpenScreenShareAsync(invalid, Guid.NewGuid(), _ => bound = true));
        Assert.Equal("share_player_offline", offline.Code);
        var valid = await SeedOnlineAsync(fixture);
        await Assert.ThrowsAsync<PlaybackServiceException>(() => runtime.OpenScreenShareAsync(
            valid with { OwnerEpoch = valid.OwnerEpoch + 1 }, Guid.NewGuid(), _ => bound = true));
        Assert.False(bound);
        await using var database = fixture.Database.CreateDbContext();
        Assert.Empty(await database.CommandRecords.ToArrayAsync());
    }

    [Fact]
    public async Task BindingFailureRollsBackOpenAndSupersedingOlderPendingIntent()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var identity = await SeedOnlineAsync(fixture);
        var runtime = Runtime(fixture);
        var previous = await fixture.Commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "SET_VOLUME", "{\"volume\":40}", 0, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.OpenScreenShareAsync(
            identity, Guid.NewGuid(), _ => throw new InvalidOperationException("fixture_bind_failure")));
        await using var database = fixture.Database.CreateDbContext();
        var retained = await database.CommandRecords.SingleAsync();
        Assert.Equal(previous.CommandId, retained.CommandId);
        Assert.Equal(CommandStatus.Pending, retained.Status);
        Assert.Equal(0, (await database.PlaybackSessions.SingleAsync(session => session.WindowId == 1)).DesiredGeneration);
    }

    [Fact]
    public async Task LateStopCannotCloseNewMediaOrSupersedeItsOpen()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var identity = await SeedOnlineAsync(fixture);
        var runtime = Runtime(fixture);
        var shareId = Guid.NewGuid();
        var share = await runtime.OpenScreenShareAsync(identity, shareId, _ => { });
        long sourceId;
        await using (var database = fixture.Database.CreateDbContext())
        {
            var source = new MediaSource { Name = "新图片", SourceType = MediaSourceType.Image, Uri = "image.png", CreatedAt = fixture.TimeProvider.GetUtcNow() };
            database.MediaSources.Add(source);
            await database.SaveChangesAsync();
            sourceId = source.Id;
        }
        await runtime.OpenSourceAsync(1, sourceId, true, 1);
        var stop = await runtime.CloseScreenShareAsync(1, shareId, share.SourceGeneration);
        Assert.Equal(ScreenShareCloseDisposition.NoLongerDesired, stop.Disposition);
        Assert.Null(stop.CommandId);
        await using var check = fixture.Database.CreateDbContext();
        Assert.False(await check.CommandRecords.AnyAsync(command => command.Command == "CLOSE"));
        Assert.Equal(1, await check.CommandRecords.CountAsync(command => command.Status == CommandStatus.Pending));
        Assert.Equal(sourceId, (await check.PlaybackSessions.SingleAsync(session => session.WindowId == 1)).MediaSourceId);
    }

    [Fact]
    public async Task MatchingStopIsQueuedNotActualReleaseAndWrongShareIsNoOp()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var identity = await SeedOnlineAsync(fixture);
        var runtime = Runtime(fixture);
        var shareId = Guid.NewGuid();
        var share = await runtime.OpenScreenShareAsync(identity, shareId, _ => { });
        var wrong = await runtime.CloseScreenShareAsync(1, Guid.NewGuid(), share.SourceGeneration);
        Assert.Equal(ScreenShareCloseDisposition.NoLongerDesired, wrong.Disposition);
        var stop = await runtime.CloseScreenShareAsync(1, shareId, share.SourceGeneration);
        Assert.Equal(ScreenShareCloseDisposition.Queued, stop.Disposition);
        Assert.NotNull(stop.CommandId);
        Assert.Equal(share.SourceGeneration + 1, stop.CloseGeneration);
        await using var database = fixture.Database.CreateDbContext();
        Assert.Equal(CommandStatus.Pending, (await database.CommandRecords.SingleAsync(command => command.Command == "CLOSE")).Status);
        Assert.Equal(0, (await database.PlaybackSessions.SingleAsync(session => session.WindowId == 1)).ObservedGeneration);
        var repeated = await runtime.CloseScreenShareAsync(1, shareId, share.SourceGeneration);
        Assert.Equal(ScreenShareCloseDisposition.NoLongerDesired, repeated.Disposition);
        Assert.Equal(1, await database.CommandRecords.CountAsync(command => command.Command == "CLOSE"));
    }

    private static RuntimeStateService Runtime(ControlHostFixture fixture) => new(
        fixture.Database, fixture.Writes, new CommandCoordinator(fixture.Commands, new QueuedCommandWakeNotifier()), fixture.TimeProvider);

    private static async Task<ScreenShareTargetIdentity> SeedOnlineAsync(ControlHostFixture fixture)
    {
        var identity = new ScreenShareTargetIdentity(1, Guid.NewGuid(), 7, 19);
        await using var database = fixture.Database.CreateDbContext();
        var group = await database.RuntimeGroupControls.SingleAsync();
        group.State = RuntimeGroupState.Armed;
        group.GroupEpoch = identity.GroupEpoch;
        database.WorkerOwnerships.Add(new WorkerOwnership
        {
            TargetKind = CommandTargetKind.Display, TargetId = 1, WorkerInstanceId = identity.WorkerInstanceId,
            OwnerEpoch = identity.OwnerEpoch, Status = WorkerOwnershipState.Online,
            LastTransportHeartbeat = fixture.TimeProvider.GetUtcNow(),
        });
        var session = await database.PlaybackSessions.SingleAsync(session => session.WindowId == 1);
        session.PlayerLastSeenAt = fixture.TimeProvider.GetUtcNow();
        session.TargetDisplayLabel = "独立模拟大屏";
        await database.SaveChangesAsync();
        return identity;
    }
}
