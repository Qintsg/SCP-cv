// 使用受控停止屏障验证生产 VLC 生命周期时序，不激活 VLC、窗口、Office 或音频设备。
using System.Collections.Concurrent;
using System.Windows.Threading;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class VlcResourceLifecycleTests
{
    [Fact]
    public async Task StopRunsOutsideDispatcherAndKeepsItsMessagesResponsive()
    {
        await OnDispatcherAsync(async () =>
        {
            var uiThread = Environment.CurrentManagedThreadId;
            var started = Completion();
            var release = Completion();
            var stopThread = 0;
            var stopUsedPool = false;
            var stopped = VlcResourceLifecycle.StopAsync(() =>
            {
                stopThread = Environment.CurrentManagedThreadId;
                stopUsedPool = Thread.CurrentThread.IsThreadPoolThread;
                started.SetResult();
                release.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            });
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var dispatcherMessage = false;
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => dispatcherMessage = true);
                Assert.True(dispatcherMessage);
                Assert.False(stopped.IsCompleted);
            }
            finally { release.TrySetResult(); }

            await stopped;
            Assert.NotEqual(uiThread, stopThread);
            Assert.True(stopUsedPool);
        });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task LoopAndSameSourceReplayWaitForStopAndPreserveNativeResult(bool resetPosition, bool playResult)
    {
        await OnDispatcherAsync(async () =>
        {
            var uiThread = Environment.CurrentManagedThreadId;
            var started = Completion();
            var release = Completion();
            var phases = new ConcurrentQueue<string>();
            var replay = VlcResourceLifecycle.ReplayAsync(
                () =>
                {
                    phases.Enqueue("stop-start");
                    started.SetResult();
                    release.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                    phases.Enqueue("stop-end");
                },
                () =>
                {
                    Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
                    phases.Enqueue("play");
                    return playResult;
                },
                resetPosition ? () =>
                {
                    Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
                    phases.Enqueue("reset-position");
                } : null);
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(replay.IsCompleted);
                Assert.Equal(["stop-start"], phases);
            }
            finally { release.TrySetResult(); }

            Assert.Equal(playResult, await replay);
            string[] expected = resetPosition
                ? ["stop-start", "stop-end", "reset-position", "play"]
                : ["stop-start", "stop-end", "play"];
            Assert.Equal(expected, phases);
        });
    }

    [Fact]
    public async Task ReleaseUnsubscribesFirstAndDisposesSurfaceBeforeMediaAndPlayer()
    {
        await OnDispatcherAsync(async () =>
        {
            var uiThread = Environment.CurrentManagedThreadId;
            var started = Completion();
            var release = Completion();
            var phases = new ConcurrentQueue<string>();
            void UiPhase(string phase)
            {
                Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
                phases.Enqueue(phase);
            }
            var disposal = VlcResourceLifecycle.ReleaseAsync(
                () => UiPhase("unsubscribe"),
                () =>
                {
                    Assert.NotEqual(uiThread, Environment.CurrentManagedThreadId);
                    phases.Enqueue("stop-start");
                    started.SetResult();
                    release.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                    phases.Enqueue("stop-end");
                },
                () => UiPhase("detach-view"),
                () => UiPhase("dispose-view"),
                () => UiPhase("dispose-media"),
                () => UiPhase("dispose-player"));
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(disposal.IsCompleted);
                Assert.Equal(["unsubscribe", "stop-start"], phases);
            }
            finally { release.TrySetResult(); }

            await disposal;
            Assert.Equal(
                ["unsubscribe", "stop-start", "stop-end", "detach-view", "dispose-view", "dispose-media", "dispose-player"],
                phases);
        });
    }

    [Fact]
    public async Task StopFailureDoesNotReplayOrClaimReleaseCompleted()
    {
        var failure = new InvalidOperationException("受控原生停止失败");
        var callbacks = new List<string>();
        void FailedStop() => throw failure;
        var replayError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VlcResourceLifecycle.ReplayAsync(FailedStop, () =>
            {
                callbacks.Add("play");
                return true;
            }, () => callbacks.Add("reset-position")));
        var releaseError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VlcResourceLifecycle.ReleaseAsync(
                () => callbacks.Add("unsubscribe"),
                FailedStop,
                () => callbacks.Add("detach-view"),
                () => callbacks.Add("dispose-view"),
                () => callbacks.Add("dispose-media"),
                () => callbacks.Add("dispose-player")).AsTask());

        Assert.Same(failure, replayError);
        Assert.Same(failure, releaseError);
        Assert.Equal(["unsubscribe"], callbacks);
    }

    [Fact]
    public async Task ReplayFailurePropagatesInsteadOfReturningSuccessfulPlayback()
    {
        var failure = new InvalidOperationException("受控原生重播失败");
        var phases = new ConcurrentQueue<string>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VlcResourceLifecycle.ReplayAsync(
                () => phases.Enqueue("stop"),
                () => throw failure,
                () => phases.Enqueue("reset-position")));

        Assert.Same(failure, error);
        Assert.Equal(["stop", "reset-position"], phases);
    }

    /// <summary>创建只用于事件顺序同步的屏障，避免内联延续混入原生 Stop 替身。</summary>
    /// <returns>异步延续的完成源。</returns>
    private static TaskCompletionSource Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>运行独立 STA Dispatcher 消息泵但不创建可见窗口或任何原生媒体对象。</summary>
    /// <param name="action">需要验证调用线程恢复的异步操作。</param>
    /// <returns>操作完成并且测试线程已经退出的任务。</returns>
    private static async Task OnDispatcherAsync(Func<Task> action)
    {
        var completion = Completion();
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "生命周期测试的自有 Dispatcher 线程未退出。");
        }
    }
}
