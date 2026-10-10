// 两个目标的临时共享播放投影：只留非秘密身份/原Worker/后帧，重启即清空，不进入业务数据库。SCP-cv 2026-10-11。
using System.Text.Json;
using ScpCv.Contracts.Http;

namespace ScpCv.Infrastructure.Playback;

public sealed class ScreenSharePlaybackProjection
{
    private readonly Lock _sync = new();
    private readonly Dictionary<int, Entry> _windows = [];

    /// <summary>
    /// :param target: OPEN事务重核的原Worker身份。
    /// :param id: 非秘密分享ID。
    /// :param generation: 本次OPEN分配代次；投影还会核对持久会话代次，回滚不授权展示。
    /// </summary>
    public void Bind(ScreenShareTargetIdentity target, Guid id, long generation)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.WindowId is not (1 or 2) || id == Guid.Empty || generation <= 0)
            throw new ArgumentException("临时共享投影身份无效。", nameof(target));
        lock (_sync) { _windows[target.WindowId] = new(target, id, generation, DateTimeOffset.MinValue, null); }
    }

    /// <summary>
    /// :param state: 已经通过持久Worker身份/源代次检查并提交的纯值报告。
    /// :param updatedAt: 同一持久会话单调递增的版本；异步回调倒序不能恢复旧资源。
    /// :returns: 没有有效分享身份时清除实际共享，不以adapter/playing独自推断帧。
    /// </summary>
    public void Observe(int windowId, Guid workerInstanceId, long ownerEpoch, long generation,
        JsonElement state, DateTimeOffset updatedAt)
    {
        lock (_sync)
        {
            if (!_windows.TryGetValue(windowId, out var entry) || generation < entry.Generation || updatedAt <= entry.UpdatedAt) return;
            Observation? actual = null;
            if (entry.Target.WorkerInstanceId == workerInstanceId && entry.Target.OwnerEpoch == ownerEpoch && generation == entry.Generation
                && state.ValueKind == JsonValueKind.Object
                && state.TryGetProperty("source_id", out var source) && source.ValueKind == JsonValueKind.Null
                && state.TryGetProperty("adapter_kind", out var adapter) && adapter.ValueKind == JsonValueKind.String && adapter.GetString() == "screen_share"
                && state.TryGetProperty("share_id", out var share) && share.ValueKind == JsonValueKind.String && share.TryGetGuid(out var id) && id == entry.Id
                && state.TryGetProperty("actual_frames", out var frames) && frames.ValueKind == JsonValueKind.Number && frames.TryGetInt64(out var count) && count >= 0
                && state.TryGetProperty("actual_audio", out var audio) && audio.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                var hasAudio = audio.GetBoolean();
                var receipt = ReadAudio(state, hasAudio);
                actual = new(Math.Max(entry.Actual?.Frames ?? 0, count), hasAudio, receipt.Volume, receipt.Muted);
            }
            _windows[windowId] = entry with { UpdatedAt = updatedAt, Actual = actual };
        }
    }

    /// <summary>
    /// :param session: 数据库生成的当前HTTP/SSE基线，不提供cap/SDP或媒体URI。
    /// :returns: 同窗口/源代次/适配器的非秘密临时身份；不修改源ID、不填文件时钟。
    /// </summary>
    public PlaybackSessionDto Project(PlaybackSessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_sync)
        {
            if (!_windows.TryGetValue(session.WindowId, out var entry)) return session;
            var desired = session.SourceId is null && session.DesiredGeneration == entry.Generation;
            var actual = session.ActualSourceId is null && session.ActualAdapterKind == "screen_share"
                && session.ObservedGeneration == entry.Generation ? entry.Actual : null;
            return session with
            {
                ShareId = desired ? entry.Id : null,
                SourceName = desired ? "屏幕共享" : session.SourceName,
                SourceType = desired ? "screen_share" : session.SourceType,
                SourceTypeLabel = desired ? "屏幕共享" : session.SourceTypeLabel,
                ActualShareId = actual is not null ? entry.Id : null,
                ActualFrames = actual?.Frames ?? 0,
                ActualAudio = actual?.Audio,
                ActualShareVolume = actual?.Volume,
                ActualShareMuted = actual?.Muted,
            };
        }
    }

    /// <summary>:param audio: 同一报告的活跃音轨事实。:returns: 严格成对的实际值；缺失、畸形或无音轨却非静音时保持未知。</summary>
    private static (int? Volume, bool? Muted) ReadAudio(JsonElement state, bool audio)
    {
        if (state.TryGetProperty("actual_share_volume", out var level) && level.ValueKind == JsonValueKind.Number &&
            level.TryGetInt32(out var volume) && volume is >= 0 and <= 100 &&
            state.TryGetProperty("actual_share_muted", out var silent) && silent.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            (audio || silent.GetBoolean())) return (volume, silent.GetBoolean());
        return (null, null);
    }

    private sealed record Observation(long Frames, bool Audio, int? Volume, bool? Muted);
    private sealed record Entry(ScreenShareTargetIdentity Target, Guid Id, long Generation,
        DateTimeOffset UpdatedAt, Observation? Actual);
}
