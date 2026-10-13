// 命令局部取消不能冒充运行组停机；真实管道验证失败回执与后续命令仍可执行。
using System.IO.Pipes;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;

namespace ScpCv.Integration.Tests;

public sealed class RuntimeWorkerCommandCancellationTests
{
    private static readonly string[] ExpectedCommands = ["OPEN", "CLOSE"];
    /// <summary>:returns: OPEN自身截止时间取消仅结束该命令，不能退出Worker或重放OPEN；随后CLOSE仍获确认。</summary>
    [Fact]
    public async Task LocalCommandCancellationReportsFailureAndAcceptsNextClose()
    {
        var pipeName = $"scp-cv-local-cancellation-{Guid.NewGuid():N}";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new List<string>();
        var serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(deadline.Token);
            var hello = await ReadAsync(server, deadline.Token);
            Assert.Equal("hello", hello.MessageType);
            await ReplyAsync(server, hello, "welcome", new WelcomeDto
            { ServiceEpoch = 1, GroupEpoch = 3, GroupState = "armed", AcceptedCapabilities = ["test"] }, deadline.Token);
            var ready = await ReadAsync(server, deadline.Token);
            Assert.Equal("worker_ready", ready.MessageType);
            await ReplyAsync(server, ready, "health_accepted", new { accepted = true }, deadline.Token);
            foreach (var command in ExpectedCommands)
            {
                var claim = await ReadAsync(server, deadline.Token);
                Assert.Equal("claim_request", claim.MessageType);
                var lease = new CommandLeaseDto
                {
                    CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = command,
                    OwnerEpoch = 7, GroupEpoch = 3, SourceGeneration = command == "OPEN" ? 11 : 12,
                };
                await ReplyAsync(server, claim, "command_lease", lease, deadline.Token);
                var frame = await ReadAsync(server, deadline.Token);
                Assert.Equal("command_result", frame.MessageType);
                var result = frame.Payload.Deserialize<CommandResultDto>()!;
                Assert.Equal(lease.CommandId, result.CommandId);
                Assert.Equal(command == "OPEN" ? "failed" : "completed", result.Status);
                if (command == "OPEN") Assert.Contains("CanceledException", result.ResultCode, StringComparison.Ordinal);
                await ReplyAsync(server, frame, "result_accepted", new ResultAcceptedDto
                { CommandId = lease.CommandId, Accepted = true }, deadline.Token);
                var state = await ReadAsync(server, deadline.Token);
                Assert.Equal("state_report", state.MessageType);
                Assert.Equal(lease.SourceGeneration, state.Payload.Deserialize<StateReportDto>()!.SourceGeneration);
                await ReplyAsync(server, state, "state_accepted", new { accepted = true }, deadline.Token);
            }
            await IpcFrameCodec.WritePayloadAsync(server, JsonSerializer.SerializeToUtf8Bytes(new IpcFrameDto
            {
                MessageType = "shutdown_request", MessageId = Guid.NewGuid(), OwnerEpoch = 7,
                Payload = JsonSerializer.SerializeToElement(new { reason = "test_complete" }),
            }), deadline.Token);
            await finished.Task.WaitAsync(deadline.Token);
        }, deadline.Token);
        await using var session = new RuntimeWorkerSession(pipeName,
            new("player", Guid.NewGuid(), new IpcTargetDto { Kind = "display", Id = 1 }, ["test"]));
        var run = session.RunAsync(async (lease, token) =>
        {
            commands.Add(lease.Command);
            if (lease.Command == "OPEN")
            {
                using var local = new CancellationTokenSource();
                local.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, local.Token);
            }
            token.ThrowIfCancellationRequested();
            return new("completed", "ok", JsonSerializer.SerializeToElement(new { source_generation = lease.SourceGeneration }));
        }, caller.Token);
        try
        {
            if (await Task.WhenAny(run, serverTask) == serverTask) await serverTask;
            await run.WaitAsync(deadline.Token);
            finished.TrySetResult();
            await serverTask;
            Assert.Equal(ExpectedCommands, commands);
            Assert.False(caller.IsCancellationRequested);
        }
        finally
        {
            caller.Cancel(); finished.TrySetResult();
            try { await serverTask; } catch (Exception) when (run.IsFaulted || run.IsCanceled) { }
        }
    }

    /// <summary>:param stream: 测试管道。:param token: 原有界预算。:returns: 原协议帧，不以连接成功代替命令回执。</summary>
    private static async Task<IpcFrameDto> ReadAsync(Stream stream, CancellationToken token) =>
        JsonSerializer.Deserialize<IpcFrameDto>(await IpcFrameCodec.ReadPayloadAsync(stream, TimeSpan.FromSeconds(5), token))!;

    /// <summary>:param stream: 测试管道。:param request: 原请求。:param type: 响应类型。:param body: 合同内容。:param token: 有界预算。</summary>
    private static async Task ReplyAsync<T>(Stream stream, IpcFrameDto request, string type, T body, CancellationToken token) =>
        await IpcFrameCodec.WritePayloadAsync(stream, JsonSerializer.SerializeToUtf8Bytes(new IpcFrameDto
        {
            MessageType = type, MessageId = Guid.NewGuid(), CorrelationId = request.MessageId,
            OwnerEpoch = 7, Target = request.Target, Payload = JsonSerializer.SerializeToElement(body),
        }), token);
}
