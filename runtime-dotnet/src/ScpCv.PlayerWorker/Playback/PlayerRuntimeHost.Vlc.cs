// 管理单窗口 LibVLC 播放资源及 WPF VideoView 的完整释放顺序。
using System.IO;
using System.Text.Json;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using ScpCv.Contracts.Ipc;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    private Task<SurfaceResource> OpenVlcAsync(
        string uri,
        bool autoplay,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Core.Initialize();
        var libVlc = new LibVLC();
        var player = new VlcMediaPlayer(libVlc);
        var localPath = LocalPath(uri);
        var media = File.Exists(localPath)
            ? new Media(libVlc, Path.GetFullPath(localPath), FromType.FromPath)
            : new Media(libVlc, uri, FromType.FromLocation);
        player.Media = media;
        void Ended(object? _, EventArgs __)
        {
            var generation = Volatile.Read(ref _generation);
            if (_window.Dispatcher.HasShutdownStarted) return;
            _ = _window.Dispatcher.InvokeAsync(() => HandleVlcEndedAsync(player, generation));
        }
        player.EndReached += Ended;
        var view = new VideoView { MediaPlayer = player };
        if (autoplay && !player.Play())
        {
            ReleaseVlcResource(player, Ended, view, media, libVlc);
            throw new InvalidOperationException("LibVLC 无法开始播放媒体。");
        }
        return Task.FromResult(new SurfaceResource("vlc", view, () =>
        {
            ReleaseVlcResource(player, Ended, view, media, libVlc);
            return ValueTask.CompletedTask;
        }, player));
    }

    private static void ReleaseVlcResource(
        VlcMediaPlayer player,
        EventHandler<EventArgs> ended,
        VideoView view,
        Media media,
        LibVLC libVlc)
    {
        player.EndReached -= ended;
        player.Stop();
        view.MediaPlayer = null;
        // VideoView 持有 WPF 前景窗口和 HWND；仅清空 MediaPlayer 不会释放它们。
        view.Dispose();
        media.Dispose();
        player.Dispose();
        libVlc.Dispose();
    }

    private async Task HandleVlcEndedAsync(VlcMediaPlayer player, long generation)
    {
        var action = VlcEndPolicy.Decide(_current?.Native, player, _generation, generation, _loopEnabled);
        if (action == VlcEndAction.Ignore) return;
        if (action == VlcEndAction.Replay)
        {
            player.Stop();
            player.Time = 0;
            if (player.Play()) return;
            _state = "error";
            _errorMessage = "video_loop_restart_failed";
        }
        else _state = "stopped";

        if (_officeSession is null) return;
        try { await _officeSession.ReportStateAsync(generation, Snapshot()); }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException)
        {
            Console.Error.WriteLine($"视频自然结束状态上报失败：{exception.Message}");
        }
    }

    private async Task CurrentControlAsync(
        CommandLeaseDto lease,
        string action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_current?.Kind == "powerpoint")
        {
            await SendOfficeAsync(lease, "playback", new Dictionary<string, JsonElement>
            {
                ["presentation_identity"] = JsonSerializer.SerializeToElement(_officePresentationIdentity),
                ["action"] = JsonSerializer.SerializeToElement(action),
            }, cancellationToken);
            return;
        }
        if (_current?.Native is not VlcMediaPlayer player) return;
        switch (action)
        {
            case "play": _ = player.Play(); break;
            case "pause": player.SetPause(true); break;
            case "stop": player.Stop(); break;
        }
        await Task.CompletedTask;
    }

    private void SetVolume(int volume)
    {
        if (_current?.Native is VlcMediaPlayer player) player.Volume = Math.Clamp(volume, 0, 100);
    }

    private void SetMute(bool muted)
    {
        if (_current?.Native is VlcMediaPlayer player) player.Mute = muted;
    }

    private void SetLoop(bool enabled) => _loopEnabled = enabled;
}
