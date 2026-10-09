// 应用释放共享真实终态；一个自有对象失败仍尝试其余收尾，不重放、不擦除首次失败。
using System.Runtime.ExceptionServices;
using ScpCv.Contracts.Presentations;

namespace ScpCv.PlayerWorker;

public partial class App
{
    private readonly PresentationCloseCompletion _disposeCompletion = new();

    /// <summary>:returns: 并发与重复调用共同等待第一次释放的真实成功/失败，不把disposed标志当退出证明。</summary>
    public async ValueTask DisposeAsync()
    {
        await _disposeCompletion.RunAsync(DisposeOwnedRuntimeAsync);
        GC.SuppressFinalize(this);
    }

    /// <summary>:returns: 原调用线程/Dispatcher上的单次协作释放，所有错误保留供非零退出与诊断。</summary>
    private async Task DisposeOwnedRuntimeAsync()
    {
        List<Exception>? failures = null;
        try { _stop?.Cancel(); }
        catch (Exception error) { (failures ??= []).Add(error); }
        var owned = new IAsyncDisposable?[] { _session, _runtime };
        foreach (var resource in owned.OfType<IAsyncDisposable>())
        {
            try { await resource.DisposeAsync(); }
            catch (Exception error) { (failures ??= []).Add(error); }
        }
        try { _stop?.Dispose(); }
        catch (Exception error) { (failures ??= []).Add(error); }
        finally { _stop = null; }
        if (failures is { Count: 1 }) ExceptionDispatchInfo.Throw(failures[0]);
        if (failures is { Count: > 1 }) throw new AggregateException("播放器多个自有对象退出未确认。", failures);
    }
}
