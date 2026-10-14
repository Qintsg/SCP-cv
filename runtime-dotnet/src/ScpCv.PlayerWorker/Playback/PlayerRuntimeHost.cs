// 执行单窗口的媒体切换，保留旧画面直到新资源完成预备。
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;
using ScpCv.PlayerWorker.ScreenSharing;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.PlayerWorker.Adapters;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ScpCv.PlayerWorker.Playback;

/// <summary>单个输出进程内拥有真实 WPF/WebView2/LibVLC/PDF/图片资源，不跨进程传递原生对象。</summary>
public sealed partial class PlayerRuntimeHost(
    PlayerWindow window,
    int windowId,
    RuntimeWorkerSession? officeSession = null,
    Func<LibVLC>? libVlcFactory = null,
    Action<VlcMediaPlayer>? configureVlcPlayer = null,
    string? audioOutputDeviceId = null,
    string? webViewUserDataRoot = null) : IAsyncDisposable
{
    private readonly PlayerWindow _window = window;
    private readonly int _windowId = windowId;
    private readonly RuntimeWorkerSession? _officeSession = officeSession;
    private readonly SemaphoreSlim _mediaGate = new(1, 1);
    private readonly Dictionary<string, SurfaceResource> _warmWebResources = new(StringComparer.Ordinal);
    private SurfaceResource? _current;
    private long _sourceId;
    private long _sourceRevision;
    private string _sourceUri = string.Empty;
    private long _generation;
    private string _state = "idle";
    private string _errorMessage = string.Empty;
    private bool _loopEnabled;
    private int _currentSlide;
    private int _totalSlides;
    private int _disposed;
    private static readonly TimeSpan WebViewStageTimeout = TimeSpan.FromSeconds(15);

    public Task<WorkerExecutionResult> ExecuteAsync(
        CommandLeaseDto lease,
        CancellationToken cancellationToken) =>
        _window.Dispatcher.InvokeAsync(() => ExecuteCoreAsync(lease, cancellationToken)).Task.Unwrap();

    private async Task<WorkerExecutionResult> ExecuteCoreAsync(
        CommandLeaseDto lease,
        CancellationToken cancellationToken)
    {
        // 退出已开始的新命令不能排在原生关闭后；取得gate后仍须再次核验竞态。
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _mediaGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var command = lease.Command.Trim().ToUpperInvariant();
            var resultCode = "ok";
            if (_unconfirmedCandidates.Count > 0 && command is not ("CLOSE" or "RESET_PPT"))
                throw new ScpCv.Contracts.Presentations.PresentationTransportException("候选资源清理未确认，普通播放命令已隔离。",
                    new InvalidOperationException("unconfirmed_presentation_candidate"));
            if (command == "SCREEN_SHARE_AUDIO")
                return await ExecuteScreenShareAudioAsync(lease, cancellationToken);
            if (_current?.Kind == "libreoffice" && command is "PLAY" or "PAUSE" or "STOP" or "SEEK" or "SET_VOLUME" or "SET_MUTE" or "SET_LOOP" or "PPT_MEDIA")
                throw new InvalidOperationException("LibreOffice当前未支持统一媒体控制");
            if (_current?.Kind == "screen_share" && command is not ("OPEN" or "CLOSE" or "RESET_PPT" or "SELECT_DISPLAY"))
                throw new InvalidOperationException("屏幕共享仅支持独立停止；普通媒体控制未支持。");
            if (command == "CLOSE" && IsObsoleteShareClose(lease))
                return new WorkerExecutionResult("completed", "share_no_longer_current", Snapshot());
            switch (command)
            {
                case "OPEN":
                    if (await OpenAsync(lease, cancellationToken) is { } openFailure) return openFailure;
                    break;
                case "CLOSE":
                case "RESET_PPT": await CloseAsync(lease, cancellationToken); break;
                case "PLAY": await CurrentControlAsync(lease, "play", cancellationToken); break;
                case "PAUSE": await CurrentControlAsync(lease, "pause", cancellationToken); break;
                case "STOP": await CurrentControlAsync(lease, "stop", cancellationToken); break;
                case "SEEK": await SeekAsync(Long(lease.Args, "position_ms"), cancellationToken); break;
                case "NEXT": resultCode = await NavigateRelativeAsync(lease, true, cancellationToken); break;
                case "PREV": resultCode = await NavigateRelativeAsync(lease, false, cancellationToken); break;
                case "GOTO": await NavigateAsync(lease, Int(lease.Args, "target_index", 1), cancellationToken); break;
                case "NEXT_EFFECT":
                case "PREVIOUS_EFFECT": resultCode = await StepLibreOfficeAsync(lease, cancellationToken); break;
                case "SET_VOLUME": await SetVolumeAsync(Int(lease.Args, "volume", 100), cancellationToken); break;
                case "SET_MUTE": await SetMuteAsync(Bool(lease.Args, "muted", false), cancellationToken); break;
                case "SET_LOOP": SetLoop(Bool(lease.Args, "enabled", false)); break;
                case "SELECT_DISPLAY": SelectDisplay(String(lease.Args, "target_label")); break;
                case "SHOW_ID": ShowWindowId(); break;
                case "PPT_MEDIA": throw new InvalidOperationException("统一文稿媒体控制未支持；PowerPoint入口已退役。");
                default: throw new InvalidOperationException($"PlayerWorker 不支持命令 {lease.Command}。");
            }

            _generation = Math.Max(_generation, lease.SourceGeneration);
            return new WorkerExecutionResult("completed", resultCode, Snapshot());
        }
        finally { _mediaGate.Release(); }
    }

    private async Task<WorkerExecutionResult?> OpenAsync(CommandLeaseDto lease, CancellationToken cancellationToken)
    {
        var sourceId = Long(lease.Args, "source_id");
        var isShare = String(lease.Args, "source_type", "") == "screen_share";
        using var shareLifetime = isShare ? ScreenShareOpenLifetime(lease, cancellationToken) : null;
        if (shareLifetime is not null) cancellationToken = shareLifetime.Token;
        var uri = isShare ? string.Empty : String(lease.Args, "uri");
        var sourceType = String(lease.Args, "source_type", InferType(uri));
        var autoplay = Bool(lease.Args, "autoplay", true);
        var audio = sourceType is "video" or "audio" or "rtsp" or "srt" or "custom_stream"
            ? ReadVlcAudioIntent(lease) : (Volume: (int?)null, Muted: (bool?)null);
        _loopEnabled = Bool(lease.Args, "loop", _loopEnabled);
        if (_current?.Native is VlcMediaPlayer currentPlayer &&
            sourceId == _sourceId && lease.SourceRevision == _sourceRevision &&
            string.Equals(uri, _sourceUri, StringComparison.Ordinal))
        {
            var currentMedia = OwnedVlcMedia(currentPlayer);
            var requestedVolume = audio.Volume ?? currentPlayer.Volume;
            var requestedMute = audio.Muted ?? currentPlayer.Mute;
            if (!await ReopenVlcAttemptAsync(currentPlayer, currentMedia, lease.SourceGeneration, autoplay, requestedVolume, requestedMute))
                throw new InvalidOperationException("LibVLC 无法重新播放当前媒体。");
            await ApplyVlcAudioConfirmedAsync(currentPlayer, requestedVolume, requestedMute, cancellationToken);
            _vlcVolume = requestedVolume;
            _vlcMuted = requestedMute;
            _generation = lease.SourceGeneration;
            _errorMessage = string.Empty;
            return null;
        }
        SurfaceResource next;
        switch (sourceType)
        {
            case "screen_share": next = await OpenScreenShareAsync(lease, cancellationToken); break;
            case "image": next = await OpenImageAsync(uri, cancellationToken); break;
            case "web": next = await OpenWebAsync(sourceId, lease.SourceRevision, uri, cancellationToken); break;
            case "video":
            case "audio":
            case "custom_stream":
            case "rtsp":
            case "srt": next = await OpenVlcAsync(uri, autoplay, lease.SourceGeneration,
                sourceType is "rtsp" or "srt" or "custom_stream",
                audio.Volume, audio.Muted,
                cancellationToken); break;
            case "ppt" when Path.GetExtension(LocalPath(uri)).Equals(".pdf", StringComparison.OrdinalIgnoreCase):
                next = await OpenPdfAsync(uri, Int(lease.Args, "target_slide", 1), cancellationToken);
                break;
            case "ppt" when String(lease.Args, "presentation_mode") == "slide_images":
                next = await OpenSlideImagesAsync(
                    String(lease.Args, "slide_images_directory"),
                    Int(lease.Args, "target_slide", 1), cancellationToken);
                break;
            case "ppt" when String(lease.Args, "presentation_mode") == "libreoffice":
                next = await OpenLibreOfficeAsync(lease, uri, cancellationToken);
                break;
            case "ppt": throw new InvalidOperationException("演示文稿模式无效；请选择LibreOffice或已准备的静态制品。");
            default: throw new InvalidOperationException($"不支持媒体类型 {sourceType}。");
        }

        var previous = _current;
        if (previous?.Kind is "libreoffice" or "screen_share")
        {
            try { await previous.DisposeAsync(); }
            catch (Exception retirement)
            {
                await DiscardCandidateAfterRetirementFailureAsync(next, retirement);
                throw;
            }
        }
        if (await ActivateCandidateAsync(lease, previous, next, cancellationToken) is { } activationFailure) return activationFailure;
        await ActivateWebAudioAsync(previous, next);
        // 布局属于成功发布的资源，不让失败OPEN的意图覆盖旧画面的实际布局。
        next.ActualFitText = sourceType == "ppt" ? Bool(lease.Args, "fit_text", false) : null;
        _current = next;
        // 只记住已成功发布的VLC资源；候选打开/激活失败不得覆盖旧资源的声音默认值。
        if (next.Native is VlcMediaPlayer openedPlayer)
        {
            _vlcVolume = audio.Volume ?? (openedPlayer.Volume >= 0 ? openedPlayer.Volume : _vlcVolume);
            _vlcMuted = audio.Muted ?? openedPlayer.Mute;
        }
        _sourceId = sourceId;
        _sourceRevision = lease.SourceRevision;
        _sourceUri = uri;
        _generation = lease.SourceGeneration;
        _errorMessage = string.Empty;
        if (next.Native is PdfPlaybackAdapter pdf)
        {
            _currentSlide = pdf.CurrentPage;
            _totalSlides = pdf.PageCount;
        }
        else if (next.Native is ScpCv.PlayerWorker.Presentations.IPresentationSurface native)
        {
            _currentSlide = native.CurrentSlide;
            _totalSlides = native.TotalSlides;
        }
        else if (next.Native is SlideImagePlaybackAdapter slides)
        {
            _currentSlide = slides.CurrentPage;
            _totalSlides = slides.PageCount;
        }
        else if (next.Kind != "libreoffice")
        {
            _currentSlide = 0;
            _totalSlides = 0;
        }
        // 统一媒体暂停未支持：不可将已运行的LibreOffice虚报为paused。
        _state = next.Kind is "libreoffice" or "screen_share" ? "playing" : autoplay ? next.Kind == "vlc" ? "loading" : "playing" : "paused";
        _window.SetSurface(next.Surface);
        if (previous is not null && !ReferenceEquals(previous, next) && previous.Kind is not ("web" or "libreoffice" or "screen_share"))
            await previous.DisposeAsync();
        return null;
    }

    private static async Task<SurfaceResource> OpenImageAsync(string uri, CancellationToken cancellationToken)
    {
        var adapter = new ImagePlaybackAdapter();
        var path = LocalPath(uri);
        await adapter.PrepareAsync(new(0, path, "image"), path, cancellationToken);
        await adapter.OpenAsync(cancellationToken);
        return new SurfaceResource("image", new System.Windows.Controls.Image
        {
            Source = adapter.Image,
            Stretch = Stretch.Uniform,
        }, adapter.DisposeAsync);
    }


    private static async Task<SurfaceResource> OpenPdfAsync(
        string uri,
        int initialPage,
        CancellationToken cancellationToken)
    {
        var adapter = new PdfPlaybackAdapter();
        await adapter.OpenAsync(LocalPath(uri), Math.Max(1, initialPage), cancellationToken);
        var image = new System.Windows.Controls.Image
        {
            Source = Bitmap(await adapter.RenderPageAsync(adapter.CurrentPage, cancellationToken)),
            Stretch = Stretch.Uniform,
        };
        return new SurfaceResource("pdf", image, adapter.DisposeAsync, adapter);
    }

    private static async Task<SurfaceResource> OpenSlideImagesAsync(
        string directory,
        int initialPage,
        CancellationToken cancellationToken)
    {
        var adapter = new SlideImagePlaybackAdapter();
        await adapter.OpenAsync(directory, Math.Max(1, initialPage), cancellationToken);
        var image = new System.Windows.Controls.Image
        {
            Source = Bitmap(await adapter.RenderPageAsync(adapter.CurrentPage, cancellationToken)),
            Stretch = Stretch.Uniform,
        };
        return new SurfaceResource("slide_images", image, adapter.DisposeAsync, adapter);
    }

    private async Task SeekAsync(long positionMs, CancellationToken cancellationToken)
    {
        if (_current?.Native is VlcMediaPlayer player) player.Time = Math.Max(0, positionMs);
        else if (_current?.Native is PdfPlaybackAdapter pdf) await NavigateAsync(null, (int)positionMs, cancellationToken);
    }

    private async Task NavigateAsync(CommandLeaseDto? lease, int page, CancellationToken cancellationToken)
    {
        if (_current is null) throw new InvalidOperationException("没有实际播放资源，不能执行翻页。");
        if (_current?.Native is ScpCv.PlayerWorker.Presentations.IPresentationSurface native)
        {
            if (lease is null) throw new InvalidOperationException("LibreOffice翻页缺少命令租约");
            await native.ControlAsync(lease, "goto", Math.Clamp(page, 1, native.TotalSlides), cancellationToken);
            _currentSlide = native.CurrentSlide;
            _totalSlides = native.TotalSlides;
            return;
        }
        if (_current?.Native is not PdfPlaybackAdapter && _current?.Native is not SlideImagePlaybackAdapter) return;
        var pageCount = _current.Native is PdfPlaybackAdapter pdf ? pdf.PageCount : ((SlideImagePlaybackAdapter)_current.Native!).PageCount;
        page = Math.Clamp(page, 1, pageCount);
        var bytes = _current.Native is PdfPlaybackAdapter currentPdf
            ? await currentPdf.RenderPageAsync(page, cancellationToken)
            : await ((SlideImagePlaybackAdapter)_current.Native!).RenderPageAsync(page, cancellationToken);
        ((System.Windows.Controls.Image)_current.Surface).Source = Bitmap(bytes);
        _currentSlide = page;
        _totalSlides = pageCount;
    }

    private void SelectDisplay(string targetLabel)
    {
        var screen = System.Windows.Forms.Screen.AllScreens.SingleOrDefault(item =>
            string.Equals(item.DeviceName, targetLabel, StringComparison.OrdinalIgnoreCase));
        if (screen is null)
            throw new InvalidOperationException($"显示器目标已失效：{targetLabel}");

        var bounds = screen.Bounds;
        _window.AssignBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private async Task CloseAsync(CommandLeaseDto lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CloseUnconfirmedCandidatesAsync();
        var previous = _current;
        if (previous?.Kind is "libreoffice" or "screen_share") await previous.DisposeAsync();
        previous?.SetAudible(false);
        _current = null;
        _window.SetSurface(new Grid { Background = WpfBrushes.Black });
        if (previous is not null && previous.Kind is not ("web" or "libreoffice" or "screen_share")) await previous.DisposeAsync();
        _sourceId = 0;
        _sourceRevision = 0;
        _sourceUri = string.Empty;
        _state = "idle";
        _errorMessage = string.Empty;
        _currentSlide = 0;
        _totalSlides = 0;
    }

    private void ShowWindowId()
    {
        _window.SetSurface(new TextBlock
        {
            Text = _windowId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Foreground = WpfBrushes.White,
            FontSize = 240,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
    }


    private static BitmapImage Bitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static string InferType(string uri)
    {
        var extension = Path.GetExtension(LocalPath(uri)).ToLowerInvariant();
        return extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => "image",
            ".pdf" or ".ppt" or ".pptx" => "ppt",
            _ when Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https" => "web",
            _ => "video",
        };
    }

    private static string LocalPath(string uri) =>
        uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? new Uri(uri).LocalPath : uri;

    private static string String(Dictionary<string, JsonElement> args, string key, string? fallback = null) =>
        args.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback ?? string.Empty
            : fallback ?? throw new InvalidDataException($"缺少字符串参数 {key}。");

    private static long Long(Dictionary<string, JsonElement> args, string key, long fallback = 0) =>
        args.TryGetValue(key, out var value) && value.TryGetInt64(out var parsed) ? parsed : fallback;

    private static int Int(Dictionary<string, JsonElement> args, string key, int fallback) =>
        args.TryGetValue(key, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;

    private static bool Bool(Dictionary<string, JsonElement> args, string key, bool fallback) =>
        args.TryGetValue(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private sealed class SurfaceResource(
        string kind,
        FrameworkElement surface,
        Func<ValueTask> dispose,
        object? native = null,
        Func<bool>? health = null,
        Action<bool>? audible = null) : IAsyncDisposable
    {
        public string Kind { get; } = kind;
        public FrameworkElement Surface { get; } = surface;
        public object? Native { get; } = native;
        public bool? ActualFitText { get; set; }
        public bool IsHealthy => health?.Invoke() ?? true;
        public void SetAudible(bool enabled) => audible?.Invoke(enabled);
        public ValueTask DisposeAsync() => dispose();
    }
}
