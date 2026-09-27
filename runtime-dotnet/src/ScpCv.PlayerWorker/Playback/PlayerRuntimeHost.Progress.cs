// 视频与直播播放的进度样本只在播放器 UI 线程读取，并由既有管道会话上报。
using System.Windows.Threading;
using LibVLCSharp.Shared;
using ScpCv.Contracts.Runtime;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>只对活动 VLC 表面读取真实进度；其它媒体不制造虚假的时间轴。</summary>
    public async Task<WorkerStateSample?> SampleProgressAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0) return null;
        var operation = _window.Dispatcher.InvokeAsync(
            () => _current?.Native is MediaPlayer && _generation > 0
                ? new WorkerStateSample(_generation, Snapshot())
                : null,
            DispatcherPriority.Background,
            cancellationToken);
        return await operation.Task.ConfigureAwait(false);
    }
}
