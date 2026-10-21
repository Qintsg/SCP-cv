// 一条共享上游、一份受控编码；调档只重启该自有转换器，不关闭消费者或重启MediaMTX。
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
                await using var process = PreviewProcess.Start(executable, BuildArguments(path.LocalRtspUri, selected));
                using var stop = cancellationToken.UnsafeRegister(static state => { if (OperatingSystem.IsWindows()) ((PreviewProcess)state!).RequestStop(); }, process);
                await foreach (var raw in FlvReader.ReadAsync(process.Output, cancellationToken).ConfigureAwait(false))
                {
                    var tag = raw.WithTimestamp(timeline.Map(raw.Timestamp));
                    var gop = buffer.Push(tag);
                    if (gop is null) continue;
                    publish(gop with { Encoding = selected }); status("live"); failures = 0;
                    // 只在完整组边界换档；不随机打断FLV tag，也不并行启动第二份编码器。
                    if (selected != encoding() && clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(3)) { changed = true; break; }
                }
                if (!changed && !cancellationToken.IsCancellationRequested) throw new IOException("预览编码输出已结束");
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
    public static IReadOnlyList<string> BuildArguments(string localRtspUri, PreviewEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        if (!Uri.TryCreate(localRtspUri, UriKind.Absolute, out var local) || local.Scheme != "rtsp" ||
            !local.IsLoopback || local.UserInfo.Length != 0 || !local.AbsolutePath.StartsWith("/scp-preview-", StringComparison.Ordinal) ||
            !PreviewEncoding.Levels.Contains(encoding))
            throw new PreviewException("预览转换参数无效", "invalid_preview_input", 400);
        var rate = encoding.KilobitsPerSecond.ToString(CultureInfo.InvariantCulture);
        var keyframes = Math.Max(2, (int)Math.Ceiling(encoding.FramesPerSecond / 2d)).ToString(CultureInfo.InvariantCulture);
        var filter = FormattableString.Invariant($"scale={encoding.Width}:{encoding.Height}:force_original_aspect_ratio=decrease:force_divisible_by=2,fps={encoding.FramesPerSecond},format=yuv420p");
        return ["-nostdin", "-hide_banner", "-loglevel", "error", "-max_alloc", "67108864", "-threads", "1", "-rtsp_transport", "tcp", "-timeout", "8000000", "-i", localRtspUri,
            "-map", "0:v:0", "-an", "-sn", "-dn", "-vf", filter, "-filter_threads", "1", "-c:v", "libx264", "-threads", "1",
            "-preset", "ultrafast", "-tune", "zerolatency", "-profile:v", "baseline", "-bf", "0", "-g", keyframes, "-keyint_min", keyframes,
            "-sc_threshold", "0", "-b:v", rate + "k", "-maxrate", rate + "k", "-bufsize", rate + "k",
            "-f", "flv", "-flvflags", "no_duration_filesize", "-flush_packets", "1", "pipe:1"];
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
