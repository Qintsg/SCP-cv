// 最多两个自有共享的原Player退出观察；锁定已认证PID/start/session/role，离线、换组和取消不是死亡证明。
using Microsoft.EntityFrameworkCore;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.ScreenSharing;

public sealed partial class ScreenShareProcessTracker(
    IDbContextFactory<ControlDbContext> contextFactory,
    IRegisteredProcessRegistry processes,
    ILogger<ScreenShareProcessTracker> logger,
    IRuntimeProcessExitObserver? observer = null) : IDisposable
{
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, ExitWatch> _watches = [];
    private bool _disposed;

    /// <summary>:param id: 原共享。:param target: 已预检在线的原目标。:param token: 创建请求寿命。:returns: 只有持久身份与OS验证登记精确相同才启动观察，Simulation缺观察器时不造证明。</summary>
    internal async Task TrackAsync(Guid id, ScreenShareTargetIdentity target, CancellationToken token)
    {
        if (observer is null) return;
        await using var database = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
        var worker = await database.WorkerOwnerships.AsNoTracking().SingleOrDefaultAsync(item => item.TargetKind == CommandTargetKind.Display &&
            item.TargetId == target.WindowId && item.WorkerInstanceId == target.WorkerInstanceId && item.OwnerEpoch == target.OwnerEpoch, token).ConfigureAwait(false);
        if (worker is null || worker.ProcessId <= 0 || !processes.TryGet(worker.ProcessId, out var identity) || identity is null ||
            identity.InstanceId != target.WorkerInstanceId || identity.ProcessStartTime != worker.ProcessStartTime ||
            identity.LogonSessionId != worker.LogonSessionId || identity.Role != $"player-{target.WindowId}") return;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_watches.ContainsKey(id)) return;
            if (_watches.Count >= 2) throw new InvalidOperationException("原共享退出观察尚未收回。");
            var cancellation = new CancellationTokenSource();
            _watches.Add(id, new(target, cancellation, ObserveAsync(identity, cancellation.Token)));
        }
    }

    /// <summary>:param id: 原分享。:param target: 原Worker/owner/group身份。:returns: 只有观察真正完成且明确confirmed，不把取消、异常或仍在途Task当作退出。</summary>
    internal bool HasConfirmedExit(Guid id, ScreenShareTargetIdentity target)
    {
        lock (_sync)
            return _watches.TryGetValue(id, out var watch) && watch.Target == target && !watch.Cancellation.IsCancellationRequested &&
                watch.Completion.IsCompletedSuccessfully && watch.Completion.Result.Confirmed;
    }

    /// <summary>:param isActive: 在观察锁内重核Registry，不使用可能遗漏并发另一窗口新预约的旧快照。:returns: 终态观察立即取消并回收，不累计历史PID句柄。</summary>
    internal void Prune(Func<Guid, bool> isActive)
    {
        ExitWatch[] removed;
        lock (_sync)
        {
            var ids = _watches.Keys.Where(id => !isActive(id)).ToArray();
            removed = ids.Select(id => _watches[id]).ToArray();
            foreach (var id in ids) _watches.Remove(id);
        }
        foreach (var watch in removed) { watch.Cancellation.Cancel(); watch.Cancellation.Dispose(); }
    }

    /// <summary>:param token: 宿主停机预算。:returns: 取消自有观察并等待真正收尾；预算取消不能返回正常清理。</summary>
    internal async Task StopAsync(CancellationToken token)
    {
        ExitWatch[] pending;
        lock (_sync) { _disposed = true; pending = _watches.Values.ToArray(); _watches.Clear(); }
        foreach (var watch in pending) watch.Cancellation.Cancel();
        try
        {
            await Task.WhenAll(pending.Select(item => item.Completion)).WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        finally { foreach (var watch in pending) watch.Cancellation.Dispose(); }
    }

    /// <summary>:param identity: 原生登记事实。:param token: 观察寿命。:returns: 只读死亡证据，所有失败都保留未确认；日志只写固定类型。</summary>
    private async Task<RuntimeProcessExitEvidence> ObserveAsync(RegisteredProcessIdentity identity, CancellationToken token)
    {
        try
        {
            var evidence = await observer!.WaitForExitAsync(identity, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return evidence;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return new(false, "observation_cancelled"); }
        catch (Exception error) { LogObservationFailure(logger, error.GetType().Name); return new(false, "observation_failed"); }
    }

    /// <summary>:returns: 容器同步释放时也取消观察；正常宿主停止先经StopAsync等待收尾。</summary>
    public void Dispose()
    {
        ExitWatch[] pending;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            pending = _watches.Values.ToArray(); _watches.Clear();
        }
        foreach (var watch in pending) { watch.Cancellation.Cancel(); watch.Cancellation.Dispose(); }
    }

    private sealed record ExitWatch(ScreenShareTargetIdentity Target, CancellationTokenSource Cancellation, Task<RuntimeProcessExitEvidence> Completion);

    [LoggerMessage(4302, LogLevel.Warning, "共享原Player退出观察未确认（{ErrorType}）；保留停止状态。")]
    private static partial void LogObservationFailure(ILogger logger, string errorType);
}
