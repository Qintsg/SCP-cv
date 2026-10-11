// 共享过期、换源和原生回执接续；同窗口门禁排除尚未提交的OPEN，不无条件关闭后来内容。
namespace ScpCv.ControlHost.ScreenSharing;

public sealed partial class ScreenShareService
{
    /// <summary>:param cancellationToken: 宿主寿命。:returns: 逐目标条件清理；排队不当释放，异常由周期宿主报告。</summary>
    public async Task CleanupOnceAsync(CancellationToken cancellationToken = default)
    {
        processTracker?.Prune(id => registry.GetCleanupTarget(id) is not null);
        // 重启后cap/SDP已丢失，只按原ID/代次清除孤立意图；不重播或重建授权。
        foreach (var windowId in new[] { 1, 2 })
        {
            var gate = Gate(windowId);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var desired = await runtime.GetDesiredScreenShareAsync(windowId, cancellationToken).ConfigureAwait(false);
                if (desired is not null && !registry.OwnsDesiredShare(desired))
                    await runtime.CloseScreenShareAsync(windowId, desired.ShareId, desired.SourceGeneration, cancellationToken).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        foreach (var pending in registry.GetCleanupTargets())
        {
            var gate = Gate(pending.Target.WindowId);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = registry.GetCleanupTarget(pending.Share.Id);
                if (current is null) continue;
                if (processTracker?.HasConfirmedExit(current.Share.Id, current.Target) == true)
                    registry.ObserveProcessExit(current.Share.Id, current.Target);
                var observation = await runtime.ObserveScreenShareAsync(current.Target, current.Share.Id, current.Share.SourceGeneration, cancellationToken).ConfigureAwait(false);
                if (!observation.TargetCurrent) registry.BeginSystemStop(current.Share.Id, "player_unavailable");
                else if (current.Share.SourceGeneration is not null && !observation.StillDesired)
                    registry.BeginSystemStop(current.Share.Id, "source_replaced");
                current = registry.GetCleanupTarget(current.Share.Id);
                if (current?.Share.State != ScreenShareState.Stopping || current.Share.SourceGeneration is not { } generation) continue;
                // 同Service gate内且无提交OPEN，Worker不可能领取原接收授权；不能在OPEN事务仍在途时作此判断。
                if (!observation.OpenCommitted)
                {
                    ConfirmReleased(current, generation);
                    continue;
                }
                await QueueStopAsync(current.Share, cancellationToken).ConfigureAwait(false);
                if (current.NativeReleased) ConfirmReleased(current, generation);
            }
            finally { gate.Release(); }
        }
    }

    /// <summary>:param current: 同窗口门禁下已确证无接收可能或原资源实际释放的条目。:returns: 终态与内存投影一起撤销，不把离线/新Worker当释放。</summary>
    private void ConfirmReleased(ScreenShareCleanupTarget current, long generation)
    {
        if (registry.ConfirmClosed(current.Share.Id, current.Share.WindowId, generation))
            runtime.ReleaseScreenShareProjection(current.Target, current.Share.Id, generation);
    }
}
