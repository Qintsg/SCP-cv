// 通过已认证的当前 Worker 管道通知协作退出；失联成员仍由 Supervisor 按 PID/启动时间兜底。
using System.Text.Json;
using System.Collections.Concurrent;
using ScpCv.Contracts.Ipc;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    private sealed record StopPreparation(RuntimeConnection Connection, TaskCompletionSource Completion);
    private readonly ConcurrentDictionary<Guid, StopPreparation> _stopPreparations = new();

    public async Task NotifyRuntimeShutdownAsync(string reason, CancellationToken cancellationToken = default)
    {
        // Worker可先于Supervisor的两秒心跳退出；必须先确认监控者已立停机围栏，再通知成员。
        if (_connections.TryGetValue("supervisor", out var supervisor))
            await PrepareSupervisorStopAsync(supervisor, reason, cancellationToken).ConfigureAwait(false);
        var connections = _connections.ToArray()
            .Where(item => RequiredRuntimeRoles.Contains(item.Key, StringComparer.Ordinal) || item.Key == "office");
        foreach (var (role, connection) in connections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await connection.SendAsync(new IpcFrameDto
                {
                    MessageType = "shutdown_request",
                    MessageId = Guid.NewGuid(),
                    OwnerEpoch = connection.OwnerEpoch,
                    Payload = JsonSerializer.SerializeToElement(new { reason, group_epoch = connection.GroupEpoch }),
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                LogShutdownNoticeFailed(logger, role, exception);
                connection.RequestRetirement();
                RemoveReady(connection);
            }
        }
    }

    /// <summary>:param supervisor: 当前认证连接。:param reason: 停机原因。:param token: 上层预算。:returns: 有界确认；缺确认不通知Worker、不假称已准备。</summary>
    private async Task PrepareSupervisorStopAsync(RuntimeConnection supervisor, string reason, CancellationToken token)
    {
        var id = Guid.NewGuid(); var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparation = new StopPreparation(supervisor, completion);
        if (!_stopPreparations.TryAdd(id, preparation)) throw new InvalidOperationException("停机准备身份冲突。");
        try
        {
            await supervisor.SendAsync(new IpcFrameDto
            {
                MessageType = "shutdown_request", MessageId = Guid.NewGuid(), OwnerEpoch = supervisor.OwnerEpoch,
                Payload = JsonSerializer.SerializeToElement(new { reason, group_epoch = supervisor.GroupEpoch, shutdown_id = id }),
            }, token).ConfigureAwait(false);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
        }
        finally { _stopPreparations.TryRemove(new KeyValuePair<Guid, StopPreparation>(id, preparation)); }
    }

    /// <summary>:param connection: 已认证当前连接。:param frame: nonce确认。:returns: 只有原Supervisor/Owner与本次nonce可放行成员通知。</summary>
    private IpcFrameDto AcceptSupervisorStop(RuntimeConnection connection, IpcFrameDto frame)
    {
        var accepted = connection.Identity.Role == "supervisor" && frame.OwnerEpoch == connection.OwnerEpoch &&
            frame.Target is null && frame.Payload.ValueKind == JsonValueKind.Object &&
            frame.Payload.TryGetProperty("shutdown_id", out var value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) &&
            _stopPreparations.TryGetValue(id, out var pending) && ReferenceEquals(pending.Connection, connection) && pending.Completion.TrySetResult();
        return Response(frame, "shutdown_prepared_accepted", new { accepted });
    }

    [LoggerMessage(EventId = 2210, Level = LogLevel.Warning, Message = "Worker {Role} 协作退出通知失败，保留 Supervisor 定向兜底")]
    private static partial void LogShutdownNoticeFailed(ILogger logger, string role, Exception exception);
}
