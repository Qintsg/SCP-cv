// 文件描述：登记流与设备输入的预览解析。项目：SCP-cv；文件：PreviewSourceResolver.cs；作者：Qintsg；日期：2026-10-30。
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.ControlHost.StreamPreview;

public sealed class PreviewSource
{
    public string Key { get; }
    public string UpstreamUri { get; }
    public string Quality { get; }
    public long? SourceId { get; }
    public long SourceRevision { get; }

    /// <summary>:param uri: 已许可实际地址。:param quality: 输出意图。:param sourceId: 可选媒体身份。:param revision: 源修订。:returns: 仅实际URI参与共享键，质量／媒体别名不重复拉同流。</summary>
    public PreviewSource(string uri, string quality, long? sourceId = null, long revision = 0)
    {
        if (string.IsNullOrWhiteSpace(uri) || uri.Length > 8192 || uri.Any(char.IsControl) ||
            !Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("rtsp" or "rtmp" or "srt" or "http" or "https" or "udp" or "rtp") ||
            parsed.Host.Length == 0 || parsed.Fragment.Length != 0 || quality is not ("low" or "high"))
            throw new PreviewException("此来源没有受支持的预览流", "preview_no_stream", 422);
        var builder = new UriBuilder(parsed);
        if (builder.Port == -1 && parsed.Scheme is "rtsp" or "rtmp") builder.Port = parsed.Scheme == "rtmp" ? 1935 : 554;
        if (parsed.Scheme is "udp" or "rtp") builder.Scheme = parsed.Scheme == "udp" ? "udp+mpegts" : "udp+rtp";
        UpstreamUri = builder.Uri.AbsoluteUri; Quality = quality; SourceId = sourceId; SourceRevision = revision;
        Key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(UpstreamUri))).ToLowerInvariant();
    }

    public override string ToString() => Key;
}

public sealed class PreviewSourceResolver(
    IDbContextFactory<ControlDbContext> contexts, VideoWallProtocolOptions wall, PreviewOptions options)
{
    /// <summary>:param request: 受限输入。:param cancellationToken: 取消。:returns: 已许可的服务端来源；不连接或探测任何设备。</summary>
    public async Task<PreviewSource> ResolveAsync(PreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((request.SourceId is null) == (request.Input is null) || request.SourceId is <= 0 || request.Quality is not ("low" or "high"))
            throw new PreviewException("请选择一个有效的预览来源", "invalid_preview_input", 400);
        if (request.SourceId is { } id)
        {
            await using var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var source = await database.MediaSources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
            if (source is null || source.SourceType is not (MediaSourceType.RtspStream or MediaSourceType.SrtStream or MediaSourceType.CustomStream))
                throw new PreviewException("此来源没有预览流", "preview_no_stream", 422);
            return Snapshot(source, request.Quality);
        }
        var input = request.Input!;
        string address;
        var descriptor = SiteInputCatalog.ResolveDescriptor(input.Kind, input.IpAddress, wall);
        if (descriptor is not null) address = descriptor.UnicastAddress;
        else if (input.Kind == "ip_stream")
        {
            address = input.IpAddress ?? string.Empty;
            if (!options.AllowedWallInputAddresses.Contains(address, StringComparer.Ordinal) &&
                !wall.Inputs.Values.Any(item => item.UnicastAddress == address))
                throw new PreviewException("此设备尚未登记预览地址", "preview_no_stream", 422);
        }
        else throw new PreviewException("此窗口暂未接入预览流", "preview_no_stream", 422);
        if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
            ip.ToString() != address || ip.GetAddressBytes()[0] is 0 or >= 224)
            throw new PreviewException("设备预览地址无效", "invalid_preview_input", 400);
        var known = SiteInputCatalog.ByAddress(address);
        if (known is not null && !string.IsNullOrEmpty(known.SystemKey))
        {
            await using var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var saved = await database.MediaSources.AsNoTracking().SingleOrDefaultAsync(item => item.SystemKey == known.SystemKey, cancellationToken).ConfigureAwait(false);
            if (saved is not null) return Snapshot(saved, request.Quality);
        }
        return new(SystemChannel(known?.DefaultPreviewUri ?? $"rtmp://{address}:1935/live/chn0", request.Quality), request.Quality);
    }

    /// <summary>:param source: 已保存来源，包含设备自有凭据。:param quality: 观看意图。:returns: 目录别名使用原URI/修订，非系统来源绝不猜测通道。</summary>
    private static PreviewSource Snapshot(MediaSource source, string quality)
    {
        var uri = SiteInputCatalog.All.Any(item => item.SystemKey.Length > 0 && item.SystemKey == source.SystemKey)
            ? SystemChannel(source.Uri, quality) : source.Uri;
        return new(uri, quality, source.Id, source.SourceRevision);
    }

    /// <summary>:param uri: 目录登记或保存地址。:param quality: 观看意图。:returns: 只有已确认的完整chn0结尾才切低清，不改查询参数/任意路径。</summary>
    private static string SystemChannel(string uri, string quality) =>
        quality == "low" && uri.EndsWith("/live/chn0", StringComparison.Ordinal) ? uri[..^1] + "1" : uri;

    /// <summary>:param source: 创建时源快照。:param cancellationToken: 本次取消。:returns: 登记媒体仍为原修订；不通过旧租约继续暴露已修改来源。</summary>
    public async Task<bool> IsCurrentAsync(PreviewSource source, CancellationToken cancellationToken)
    {
        if (source.SourceId is null) return true;
        await using var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await database.MediaSources.AsNoTracking().AnyAsync(item => item.Id == source.SourceId && item.SourceRevision == source.SourceRevision, cancellationToken).ConfigureAwait(false);
    }
}
