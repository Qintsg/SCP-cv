// 固定无声预览参数：H264原码封装优先，兼容模式受控编码，不接受外部argv。
namespace ScpCv.ClientPreviewHost;

internal sealed record PreviewConfiguration(string Uri, string Mode);

internal static class PreviewArguments
{
    /// <summary>:param configuration: 私有stdin首配置。:returns: 严格RTSP/RTMP输入和固定FLV输出。</summary>
    public static IReadOnlyList<string> Build(PreviewConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Uri.Length > 8192 || configuration.Uri.Any(char.IsControl) ||
            !System.Uri.TryCreate(configuration.Uri, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("rtmp" or "rtsp") || uri.Host.Length == 0 || uri.Fragment.Length != 0 ||
            configuration.Mode is not ("copy" or "compatible")) throw new InvalidDataException("预览配置无效");
        var arguments = new List<string> { "-nostdin", "-hide_banner", "-loglevel", "error", "-max_alloc", "67108864",
            "-protocol_whitelist", "rtmp,rtsp,tcp,udp,rtp", "-threads", "1" };
        if (uri.Scheme == "rtsp") arguments.AddRange(["-rtsp_transport", "tcp", "-timeout", "8000000"]);
        else arguments.AddRange(["-rw_timeout", "8000000", "-rtmp_live", "live", "-rtmp_buffer", "0",
            "-analyzeduration", "500000", "-probesize", "1048576"]);
        arguments.AddRange(["-i", configuration.Uri, "-map", "0:v:0", "-an", "-sn", "-dn"]);
        if (configuration.Mode == "copy") arguments.AddRange(["-c:v", "copy"]);
        else arguments.AddRange(["-vf", "scale=w='min(1920,iw)':h='min(1080,ih)':force_original_aspect_ratio=decrease:force_divisible_by=2,fps=25,format=yuv420p",
            "-filter_threads", "1", "-c:v", "libx264", "-threads", "1", "-preset", "ultrafast", "-tune", "zerolatency",
            "-profile:v", "baseline", "-bf", "0", "-g", "12", "-keyint_min", "12", "-sc_threshold", "0",
            "-b:v", "6000k", "-maxrate", "6000k", "-bufsize", "3000k"]);
        arguments.AddRange(["-f", "flv", "-flvflags", "no_duration_filesize", "-flush_packets", "1", "pipe:1"]);
        return arguments;
    }
}
