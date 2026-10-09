// 管道连接只管理传输寿命；原生进程退出观察不随连接断开而取消。
using System.IO.Pipes;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Domain.Model;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    private sealed class RuntimeConnection(
        NamedPipeServerStream stream,
        RegisteredProcessIdentity identity,
        (CommandTargetKind Kind, int Id)? target,
        long ownerEpoch,
        long groupEpoch,
        CancellationToken stoppingToken) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly object _lifecycleSync = new();
        private readonly CancellationTokenSource _lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        private readonly BrokerTaskDrain _dispatches = new();
        private readonly TaskCompletionSource _retirementRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _retired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? _sendIdle;
        private int _activeSends;
        private int _closing;
        private int _finishing;
        public RegisteredProcessIdentity Identity { get; } = identity;
        public (CommandTargetKind Kind, int Id)? Target { get; } = target;
        public long OwnerEpoch { get; } = ownerEpoch;
        public long GroupEpoch { get; } = groupEpoch;
        public CancellationToken Lifetime => _lifetime.Token;
        public bool IsRetiring => Volatile.Read(ref _closing) != 0;

        /// <summary>:param frame: 当前连接帧。:param cancellationToken: caller寿命。:returns: gate归还后的发送终态；退休先取消全部waiter。</summary>
        public async Task SendAsync(IpcFrameDto frame, CancellationToken cancellationToken)
        {
            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_closing != 0, this);
                if (_activeSends++ == 0) _sendIdle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            var entered = false;
            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
                try
                {
                    await _sendGate.WaitAsync(budget.Token).ConfigureAwait(false);
                    entered = true;
                    await IpcFrameCodec.WritePayloadAsync(stream, JsonSerializer.SerializeToUtf8Bytes(frame), budget.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                { throw new ObjectDisposedException(nameof(RuntimeConnection), "连接正在退休，未重放发送。"); }
            }
            finally
            {
                if (entered) _sendGate.Release();
                lock (_lifecycleSync)
                {
                    if (--_activeSends == 0) _sendIdle!.TrySetResult();
                }
            }
        }

        /// <summary>:param work: 当前reader已接收的分派。:returns: 登记先于运行；退休之后不再接纳旧分派。</summary>
        public void Dispatch(Func<Task> work)
        {
            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_closing != 0, this);
                _dispatches.Run(work);
            }
        }

        /// <summary>:returns: 发起一次退休并等待owner归还全部分派/发送；不得由reader自身等待自身终态。</summary>
        public ValueTask DisposeAsync()
        {
            RequestRetirement();
            return new ValueTask(_retired.Task);
        }

        /// <summary>:returns: 只发起本连接退休，不在自身分派内等待自己；slot由owner完整展开后摘除。</summary>
        public void RequestRetirement() => _ = RequestRetirementCore();

        /// <summary>:param detach: owner的精确连接/Ready摘除。:returns: 只有reader的finally负责一次完整释放及终态。</summary>
        public Task CompleteRetirementAsync(Action detach)
        {
            if (Interlocked.Exchange(ref _finishing, 1) == 0) _ = FinishRetirementAsync(detach);
            return _retired.Task;
        }

        /// <summary>:returns: 只阻断本连接IO，使reader及gate等待者展开；此处不销毁仍在使用的gate。</summary>
        private Task RequestRetirementCore()
        {
            var start = false;
            lock (_lifecycleSync)
            {
                if (_closing == 0) { _closing = 1; start = true; }
            }
            if (start)
            {
                try
                {
                    try { _lifetime.Cancel(); }
                    finally { stream.Dispose(); }
                    _retirementRequested.TrySetResult();
                }
                catch (Exception error) { _retirementRequested.TrySetException(error); }
            }
            return _retirementRequested.Task;
        }

        /// <summary>:param detach: 精确摘除回调。:returns: 最终gate释放晚于所有分派与发送，失败也缓存且不重放。</summary>
        private async Task FinishRetirementAsync(Action detach)
        {
            try
            {
                try { await RequestRetirementCore().ConfigureAwait(false); }
                finally
                {
                    try { await _dispatches.DrainAsync(CancellationToken.None).ConfigureAwait(false); }
                    finally
                    {
                        try
                        {
                            Task sends;
                            lock (_lifecycleSync) sends = _sendIdle?.Task ?? Task.CompletedTask;
                            await sends.ConfigureAwait(false);
                            await stream.DisposeAsync().ConfigureAwait(false);
                            _sendGate.Dispose();
                            _lifetime.Dispose();
                        }
                        finally { detach(); }
                    }
                }
                _retired.TrySetResult();
            }
            catch (Exception error) { _retired.TrySetException(error); }
        }
    }
}
