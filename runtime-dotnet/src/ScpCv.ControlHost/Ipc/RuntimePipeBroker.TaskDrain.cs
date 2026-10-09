// Broker自有任务的真实完成门禁：登记先于运行，已取消的停止等待不擦除仍在展开的任务。
using System.Runtime.ExceptionServices;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    private readonly BrokerTaskDrain _connectionHandlers = new();

    /// <summary>隐藏并发登记和展开细节；调用方仍只需要公开StopAsync，不暴露测试接口。</summary>
    private sealed class BrokerTaskDrain
    {
        private readonly object _sync = new();
        private readonly HashSet<Task> _pending = [];
        private ExceptionDispatchInfo? _failure;

        /// <summary>:param work: 自有工作与全部finally清理。:returns: 无；终态由DrainAsync收集。</summary>
        public void Run(Func<Task> work)
        {
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_sync) _pending.Add(completed.Task);
            _ = CompleteAsync(work, completed);
        }

        /// <summary>:param cancellationToken: 只取消本次等待。:returns: 全部自有任务已展开，或明确失败。</summary>
        public async Task DrainAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Task[] pending;
                lock (_sync)
                {
                    if (_pending.Count == 0)
                    {
                        _failure?.Throw();
                        return;
                    }
                    pending = [.. _pending];
                }
                await Task.WhenAll(pending).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>完成信号不早于资源释放；失败保存至drain，不产生不可观察的faulted任务。</summary>
        private async Task CompleteAsync(Func<Task> work, TaskCompletionSource completed)
        {
            try { await work().ConfigureAwait(false); }
            catch (Exception exception)
            {
                lock (_sync) _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                lock (_sync)
                {
                    _pending.Remove(completed.Task);
                    completed.TrySetResult();
                }
            }
        }
    }
}
