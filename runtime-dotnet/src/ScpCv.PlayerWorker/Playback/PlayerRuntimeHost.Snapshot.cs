// 播放快照与原生 VLC 观测对齐；只有当前资源的不可变尝试可以向当前 generation 投影。
using System.Text.Json;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>当前源身份、页码与实时播放状态使用同一快照，不只报告控制命令意图。</summary>
    private JsonElement Snapshot()
    {
        var position = _current?.Native is VlcMediaPlayer player ? Math.Max(0, player.Time) : 0;
        var duration = _current?.Native is VlcMediaPlayer mediaPlayer ? Math.Max(0, mediaPlayer.Length) : 0;
        var playback = ReadVlcPlaybackState();
        return JsonSerializer.SerializeToElement(new
        {
            source_generation = _generation,
            source_id = _sourceId == 0 ? (long?)null : _sourceId,
            playback_state = playback.State,
            playback_mode = _current?.Kind switch { "pdf" => "pdf", "slide_images" => "slide_images", "powerpoint" => "powerpoint", _ => "" },
            adapter_kind = _current?.Kind ?? string.Empty,
            current_slide = _currentSlide,
            total_slides = _totalSlides,
            position_ms = position,
            duration_ms = duration,
            error_message = playback.Error,
        });
    }


}
