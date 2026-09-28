// VLC 回调按独立尝试捕获不可变 token；只写该 token，当前画面和重播由 UI/媒体门禁管理。
using LibVLCSharp.Shared;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    private readonly Dictionary<VlcMediaPlayer, VlcAttemptBinding> _vlcBindings = [];
    private readonly Dictionary<VlcMediaPlayer, Media> _vlcMedia = [];

    /// <summary>重播继续使用表面拥有的 Media，不把 getter 的临时引用留在后续回调里。</summary>
    private Media OwnedVlcMedia(VlcMediaPlayer player) => _vlcMedia.TryGetValue(player, out var media)
        ? media : throw new InvalidOperationException("当前 VLC 媒体不存在。");

    /// <summary>新尝试在 Play 前订阅，因此尚未切入表面的快速 Error/Playing 也会被保留。</summary>
    private void BindVlcAttempt(VlcMediaPlayer player, Media media, long generation)
    {
        _vlcBindings.TryGetValue(player, out var previous);
        var attempt = new VlcPlaybackAttempt(generation);
        void Failed(object? _, EventArgs __) => attempt.RecordError();
        void Playing(object? _, EventArgs __) => attempt.RecordPlaying();
        void Ended(object? _, EventArgs __)
        {
            attempt.RecordEnded();
            if (_window.Dispatcher.HasShutdownStarted || Volatile.Read(ref _disposed) != 0) return;
            _ = ObserveVlcEndDispatchAsync(player, media, attempt);
        }
        player.EncounteredError += Failed;
        player.Playing += Playing;
        player.EndReached += Ended;
        _vlcBindings[player] = new(attempt, Failed, Playing, Ended);
        // 健康同源切换先订阅候选 token，再退役旧 token，避免订阅空窗吞掉当前输入的错误。
        if (previous is not null) DetachVlcBinding(player, previous);
    }

    /// <summary>先废弃 token 并解除全部事件，再等待停止，旧原生线程不能标记下一轮尝试。</summary>
    private void UnbindVlcAttempt(VlcMediaPlayer player)
    {
        if (!_vlcBindings.Remove(player, out var binding)) return;
        DetachVlcBinding(player, binding);
    }

    /// <summary>解除指定不可变绑定，不删除可能已替换的新绑定。</summary>
    private static void DetachVlcBinding(VlcMediaPlayer player, VlcAttemptBinding binding)
    {
        player.EncounteredError -= binding.Error;
        player.Playing -= binding.Playing;
        player.EndReached -= binding.Ended;
    }

    /// <summary>健康同源打开保留进度/暂停恢复，只有真实失败、结束或停止才重新启动输入。</summary>
    private async Task<bool> ReopenVlcAttemptAsync(VlcMediaPlayer player, Media media, long generation, bool autoplay)
    {
        _vlcBindings.TryGetValue(player, out var previous);
        if (previous?.Attempt.HasError == true || player.State is VLCState.Error or VLCState.Ended or VLCState.Stopped)
            return await RestartVlcAttemptAsync(player, media, generation, autoplay);
        // autoplay=false 的首次 OPEN 只设置 Media，尚无输入线程；下次显式启动不能只换 token。
        if (autoplay && player.State == VLCState.NothingSpecial)
            return await RestartVlcAttemptAsync(player, media, generation, autoplay);
        BindVlcAttempt(player, media, generation);
        // 订阅交接期间旧输入确已结束/失败，显式 OPEN 的恢复意图仍能重新启动，不复用旧错误。
        if (previous?.Attempt.HasError == true || player.State is VLCState.Error or VLCState.Ended or VLCState.Stopped)
            return await RestartVlcAttemptAsync(player, media, generation, autoplay);
        if (autoplay && player.State == VLCState.Paused) player.SetPause(false);
        else if (!autoplay) player.SetPause(true);
        _state = autoplay ? "loading" : "paused";
        _errorMessage = string.Empty;
        return true;
    }

    /// <summary>显式重开、失败重试和循环只复用原生播放器，不复用旧尝试的状态或回调。</summary>
    private async Task<bool> RestartVlcAttemptAsync(VlcMediaPlayer player, Media media, long generation, bool autoplay)
    {
        UnbindVlcAttempt(player);
        await VlcResourceLifecycle.StopAsync(player.Stop);
        player.Time = 0;
        BindVlcAttempt(player, media, generation);
        _state = autoplay ? "loading" : "paused";
        _errorMessage = string.Empty;
        return !autoplay || player.Play(media);
    }

    /// <summary>延迟结束处理必须仍属于当前资源、当前 generation 和当前不可变尝试。</summary>
    private bool IsCurrentVlcAttempt(VlcMediaPlayer player, VlcPlaybackAttempt attempt) =>
        ReferenceEquals(_current?.Native, player) && _generation == attempt.SourceGeneration &&
        _vlcBindings.TryGetValue(player, out var binding) && ReferenceEquals(binding.Attempt, attempt);

    /// <summary>观察异步 Dispatcher 任务的异常；原生回调不等待 UI 或直接 Stop。</summary>
    private async Task ObserveVlcEndDispatchAsync(VlcMediaPlayer player, Media media, VlcPlaybackAttempt attempt)
    {
        try
        {
            await _window.Dispatcher.InvokeAsync(() => HandleVlcEndedAsync(player, media, attempt)).Task.Unwrap();
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            if (Volatile.Read(ref _disposed) == 0) Console.Error.WriteLine($"视频结束回调被取消：{exception.GetType().Name}");
        }
        catch (Exception exception) { Console.Error.WriteLine($"视频结束回调失败：{exception.GetType().Name}"); }
    }

    /// <summary>只投影当前表面的本次尝试，旧资源即使晚到错误也不能改写新画面。</summary>
    private (string State, string Error) ReadVlcPlaybackState()
    {
        if (_current?.Native is not VlcMediaPlayer player || !_vlcBindings.TryGetValue(player, out var binding) ||
            binding.Attempt.SourceGeneration != _generation) return (_state, _errorMessage);
        var state = binding.Attempt.ResolveState(player.State, _state);
        var error = state == "error" && _errorMessage.Length == 0
            ? "VLC 媒体连接或解码失败；请检查源地址、在线状态和格式支持。"
            : _errorMessage;
        return (state, error);
    }

    private sealed record VlcAttemptBinding(VlcPlaybackAttempt Attempt,
        EventHandler<EventArgs> Error, EventHandler<EventArgs> Playing, EventHandler<EventArgs> Ended);
}
