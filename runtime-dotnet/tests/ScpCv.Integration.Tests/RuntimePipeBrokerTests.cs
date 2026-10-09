// 运行组管道身份、就绪与命令闭环回归。
// 真 Named Pipe 的 Worker/Office 行为与共用夹具；Supervisor 门禁拆入独立 partial。
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
    public async Task WorkerHandshakeReturnsOwnerEpochAndSupportsHealthClaimAndWake()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var startRequest = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        await fixture.RuntimeAuthority.ArmAsync(startRequest, starting.GroupEpoch);

        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);

        var instanceId = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "player-1", instanceId));
        await using var client = await ConnectAsync(server.PipeName);

        await WriteAsync(client, Frame("hello", instanceId, 0, DisplayTarget(1), new HelloDto
        {
            Role = "player-1",
            ProcessId = process.Id,
            ProcessStartTime = UtcStart(process).ToString("O"),
            LogonSessionId = process.SessionId,
            Capabilities = ["image"],
        }));
        var welcome = await ReadAsync(client);

        Assert.Equal("welcome", welcome.MessageType);
        Assert.True(welcome.OwnerEpoch > 0);
        var welcomePayload = welcome.Payload.Deserialize<WelcomeDto>();
        Assert.NotNull(welcomePayload);
        Assert.Equal(starting.GroupEpoch, welcomePayload.GroupEpoch);
        Assert.Equal("armed", welcomePayload.GroupState);

        await WriteAsync(client, Frame("worker_ready", instanceId, welcome.OwnerEpoch, DisplayTarget(1), new WorkerReadyDto
        {
            UiReady = true,
            Dependencies = new Dictionary<string, string> { ["wpf"] = "ready" },
        }));
        var ready = await ReadAsync(client);
        Assert.Equal("health_accepted", ready.MessageType);
        Assert.True(ready.Payload.GetProperty("accepted").GetBoolean());

        await WriteAsync(client, Frame("claim_request", instanceId, welcome.OwnerEpoch, DisplayTarget(1), new ClaimRequestDto
        {
            GroupEpoch = starting.GroupEpoch,
        }));
        var noWork = await ReadAsync(client);
        Assert.Equal("no_work", noWork.MessageType);

        await broker.WakeAsync(new CommandWakeSignal(CommandTargetKind.Display, 1, 42));
        var wake = await ReadAsync(client);
        Assert.Equal("wake", wake.MessageType);
        Assert.Null(wake.CorrelationId);
        Assert.Equal(42, wake.Payload.Deserialize<WakeDto>()!.HighestSequence);

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BrokerKeepsIndependentConcurrentPlayerConnections()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var startRequest = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        await fixture.RuntimeAuthority.ArmAsync(startRequest, starting.GroupEpoch);

        var registry = new RegisteredProcessRegistry();
        using var process = Process.GetCurrentProcess();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);

        var firstId = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "player-1", firstId));
        await using var first = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(first, process, "player-1", firstId, DisplayTarget(1));
        Assert.Equal("welcome", (await ReadAsync(first)).MessageType);

        var secondId = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "player-2", secondId));
        await using var second = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(second, process, "player-2", secondId, DisplayTarget(2));
        Assert.Equal("welcome", (await ReadAsync(second)).MessageType);

        await broker.WakeAsync(new CommandWakeSignal(CommandTargetKind.Display, 1, 11));
        await broker.WakeAsync(new CommandWakeSignal(CommandTargetKind.Display, 2, 22));
        Assert.Equal(11, (await ReadAsync(first)).Payload.Deserialize<WakeDto>()!.HighestSequence);
        Assert.Equal(22, (await ReadAsync(second)).Payload.Deserialize<WakeDto>()!.HighestSequence);

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AudioFinishedIsAcceptedAndDeduplicatedThroughBrokerAfterServiceRestart()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var coordinator = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var setupAudio = new BackgroundAudioService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
        var (firstSourceId, secondSourceId) = await SeedAudioSourcesAsync(fixture);
        await setupAudio.SetPlaylistAsync([firstSourceId, secondSourceId]);
        await setupAudio.PlaySourceAsync(firstSourceId);
        await fixture.Writes.ExecuteAsync(async (database, cancellationToken) =>
        {
            var state = await database.BackgroundAudioStates.SingleAsync(cancellationToken);
            state.PlaybackState = PlaybackState.Playing;
        });

        var startRequest = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        await fixture.RuntimeAuthority.ArmAsync(startRequest, starting.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);

        var instanceId = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "audio", instanceId));
        await using var client = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(client, process, "audio", instanceId, new IpcTargetDto { Kind = "audio", Id = 1 });
        var welcome = await ReadAsync(client);
        await WriteAsync(client, Frame("worker_ready", instanceId, welcome.OwnerEpoch, new IpcTargetDto { Kind = "audio", Id = 1 }, new WorkerReadyDto
        {
            UiReady = true,
            Dependencies = new Dictionary<string, string> { ["libvlc"] = "ready" },
        }));
        Assert.Equal("health_accepted", (await ReadAsync(client)).MessageType);

        var eventId = Guid.NewGuid();
        var finished = Frame("audio_finished", instanceId, welcome.OwnerEpoch, new IpcTargetDto { Kind = "audio", Id = 1 }, new AudioFinishedDto
        {
            EventId = eventId,
            SourceId = firstSourceId,
            SourceGeneration = 1,
        });
        await WriteAsync(client, finished);
        Assert.Equal("event_accepted", (await ReadAsync(client)).MessageType);
        await WriteAsync(client, finished with { MessageId = Guid.NewGuid() });
        Assert.Equal("event_accepted", (await ReadAsync(client)).MessageType);

        await using var check = fixture.Database.CreateDbContext();
        Assert.Equal(secondSourceId, (await check.BackgroundAudioStates.SingleAsync()).CurrentSourceId);
        Assert.Single(await check.CommandRecords.Where(item => item.TriggerEventId == eventId).ToArrayAsync());
        await broker.StopAsync(CancellationToken.None);
    }


    /// <summary>退役入口无论旧期限或回退参数均不再转发，迟到结果也不能写业务状态。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetiredOfficeOpenDoesNotForwardOrPersistAndRejectsLateCompletion(bool expired)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var startRequest = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        await fixture.RuntimeAuthority.ArmAsync(startRequest, starting.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        try
        {
            var officeId = Guid.NewGuid();
            registry.Register(CurrentIdentity(process, "office", officeId));
            await using var office = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(office, process, "office", officeId, null);
            var officeWelcome = await ReadAsync(office);
            var playerId = Guid.NewGuid();
            registry.Register(CurrentIdentity(process, "player-1", playerId));
            await using var player = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(player, process, "player-1", playerId, DisplayTarget(1));
            var playerWelcome = await ReadAsync(player);
            var operationId = Guid.NewGuid();
            await WriteAsync(player, Frame("office_request", playerId, playerWelcome.OwnerEpoch, DisplayTarget(1), new OfficeRequestDto
            {
                OfficeOperationId = operationId, ParentCommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(),
                SourceGeneration = 7, GroupEpoch = starting.GroupEpoch,
                Deadline = DateTimeOffset.UtcNow.AddSeconds(expired ? -5 : 5).ToString("O"), Operation = "open",
                Parameters = new Dictionary<string, JsonElement>
                {
                    ["window_id"] = JsonSerializer.SerializeToElement(1),
                    ["source_id"] = JsonSerializer.SerializeToElement(42L),
                    ["fallback_uri"] = JsonSerializer.SerializeToElement("old.pdf"),
                },
            }));
            var result = (await ReadAsync(player)).Payload.Deserialize<OfficeResultDto>();
            Assert.NotNull(result);
            Assert.Equal("failed", result.Status);
            Assert.Equal("powerpoint_retired", result.ErrorCode);
            Assert.False(broker.CanConvertSlides);
            await WriteAsync(office, Frame("office_result", officeId, officeWelcome.OwnerEpoch, null, new OfficeResultDto
            {
                OfficeOperationId = operationId, Status = "succeeded", ResultFingerprint = "late",
                Result = JsonSerializer.SerializeToElement(new { playback_mode = "powerpoint" }),
            }));
            var late = await ReadAsync(office);
            Assert.Equal("office_result_accepted", late.MessageType);
            Assert.False(late.Payload.GetProperty("accepted").GetBoolean());
            await using var check = fixture.Database.CreateDbContext();
            Assert.Empty(await check.OfficeOperations.ToArrayAsync());
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    private static RuntimePipeBroker CreateBroker(
        ControlHostFixture fixture,
        RegisteredProcessRegistry registry,
        NamedPipeServer server)
    {
        var commandCoordinator = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = new RuntimeStateService(fixture.Database, fixture.Writes, commandCoordinator, fixture.TimeProvider);
        var audio = new BackgroundAudioService(fixture.Database, fixture.Writes, commandCoordinator, fixture.TimeProvider);
        var events = new SseEventHub(runtime, audio, new SseEventStreamOptions());
        var projections = new RuntimeProjectionPublisher(fixture.Writes, events, fixture.TimeProvider);
        var leases = new CommandLeaseService(
            fixture.Commands,
            fixture.RuntimeAuthority,
            fixture.Database,
            fixture.Writes,
            fixture.TimeProvider);
        var dispatcher = new RuntimeMessageDispatcher(
            leases,
            new CommandResultService(fixture.Commands),
            projections,
            new AudioFinishedEventProcessor(audio));
        return new RuntimePipeBroker(
            server,
            registry,
            dispatcher,
            fixture.RuntimeAuthority,
            NullLogger<RuntimePipeBroker>.Instance);
    }

    private static RegisteredProcessIdentity CurrentIdentity(Process process, string role, Guid instanceId) =>
        new(process.Id, UtcStart(process), process.SessionId, role, instanceId);

    private static DateTimeOffset UtcStart(Process process) =>
        new(process.StartTime.ToUniversalTime(), TimeSpan.Zero);

    private static IpcTargetDto DisplayTarget(int id) => new() { Kind = "display", Id = id };

    private static async Task<(long First, long Second)> SeedAudioSourcesAsync(ControlHostFixture fixture)
    {
        await using var database = fixture.Database.CreateDbContext();
        var first = new MediaSource
        {
            SourceType = MediaSourceType.Audio,
            Name = "broker-audio-1",
            Uri = "broker-audio-1.mp3",
            SourceRevision = 1,
            CreatedAt = fixture.TimeProvider.GetUtcNow(),
        };
        var second = new MediaSource
        {
            SourceType = MediaSourceType.Audio,
            Name = "broker-audio-2",
            Uri = "broker-audio-2.mp3",
            SourceRevision = 1,
            CreatedAt = fixture.TimeProvider.GetUtcNow(),
        };
        database.MediaSources.AddRange(first, second);
        await database.SaveChangesAsync();
        return (first.Id, second.Id);
    }

    private static IpcFrameDto Frame<T>(string type, Guid instanceId, long ownerEpoch, IpcTargetDto? target, T payload) => new()
    {
        MessageType = type,
        MessageId = Guid.NewGuid(),
        InstanceId = instanceId,
        OwnerEpoch = ownerEpoch,
        Target = target,
        Payload = JsonSerializer.SerializeToElement(payload),
    };

    private static async Task<NamedPipeClientStream> ConnectAsync(string pipeName)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(timeout.Token);
        return client;
    }

    private static Task WriteHelloAsync(
        Stream stream,
        Process process,
        string role,
        Guid instanceId,
        IpcTargetDto? target) =>
        WriteAsync(stream, Frame("hello", instanceId, 0, target, new HelloDto
        {
            Role = role,
            ProcessId = process.Id,
            ProcessStartTime = UtcStart(process).ToString("O"),
            LogonSessionId = process.SessionId,
        }));

    private static Task WriteAsync(Stream stream, IpcFrameDto frame) =>
        IpcFrameCodec.WritePayloadAsync(stream, JsonSerializer.SerializeToUtf8Bytes(frame)).AsTask();

    private static async Task<IpcFrameDto> ReadAsync(Stream stream)
    {
        var bytes = await IpcFrameCodec.ReadPayloadAsync(stream, TimeSpan.FromSeconds(5));
        return JsonSerializer.Deserialize<IpcFrameDto>(bytes)
            ?? throw new InvalidDataException("Broker 返回空 IPC 帧。");
    }
}
