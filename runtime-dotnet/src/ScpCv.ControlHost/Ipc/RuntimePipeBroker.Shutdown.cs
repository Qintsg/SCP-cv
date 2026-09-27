// 通过已认证的当前 Worker 管道通知协作退出；失联成员仍由 Supervisor 按 PID/启动时间兜底。
using System.Text.Json;
using ScpCv.Contracts.Ipc;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    public async Task NotifyRuntimeShutdownAsync(string reason, CancellationToken cancellationToken = default)
    {
        var connections = _connections.ToArray()
            .Where(item => RequiredRuntimeRoles.Contains(item.Key, StringComparer.Ordinal));
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
                _connections.TryRemove(new KeyValuePair<string, RuntimeConnection>(role, connection));
                RemoveReady(connection.Identity.Role, connection.Identity.InstanceId);
            }
        }
    }

    [LoggerMessage(EventId = 2210, Level = LogLevel.Warning, Message = "Worker {Role} 协作退出通知失败，保留 Supervisor 定向兜底")]
    private static partial void LogShutdownNoticeFailed(ILogger logger, string role, Exception exception);
}
