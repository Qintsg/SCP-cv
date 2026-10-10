// 动态共享独立OPEN/切入/采样；null媒体ID与cap仅内存，绝不进入普通网页保温缓存。
using System.Globalization;
using System.IO;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    internal Func<ScreenShareReceiverIdentity, CancellationToken, Task<IScreenShareSurface>>? ScreenShareFactory { get; init; }
    private readonly List<ScreenShareReleaseEvidenceDto> _releasedScreenShares = [];

    /// <summary>:param id: 实际释放的资源。:param generation: 原OPEN代次。:returns: 有界非秘密回执，重复Dispose不增加条目。</summary>
    private void RememberScreenShareRelease(Guid id, long generation)
    {
        if (_releasedScreenShares.Any(item => item.ShareId == id && item.SourceGeneration == generation)) return;
        _releasedScreenShares.Add(new() { ShareId = id, SourceGeneration = generation });
        if (_releasedScreenShares.Count > 32) _releasedScreenShares.RemoveAt(0);
    }

    /// <summary>:param lease: 原Worker认证后的租约。:param token: OPEN寿命。:returns: 已协商且已有真实预备视频帧的专用资源。</summary>
    private async Task<SurfaceResource> OpenScreenShareAsync(CommandLeaseDto lease, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Long(lease.Args, "source_id") != 0 || !Guid.TryParse(String(lease.Args, "share_id"), out var shareId))
            throw new InvalidDataException("屏幕共享动态源身份无效。");
        var identity = ScreenShareReceiverIdentity.Parse(String(lease.Args, "receiver_uri"), shareId, _windowId, lease.SourceGeneration);
        var native = await (ScreenShareFactory is { } factory ? factory(identity, token) : ScreenShareSurface.CreateAsync(_window, identity, token,
            () => RememberScreenShareRelease(shareId, lease.SourceGeneration)));
        Task? disposal = null;
        // 关闭失败保持同一失败Task，不重复原生副作用；只有完整Dispose成功才记录原身份。
        /// <summary>:returns: 原对象的实际关闭完成；请求租约身份不冒替原生对象自身身份。</summary>
        async Task DisposeAndRecordAsync()
        {
            await native.DisposeAsync();
            RememberScreenShareRelease(native.State.Identity.ShareId, native.State.Identity.SourceGeneration);
        }
        var resource = new SurfaceResource("screen_share", native.Surface, () => new(disposal ??= DisposeAndRecordAsync()), native);
        try
        {
            if (!ReferenceEquals(native.State.Identity, identity)) throw new InvalidDataException("屏幕共享资源与本次租约身份不符。");
            await native.State.WaitPreparedAsync(token);
            token.ThrowIfCancellationRequested();
            return resource;
        }
        catch (Exception preparation)
        {
            await DiscardCandidateAfterRetirementFailureAsync(resource, preparation);
            throw;
        }
    }

    /// <summary>:param lease: 共享OPEN。:param token: 上层取消。:returns: 共享所有阶段共用最多20秒且不超过租约deadline，不逐阶段延长预算。</summary>
    private static CancellationTokenSource ScreenShareOpenLifetime(CommandLeaseDto lease, CancellationToken token)
        => ScreenShareCommandLifetime(lease, TimeSpan.FromSeconds(20), token);

    /// <summary>:param lease: OPEN或专用控制。:param token: 上层取消。:param budget: 对应阶段上限。:returns: 有界寿命永不超过原租约deadline。</summary>
    private static CancellationTokenSource ScreenShareCommandLifetime(CommandLeaseDto lease, TimeSpan budget, CancellationToken token)
    {
        if (lease.Deadline is not null)
        {
            if (!DateTimeOffset.TryParse(lease.Deadline, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var deadline))
                throw new InvalidDataException("屏幕共享命令截止时间无效。");
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining < budget) budget = remaining;
        }
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        // CancelAfter(0)仍依赖定时器异步触发，不能用于已过期租约的同步拒绝。
        if (budget <= TimeSpan.Zero) lifetime.Cancel();
        else lifetime.CancelAfter(budget);
        return lifetime;
    }

    /// <summary>:param lease: 可能来自旧分享的CLOSE。:returns: 只关当前相同分享/原OPEN代次；旧终止不能关闭后来媒体。</summary>
    private bool IsObsoleteShareClose(CommandLeaseDto lease) => String(lease.Args, "source_type", "") == "screen_share" &&
        Long(lease.Args, "close_source_generation") > 0 && (_current?.Native is not IScreenShareSurface share ||
        _generation != Long(lease.Args, "close_source_generation") ||
        !Guid.TryParse(String(lease.Args, "share_id", ""), out var id) || id != share.State.Identity.ShareId);

    /// <summary>:returns: 只读取当前对象的不可变身份与观测；被退役实例的迟到消息不能进入此路径。</summary>
    private (string State, string Error) ReadScreenSharePlaybackState()
    {
        if (_current?.Native is not IScreenShareSurface share || share.State.Identity.SourceGeneration != _generation)
            return (_state, _errorMessage);
        return share.State.ErrorCode.Length > 0 ? ("error", "屏幕共享接收已结束：" + share.State.ErrorCode) :
            (share.State.IsPlaying ? "playing" : "loading", string.Empty);
    }
}
