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
    private LibVLC? _libVlc;

    private LibVLC GetOrCreateLibVlc()
    {
        if (_libVlc is not null) return _libVlc;
        Core.Initialize();
        return _libVlc = libVlcFactory?.Invoke() ?? new LibVLC();
    }

    private void DisposeVlcInstance()
    {
        _libVlc?.Dispose();
        _libVlc = null;
    }

    private async Task<SurfaceResource> OpenVlcAsync(
        string uri,
        bool autoplay,
        long generation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 一个 PlayerWorker 生命周期只创建一个 LibVLC 实例，切源时只替换 MediaPlayer。
        var libVlc = GetOrCreateLibVlc();
        var player = new VlcMediaPlayer(libVlc);
        try { configureVlcPlayer?.Invoke(player); }
        catch { player.Dispose(); throw; }
        var localPath = LocalPath(uri);
        var media = File.Exists(localPath)
            ? new Media(libVlc, Path.GetFullPath(localPath), FromType.FromPath)
            : new Media(libVlc, uri, FromType.FromLocation);
        player.Media = media;
        _vlcMedia[player] = media;
        BindVlcAttempt(player, media, generation);
        var view = new VideoView { MediaPlayer = player };
        if (autoplay && !player.Play())
        {
            await ReleaseVlcResourceAsync(player, view, media);
            throw new InvalidOperationException("LibVLC 无法开始播放媒体。");
        }
        return new SurfaceResource("vlc", view, async () =>
        {
            await ReleaseVlcResourceAsync(player, view, media);
        }, player);
    }

    private ValueTask ReleaseVlcResourceAsync(
        VlcMediaPlayer player,
        VideoView view,
        Media media)
        => VlcResourceLifecycle.ReleaseAsync(
            () => { UnbindVlcAttempt(player); _vlcMedia.Remove(player); },
            player.Stop,
            () => view.MediaPlayer = null,
            view.Dispose,
            media.Dispose,
            player.Dispose);

    private async Task HandleVlcEndedAsync(VlcMediaPlayer player, Media media, VlcPlaybackAttempt attempt)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        await _mediaGate.WaitAsync();
        try
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            if (!IsCurrentVlcAttempt(player, attempt)) return;
            if (attempt.HasError) return;
            var action = VlcEndPolicy.Decide(_current?.Native, player, _generation, attempt.SourceGeneration, _loopEnabled);
            if (action == VlcEndAction.Ignore) return;
            if (action == VlcEndAction.Replay)
            {
                // 先在线程池等待原生线程结束，UI 保持泵消息；媒体门禁阻止切源并发处置 player。
                if (await RestartVlcAttemptAsync(player, media, attempt.SourceGeneration, autoplay: true)) return;
                _state = "error";
                _errorMessage = "video_loop_restart_failed";
            }
            else _state = "stopped";

            if (_officeSession is null) return;
            try { await _officeSession.ReportStateAsync(attempt.SourceGeneration, Snapshot()); }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException)
            {
                Console.Error.WriteLine($"视频自然结束状态上报失败：{exception.Message}");
            }
        }
        finally { _mediaGate.Release(); }
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
            _state = action switch { "play" => "playing", "pause" => "paused", _ => "stopped" };
            return;
        }
        if (_current?.Native is not VlcMediaPlayer player)
        {
            _state = action switch { "play" => "playing", "pause" => "paused", _ => "stopped" };
            return;
        }
        switch (action)
        {
            case "play":
                if (player.State == VLCState.Paused && _vlcBindings.TryGetValue(player, out var paused) && !paused.Attempt.HasError)
                {
                    player.SetPause(false);
                    _state = "loading";
                }
                else if (player.State == VLCState.Playing && _vlcBindings.TryGetValue(player, out var playing) && !playing.Attempt.HasError)
                    _state = "playing";
                else
                {
                    var media = OwnedVlcMedia(player);
                    if (!await RestartVlcAttemptAsync(player, media, _generation, autoplay: true))
                        throw new InvalidOperationException("LibVLC 未接受当前媒体重播。");
                }
                break;
            case "pause": player.SetPause(true); _state = "paused"; break;
            case "stop":
                UnbindVlcAttempt(player);
                await VlcResourceLifecycle.StopAsync(player.Stop);
                _state = "stopped";
                _errorMessage = string.Empty;
                break;
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
