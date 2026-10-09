// 将墙面下发及状态提交作为单个主机操作协调，避免预案与手动布局提交交错。
namespace ScpCv.Infrastructure.VideoWall;

public sealed class VideoWallOperationGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_gate);
    }

    public void Dispose() => _gate.Dispose();

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.Release();
        }
    }
}
