// 已认证 Supervisor 的进程观察独立于 pipe；宿主停机时取消并收回所有观察任务。
using System.Collections.Concurrent;
using ScpCv.Domain.Model;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    private readonly IRuntimeProcessExitObserver _processExitObserver = processExitObserver ?? new WindowsRuntimeProcessExitObserver();
    private readonly ConcurrentDictionary<(Guid Instance, long Epoch), Task> _supervisorExitWatches = new();
    /// <summary>只有已认证、已授权的 Supervisor 会被观察；同一组重连复用观察。</summary>
    private void ObserveSupervisorExit(RuntimeConnection connection, RuntimeGroupState state, CancellationToken stoppingToken)
    {
        if (connection.Identity.Role != "supervisor" || state is not (RuntimeGroupState.Starting or RuntimeGroupState.Armed)) return;
        var key = (connection.Identity.InstanceId, connection.GroupEpoch);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_supervisorExitWatches.TryAdd(key, completed.Task)) return;
        _ = ObserveSupervisorExitAsync(connection.Identity, connection.GroupEpoch, key, completed, stoppingToken);
    }

    /// <summary>确证原进程退出后持久化并发布故障；完整处理取消和观察异常。</summary>
    private async Task ObserveSupervisorExitAsync(RegisteredProcessIdentity identity, long groupEpoch,
        (Guid Instance, long Epoch) key, TaskCompletionSource completed, CancellationToken stoppingToken)
    {
        try
        {
            var evidence = await _processExitObserver.WaitForExitAsync(identity, stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            if (!evidence.Confirmed)
            {
                LogExitUnverifiable(logger, identity.ProcessId, groupEpoch, evidence.Reason);
                return;
            }
            await PersistConfirmedExitAsync(identity, groupEpoch, evidence, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) { LogExitObservationFailed(logger, identity.ProcessId, groupEpoch, exception); }
        finally
        {
            _supervisorExitWatches.TryRemove(key, out _);
            completed.TrySetResult();
        }
    }

    /// <summary>确证死亡证据不随暂时写库失败丢失；每次重试仍由原子代次门禁裁决。</summary>
    private async Task PersistConfirmedExitAsync(RegisteredProcessIdentity identity, long groupEpoch,
        RuntimeProcessExitEvidence evidence, CancellationToken stoppingToken)
    {
        var delayMilliseconds = 250;
        while (!stoppingToken.IsCancellationRequested)
        {
            bool faulted;
            try
            {
                faulted = await authority.TryFaultSupervisorExitAsync(groupEpoch, identity.ProcessId, identity.InstanceId, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                LogExitPersistenceRetry(logger, identity.ProcessId, groupEpoch, delayMilliseconds, exception);
                await Task.Delay(delayMilliseconds, stoppingToken).ConfigureAwait(false);
                delayMilliseconds = Math.Min(delayMilliseconds * 2, 5_000);
                continue;
            }
            if (faulted)
            {
                RemoveGroupReadiness(groupEpoch);
                dispatcher.PublishRuntimeFault();
                LogSupervisorExited(logger, identity.ProcessId, identity.ProcessStartTime, identity.LogonSessionId, groupEpoch, evidence.Reason);
            }
            else LogExitIgnored(logger, identity.ProcessId, groupEpoch);
            return;
        }
    }

    /// <summary>只移除故障旧组的 Ready 证据，避免迟到通知影响新组。</summary>
    private void RemoveGroupReadiness(long groupEpoch)
    {
        TaskCompletionSource changed;
        lock (_readinessSync)
        {
            foreach (var role in _readyWorkers.Where(pair => pair.Value.GroupEpoch == groupEpoch).Select(pair => pair.Key).ToArray())
                _readyWorkers.Remove(role);
            changed = _readinessChanged;
            _readinessChanged = NewReadinessSignal();
        }
        changed.TrySetResult();
    }

    /// <summary>宿主停止后等待原生观察释放句柄，不把取消误记为运行故障。</summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(_supervisorExitWatches.Values).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(2211, LogLevel.Warning, "无法证明 Supervisor PID {ProcessId} / epoch {GroupEpoch} 已退出：{Reason}")]
    private static partial void LogExitUnverifiable(ILogger logger, int processId, long groupEpoch, string reason);

    [LoggerMessage(2212, LogLevel.Error, "观察 Supervisor PID {ProcessId} / epoch {GroupEpoch} 退出失败")]
    private static partial void LogExitObservationFailed(ILogger logger, int processId, long groupEpoch, Exception exception);

    [LoggerMessage(2213, LogLevel.Warning, "确认 Supervisor PID {ProcessId} / start {StartTime} / session {SessionId} / epoch {GroupEpoch} 已退出（{Reason}），运行组标记 Faulted，未声称整组 Stopped")]
    private static partial void LogSupervisorExited(ILogger logger, int processId, DateTimeOffset startTime, int sessionId, long groupEpoch, string reason);

    [LoggerMessage(2214, LogLevel.Debug, "忽略 Supervisor PID {ProcessId} / epoch {GroupEpoch} 的迟到退出：该组不再获准运行")]
    private static partial void LogExitIgnored(ILogger logger, int processId, long groupEpoch);

    [LoggerMessage(2215, LogLevel.Error, "Supervisor PID {ProcessId} / epoch {GroupEpoch} 的确证退出尚未记入故障状态，保留证据并在 {DelayMilliseconds} ms 后重试")]
    private static partial void LogExitPersistenceRetry(ILogger logger, int processId, long groupEpoch, int delayMilliseconds, Exception exception);
}
