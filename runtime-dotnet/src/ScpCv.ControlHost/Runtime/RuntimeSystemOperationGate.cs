// 单ControlHost内启停排他；忙时立即拒绝，不把旧请求排队到新代次后执行。
namespace ScpCv.ControlHost.Runtime;

/// <summary>只保护HTTP启停序列，不替代原生子进程身份、退出或超时证明。</summary>
public sealed class RuntimeSystemOperationGate
{
    private int _busy;

    /// <summary>
    /// :returns: 独占整个状态/外部IO序列的租约；已有操作时返回null且无副作用。
    /// </summary>
    internal OperationLease? TryEnter() =>
        Interlocked.CompareExchange(ref _busy, 1, 0) == 0 ? new OperationLease(this) : null;

    internal sealed class OperationLease(RuntimeSystemOperationGate owner) : IDisposable
    {
        private RuntimeSystemOperationGate? _owner = owner;

        /// <summary>:returns: 正常、错误及取消路径均释放；同租约重复释放不解锁后来的操作。</summary>
        public void Dispose()
        {
            var held = Interlocked.Exchange(ref _owner, null);
            if (held is not null) Volatile.Write(ref held._busy, 0);
        }
    }
}
