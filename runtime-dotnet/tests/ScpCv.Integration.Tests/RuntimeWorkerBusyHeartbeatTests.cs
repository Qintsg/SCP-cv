// 长命令等待期间仍保持认证传输心跳；续租不冒充心跳，心跳不冒充实际UI进度。
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;

namespace ScpCv.Integration.Tests;

public sealed class RuntimeWorkerBusyHeartbeatTests
{
    /// <summary>:returns: 真实命名管道上的长命令在五秒离线阈值前有心跳，仍续租且只执行一次。</summary>
    [Fact]
    public async Task BusyWorkerKeepsTransportAliveWithoutInventingUiProgress()
    {
        var pipeName = $"scp-cv-busy-heartbeat-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(18));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var completion = new TaskCompletionSource<WorkerExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandId = Guid.NewGuid();
        var claimToken = Guid.NewGuid();
        var executed = 0;
        var serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(timeout.Token);
            var hello = await ReadAsync(server, timeout.Token);
            Assert.Equal("hello", hello.MessageType);
            await ReplyAsync(server, hello, "welcome", new WelcomeDto
            { ServiceEpoch = 1, GroupEpoch = 3, GroupState = "armed", AcceptedCapabilities = ["test"] }, timeout.Token);
            var ready = await ReadAsync(server, timeout.Token);
            Assert.Equal("worker_ready", ready.MessageType);
            await ReplyAsync(server, ready, "health_accepted", new { accepted = true }, timeout.Token);
            var claim = await ReadAsync(server, timeout.Token);
            Assert.Equal("claim_request", claim.MessageType);
            await ReplyAsync(server, claim, "command_lease", new CommandLeaseDto
            {
                CommandId = commandId, ClaimToken = claimToken, OwnerEpoch = 7, GroupEpoch = 3,
                TargetSequence = 1, SourceGeneration = 11, Command = "OPEN",
                LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
            }, timeout.Token);
            var elapsed = Stopwatch.StartNew();
            var heartbeats = 0;
            var renewed = false;
            while (true)
            {
                var frame = await ReadAsync(server, timeout.Token);
                if (frame.MessageType == "health_report")
                {
                    if (heartbeats == 0) Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), "长命令必须在原五秒阈值内发送传输心跳");
                    var health = frame.Payload.Deserialize<HealthReportDto>()!;
                    Assert.True(health.TransportHealthy);
                    Assert.False(health.UiHealthy, "仍在执行的命令不能用心跳授予真实UI进度");
                    heartbeats++;
                    await ReplyAsync(server, frame, "health_accepted", new { accepted = true }, timeout.Token);
                }
                else if (frame.MessageType == "lease_renew")
                {
                    Assert.True(heartbeats > 0, "命令续租不能替代目标在线心跳");
                    var renewal = frame.Payload.Deserialize<LeaseRenewDto>()!;
                    Assert.Equal(commandId, renewal.CommandId);
                    Assert.Equal(claimToken, renewal.ClaimToken);
                    renewed = true;
                    await ReplyAsync(server, frame, "renew_result", new RenewResultDto
                    { Accepted = true, Reason = "accepted", LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O") }, timeout.Token);
                    completion.TrySetResult(new("completed", "ok", JsonSerializer.SerializeToElement(new { source_generation = 11 })));
                }
                else
                {
                    Assert.Equal("command_result", frame.MessageType);
                    Assert.True(renewed);
                    Assert.True(heartbeats >= 2);
                    await ReplyAsync(server, frame, "result_accepted", new ResultAcceptedDto
                    { CommandId = commandId, Accepted = true }, timeout.Token);
                    break;
                }
            }
            var state = await ReadAsync(server, timeout.Token);
            Assert.Equal("state_report", state.MessageType);
            await ReplyAsync(server, state, "state_accepted", new { accepted = true }, timeout.Token);
            await IpcFrameCodec.WritePayloadAsync(server, JsonSerializer.SerializeToUtf8Bytes(new IpcFrameDto
            {
                MessageType = "shutdown_request", MessageId = Guid.NewGuid(), OwnerEpoch = 7,
                Payload = JsonSerializer.SerializeToElement(new { reason = "test_done" }),
            }), timeout.Token);
            // shutdown是异步通知；等接收循环完成才关测试管道，避免EOF抢先掩盖协作停止。
            await runFinished.Task.WaitAsync(timeout.Token);
        }, timeout.Token);
        await using var session = new RuntimeWorkerSession(pipeName,
            new("player", Guid.NewGuid(), new IpcTargetDto { Kind = "display", Id = 1 }, ["test"]));
        var run = session.RunAsync(async (_, token) =>
        {
            Interlocked.Increment(ref executed);
            return await completion.Task.WaitAsync(token);
        }, caller.Token);
        try
        {
            var first = await Task.WhenAny(run, serverTask);
            if (first == serverTask) await serverTask;
            await run.WaitAsync(timeout.Token);
            runFinished.TrySetResult();
            await serverTask;
            Assert.Equal(1, executed);
            Assert.False(timeout.IsCancellationRequested);
        }
        finally
        {
            caller.Cancel();
            runFinished.TrySetResult();
            try { await run; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>:param stream: 本轮管道。:param token: 有界寿命。:returns: 不超过七秒的真实协议帧。</summary>
    private static async Task<IpcFrameDto> ReadAsync(Stream stream, CancellationToken token) =>
        JsonSerializer.Deserialize<IpcFrameDto>(await IpcFrameCodec.ReadPayloadAsync(stream, TimeSpan.FromSeconds(7), token))!;

    /// <summary>:param stream: 本轮管道。:param request: 原请求。:param type: 响应类型。:param body: 响应。:param token: 寿命。</summary>
    private static async Task ReplyAsync<T>(Stream stream, IpcFrameDto request, string type, T body, CancellationToken token) =>
        await IpcFrameCodec.WritePayloadAsync(stream, JsonSerializer.SerializeToUtf8Bytes(new IpcFrameDto
        {
            MessageType = type, MessageId = Guid.NewGuid(), CorrelationId = request.MessageId,
            OwnerEpoch = 7, Target = request.Target, Payload = JsonSerializer.SerializeToElement(body),
        }), token);
}
