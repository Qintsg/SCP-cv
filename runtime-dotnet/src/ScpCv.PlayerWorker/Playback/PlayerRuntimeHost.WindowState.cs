// 文件描述：与媒体代次独立的窗口维护观测。项目：SCP-cv；文件：PlayerRuntimeHost.WindowState.cs；作者：Qintsg；日期：2026-10-29。
using System.Text.Json;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:returns: 当前UI线程的真实窗口顺序及独立观测序列，不投影媒体状态。</summary>
    private JsonElement WindowSnapshot() => JsonSerializer.SerializeToElement(new
    {
        window_foreground = _window.WindowForeground,
        window_observation_sequence = checked(++_windowObservationSequence),
        window_observation_only = true,
    });
}
