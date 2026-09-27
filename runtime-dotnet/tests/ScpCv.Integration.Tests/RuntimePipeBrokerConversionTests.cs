// ControlHost 准备作业经管道转发给 OfficeHost，结果带作业身份落库。
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

public sealed class RuntimePipeBrokerConversionTests
{
    [Fact]
    public async Task ConversionUsesRegisteredOfficeOperationWithoutAcquiringSlideShowSlot()
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
            registry.Register(new RegisteredProcessIdentity(process.Id,
                new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
                process.SessionId, "office", officeId));
            await using var office = await ConnectAsync(server.PipeName);
            await WriteAsync(office, Frame("hello", officeId, 0, new HelloDto
            {
                Role = "office",
                ProcessId = process.Id,
                ProcessStartTime = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero).ToString("O"),
                LogonSessionId = process.SessionId,
            }));
            var welcome = await ReadAsync(office);
            Assert.Equal("welcome", welcome.MessageType);

            var jobId = Guid.NewGuid();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var conversion = broker.ConvertSlidesAsync(jobId, 7, @"C:\source.pptx", @"C:\staging", timeout.Token);
            var forwarded = await ReadAsync(office);
            var request = forwarded.Payload.Deserialize<OfficeRequestDto>();
            Assert.NotNull(request);
            Assert.Equal("export_slides", request.Operation);
            Assert.Equal(jobId, request.ParentJobId);
            Assert.Equal(starting.GroupEpoch, request.GroupEpoch);

            await WriteAsync(office, Frame("office_result", officeId, welcome.OwnerEpoch, new OfficeResultDto
            {
                OfficeOperationId = jobId,
                Status = "succeeded",
                ResultFingerprint = "sha256:conversion",
                Result = JsonSerializer.SerializeToElement(new { page_count = 3 }),
            }));
            var accepted = await ReadAsync(office);
            Assert.Equal("office_result_accepted", accepted.MessageType);
            Assert.True(accepted.Payload.GetProperty("accepted").GetBoolean());
            Assert.Equal("succeeded", (await conversion).Status);
            await using var database = fixture.Database.CreateDbContext();
            Assert.Equal(OperationStatus.Succeeded,
                (await database.OfficeOperations.SingleAsync(item => item.OperationId == jobId)).Status);
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    private static RuntimePipeBroker CreateBroker(
        ControlHostFixture fixture,
        RegisteredProcessRegistry registry,
        NamedPipeServer server)
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
        return new RuntimePipeBroker(server, registry, dispatcher,
            fixture.RuntimeAuthority, NullLogger<RuntimePipeBroker>.Instance);
    }

    private static IpcFrameDto Frame<T>(string type, Guid instanceId, long epoch, T payload) => new()
    {
        MessageType = type,
        MessageId = Guid.NewGuid(),
        InstanceId = instanceId,
        OwnerEpoch = epoch,
        Payload = JsonSerializer.SerializeToElement(payload),
    };

    private static async Task<NamedPipeClientStream> ConnectAsync(string pipeName)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(timeout.Token);
        return client;
    }

    private static Task WriteAsync(Stream stream, IpcFrameDto frame) =>
        IpcFrameCodec.WritePayloadAsync(stream, JsonSerializer.SerializeToUtf8Bytes(frame)).AsTask();

    private static async Task<IpcFrameDto> ReadAsync(Stream stream)
    {
        var bytes = await IpcFrameCodec.ReadPayloadAsync(stream, TimeSpan.FromSeconds(5));
        return JsonSerializer.Deserialize<IpcFrameDto>(bytes)
            ?? throw new InvalidDataException("Broker 返回空 IPC 帧。");
    }
}
