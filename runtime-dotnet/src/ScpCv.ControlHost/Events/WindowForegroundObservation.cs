// 文件描述：仅接受当前所有者的新窗口观测，独立于媒体代次。项目：SCP-cv；文件：WindowForegroundObservation.cs；作者：Qintsg；日期：2026-10-29。
using System.Text.Json;
using ScpCv.Domain.Model;

namespace ScpCv.ControlHost.Events;

internal static class WindowForegroundObservation
{
    /// <summary>:param session: 已通过Worker身份核验的会话。:param state: 实际窗口快照。:returns: 是否采用新观测。</summary>
    internal static bool Apply(PlaybackSession session, JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object || !state.TryGetProperty("window_foreground", out var foreground)
            || foreground.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !state.TryGetProperty("window_observation_sequence", out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var sequence)
            || sequence <= session.WindowObservationSequence) return false;
        session.WindowForeground = foreground.GetBoolean();
        session.WindowObservationSequence = sequence;
        return true;
    }
}
