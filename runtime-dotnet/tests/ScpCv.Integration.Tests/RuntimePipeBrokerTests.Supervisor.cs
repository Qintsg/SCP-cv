// Supervisor 当前组注册与完整角色 Ready 门禁；共享 helper 保留在主测试 partial。
// 运行组管道身份、就绪与命令闭环回归。
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    [Fact]
    public async Task RuntimeReadinessRequiresEveryWorkerRoleInTheCurrentStartingEpoch()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(Guid.NewGuid());
        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);

        var before = await broker.WaitForRuntimeReadyAsync(starting.GroupEpoch, TimeSpan.FromMilliseconds(100));
        Assert.False(before.Ready);
        Assert.Equal(4, before.MissingRoles.Count);

        var clients = new List<NamedPipeClientStream>();
        NamedPipeClientStream? officeClient = null;
        IpcFrameDto? officeWelcome = null;
        Guid officeInstanceId = Guid.Empty;
        try
        {
            foreach (var role in new[] { "player-1", "player-2", "audio", "office" })
            {
                var instanceId = Guid.NewGuid();
                registry.Register(CurrentIdentity(process, role, instanceId));
                var client = await ConnectAsync(server.PipeName);
                clients.Add(client);
                var target = role.StartsWith("player-", StringComparison.Ordinal)
                    ? DisplayTarget(int.Parse(role[7..], System.Globalization.CultureInfo.InvariantCulture))
                    : role == "audio" ? new IpcTargetDto { Kind = "audio", Id = 1 } : null;
                await WriteHelloAsync(client, process, role, instanceId, target);
                var welcome = await ReadAsync(client);
                Assert.Equal("starting", welcome.Payload.Deserialize<WelcomeDto>()!.GroupState);
                var initiallyReady = role != "office";
                await WriteAsync(client, Frame("worker_ready", instanceId, welcome.OwnerEpoch, target, new WorkerReadyDto
                {
                    UiReady = initiallyReady,
                    Dependencies = new Dictionary<string, string> { [role] = "ready" },
                }));
                var accepted = await ReadAsync(client);
                Assert.True(accepted.Payload.GetProperty("accepted").GetBoolean());
                if (role == "office")
                {
                    officeClient = client;
                    officeWelcome = welcome;
                    officeInstanceId = instanceId;
                }
            }

            var missingOffice = await broker.WaitForRuntimeReadyAsync(starting.GroupEpoch, TimeSpan.FromMilliseconds(100));
            Assert.False(missingOffice.Ready);
            Assert.Equal(["office"], missingOffice.MissingRoles);

            Assert.NotNull(officeClient);
            Assert.NotNull(officeWelcome);
            await WriteAsync(officeClient, Frame("worker_ready", officeInstanceId, officeWelcome.OwnerEpoch, null, new WorkerReadyDto
            {
                UiReady = true,
                Dependencies = new Dictionary<string, string> { ["office"] = "ready" },
            }));
            Assert.True((await ReadAsync(officeClient)).Payload.GetProperty("accepted").GetBoolean());

            var ready = await broker.WaitForRuntimeReadyAsync(starting.GroupEpoch, TimeSpan.FromSeconds(2));
            Assert.True(ready.Ready);
            Assert.Empty(ready.MissingRoles);

            var staleEpoch = await broker.WaitForRuntimeReadyAsync(starting.GroupEpoch + 1, TimeSpan.FromMilliseconds(100));
            Assert.False(staleEpoch.Ready);
            Assert.Equal(4, staleEpoch.MissingRoles.Count);

            await officeClient.DisposeAsync();
            for (var attempt = 0; attempt < 50 && broker.GetRuntimeReadiness(starting.GroupEpoch).Ready; attempt++)
            {
                await Task.Delay(20);
            }
            var disconnected = broker.GetRuntimeReadiness(starting.GroupEpoch);
            Assert.False(disconnected.Ready);
            Assert.Contains("office", disconnected.MissingRoles);
        }
        finally
        {
            foreach (var client in clients) await client.DisposeAsync();
            await broker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SupervisorHeartbeatIsAcceptedWithoutDisplayTarget()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var startRequest = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        var registry = new RegisteredProcessRegistry();
        using var current = Process.GetCurrentProcess();
        var server = new NamedPipeServer(Guid.NewGuid(), current.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);

        var supervisorId = Guid.NewGuid();
        registry.Register(CurrentIdentity(current, "supervisor", supervisorId));
        await using var client = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(client, current, "supervisor", supervisorId, target: null);
        var welcome = await ReadAsync(client);
        Assert.Equal("welcome", welcome.MessageType);
        Assert.Equal(starting.GroupEpoch, welcome.Payload.Deserialize<WelcomeDto>()!.GroupEpoch);

        await WriteAsync(client, Frame("health_report", supervisorId, 0, null, new HealthReportDto
        {
            TransportHealthy = true,
            UiHealthy = false,
            ReportSequence = 1,
            ObservedAt = DateTimeOffset.UtcNow.ToString("O"),
        }));
        var heartbeat = await ReadAsync(client);

        Assert.Equal("health_accepted", heartbeat.MessageType);
        Assert.True(heartbeat.Payload.GetProperty("accepted").GetBoolean());
        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SupervisorCanRegisterOnlyAnExistingMatchingChildProcess()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var requestId = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        await fixture.RuntimeAuthority.ArmAsync(requestId, starting.GroupEpoch);
        var registry = new RegisteredProcessRegistry();
        using var current = Process.GetCurrentProcess();
        var server = new NamedPipeServer(Guid.NewGuid(), current.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);

        var supervisorId = Guid.NewGuid();
        registry.Register(CurrentIdentity(current, "supervisor", supervisorId));
        await using var client = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(client, current, "supervisor", supervisorId, target: null);
        Assert.Equal("welcome", (await ReadAsync(client)).MessageType);

        using var child = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            ArgumentList = { "-NoProfile", "-Command", "Start-Sleep -Seconds 30" },
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        try
        {
            var childId = Guid.NewGuid();
            var valid = Frame("register_process", supervisorId, 0, null, new RegisterProcessDto
            {
                Role = "audio",
                ProcessId = child.Id,
                ProcessStartTime = UtcStart(child).ToString("O"),
                LogonSessionId = child.SessionId,
                InstanceId = childId,
            });
            await WriteAsync(client, valid);
            var accepted = (await ReadAsync(client)).Payload.Deserialize<RegistrationResultDto>();
            Assert.NotNull(accepted);
            Assert.True(accepted.Accepted);
            Assert.True(registry.TryGet(child.Id, out var registered));
            Assert.Equal(childId, registered!.InstanceId);

            await WriteAsync(client, Frame("register_process", supervisorId, 0, null, new RegisterProcessDto
            {
                Role = "audio",
                ProcessId = child.Id,
                ProcessStartTime = UtcStart(child).AddSeconds(1).ToString("O"),
                LogonSessionId = child.SessionId,
                InstanceId = Guid.NewGuid(),
            }));
            var rejected = (await ReadAsync(client)).Payload.Deserialize<RegistrationResultDto>();
            Assert.NotNull(rejected);
            Assert.False(rejected.Accepted);
            Assert.Equal("process_identity_mismatch", rejected.Reason);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }
        }

        await broker.StopAsync(CancellationToken.None);
    }

}
