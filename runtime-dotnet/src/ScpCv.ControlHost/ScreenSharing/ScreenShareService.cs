// 分享编排沿用持久命令，预约无播放副作用，停止先撤销授权。SCP-cv 2026-10-10。
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.ScreenSharing;

public sealed class ScreenShareService(ScreenShareRegistry registry, RuntimeStateService runtime) : IDisposable
{
    private readonly SemaphoreSlim[] _windowGates = [new(1, 1), new(1, 1)];

    public async Task<ScreenShareSnapshot> CreateAsync(string ownerId, int windowId, bool hasAudio, CancellationToken cancellationToken = default)
    {
        var gate = Gate(windowId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var target = await runtime.GetScreenShareTargetAsync(windowId, cancellationToken).ConfigureAwait(false);
            return registry.Create(ownerId, target, hasAudio);
        }
        finally { gate.Release(); }
    }

    /// <summary>原预约身份在SQLite事务中重核；绑定在提交前完成，异常不保留媒体授权。</summary>
    public async Task<ScreenShareSnapshot> OfferAsync(string ownerId, Guid id, string type, string sdp, CancellationToken cancellationToken = default)
    {
        var target = registry.GetTargetOwned(ownerId, id);
        var gate = Gate(target.WindowId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            registry.SetOffer(ownerId, id, type, sdp);
            try
            {
                await runtime.OpenScreenShareAsync(target, id,
                    generation => registry.BindReceiver(ownerId, id, generation), cancellationToken).ConfigureAwait(false);
                return registry.GetOwned(ownerId, id);
            }
            catch
            {
                // 即使异常发生在提交后Wake，也不允许保留可重新接收的cap；清理服务接续条件CLOSE。
                registry.BeginStop(ownerId, id, "open_failed");
                throw;
            }
        }
        finally { gate.Release(); }
    }

    /// <summary>返回stopping不等于原生资源已释放；条件CLOSE不会关闭后来换入的内容。</summary>
    public async Task<ScreenShareSnapshot> StopAsync(string ownerId, Guid id, CancellationToken cancellationToken = default)
    {
        var share = registry.GetOwned(ownerId, id);
        var gate = Gate(share.WindowId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopping = registry.BeginStop(ownerId, id, "publisher_stop");
            await QueueStopAsync(stopping, cancellationToken).ConfigureAwait(false);
            return registry.GetOwned(ownerId, id);
        }
        finally { gate.Release(); }
    }

    /// <summary>接收资源已失败时同样撤销cap并排条件CLOSE，HTTP受理不释放窗口预约。</summary>
    public async Task StopReceiverAsync(Guid id, int windowId, long generation, string token, CancellationToken cancellationToken = default)
    {
        var gate = Gate(windowId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await QueueStopAsync(registry.StopReceiver(id, windowId, generation, token), cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task QueueStopAsync(ScreenShareSnapshot share, CancellationToken cancellationToken)
    {
        if (share.State == ScreenShareState.Stopping && share.SourceGeneration is { } generation)
            await runtime.CloseScreenShareAsync(share.WindowId, share.Id, generation, cancellationToken).ConfigureAwait(false);
    }

    private SemaphoreSlim Gate(int windowId) => windowId is 1 or 2 ? _windowGates[windowId - 1]
        : throw new ScreenShareException("分享目标只能是窗口1或窗口2。", "share_window_invalid");

    public void Dispose()
    {
        foreach (var gate in _windowGates) gate.Dispose();
    }
}
