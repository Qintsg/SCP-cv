// 文稿关闭的单次执行入口；调用方必须观察关闭完成，而不是把重复调用视为退出证明。
namespace ScpCv.Contracts.Presentations;

internal sealed class PresentationCloseCompletion
{
    private readonly object _gate = new();
    private Task? _completion;

    /// <summary>:param close: 当前资源的一次关闭操作。:returns: 所有调用共同等待的关闭终态，失败也不会重放。</summary>
    internal ValueTask RunAsync(Func<Task> close)
    {
        TaskCompletionSource? started = null;
        Task completion;
        lock (_gate)
        {
            if (_completion is null)
            {
                started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _completion = started.Task;
            }
            completion = _completion;
        }

        // 先发布同一个Task再启动外部关闭；同步抛错、并发或重入都不能绕过终态记忆。
        if (started is not null) _ = CompleteAsync(close, started);
        return new ValueTask(completion);
    }

    /// <summary>:param close: 唯一外部关闭操作。:param completion: 已发布的关闭完成槽。:returns: 把真实成功/失败写入一次终态。</summary>
    private static async Task CompleteAsync(Func<Task> close, TaskCompletionSource completion)
    {
        try
        {
            await close().ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
        catch (Exception error) { completion.TrySetException(error); }
    }
}
