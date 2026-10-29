// 文件描述：在UI线程采样实际媒体及窗口顺序。项目：SCP-cv；文件：PlayerRuntimeHost.Progress.cs；作者：Qintsg；日期：2026-10-29。
using System.Windows.Threading;
using System.IO;
using LibVLCSharp.Shared;
using ScpCv.Contracts.Runtime;
using ScpCv.PlayerWorker.Presentations;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:param cancellationToken: Worker会话寿命。:returns: 当前源的真实观测，忙时不排队旧采样。</summary>
    public async Task<WorkerStateSample?> SampleProgressAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0 || _mediaGate.CurrentCount == 0) return null;
        var operation = _window.Dispatcher.InvokeAsync(() => SampleCoreAsync(cancellationToken),
            DispatcherPriority.Background, cancellationToken);
        return await operation.Task.Unwrap().ConfigureAwait(false);
    }

    private async Task<WorkerStateSample?> SampleCoreAsync(CancellationToken token)
    {
        // 公共入口之后可能已完成退出；晚到Dispatcher采样不能访问已释放的gate。
        if (Volatile.Read(ref _disposed) != 0) return null;
        if (!await _mediaGate.WaitAsync(0, token)) return null;
        try
        {
            if (Volatile.Read(ref _disposed) != 0) return null;
            if (_generation <= 0 && _releasedScreenShares.Count == 0 && _window.NativeHandle == 0) return null;
            if (_unconfirmedCandidates.Count > 0)
                return _window.NativeHandle != 0 ? new WorkerStateSample(_generation, WindowSnapshot()) : null;
            if (_current?.Native is IPresentationSurface native)
            {
                try
                {
                    await native.ObserveAsync(token);
                    _currentSlide = native.CurrentSlide; _totalSlides = native.TotalSlides;
                }
                catch (Exception error) when (error is IOException or InvalidOperationException)
                {
                    // 不抹去实际源占用；不能把断管或观测失败当成正常关闭。
                    _state = "error"; _errorMessage = error.Message;
                }
                return new WorkerStateSample(_generation, Snapshot());
            }
            // 空闲/旧图片也重复携带有界释放回执，避免一次结果回报丢失后永久占用服务端槽。
            return _window.NativeHandle != 0 || _current?.Native is MediaPlayer or IScreenShareSurface || _releasedScreenShares.Count > 0
                ? new WorkerStateSample(_generation, Snapshot()) : null;
        }
        finally { _mediaGate.Release(); }
    }
}
