// Worker 管道会话的命令确认、异步事件与播出进度回归。
using System.IO.Pipes;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;

namespace ScpCv.Integration.Tests;

public sealed class RuntimeWorkerSessionTests
{
    /// <summary>
    /// 已取消的调用方不得在未进入领取循环时得到成功结果。
    /// :returns: 完成入场取消契约验证的任务。
    /// </summary>
    [Fact]
    public async Task AlreadyCancelledCallerDoesNotCompleteSuccessfully()
    {
        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();
        await using var session = CreateSession($"scp-cv-pre-cancelled-{Guid.NewGuid():N}", Guid.NewGuid());
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.RunAsync(
            (_, _) => throw new InvalidOperationException("已取消的调用方不应执行命令"), callerCancellation.Token));
        Assert.Equal(callerCancellation.Token, exception.CancellationToken);
    }

    /// <summary>
    /// 等待领取响应时取消调用方，保持 await 路径的取消契约且无需对端断线。
    /// :returns: 完成在途领取取消验证的任务。
    /// </summary>
    [Fact]
    public async Task CallerCancellationWhileWaitingForClaimRemainsCancelled()
    {
        var pipeName = $"scp-cv-pending-claim-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var callerCancellation = new CancellationTokenSource();
        var claimReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await using var server = CreateServer(pipeName);
            await server.WaitForConnectionAsync(timeout.Token);
            await AcceptWorkerAsync(server, timeout.Token);
            var claim = await ReadAsync(server, timeout.Token);
            Assert.Equal("claim_request", claim.MessageType);
            claimReceived.SetResult();
            await runFinished.Task.WaitAsync(timeout.Token);
        }, timeout.Token);

        await using var session = CreateSession(pipeName, Guid.NewGuid());
        var run = session.RunAsync((_, _) => throw new InvalidOperationException("取消用例不应执行命令"),
            callerCancellation.Token);
        try
        {
            await claimReceived.Task.WaitAsync(timeout.Token);
            callerCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(timeout.Token));
            Assert.False(timeout.IsCancellationRequested);
        }
        finally
        {
            callerCancellation.Cancel();
            runFinished.TrySetResult();
            await serverTask;
        }
    }

    /// <summary>
    /// 采样器在轮询尾部取消调用方且没有状态待发时，循环仍须报告调用方取消。
    /// :returns: 完成公共命名管道取消契约验证的任务。
    /// </summary>
    [Fact]
    public async Task CallerCancellationAfterStateSamplingDoesNotCompleteSuccessfully()
    {
        var pipeName = $"scp-cv-cancellation-test-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var callerCancellation = new CancellationTokenSource();
        var runFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await using var server = CreateServer(pipeName);
            await server.WaitForConnectionAsync(timeout.Token);
            await AcceptWorkerAsync(server, timeout.Token);
            var claim = await ReadAsync(server, timeout.Token);
            Assert.Equal("claim_request", claim.MessageType);
            await WriteAsync(server, Response(claim, "no_work", new NoWorkDto
            {
                Reason = "empty", RetryAfterMs = 1000,
            }, ownerEpoch: 7), timeout.Token);
            var heartbeat = await ReadAsync(server, timeout.Token);
            Assert.Equal("health_report", heartbeat.MessageType);
            await WriteAsync(server, Response(heartbeat, "health_accepted", new { accepted = true }, ownerEpoch: 7), timeout.Token);
            // 对端在 RunAsync 完成之前不关闭管道，排除 EOF 与调用方取消的竞争。
            await runFinished.Task.WaitAsync(timeout.Token);
        }, timeout.Token);

        await using var session = CreateSession(pipeName, Guid.NewGuid());
        var run = session.RunAsync((_, _) => throw new InvalidOperationException("取消用例不应执行命令"),
            _ =>
            {
                callerCancellation.Cancel();
                return Task.FromResult<WorkerStateSample?>(null);
            }, callerCancellation.Token);
        try
        {
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => run.WaitAsync(timeout.Token));
            Assert.Equal(callerCancellation.Token, exception.CancellationToken);
            Assert.False(timeout.IsCancellationRequested);
        }
        finally
        {
            callerCancellation.Cancel();
            runFinished.TrySetResult();
            await serverTask;
        }
    }

    [Fact]
    public async Task ServerShutdownCompletesLoopWithoutCancellingCallerToken()
    {
        var pipeName = $"scp-cv-shutdown-test-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await using var server = CreateServer(pipeName);
            await server.WaitForConnectionAsync(timeout.Token);
            await AcceptWorkerAsync(server, timeout.Token);
            var claim = await ReadAsync(server, timeout.Token);
            await WriteAsync(server, Response(claim, "no_work", new NoWorkDto
            {
                Reason = "empty", RetryAfterMs = 1000,
            }, ownerEpoch: 7), timeout.Token);
            await WriteAsync(server, new IpcFrameDto
            {
                MessageType = "shutdown_request",
                MessageId = Guid.NewGuid(),
                OwnerEpoch = 7,
                Payload = JsonSerializer.SerializeToElement(new { reason = "test_stop" }),
            }, timeout.Token);
            await runFinished.Task.WaitAsync(timeout.Token);
        }, timeout.Token);

        await using var session = CreateSession(pipeName, Guid.NewGuid());
        try
        {
            await session.RunAsync((_, _) => throw new InvalidOperationException("停机用例不应执行命令"), timeout.Token);
            Assert.False(timeout.IsCancellationRequested);
        }
        finally { runFinished.TrySetResult(); }
        await serverTask;
    }

    /// <summary>
    /// 采样器按协作停止返回空状态时，服务端取消仍正常结束且不取消调用方。
    /// :returns: 完成采样尾部协作停止验证的任务。
    /// </summary>
    [Fact]
    public async Task ServerShutdownAfterStateSamplingCompletesSuccessfully()
    {
        var pipeName = $"scp-cv-sampled-shutdown-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sampling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await using var server = CreateServer(pipeName);
            await server.WaitForConnectionAsync(timeout.Token);
            await AcceptWorkerAsync(server, timeout.Token);
            var claim = await ReadAsync(server, timeout.Token);
            await WriteAsync(server, Response(claim, "no_work", new NoWorkDto
            {
                Reason = "empty", RetryAfterMs = 1000,
            }, ownerEpoch: 7), timeout.Token);
            var heartbeat = await ReadAsync(server, timeout.Token);
            Assert.Equal("health_report", heartbeat.MessageType);
            await WriteAsync(server, Response(heartbeat, "health_accepted", new { accepted = true }, ownerEpoch: 7), timeout.Token);
            await sampling.Task.WaitAsync(timeout.Token);
            await WriteAsync(server, new IpcFrameDto
            {
                MessageType = "shutdown_request",
                MessageId = Guid.NewGuid(),
                OwnerEpoch = 7,
                Payload = JsonSerializer.SerializeToElement(new { reason = "test_stop" }),
            }, timeout.Token);
            await runFinished.Task.WaitAsync(timeout.Token);
        }, timeout.Token);

        await using var session = CreateSession(pipeName, Guid.NewGuid());
        try
        {
            await session.RunAsync((_, _) => throw new InvalidOperationException("停机用例不应执行命令"),
                async cancellationToken =>
                {
                    sampling.SetResult();
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
                    return null;
                }, timeout.Token).WaitAsync(timeout.Token);
            Assert.False(timeout.IsCancellationRequested);
        }
        finally
        {
            runFinished.TrySetResult();
            await serverTask;
        }
    }

    /// <summary>
    /// 空闲 Worker 持续上报实际进度，并在对端保持连接时按调用方取消结束。
    /// :returns: 完成进度上报与取消验证的任务。
    /// </summary>
    [Fact]
    public async Task IdleWorkerPublishesLiveProgressWithoutAnotherControlCommand()
    {
        var pipeName = $"scp-cv-progress-test-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var reported = new TaskCompletionSource<StateReportDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await using var server = CreateServer(pipeName);
            await server.WaitForConnectionAsync(timeout.Token);
            await AcceptWorkerAsync(server, timeout.Token);
            while (!timeout.IsCancellationRequested)
            {
                var request = await ReadAsync(server, timeout.Token);
                switch (request.MessageType)
                {
                    case "claim_request":
                        await WriteAsync(server, Response(request, "no_work", new NoWorkDto
                        {
                            Reason = "empty", RetryAfterMs = 1000,
                        }, ownerEpoch: 7), timeout.Token);
                        break;
                    case "health_report":
                        await WriteAsync(server, Response(request, "health_accepted", new { accepted = true }, ownerEpoch: 7), timeout.Token);
                        break;
                    case "state_report":
                        await WriteAsync(server, Response(request, "state_accepted", new { accepted = true }, ownerEpoch: 7), timeout.Token);
                        reported.SetResult(request.Payload.Deserialize<StateReportDto>()!);
                        await runFinished.Task.WaitAsync(timeout.Token);
                        return;
                    default:
                        throw new InvalidOperationException($"未预期消息：{request.MessageType}");
                }
            }
        }, timeout.Token);

        await using var session = CreateSession(pipeName, Guid.NewGuid());
        var run = session.RunAsync((_, _) => throw new InvalidOperationException("此用例不应执行命令"),
            _ => Task.FromResult<WorkerStateSample?>(new WorkerStateSample(
                11, JsonSerializer.SerializeToElement(new { source_generation = 11, position_ms = 2400 }))),
            callerCancellation.Token);
        try
        {
            var state = await reported.Task.WaitAsync(timeout.Token);
            callerCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(timeout.Token));
            Assert.False(timeout.IsCancellationRequested);
            Assert.Equal(11, state.SourceGeneration);
            Assert.Equal(2400, state.State.GetProperty("position_ms").GetInt32());
        }
        finally
        {
            callerCancellation.Cancel();
            runFinished.TrySetResult();
            await serverTask;
        }
    }

    [Fact]
    public async Task LostResultAcknowledgementReconnectsAndReplaysWithoutExecutingAgain()
    {
        var pipeName = $"scp-cv-session-test-{Guid.NewGuid():N}";
        var instanceId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var claimToken = Guid.NewGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var firstResult = new TaskCompletionSource<IpcFrameDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replayedResult = new TaskCompletionSource<IpcFrameDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            await using (var first = CreateServer(pipeName))
            {
                await first.WaitForConnectionAsync(timeout.Token);
                await AcceptWorkerAsync(first, timeout.Token);
                var claim = await ReadAsync(first, timeout.Token);
                Assert.Equal("claim_request", claim.MessageType);
                await WriteAsync(first, Response(claim, "command_lease", new CommandLeaseDto
                {
                    CommandId = commandId,
                    TargetSequence = 1,
                    Command = "PLAY",
                    ClaimToken = claimToken,
                    OwnerEpoch = 7,
                    GroupEpoch = 3,
                    SourceGeneration = 11,
                    LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
                }, ownerEpoch: 7), timeout.Token);
                firstResult.SetResult(await ReadAsync(first, timeout.Token));
                // 模拟命令已经落地，但 ResultAccepted 在传输前连接丢失。
            }

            await using var second = CreateServer(pipeName);
            await second.WaitForConnectionAsync(timeout.Token);
            await AcceptWorkerAsync(second, timeout.Token);
            var replay = await ReadAsync(second, timeout.Token);
            replayedResult.SetResult(replay);
            await WriteAsync(second, Response(replay, "result_accepted", new ResultAcceptedDto
            {
                CommandId = commandId,
                Accepted = true,
                Duplicate = true,
            }, ownerEpoch: 7), timeout.Token);

            var nextClaim = await ReadAsync(second, timeout.Token);
            Assert.Equal("claim_request", nextClaim.MessageType);
            await WriteAsync(second, Response(nextClaim, "no_work", new NoWorkDto
            {
                Reason = "empty",
                RetryAfterMs = 1000,
            }, ownerEpoch: 7), timeout.Token);
        }, timeout.Token);

        var executionCount = 0;
        await using var session = CreateSession(pipeName, instanceId);
        var run = session.RunAsync((_, _) =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.FromResult(new WorkerExecutionResult(
                "completed",
                "ok",
                JsonSerializer.SerializeToElement(new { source_generation = 11, playback_state = "playing" })));
        }, timeout.Token);

        var original = await firstResult.Task.WaitAsync(timeout.Token);
        var replayed = await replayedResult.Task.WaitAsync(timeout.Token);
        await serverTask;
        timeout.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal(1, executionCount);
        Assert.Equal("command_result", original.MessageType);
        Assert.Equal(original.MessageId, replayed.MessageId);
        Assert.Equal(
            original.Payload.Deserialize<CommandResultDto>()!.ResultHash,
            replayed.Payload.Deserialize<CommandResultDto>()!.ResultHash);
    }

    [Fact]
    public async Task LostAudioEventAcknowledgementReusesMessageAndEventIdentityAfterReconnect()
    {
        var pipeName = $"scp-cv-session-test-{Guid.NewGuid():N}";
        var instanceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var firstEvent = new TaskCompletionSource<IpcFrameDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replayedEvent = new TaskCompletionSource<IpcFrameDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            await using (var first = CreateServer(pipeName))
            {
                await first.WaitForConnectionAsync(timeout.Token);
                await AcceptWorkerAsync(first, timeout.Token);
                firstEvent.SetResult(await ReadAsync(first, timeout.Token));
            }

            await using var second = CreateServer(pipeName);
            await second.WaitForConnectionAsync(timeout.Token);
            await AcceptWorkerAsync(second, timeout.Token);
            var replay = await ReadAsync(second, timeout.Token);
            replayedEvent.SetResult(replay);
            await WriteAsync(second, Response(replay, "event_accepted", new
            {
                accepted = true,
                event_id = eventId,
            }, ownerEpoch: 7), timeout.Token);
        }, timeout.Token);

        await using var session = CreateSession(pipeName, instanceId);
        await session.SendAudioFinishedAsync(new AudioFinishedDto
        {
            EventId = eventId,
            SourceId = 9,
            SourceGeneration = 12,
        }, timeout.Token);
        await serverTask;

        var original = await firstEvent.Task.WaitAsync(timeout.Token);
        var replayed = await replayedEvent.Task.WaitAsync(timeout.Token);
        Assert.Equal("audio_finished", original.MessageType);
        Assert.Equal(original.MessageId, replayed.MessageId);
        Assert.Equal(eventId, replayed.Payload.Deserialize<AudioFinishedDto>()!.EventId);
    }

    private static RuntimeWorkerSession CreateSession(string pipeName, Guid instanceId) => new(
        pipeName,
        new RuntimeWorkerIdentity(
            "audio",
            instanceId,
            new IpcTargetDto { Kind = "audio", Id = 1 },
            ["test"]));

    private static async Task AcceptWorkerAsync(Stream server, CancellationToken cancellationToken)
    {
        var hello = await ReadAsync(server, cancellationToken);
        Assert.Equal("hello", hello.MessageType);
        await WriteAsync(server, Response(hello, "welcome", new WelcomeDto
        {
            ServiceEpoch = 1,
            GroupEpoch = 3,
            GroupState = "armed",
            AcceptedCapabilities = ["test"],
        }, ownerEpoch: 7), cancellationToken);

        var ready = await ReadAsync(server, cancellationToken);
        Assert.Equal("worker_ready", ready.MessageType);
        await WriteAsync(server, Response(ready, "health_accepted", new { accepted = true }, ownerEpoch: 7), cancellationToken);
    }

    private static NamedPipeServerStream CreateServer(string pipeName) => new(
        pipeName,
        PipeDirection.InOut,
        1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private static IpcFrameDto Response<T>(
        IpcFrameDto request,
        string type,
        T payload,
        long ownerEpoch) => new()
    {
        MessageType = type,
        MessageId = Guid.NewGuid(),
        CorrelationId = request.MessageId,
        InstanceId = Guid.Empty,
        OwnerEpoch = ownerEpoch,
        Target = request.Target,
        Payload = JsonSerializer.SerializeToElement(payload),
    };

    private static Task WriteAsync(
        Stream stream,
        IpcFrameDto frame,
        CancellationToken cancellationToken) =>
        IpcFrameCodec.WritePayloadAsync(stream, JsonSerializer.SerializeToUtf8Bytes(frame), cancellationToken).AsTask();

    private static async Task<IpcFrameDto> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = await IpcFrameCodec.ReadPayloadAsync(stream, TimeSpan.FromSeconds(5), cancellationToken);
        return JsonSerializer.Deserialize<IpcFrameDto>(bytes)!;
    }
}
