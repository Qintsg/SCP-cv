// VLC 原生停止在后台等待，完成后回到调用线程执行重播或 WPF 资源释放。
namespace ScpCv.PlayerWorker.Playback;

/// <summary>保持播放器 UI 消息泵可用的 VLC 生命周期时序；不创建或持有原生对象。</summary>
public static class VlcResourceLifecycle
{
    /// <summary>在线程池等待同步的原生 Stop，不在 VLC 回调或 UI 线程上阻塞。</summary>
    /// <param name="stop">当前自有播放器的同步停止操作。</param>
    /// <returns>原生停止已经完成的任务；原始异常不会被吞掉。</returns>
    public static Task StopAsync(Action stop)
    {
        ArgumentNullException.ThrowIfNull(stop);
        return Task.Run(stop, CancellationToken.None);
    }

    /// <summary>停止完成后按调用上下文重置位置并重播，原生 Play 的失败保持为 false。</summary>
    /// <param name="stop">当前自有播放器的同步停止操作。</param>
    /// <param name="replay">使用当前媒体重播并返回真实结果。</param>
    /// <param name="resetPosition">同源重开时的位置重置；循环重播无需此操作。</param>
    /// <returns>重播是否被原生播放器接受。</returns>
    public static async Task<bool> ReplayAsync(Action stop, Func<bool> replay, Action? resetPosition = null)
    {
        ArgumentNullException.ThrowIfNull(replay);
        await StopAsync(stop);
        resetPosition?.Invoke();
        return replay();
    }

    /// <summary>先隔离结束回调，再等待停止，最后在调用上下文释放视图、媒体和播放器。</summary>
    /// <param name="unsubscribe">解除当前媒体结束事件。</param>
    /// <param name="stop">当前自有播放器的同步停止操作。</param>
    /// <param name="detachView">清空视图与播放器绑定。</param>
    /// <param name="disposeView">释放仍由 UI 线程拥有的视图。</param>
    /// <param name="disposeMedia">释放当前媒体对象。</param>
    /// <param name="disposePlayer">释放当前播放器；Worker 的共享 LibVLC 不在此释放。</param>
    /// <returns>本次资源已按顺序释放的异步结果。</returns>
    public static async ValueTask ReleaseAsync(
        Action unsubscribe,
        Action stop,
        Action detachView,
        Action disposeView,
        Action disposeMedia,
        Action disposePlayer)
    {
        ArgumentNullException.ThrowIfNull(unsubscribe);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(detachView);
        ArgumentNullException.ThrowIfNull(disposeView);
        ArgumentNullException.ThrowIfNull(disposeMedia);
        ArgumentNullException.ThrowIfNull(disposePlayer);
        unsubscribe();
        await StopAsync(stop);
        detachView();
        disposeView();
        disposeMedia();
        disposePlayer();
    }
}
