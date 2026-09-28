// 真管道认证与独立 SQLite 验证 Supervisor 退出闭环；原生死亡 seam 可控，不操作现场进程。
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class SupervisorExitRecoveryTests
{
    /// <summary>原生死亡证据与管道连接解耦，退出后清晰报故障而不伪称整组停止。</summary>
    [Fact]
    public async Task ConfirmedSupervisorExitFaultsArmedGroupAndFencesBothPlayers()
    {
        await using var context = await ExitContext.CreateAsync();
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await client.DisposeAsync();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var snapshots = context.Events.ReadAsync(0, budget.Token).GetAsyncEnumerator();
        Assert.True(await snapshots.MoveNextAsync());
        var update = snapshots.MoveNextAsync().AsTask();

        context.Observer.Complete(new(true, "process_exited"));
        await context.WaitForStateAsync(RuntimeGroupState.Faulted);
        Assert.True(await update.WaitAsync(budget.Token));
        Assert.False(snapshots.Current.IsHeartbeat);
        using var snapshot = JsonDocument.Parse(snapshots.Current.Data);
        Assert.All(snapshot.RootElement.GetProperty("sessions").EnumerateArray(), session =>
            Assert.Equal("error", session.GetProperty("playback_state").GetString()));

        var group = await context.Fixture.RuntimeAuthority.GetGroupAsync();
        Assert.Equal(context.GroupEpoch, group.GroupEpoch);
        Assert.Equal($"supervisor_exited:{context.Identity.ProcessId}:{context.Identity.InstanceId:N}", group.StopReason);
        await using var database = context.Fixture.Database.CreateDbContext();
        Assert.All(await database.WorkerOwnerships.ToListAsync(), owner => Assert.Equal(WorkerOwnershipState.Faulted, owner.Status));
        Assert.All(await database.PlaybackSessions.Where(session => session.WindowId <= 2).ToListAsync(), session =>
        {
            Assert.Equal(PlaybackState.Error, session.PlaybackState);
            Assert.Null(session.PlayerLastSeenAt);
            Assert.Contains("Supervisor", session.ErrorMessage, StringComparison.Ordinal);
        });
        Assert.False(await context.Fixture.RuntimeAuthority.RecordHeartbeatAsync(
            CommandTargetKind.Display, 1, context.Player.WorkerInstanceId, context.Player.OwnerEpoch, uiProgress: false));
    }

    /// <summary>传输断开但进程未证明退出时不修改运行组；重连不会叠加原生观察。</summary>
    [Fact]
    public async Task PipeDisconnectAndReconnectDoNotFaultLiveSupervisorOrDuplicateObservation()
    {
        await using var context = await ExitContext.CreateAsync();
        await using var first = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await first.DisposeAsync();
        await using var second = await context.ConnectSupervisorAsync();

        Assert.Equal(1, context.Observer.Calls);
        Assert.Equal(RuntimeGroupState.Armed, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).State);
        context.Observer.Complete(new(false, "process_identity_unverifiable"));
        await context.Logger.WaitForAsync(2211);
        Assert.Equal(RuntimeGroupState.Armed, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).State);
    }

    /// <summary>显式启动的新组不受旧观察任务的迟到退出影响。</summary>
    [Fact]
    public async Task LateExitEvidenceCannotFaultNewGroupOrReplaceItsPicture()
    {
        await using var context = await ExitContext.CreateAsync();
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var draining = await context.Fixture.RuntimeAuthority.BeginDrainAsync("test_stop");
        await context.Fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        var request = Guid.NewGuid();
        var newer = await context.Fixture.RuntimeAuthority.BeginStartAsync(request);
        await context.Fixture.RuntimeAuthority.ArmAsync(request, newer.GroupEpoch);
        context.Observer.Complete(new(true, "process_exited"));
        await context.Logger.WaitForAsync(2214);

        var group = await context.Fixture.RuntimeAuthority.GetGroupAsync();
        Assert.Equal(newer.GroupEpoch, group.GroupEpoch);
        Assert.Equal(RuntimeGroupState.Armed, group.State);
        Assert.Empty(group.StopReason);
    }

    /// <summary>旧 Supervisor 的重连不能在新组上重新建立观察或替换其控制连接。</summary>
    [Fact]
    public async Task OldSupervisorCannotRebindToNewGroupByReconnecting()
    {
        await using var context = await ExitContext.CreateAsync();
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var draining = await context.Fixture.RuntimeAuthority.BeginDrainAsync("test_stop");
        await context.Fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        var request = Guid.NewGuid();
        var newer = await context.Fixture.RuntimeAuthority.BeginStartAsync(request);
        await context.Fixture.RuntimeAuthority.ArmAsync(request, newer.GroupEpoch);

        await Assert.ThrowsAnyAsync<IOException>(() => context.ConnectSupervisorAsync());
        Assert.Equal(1, context.Observer.Calls);
        Assert.Equal(RuntimeGroupState.Armed, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).State);
        context.Observer.Complete(new(true, "process_exited"));
        await context.Logger.WaitForAsync(2214);
        Assert.Equal(newer.GroupEpoch, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).GroupEpoch);
    }

    /// <summary>旧组或故障组的 Supervisor 不能继续注册子进程污染新组身份表。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FencedSupervisorCannotRegisterChildren(bool startNewGroup)
    {
        await using var context = await ExitContext.CreateAsync();
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (startNewGroup)
        {
            var draining = await context.Fixture.RuntimeAuthority.BeginDrainAsync("test_stop");
            await context.Fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
            var request = Guid.NewGuid();
            var newer = await context.Fixture.RuntimeAuthority.BeginStartAsync(request);
            await context.Fixture.RuntimeAuthority.ArmAsync(request, newer.GroupEpoch);
        }
        else
        {
            context.Observer.Complete(new(true, "process_exited"));
            await context.Logger.WaitForAsync(2213);
        }

        var response = await client.ExchangeAsync(new IpcFrameDto
        {
            MessageType = "register_process", MessageId = Guid.NewGuid(), InstanceId = context.Identity.InstanceId,
            Payload = JsonSerializer.SerializeToElement(new RegisterProcessDto
            {
                Role = "audio", ProcessId = context.Identity.ProcessId,
                ProcessStartTime = context.Identity.ProcessStartTime.ToString("O"),
                LogonSessionId = context.Identity.LogonSessionId, InstanceId = Guid.NewGuid(),
            }),
        });

        var registration = response.Payload.Deserialize<RegistrationResultDto>();
        Assert.NotNull(registration);
        Assert.False(registration.Accepted);
        Assert.Equal("group_fenced", registration.Reason);
    }

    /// <summary>已确认停止的状态不被旧 Supervisor 观察回写为故障。</summary>
    [Fact]
    public async Task LateExitEvidenceDoesNotOverwriteConfirmedStoppedState()
    {
        await using var context = await ExitContext.CreateAsync();
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var draining = await context.Fixture.RuntimeAuthority.BeginDrainAsync("test_confirmed_stop");
        await context.Fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);

        context.Observer.Complete(new(true, "process_exited"));
        await context.Logger.WaitForAsync(2214);

        var stopped = await context.Fixture.RuntimeAuthority.GetGroupAsync();
        Assert.Equal(RuntimeGroupState.Stopped, stopped.State);
        Assert.Equal("test_confirmed_stop", stopped.StopReason);
    }

    /// <summary>观察异常留诊断但不伪称死亡。</summary>
    [Fact]
    public async Task ObserverExceptionIsLoggedWithoutFaultingGroup()
    {
        await using var context = await ExitContext.CreateAsync();
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.Observer.Fail(new IOException("受控原生观察故障"));
        await context.Logger.WaitForAsync(2212);

        Assert.Equal(RuntimeGroupState.Armed, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).State);
    }

    /// <summary>故障不会直接重领旧 Processing；确认停机后新组可播，旧结果不能复活。</summary>
    [Fact]
    public async Task FaultThenConfirmedStopAndExplicitRestartRetiresOldClaimWithoutReplay()
    {
        await using var context = await ExitContext.CreateAsync();
        await context.Fixture.Commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "NEXT", "{}", 1, 1));
        var old = await context.Fixture.Commands.ClaimAsync(new ClaimCommand(CommandTargetKind.Display, 1,
            context.Player.WorkerInstanceId, context.Player.OwnerEpoch, context.GroupEpoch, TimeSpan.FromSeconds(30)));
        Assert.NotNull(old);
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        context.Observer.Complete(new(true, "process_exited"));
        await context.Logger.WaitForAsync(2213);
        Assert.Null(await context.Fixture.Commands.ClaimAsync(new ClaimCommand(CommandTargetKind.Display, 1,
            context.Player.WorkerInstanceId, context.Player.OwnerEpoch, context.GroupEpoch, TimeSpan.FromSeconds(30))));
        await using (var fault = context.Fixture.Database.CreateDbContext())
            Assert.Equal(CommandStatus.Processing, (await fault.CommandRecords.SingleAsync()).Status);

        var draining = await context.Fixture.RuntimeAuthority.BeginDrainAsync("test_exit_proof");
        await context.Fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        var request = Guid.NewGuid();
        var starting = await context.Fixture.RuntimeAuthority.BeginStartAsync(request);
        await context.Fixture.RuntimeAuthority.ArmAsync(request, starting.GroupEpoch);
        var replacement = await context.Fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(CommandTargetKind.Display, 1,
            Guid.NewGuid(), 202, context.Fixture.TimeProvider.GetUtcNow(), 1, starting.GroupEpoch, "[]", PreviousOwnerExitConfirmed: true));
        var next = await context.Fixture.Commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "OPEN", "{}", 2, 1));
        var claimed = await context.Fixture.Commands.ClaimAsync(new ClaimCommand(CommandTargetKind.Display, 1,
            replacement.WorkerInstanceId, replacement.OwnerEpoch, starting.GroupEpoch, TimeSpan.FromSeconds(30)));
        Assert.NotNull(claimed);
        Assert.Equal(next.CommandId, claimed.CommandId);
        await Assert.ThrowsAsync<CommandFenceException>(() => context.Fixture.Commands.CompleteAsync(new CompleteCommand(
            old.CommandId, old.ClaimToken!.Value, old.OwnerEpoch, CommandStatus.Completed, "ok", "late", "{}")));
    }

    /// <summary>宿主停止取消原生观察，不能把正常停机写成运行组故障。</summary>
    [Fact]
    public async Task HostStopCancelsObservationWithoutFaultingGroup()
    {
        await using var context = await ExitContext.CreateAsync();
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await context.Broker.StopAsync(CancellationToken.None);

        Assert.True(context.Observer.Cancelled);
        Assert.Equal(RuntimeGroupState.Armed, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).State);
    }

    /// <summary>已取得死亡证据后，首次故障状态提交失败不得永久丢弃证据。</summary>
    [Fact]
    public async Task ConfirmedDeathSurvivesTransientFaultStateCommitFailure()
    {
        var failure = new FaultCommitInterceptor();
        await using var context = await ExitContext.CreateAsync(failure);
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.Observer.Complete(new(true, "process_exited"));
        await failure.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.WaitForStateAsync(RuntimeGroupState.Faulted);

        Assert.True(failure.Attempts >= 2);
        Assert.Equal(1, context.Observer.Calls);
        await context.Logger.WaitForAsync(2213);
    }

    /// <summary>重试故障状态期间新组已启动，旧死亡证据只能被忽略。</summary>
    [Fact]
    public async Task FaultStateCommitRetryCannotDamageNewerArmedGroup()
    {
        var failure = new FaultCommitInterceptor(holdFailures: true);
        await using var context = await ExitContext.CreateAsync(failure);
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        context.Observer.Complete(new(true, "process_exited"));
        await failure.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var draining = await context.Fixture.RuntimeAuthority.BeginDrainAsync("test_confirmed_exit");
        await context.Fixture.RuntimeAuthority.CompleteStopAsync(draining.GroupEpoch);
        var request = Guid.NewGuid();
        var newer = await context.Fixture.RuntimeAuthority.BeginStartAsync(request);
        await context.Fixture.RuntimeAuthority.ArmAsync(request, newer.GroupEpoch);

        failure.Release();
        await context.Logger.WaitForAsync(2214);

        var group = await context.Fixture.RuntimeAuthority.GetGroupAsync();
        Assert.Equal(newer.GroupEpoch, group.GroupEpoch);
        Assert.Equal(RuntimeGroupState.Armed, group.State);
        Assert.Empty(group.StopReason);
        Assert.Equal(1, context.Observer.Calls);
    }

    /// <summary>已提交但回执异常时核对同一持久证据，仍须发布 SSE 而非重复修改会话。</summary>
    [Fact]
    public async Task LostFaultCommitReceiptStillPublishesPersistedFaultSnapshot()
    {
        var failure = new LostFaultCommitReceiptInterceptor();
        await using var context = await ExitContext.CreateAsync(failure);
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var snapshots = context.Events.ReadAsync(0, budget.Token).GetAsyncEnumerator();
        Assert.True(await snapshots.MoveNextAsync());
        var update = snapshots.MoveNextAsync().AsTask();
        context.Observer.Complete(new(true, "process_exited"));
        await failure.Failed.Task.WaitAsync(budget.Token);
        await using var database = context.Fixture.Database.CreateDbContext();
        var committed = await database.PlaybackSessions.AsNoTracking().SingleAsync(session => session.WindowId == 1);

        await context.Logger.WaitForAsync(2213);
        Assert.True(await update.WaitAsync(budget.Token));
        Assert.Equal(RuntimeGroupState.Faulted, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).State);
        var after = await database.PlaybackSessions.AsNoTracking().SingleAsync(session => session.WindowId == 1);
        Assert.Equal(committed.LastUpdatedAt, after.LastUpdatedAt);
        Assert.Equal(1, context.Observer.Calls);
    }

    /// <summary>宿主停止同时取消确证证据的写库退避，不留下运行中的重试任务。</summary>
    [Fact]
    public async Task HostStopCancelsFaultPersistenceRetryWithoutFurtherWrites()
    {
        var failure = new FaultCommitInterceptor(holdFailures: true);
        await using var context = await ExitContext.CreateAsync(failure);
        await using var client = await context.ConnectSupervisorAsync();
        await context.Observer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        context.Observer.Complete(new(true, "process_exited"));
        await context.Logger.WaitForAsync(2215);

        await context.Broker.StopAsync(CancellationToken.None);
        Assert.Equal(RuntimeGroupState.Armed, (await context.Fixture.RuntimeAuthority.GetGroupAsync()).State);
        Assert.Equal(1, failure.Attempts);
    }

}
