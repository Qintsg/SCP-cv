// 文件描述：按需单编码器与成熟RTMP媒体接收；项目：SCP-cv；文件：PreviewProducer.cs；作者：Qintsg；日期：2026-10-21。
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace ScpCv.ControlHost.StreamPreview;

public interface IPreviewProducer
{
    Task RunAsync(PreviewSource source, Func<PreviewEncoding> encoding, Action<PreviewGop> publish,
        Action<string> status, CancellationToken cancellationToken);
}

public sealed class PreviewProducer(MediaMtxPreviewClient relay, PreviewOptions options, TimeProvider clock) : IPreviewProducer
{
    /// <summary>:param source: 服务端许可来源。:param encoding: 当前资源允许档位。:param publish: 完整GOP回调。:param status: 固定诊断状态。:param cancellationToken: 所有观看者生命周期。:returns: 取消前按需维护一份上游，故障退避，退出仅清理自有路径／Job。</summary>
    public async Task RunAsync(PreviewSource source, Func<PreviewEncoding> encoding, Action<PreviewGop> publish,
        Action<string> status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(encoding); ArgumentNullException.ThrowIfNull(publish); ArgumentNullException.ThrowIfNull(status);
        if (!OperatingSystem.IsWindows()) throw new PreviewException("服务器尚未接入Windows预览转换器", "preview_dependency_missing");
        var executable = FindExecutable(options.FfmpegPath);
        status("connecting");
        await using var path = await relay.RegisterAsync(source, cancellationToken).ConfigureAwait(false);
        var timeline = new FlvTimeline(); var buffer = new FlvGopBuffer(); var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var selected = encoding(); var started = clock.GetTimestamp(); var changed = false;
            timeline.BeginEpoch(); buffer.BeginEpoch();
            try
            {
                await using var receiver = await PreviewRtmpReceiver.StartAsync(cancellationToken).ConfigureAwait(false);
                await using var process = PreviewProcess.Start(executable, BuildArguments(path.LocalRtspUri, selected, receiver.PublishUri));
                using var epoch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var exited = ObserveExitAsync(process, receiver, epoch.Token);
                using var stop = cancellationToken.UnsafeRegister(static state => { if (OperatingSystem.IsWindows()) ((PreviewProcess)state!).RequestStop(); }, process);
                try
                {
                    await foreach (var raw in receiver.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var tag = raw.WithTimestamp(timeline.Map(raw.Timestamp));
                        var gop = buffer.Push(tag);
                        if (gop is null) continue;
                        publish(gop with { Encoding = selected }); status("live"); failures = 0;
                        if (selected != encoding() && clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(3)) { changed = true; break; }
                    }
                    if (!changed && !cancellationToken.IsCancellationRequested) throw new IOException("预览编码输出已结束");
                }
                finally { await epoch.CancelAsync().ConfigureAwait(false); await exited.ConfigureAwait(false); }
            }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
                error is IOException or Win32Exception or InvalidDataException or PreviewException)
            {
                if (error is PreviewException { Code: "preview_cleanup_pending" }) throw;
                status("reconnecting"); failures = Math.Min(failures + 1, 4);
                try { await path.EnsureAsync(source, cancellationToken).ConfigureAwait(false); }
                catch (PreviewException restore) when (restore.Code != "preview_cleanup_pending") { /* 保留同一逻辑流退避，不重启MediaMTX或创建第二个编码器。 */ }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { /* 管理API短超时，下一轮继续退避恢复。 */ }
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, 1 << failures)), clock, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>:param localRtspUri: 仅自有MediaMTX回环路径。:param encoding: 有界档位。:returns: 不经shell、不含上游凭据的FFmpeg参数。</summary>
    public static IReadOnlyList<string> BuildArguments(string localRtspUri, PreviewEncoding encoding, string publishUri)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        if (!Uri.TryCreate(localRtspUri, UriKind.Absolute, out var local) || local.Scheme != "rtsp" ||
            !local.IsLoopback || local.UserInfo.Length != 0 || !local.AbsolutePath.StartsWith("/scp-preview-", StringComparison.Ordinal) ||
            !PreviewEncoding.Levels.Contains(encoding) || !Uri.TryCreate(publishUri, UriKind.Absolute, out var destination) ||
            destination.Scheme != "rtmp" || destination.Host != "127.0.0.1" || destination.Port is < 1 or > 65535 ||
            destination.UserInfo.Length != 0 || destination.Query.Length != 0 || destination.Fragment.Length != 0 ||
            !destination.AbsolutePath.StartsWith("/scp-preview/", StringComparison.Ordinal) ||
            !Guid.TryParseExact(destination.AbsolutePath[13..], "N", out _))
            throw new PreviewException("预览转换参数无效", "invalid_preview_input", 400);
        var rate = encoding.KilobitsPerSecond.ToString(CultureInfo.InvariantCulture);
        var keyframes = KeyframeInterval(encoding).ToString(CultureInfo.InvariantCulture);
        var filter = FormattableString.Invariant($"scale={encoding.Width}:{encoding.Height}:force_original_aspect_ratio=decrease:force_divisible_by=2,fps={encoding.FramesPerSecond},format=yuv420p");
        return ["-nostdin", "-hide_banner", "-loglevel", "error", "-max_alloc", "67108864", "-threads", "1", "-rtsp_transport", "tcp", "-timeout", "8000000",
            "-allowed_media_types", "video", "-analyzeduration", "500000", "-probesize", "1048576", "-fflags", "nobuffer", "-i", localRtspUri,
            "-map", "0:v:0", "-an", "-sn", "-dn", "-vf", filter, "-filter_threads", "1", "-c:v", "libx264", "-threads", "1",
            "-preset", "ultrafast", "-tune", "zerolatency", "-profile:v", "baseline", "-bf", "0", "-g", keyframes, "-keyint_min", keyframes,
            "-sc_threshold", "0", "-b:v", rate + "k", "-maxrate", rate + "k", "-bufsize", rate + "k",
            "-f", "flv", "-flvflags", "no_duration_filesize", "-flush_packets", "1", publishUri];
    }

    /// <summary>:param encoding: 已验证编码档。:returns: 约200至250ms完整组，低帧率允许每帧关键帧。</summary>
    public static int KeyframeInterval(PreviewEncoding encoding) => Math.Max(1, (int)Math.Ceiling(encoding.FramesPerSecond / 5d));

    /// <summary>:param process: 本轮精确进程。:param receiver: 本轮接收器。:param cancellationToken: 结束等待。:returns: 编码器退出会立即结束媒体等待。</summary>
    private static async Task ObserveExitAsync(PreviewProcess process, PreviewRtmpReceiver receiver, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("预览转换器需要Windows");
        try { await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); receiver.EncoderExited(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    /// <summary>:param configured: 服务器配置的可执行文件，不来自请求。:returns: 实际绝对路径；缺少则固定错误，不启动shell或下载。</summary>
    public static string FindExecutable(string configured)
    {
        if (Path.IsPathFullyQualified(configured) && File.Exists(configured)) return Path.GetFullPath(configured);
        if (configured.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0)
            foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(folder.Trim('"'), configured.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? configured : configured + ".exe");
                if (Path.IsPathFullyQualified(candidate) && File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        throw new PreviewException("服务器未安装或未配置FFmpeg，观看设备无需安装", "preview_dependency_missing");
    }
}

public sealed class UnavailablePreviewProducer : IPreviewProducer
{
    public Task RunAsync(PreviewSource source, Func<PreviewEncoding> encoding, Action<PreviewGop> publish,
        Action<string> status, CancellationToken cancellationToken) =>
        Task.FromException(new PreviewException("当前模拟运行不启动实体预览转换器", "preview_dependency_missing"));
}
