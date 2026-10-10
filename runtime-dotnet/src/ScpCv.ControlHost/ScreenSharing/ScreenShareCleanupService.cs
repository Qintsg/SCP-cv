// 共享清理周期不依赖发布者轮询；只排原身份条件CLOSE，不自动采集/播放，不记录信令或外部异常内容。
namespace ScpCv.ControlHost.ScreenSharing;

public sealed partial class ScreenShareCleanupService(ScreenShareService shares, ILogger<ScreenShareCleanupService> logger, ScreenShareProcessTracker processTracker) : BackgroundService
{
    /// <summary>:param stoppingToken: 宿主停止。:returns: 一秒周期；停止只取消调度，不把预算取消当实际释放。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try { await shares.CleanupOnceAsync(stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error) { LogCycleFailure(logger, error.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>:param cancellationToken: 宿主停止预算。:returns: 调度与原Player观察都真正收尾，取消不等于释放。</summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (ExecuteTask is not null) await ExecuteTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        await processTracker.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(4301, LogLevel.Warning, "共享清理未确认（{ErrorType}）；保留停止状态，下轮仅重新核对原身份。")]
    private static partial void LogCycleFailure(ILogger logger, string errorType);
}
