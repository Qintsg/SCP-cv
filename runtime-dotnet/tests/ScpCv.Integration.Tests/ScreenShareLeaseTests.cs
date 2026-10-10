// 动态共享凭据只注入已认证且属于原Worker的租约；真实SQLite，不启动原生窗口。SCP-cv 2026-10-10。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.Ipc;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class ScreenShareLeaseTests
{
    [Fact]
    public async Task MatchingAuthenticatedLeaseGetsEphemeralFragmentCredentialNotDatabaseCopy()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (runtime, registry, target, share) = await ArrangeAsync(fixture);
        var dispatcher = Dispatcher(fixture, runtime, registry);
        var response = await dispatcher.DispatchAsync(Claim(target));
        Assert.Equal("command_lease", response.MessageType);
        var lease = response.Payload.Deserialize<CommandLeaseDto>()!;
        var uri = new Uri(lease.Args["receiver_uri"].GetString()!);
        Assert.True(uri.IsLoopback);
        Assert.Equal(65, uri.Fragment.Length);
        Assert.DoesNotContain("token", uri.Query, StringComparison.Ordinal);
        var grant = registry.GetReceiverGrant(share.Id, target, lease.SourceGeneration);
        Assert.Equal("#" + grant.Token, uri.Fragment);
        await using var database = fixture.Database.CreateDbContext();
        var command = await database.CommandRecords.SingleAsync();
        Assert.Equal(CommandStatus.Processing, command.Status);
        Assert.DoesNotContain(grant.Token, command.ArgsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("receiver_uri", command.ArgsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongInstanceIsFencedBeforeClaimAndOriginalCanStillClaim()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (runtime, registry, target, _) = await ArrangeAsync(fixture);
        var dispatcher = Dispatcher(fixture, runtime, registry);
        var wrong = await dispatcher.DispatchAsync(Claim(target with { WorkerInstanceId = Guid.NewGuid() }));
        Assert.Equal("no_work", wrong.MessageType);
        Assert.Equal("fenced", wrong.Payload.GetProperty("reason").GetString());
        Assert.Equal("command_lease", (await dispatcher.DispatchAsync(Claim(target))).MessageType);
    }

    [Fact]
    public async Task AuthorizedReplacementWorkerCannotReceiveOldShareAndRejectedLeaseIsTerminal()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (runtime, registry, target, _) = await ArrangeAsync(fixture);
        var replacement = target with { WorkerInstanceId = Guid.NewGuid(), OwnerEpoch = 8 };
        await using (var database = fixture.Database.CreateDbContext())
        {
            var worker = await database.WorkerOwnerships.SingleAsync();
            worker.WorkerInstanceId = replacement.WorkerInstanceId;
            worker.OwnerEpoch = replacement.OwnerEpoch;
            await database.SaveChangesAsync();
        }
        var response = await Dispatcher(fixture, runtime, registry).DispatchAsync(Claim(replacement));
        Assert.Equal("no_work", response.MessageType);
        Assert.DoesNotContain("receiver_uri", response.Payload.GetRawText(), StringComparison.Ordinal);
        await using var check = fixture.Database.CreateDbContext();
        var command = await check.CommandRecords.SingleAsync();
        Assert.Equal(CommandStatus.Failed, command.Status);
        Assert.Equal("share_receiver_denied", command.ResultCode);
    }

    [Fact]
    public async Task MissingShareAuthorityFailsClosedInsteadOfReturningReplayableOpen()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (runtime, registry, target, _) = await ArrangeAsync(fixture);
        var response = await Dispatcher(fixture, runtime, registry, configured: false).DispatchAsync(Claim(target));
        Assert.Equal("no_work", response.MessageType);
        await using var database = fixture.Database.CreateDbContext();
        Assert.Equal(CommandStatus.Failed, (await database.CommandRecords.SingleAsync()).Status);
    }

    private static async Task<(RuntimeStateService Runtime, ScreenShareRegistry Registry, ScreenShareTargetIdentity Target, ScreenShareSnapshot Share)> ArrangeAsync(ControlHostFixture fixture)
    {
        var target = new ScreenShareTargetIdentity(1, Guid.NewGuid(), 7, 19);
        await using (var database = fixture.Database.CreateDbContext())
        {
            var group = await database.RuntimeGroupControls.SingleAsync();
            group.State = RuntimeGroupState.Armed;
            group.GroupEpoch = target.GroupEpoch;
            database.WorkerOwnerships.Add(new WorkerOwnership
            {
                TargetKind = CommandTargetKind.Display, TargetId = 1, WorkerInstanceId = target.WorkerInstanceId,
                OwnerEpoch = target.OwnerEpoch, Status = WorkerOwnershipState.Online,
                LastTransportHeartbeat = fixture.TimeProvider.GetUtcNow(),
            });
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
            session.PlayerLastSeenAt = fixture.TimeProvider.GetUtcNow();
            session.TargetDisplayLabel = "独立测试目标";
            await database.SaveChangesAsync();
        }
        var runtime = new RuntimeStateService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new QueuedCommandWakeNotifier()), fixture.TimeProvider);
        var registry = new ScreenShareRegistry(fixture.TimeProvider);
        var share = registry.Create("alice", target, false);
        registry.SetOffer("alice", share.Id, "offer", "v=0\r\n");
        await runtime.OpenScreenShareAsync(target, share.Id, generation => registry.BindReceiver("alice", share.Id, generation));
        return (runtime, registry, target, share);
    }

    private static RuntimeMessageDispatcher Dispatcher(ControlHostFixture fixture, RuntimeStateService runtime, ScreenShareRegistry registry, bool configured = true)
    {
        var coordinator = new CommandCoordinator(fixture.Commands, new QueuedCommandWakeNotifier());
        var audio = new BackgroundAudioService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
        var events = new SseEventHub(runtime, audio, new SseEventStreamOptions());
        return new(new CommandLeaseService(fixture.Commands, fixture.RuntimeAuthority, fixture.Database, fixture.Writes, fixture.TimeProvider),
            new CommandResultService(fixture.Commands), new RuntimeProjectionPublisher(fixture.Writes, events, fixture.TimeProvider),
            screenShares: configured ? new ScreenShareLeaseAuthority(registry, new ScreenShareOptions()) : null);
    }

    private static IpcFrameDto Claim(ScreenShareTargetIdentity target) => new()
    {
        MessageType = "claim_request", InstanceId = target.WorkerInstanceId, OwnerEpoch = target.OwnerEpoch,
        Target = new IpcTargetDto { Kind = "display", Id = target.WindowId },
        Payload = JsonSerializer.SerializeToElement(new ClaimRequestDto { GroupEpoch = target.GroupEpoch }),
    };
}
