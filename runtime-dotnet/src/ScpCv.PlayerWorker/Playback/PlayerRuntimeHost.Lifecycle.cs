// 宿主退出共享真实关闭终态；资源确认结束前不清当前画面，失败不因重复调用而变成成功。
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using ScpCv.Contracts.Presentations;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    private readonly PresentationCloseCompletion _close = new();

    /// <summary>:param candidate: 尚未发布的自有候选。:param retirement: 旧资源关闭失败。:returns: 清理成功由原catch保持原异常；双失败保留两原因且明确不确定。</summary>
    private async Task DiscardCandidateAfterRetirementFailureAsync(SurfaceResource candidate, Exception retirement)
    {
        try { await candidate.DisposeAsync(); }
        catch (Exception cleanup)
        {
            _unconfirmedCandidates.Add(candidate);
            // 两次外部关闭均已开始，不能让最后一个一般IO异常把它们降为可安全重试的失败。
            throw new PresentationTransportException("旧播放资源关闭失败，候选资源清理也未确认；不能安全重试。",
                new AggregateException(retirement, cleanup));
        }
    }

    /// <summary>:returns: 所有退出调用共同等待同一关闭结果，保持WPF所属Dispatcher，不重放外部关闭。</summary>
    public ValueTask DisposeAsync() => _close.RunAsync(DisposeOnOwnedDispatcherAsync);

    /// <summary>:returns: 在原UI线程执行一次退出；拒绝新命令但不提前宣布资源已经关闭。</summary>
    private async Task DisposeOnOwnedDispatcherAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        if (_window.Dispatcher.CheckAccess()) await DisposeCoreAsync();
        else await _window.Dispatcher.InvokeAsync(DisposeCoreAsync).Task.Unwrap();
    }

    /// <summary>:returns: 尝试关闭每个自有资源；完整确认后才撤画面和所有权，失败原样保留。</summary>
    private async Task DisposeCoreAsync()
    {
        await _mediaGate.WaitAsync();
        try
        {
            var resources = _warmWebResources.Values.Concat(_unconfirmedCandidates).Append(_current).OfType<SurfaceResource>().Distinct().ToArray();
            List<Exception>? failures = null;
            foreach (var resource in resources)
            {
                try { await resource.DisposeAsync(); }
                catch (Exception error) { (failures ??= []).Add(error); }
            }
            if (failures is { Count: 1 }) ExceptionDispatchInfo.Throw(failures[0]);
            if (failures is { Count: > 1 }) throw new AggregateException("多个自有播放资源退出未确认。", failures);
            DisposeVlcInstance();
            _window.SetSurface(new Grid { Background = System.Windows.Media.Brushes.Black });
            _current = null;
            _warmWebResources.Clear();
            _unconfirmedCandidates.Clear();
        }
        finally
        {
            _mediaGate.Release();
            _mediaGate.Dispose();
        }
    }
}
