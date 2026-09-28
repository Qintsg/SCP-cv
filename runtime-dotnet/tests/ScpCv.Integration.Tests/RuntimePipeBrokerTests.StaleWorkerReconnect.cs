// 跨组旧 Worker 重连的独立红回归；只操作真管道与临时库，不创建播放器或 Office。
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    /// <summary>同组内所有角色均可断线重连，不把传输变化误作实例换组。</summary>
    [Theory]
    [InlineData("player-1")]
    [InlineData("player-2")]
    [InlineData("audio")]
    [InlineData("office")]
    [InlineData("supervisor")]
    public async Task SameGroupRuntimeInstanceMayReconnect(string role)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid();
        var group = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, group.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var instance = Guid.NewGuid();
        var registry = new RegisteredProcessRegistry();
        registry.Register(CurrentIdentity(process, role, instance));
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        try
        {
            var target = ReconnectTarget(role);
            await using var first = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(first, process, role, instance, target);
            var initialWelcome = await ReadAsync(first);
            Assert.Equal("welcome", initialWelcome.MessageType);
            await first.DisposeAsync();

            await using var second = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(second, process, role, instance, target);
            var reconnected = await ReadAsync(second);
            Assert.Equal("welcome", reconnected.MessageType);
            Assert.Equal(group.GroupEpoch, reconnected.Payload.Deserialize<WelcomeDto>()!.GroupEpoch);
            if (target is not null) Assert.Equal(initialWelcome.OwnerEpoch, reconnected.OwnerEpoch);
            await WriteAsync(second, Frame("worker_ready", instance, reconnected.OwnerEpoch, target, ReadyForReconnect()));
            Assert.True((await ReadAsync(second)).Payload.GetProperty("accepted").GetBoolean());
            if (role != "supervisor") Assert.DoesNotContain(role, broker.GetRuntimeReadiness(group.GroupEpoch).MissingRoles);
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    /// <summary>新组可登记新实例；新 Supervisor 不得把旧实例重新登记为本组成员。</summary>
    [Theory]
    [InlineData("player-1")]
    [InlineData("player-2")]
    [InlineData("audio")]
    [InlineData("office")]
    public async Task NewSupervisorMayRegisterNewWorkerButCannotRebindOldInstance(string role)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid();
        var oldGroup = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, oldGroup.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var oldInstance = Guid.NewGuid();
        var registry = new RegisteredProcessRegistry();
        registry.Register(CurrentIdentity(process, role, oldInstance));
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        try
        {
            var target = ReconnectTarget(role);
            await using var previous = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(previous, process, role, oldInstance, target);
            var oldWelcome = await ReadAsync(previous);
            Assert.Equal("welcome", oldWelcome.MessageType);
            await previous.DisposeAsync();
            Assert.True(await fixture.RuntimeAuthority.TryFaultSupervisorExitAsync(oldGroup.GroupEpoch, 904, Guid.NewGuid()));
            var nextRequest = Guid.NewGuid();
            var newGroup = await fixture.RuntimeAuthority.BeginStartAsync(nextRequest);
            await fixture.RuntimeAuthority.ArmAsync(nextRequest, newGroup.GroupEpoch);

            var supervisorInstance = Guid.NewGuid();
            registry.Register(CurrentIdentity(process, "supervisor", supervisorInstance));
            await using var supervisor = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(supervisor, process, "supervisor", supervisorInstance, null);
            Assert.Equal("welcome", (await ReadAsync(supervisor)).MessageType);
            await WriteAsync(supervisor, Frame("register_process", supervisorInstance, 0, null,
                ChildRegistration(process, role, oldInstance)));
            var rejected = (await ReadAsync(supervisor)).Payload.Deserialize<RegistrationResultDto>()!;
            Assert.False(rejected.Accepted);
            Assert.Equal("group_fenced", rejected.Reason);
            Assert.True(registry.TryGet(process.Id, out var preserved));
            Assert.Equal(supervisorInstance, preserved!.InstanceId);

            var newInstance = Guid.NewGuid();
            await WriteAsync(supervisor, Frame("register_process", supervisorInstance, 0, null,
                ChildRegistration(process, role, newInstance)));
            Assert.True((await ReadAsync(supervisor)).Payload.Deserialize<RegistrationResultDto>()!.Accepted);
            await using var replacement = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(replacement, process, role, newInstance, target);
            var welcome = await ReadAsync(replacement);
            Assert.Equal("welcome", welcome.MessageType);
            Assert.Equal(newGroup.GroupEpoch, welcome.Payload.Deserialize<WelcomeDto>()!.GroupEpoch);
            Assert.True(welcome.OwnerEpoch > oldWelcome.OwnerEpoch);
            await WriteAsync(replacement, Frame("worker_ready", newInstance, welcome.OwnerEpoch, target, ReadyForReconnect()));
            Assert.True((await ReadAsync(replacement)).Payload.GetProperty("accepted").GetBoolean());
            Assert.DoesNotContain(role, broker.GetRuntimeReadiness(newGroup.GroupEpoch).MissingRoles);
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    /// <summary>本组 Supervisor 已登记的旧实例即使从未握手，也不能延迟到下一组认领。</summary>
    [Theory]
    [InlineData("player-1")]
    [InlineData("player-2")]
    [InlineData("audio")]
    [InlineData("office")]
    public async Task OldSupervisorRegistrationFencesDelayedFirstWorkerHello(string role)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var oldRequest = Guid.NewGuid();
        var oldGroup = await fixture.RuntimeAuthority.BeginStartAsync(oldRequest);
        await fixture.RuntimeAuthority.ArmAsync(oldRequest, oldGroup.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var supervisorInstance = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "supervisor", supervisorInstance));
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        try
        {
            await using var supervisor = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(supervisor, process, "supervisor", supervisorInstance, null);
            Assert.Equal("welcome", (await ReadAsync(supervisor)).MessageType);
            var workerInstance = Guid.NewGuid();
            await WriteAsync(supervisor, Frame("register_process", supervisorInstance, 0, null,
                ChildRegistration(process, role, workerInstance)));
            Assert.True((await ReadAsync(supervisor)).Payload.Deserialize<RegistrationResultDto>()!.Accepted);
            Assert.True(registry.TryGet(process.Id, out var registered));
            Assert.Equal(workerInstance, registered!.InstanceId);

            Assert.True(await fixture.RuntimeAuthority.TryFaultSupervisorExitAsync(oldGroup.GroupEpoch, 903, supervisorInstance));
            var nextRequest = Guid.NewGuid();
            var newGroup = await fixture.RuntimeAuthority.BeginStartAsync(nextRequest);
            await fixture.RuntimeAuthority.ArmAsync(nextRequest, newGroup.GroupEpoch);
            await using var worker = await ConnectAsync(server.PipeName);
            var welcome = await ReconnectHelloAsync(worker, process, role, workerInstance, ReconnectTarget(role));

            Assert.True(welcome is null || welcome.MessageType != "welcome",
                $"旧组 {oldGroup.GroupEpoch} 登记但从未 hello 的 {role} 取得新组 {newGroup.GroupEpoch} welcome。");
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    /// <summary>旧实例在新组 Starting/Armed 时不能重新成为该组 Ready 角色。</summary>
    [Theory]
    [InlineData("player-1", false)]
    [InlineData("player-2", false)]
    [InlineData("audio", false)]
    [InlineData("office", false)]
    [InlineData("player-1", true)]
    [InlineData("player-2", true)]
    [InlineData("audio", true)]
    [InlineData("office", true)]
    public async Task StaleWorkerCannotRejoinNewGroupReadiness(string role, bool armNewGroup)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid();
        var oldGroup = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, oldGroup.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var instance = Guid.NewGuid();
        var registry = new RegisteredProcessRegistry();
        registry.Register(CurrentIdentity(process, role, instance));
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        try
        {
            var target = ReconnectTarget(role);
            await using var original = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(original, process, role, instance, target);
            var oldWelcome = await ReadAsync(original);
            Assert.Equal("welcome", oldWelcome.MessageType);
            Assert.Equal(oldGroup.GroupEpoch, oldWelcome.Payload.Deserialize<WelcomeDto>()!.GroupEpoch);
            await WriteAsync(original, Frame("worker_ready", instance, oldWelcome.OwnerEpoch, target, ReadyForReconnect()));
            Assert.True((await ReadAsync(original)).Payload.GetProperty("accepted").GetBoolean());
            await original.DisposeAsync();

            Assert.True(await fixture.RuntimeAuthority.TryFaultSupervisorExitAsync(oldGroup.GroupEpoch, 901, Guid.NewGuid()));
            var nextRequest = Guid.NewGuid();
            var newGroup = await fixture.RuntimeAuthority.BeginStartAsync(nextRequest);
            if (armNewGroup) await fixture.RuntimeAuthority.ArmAsync(nextRequest, newGroup.GroupEpoch);
            Assert.True(registry.TryGet(process.Id, out var oldRegistration));
            Assert.Equal(instance, oldRegistration!.InstanceId);

            await using var reconnect = await ConnectAsync(server.PipeName);
            var welcome = await ReconnectHelloAsync(reconnect, process, role, instance, target);
            if (welcome is null || welcome.MessageType != "welcome") return;
            await WriteAsync(reconnect, Frame("worker_ready", instance, welcome.OwnerEpoch, target, ReadyForReconnect()));
            var readyResponse = await ReadAsync(reconnect);
            var roleReady = !broker.GetRuntimeReadiness(newGroup.GroupEpoch).MissingRoles.Contains(role);
            var accepted = readyResponse.Payload.TryGetProperty("accepted", out var value) && value.GetBoolean();
            Assert.False(accepted || roleReady,
                $"旧 {role} 跨组认领成功：group {oldGroup.GroupEpoch}->{newGroup.GroupEpoch}, " +
                $"owner {oldWelcome.OwnerEpoch}->{welcome.OwnerEpoch}, state {(armNewGroup ? "Armed" : "Starting")}, " +
                $"healthAccepted={accepted}, currentRoleReady={roleReady}。");
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    /// <summary>源代次不是运行组身份；旧组请求被挡住后，旧实例仍不得领取新组命令或恢复旧画面。</summary>
    [Theory]
    [InlineData("player-1")]
    [InlineData("audio")]
    public async Task SourceGenerationCannotReplaceCrossGroupWorkerFence(string role)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid();
        var oldGroup = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, oldGroup.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var instance = Guid.NewGuid();
        var registry = new RegisteredProcessRegistry();
        registry.Register(CurrentIdentity(process, role, instance));
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        try
        {
            var target = ReconnectTarget(role)!;
            var targetKind = role == "audio" ? CommandTargetKind.Audio : CommandTargetKind.Display;
            await using var original = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(original, process, role, instance, target);
            var oldWelcome = await ReadAsync(original);
            Assert.Equal("welcome", oldWelcome.MessageType);
            const long oldGeneration = 7;
            await fixture.Writes.ExecuteAsync(async (database, token) =>
            {
                if (targetKind == CommandTargetKind.Display)
                {
                    var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1, token);
                    session.DesiredGeneration = oldGeneration;
                    session.ObservedGeneration = oldGeneration;
                    session.PlaybackState = PlaybackState.Playing;
                }
                else
                {
                    var audio = await database.BackgroundAudioStates.SingleAsync(token);
                    audio.DesiredGeneration = oldGeneration;
                    audio.ObservedGeneration = oldGeneration;
                    audio.PlaybackState = PlaybackState.Playing;
                }
            });
            await original.DisposeAsync();
            Assert.True(await fixture.RuntimeAuthority.TryFaultSupervisorExitAsync(oldGroup.GroupEpoch, 902, Guid.NewGuid()));
            var newRequest = Guid.NewGuid();
            var newGroup = await fixture.RuntimeAuthority.BeginStartAsync(newRequest);
            await fixture.RuntimeAuthority.ArmAsync(newRequest, newGroup.GroupEpoch);

            await using var reconnect = await ConnectAsync(server.PipeName);
            var welcome = await ReconnectHelloAsync(reconnect, process, role, instance, target);
            if (welcome is null || welcome.MessageType != "welcome") return;
            await WriteAsync(reconnect, Frame("claim_request", instance, welcome.OwnerEpoch, target,
                new ClaimRequestDto { GroupEpoch = oldGroup.GroupEpoch }));
            var oldClaim = await ReadAsync(reconnect);
            Assert.Equal("no_work", oldClaim.MessageType);
            Assert.Equal("fenced", oldClaim.Payload.Deserialize<NoWorkDto>()!.Reason);

            await WriteAsync(reconnect, Frame("state_report", instance, welcome.OwnerEpoch, target, new StateReportDto
            {
                SourceGeneration = oldGeneration,
                State = JsonSerializer.SerializeToElement(new { playback_state = "playing", error_message = "" }),
            }));
            var oldState = await ReadAsync(reconnect);
            var oldStateAccepted = oldState.Payload.GetProperty("accepted").GetBoolean();
            var queued = await fixture.Commands.EnqueueAsync(new EnqueueCommand(targetKind, 1, "SET_VOLUME", "{}", oldGeneration + 1, 1));
            await WriteAsync(reconnect, Frame("claim_request", instance, welcome.OwnerEpoch, target,
                new ClaimRequestDto { GroupEpoch = newGroup.GroupEpoch }));
            var newClaim = await ReadAsync(reconnect);
            var claimed = newClaim.MessageType == "command_lease";
            if (claimed)
            {
                var lease = newClaim.Payload.Deserialize<CommandLeaseDto>()!;
                Assert.Equal(queued.CommandId, lease.CommandId);
                Assert.Equal(oldGeneration + 1, lease.SourceGeneration);
                Assert.Equal(newGroup.GroupEpoch, lease.GroupEpoch);
                await using var database = fixture.Database.CreateDbContext();
                var owner = await database.WorkerOwnerships.AsNoTracking().SingleAsync(item => item.TargetKind == targetKind && item.TargetId == 1);
                var persisted = await database.CommandRecords.AsNoTracking().SingleAsync(item => item.CommandId == queued.CommandId);
                Assert.Equal(WorkerOwnershipState.Online, owner.Status);
                Assert.Equal(instance, persisted.ConsumerInstanceId);
                Assert.Equal(CommandStatus.Processing, persisted.Status);
            }
            Assert.False(oldStateAccepted || claimed,
                $"旧 {role} 跨组恢复：旧 group claim=fenced，但未变化的旧 generation {oldGeneration} 状态 accepted={oldStateAccepted}；" +
                $"新 group {newGroup.GroupEpoch} / generation {oldGeneration + 1} 命令被旧 instance 领取={claimed}；" +
                $"owner {oldWelcome.OwnerEpoch}->{welcome.OwnerEpoch}。");
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    /// <summary>角色目标沿用产品约束，不把 Office 伪造成 display。</summary>
    private static IpcTargetDto? ReconnectTarget(string role) => role switch
    {
        "player-1" => DisplayTarget(1),
        "player-2" => DisplayTarget(2),
        "audio" => new IpcTargetDto { Kind = "audio", Id = 1 },
        _ => null,
    };

    /// <summary>只声明就绪依赖，不启动任何原生组件。</summary>
    private static WorkerReadyDto ReadyForReconnect() => new()
    {
        UiReady = true,
        Dependencies = new Dictionary<string, string> { ["audit"] = "ready" },
    };

    /// <summary>同一测试进程提供真实 OS PID/start/session，不伪造原生身份。</summary>
    private static RegisterProcessDto ChildRegistration(Process process, string role, Guid instance) => new()
    {
        Role = role,
        ProcessId = process.Id,
        ProcessStartTime = UtcStart(process).ToString("O"),
        LogonSessionId = process.SessionId,
        InstanceId = instance,
    };

    /// <summary>允许正确产品门禁直接断开旧握手；未拒绝则继续记录公共响应。</summary>
    private static async Task<IpcFrameDto?> ReconnectHelloAsync(NamedPipeClientStream stream, Process process,
        string role, Guid instance, IpcTargetDto? target)
    {
        try
        {
            await WriteHelloAsync(stream, process, role, instance, target);
            return await ReadAsync(stream);
        }
        catch (IOException) { return null; }
    }
}
