// 文件描述：序列化实际媒体和有序窗口观测。项目：SCP-cv；文件：PlayerRuntimeHost.Snapshot.cs；作者：Qintsg；日期：2026-10-29。
using System.Text.Json;
using ScpCv.PlayerWorker.ScreenSharing;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>当前源身份、页码与实时播放状态使用同一快照，不只报告控制命令意图。</summary>
    private JsonElement Snapshot()
    {
        long? position = _current?.Kind is "libreoffice" or "screen_share" ? null : _current?.Native is VlcMediaPlayer player ? Math.Max(0, player.Time) : 0;
        long? duration = _current?.Kind is "libreoffice" or "screen_share" ? null : _current?.Native is VlcMediaPlayer mediaPlayer ? Math.Max(0, mediaPlayer.Length) : 0;
        var share = _current?.Native as IScreenShareSurface;
        var playback = share is null ? ReadVlcPlaybackState() : ReadScreenSharePlaybackState();
        return JsonSerializer.SerializeToElement(new
        {
            window_foreground = _window.WindowForeground,
            window_observation_sequence = checked(++_windowObservationSequence),
            source_generation = _generation,
            source_id = _sourceId == 0 ? (long?)null : _sourceId,
            playback_state = playback.State,
            playback_mode = _current?.Kind switch { null => "none", "pdf" => "pdf", "slide_images" => "slide_images", "libreoffice" => "libreoffice", _ => "" },
            adapter_kind = _current?.Kind ?? string.Empty,
            actual_fit_text = _current?.ActualFitText,
            share_id = share?.State.Identity.ShareId,
            actual_frames = share?.State.Frames ?? 0,
            actual_audio = share?.State.HasAudio,
            actual_share_volume = share?.State.Audio?.Volume,
            actual_share_muted = share?.State.Audio?.Muted,
            released_screen_shares = _releasedScreenShares.ToArray(),
            current_slide = _currentSlide,
            total_slides = _totalSlides,
            position_ms = position,
            duration_ms = duration,
            error_message = playback.Error,
        });
    }


}
