// 共享专用音频命令在媒体锁内核不可变身份与原OPEN代次；迟到请求不能控制后来共享/VLC或推进源代次。
using System.IO;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:param lease: 已认证Worker领取的专用租约。:param token: 控制寿命。:returns: 原生属性成功确认后的快照，不将排队或请求值当实际结果。</summary>
    private async Task<WorkerExecutionResult> ExecuteScreenShareAudioAsync(CommandLeaseDto lease, CancellationToken token)
    {
        using var lifetime = ScreenShareCommandLifetime(lease, TimeSpan.FromSeconds(3), token);
        token = lifetime.Token;
        token.ThrowIfCancellationRequested();
        if (_current?.Native is not IScreenShareSurface share || lease.SourceGeneration <= 0 ||
            lease.SourceGeneration != _generation || lease.SourceGeneration != share.State.Identity.SourceGeneration ||
            lease.Args.Count != 4 || !lease.Args.TryGetValue("share_id", out var id) || id.ValueKind != JsonValueKind.String ||
            !id.TryGetGuid(out var parsed) || parsed != share.State.Identity.ShareId ||
            !lease.Args.TryGetValue("window_id", out var target) || target.ValueKind != JsonValueKind.Number ||
            !target.TryGetInt32(out var window) || window != _windowId || window != share.State.Identity.WindowId)
            throw new InvalidDataException("屏幕共享音频控制身份或代次不符。");
        if (!lease.Args.TryGetValue("volume", out var level) || level.ValueKind != JsonValueKind.Number ||
            !level.TryGetInt32(out var volume) || volume is < 0 or > 100 ||
            !lease.Args.TryGetValue("muted", out var muted) || muted.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("屏幕共享音频控制参数无效。");
        if (!share.State.IsPlaying) throw new InvalidOperationException("屏幕共享尚未出画或已结束。");
        share.State.InvalidateAudio();
        await share.ApplyAudioAsync(volume, muted.GetBoolean(), token);
        token.ThrowIfCancellationRequested();
        if (!share.State.IsPlaying || share.State.Audio is null)
            throw new IOException("屏幕共享音频控制未获得有效原生回执。");
        return new("completed", "ok", Snapshot());
    }
}
