// 应用协作退出的单一接线点；等待资源释放并保留错误，实际终止由所属Dispatcher执行。
using ScpCv.Contracts.Presentations;

namespace ScpCv.PlayerWorker.Playback;

internal sealed class PlayerApplicationExit(Func<Task> dispose, Action<Exception> report, Func<int, Task> exit)
{
    private readonly PresentationCloseCompletion _completion = new();
    private int _requestedExitCode;
    internal Exception? Failure { get; private set; }

    /// <summary>:param exitCode: 调用方已有的故障码，正常为0。:returns: 同一次协作退出，重复调用不再次释放/终止。</summary>
    internal ValueTask RequestAsync(int exitCode = 0)
    {
        if (exitCode != 0) Interlocked.Exchange(ref _requestedExitCode, exitCode);
        return _completion.RunAsync(CompleteAsync);
    }

    /// <summary>:returns: 关闭失败保留原错误并明确非零退出，兜底或重复请求不冒充正常完成。</summary>
    private async Task CompleteAsync()
    {
        try
        {
            try { await dispose(); }
            catch (Exception error) { Failure = error; report(error); }
        }
        finally
        {
            // 即使日志输出自身失败，也不能把资源关闭失败变成退出码0。
            await exit(Failure is null ? Volatile.Read(ref _requestedExitCode) : 1);
        }
    }
}
