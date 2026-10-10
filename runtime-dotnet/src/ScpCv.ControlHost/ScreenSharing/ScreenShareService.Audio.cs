// 本人共享音频：与停止共用窗口锁，GET只合并原Worker已提交的实际观测，不写请求值或自动重试。
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.ScreenSharing;

public sealed partial class ScreenShareService
{
    /// <summary>:param ownerId: 当前登录身份。:param id: 原分享。:returns: 非秘密快照，实际音量缺证据时为null。</summary>
    public async Task<ScreenShareSnapshot> GetOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken = default)
    {
        var share = registry.GetOwned(ownerId, id);
        var session = await runtime.GetSessionAsync(share.WindowId, cancellationToken).ConfigureAwait(false);
        share = registry.GetOwned(ownerId, id);
        var actual = share.State == ScreenShareState.Receiving && session.PlayerOnline &&
            session.ShareId == id && session.ActualShareId == id && session.ActualFrames > 0 &&
            session.DesiredGeneration == share.SourceGeneration && session.ObservedGeneration == share.SourceGeneration &&
            session.PlaybackState == "playing" && !session.CleanupPending;
        return share with { ActualVolume = actual ? session.ActualShareVolume : null, ActualMuted = actual ? session.ActualShareMuted : null };
    }

    /// <summary>:param volume: 整数0..100。:param muted: 明确静音意图。:returns: 原代次专用命令受理；实际结果须另行查询。</summary>
    public async Task<ScreenShareAudioCommandReceipt> SetAudioAsync(string ownerId, Guid id, int volume, bool muted,
        CancellationToken cancellationToken = default)
    {
        var initial = registry.GetOwned(ownerId, id);
        var gate = Gate(initial.WindowId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var share = registry.GetOwned(ownerId, id);
            if (share.State != ScreenShareState.Receiving || share.SourceGeneration is not { } generation)
                throw new ScreenShareException("分享尚未实际接收或正在结束。", "share_audio_unavailable", 409);
            return await runtime.SetScreenShareAudioAsync(registry.GetTargetOwned(ownerId, id), id, generation,
                volume, muted, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}
