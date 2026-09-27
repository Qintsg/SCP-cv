// 协作退出帧必须从真实 broker 发送到已认证管道，不能只保留 Worker 接收分支。
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.Ipc;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class RuntimePipeShutdownTests
{
    [Fact]
    public async Task BrokerSendsShutdownToRegisteredOfficeConnection()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var requestId = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        await fixture.RuntimeAuthority.ArmAsync(requestId, starting.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        try
        {
            var instanceId = Guid.NewGuid();
            var processStart = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            registry.Register(new RegisteredProcessIdentity(process.Id, processStart, process.SessionId, "office", instanceId));
            await using var client = new NamedPipeClientStream(".", server.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(timeout.Token);
            await IpcFrameCodec.WritePayloadAsync(client, JsonSerializer.SerializeToUtf8Bytes(new IpcFrameDto
            {
                MessageType = "hello",
                MessageId = Guid.NewGuid(),
                InstanceId = instanceId,
                Payload = JsonSerializer.SerializeToElement(new HelloDto
                {
                    Role = "office", ProcessId = process.Id, ProcessStartTime = processStart.ToString("O"),
                    LogonSessionId = process.SessionId,
                }),
            }), timeout.Token);
            var welcome = await ReadAsync(client, timeout.Token);
            Assert.Equal("welcome", welcome.MessageType);

            await broker.NotifyRuntimeShutdownAsync("qa_stop", timeout.Token);

            var shutdown = await ReadAsync(client, timeout.Token);
            Assert.Equal("shutdown_request", shutdown.MessageType);
            Assert.Null(shutdown.CorrelationId);
            Assert.Equal(welcome.OwnerEpoch, shutdown.OwnerEpoch);
            Assert.Equal(starting.GroupEpoch, shutdown.Payload.GetProperty("group_epoch").GetInt64());
            Assert.Equal("qa_stop", shutdown.Payload.GetProperty("reason").GetString());
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    private static RuntimePipeBroker CreateBroker(ControlHostFixture fixture, RegisteredProcessRegistry registry, NamedPipeServer server)
    {
        var commands = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = new RuntimeStateService(fixture.Database, fixture.Writes, commands, fixture.TimeProvider);
        var audio = new BackgroundAudioService(fixture.Database, fixture.Writes, commands, fixture.TimeProvider);
        var events = new SseEventHub(runtime, audio, new SseEventStreamOptions());
        var publisher = new RuntimeProjectionPublisher(fixture.Writes, events, fixture.TimeProvider);
        var leases = new CommandLeaseService(fixture.Commands, fixture.RuntimeAuthority,
            fixture.Database, fixture.Writes, fixture.TimeProvider);
        var dispatcher = new RuntimeMessageDispatcher(leases,
            new CommandResultService(fixture.Commands), publisher, new AudioFinishedEventProcessor(audio));
        return new RuntimePipeBroker(server, registry, dispatcher, fixture.RuntimeAuthority, NullLogger<RuntimePipeBroker>.Instance);
    }

    private static async Task<IpcFrameDto> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = await IpcFrameCodec.ReadPayloadAsync(stream, TimeSpan.FromSeconds(5), cancellationToken);
        return JsonSerializer.Deserialize<IpcFrameDto>(bytes)!;
    }
}
