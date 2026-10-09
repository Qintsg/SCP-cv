// 同角色替换串行等待旧连接完整退休；Ready归属到连接对象，不只看可重用的worker instance。
using System.Collections.Concurrent;
using ScpCv.Contracts.Ipc;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _roleInstallGates = new(StringComparer.Ordinal);

    /// <summary>:param connection: 已验证身份的新连接。:param token: broker寿命。:returns: 旧owner全部展开后才发布新连接。</summary>
    private async Task InstallConnectionAsync(RuntimeConnection connection, CancellationToken token)
    {
        var role = connection.Identity.Role;
        var gate = _roleInstallGates.GetOrAdd(role, _ => new SemaphoreSlim(1, 1));
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(IpcProtocol.HandshakeTimeout);
        var entered = false;
        try
        {
            await gate.WaitAsync(budget.Token).ConfigureAwait(false);
            entered = true;
            if (_connections.TryGetValue(role, out var old))
            {
                RemoveReady(old);
                await old.DisposeAsync().AsTask().WaitAsync(budget.Token).ConfigureAwait(false);
            }
            budget.Token.ThrowIfCancellationRequested();
            _connections[role] = connection;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TimeoutException("旧连接未在既有握手预算内退休；未发布新连接或假报就绪。"); }
        finally { if (entered) gate.Release(); }
    }

    /// <summary>:param connection: 要移除的确切传输owner。:returns: 旧finally/Wake失败不能移除同instance新连接的Ready。</summary>
    private void RemoveReady(RuntimeConnection connection)
    {
        TaskCompletionSource? changed = null;
        lock (_readinessSync)
        {
            if (_readyWorkers.TryGetValue(connection.Identity.Role, out var existing) &&
                ReferenceEquals(existing.Connection, connection))
            {
                _readyWorkers.Remove(connection.Identity.Role);
                changed = _readinessChanged;
                _readinessChanged = NewReadinessSignal();
            }
        }
        changed?.TrySetResult();
    }
}
