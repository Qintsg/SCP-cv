// 预览仅解析已登记媒体或明确设备输入；不接受任意URL、不向浏览器暴露凭据。
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
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
            !Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("rtsp" or "rtmp") ||
            parsed.Host.Length == 0 || parsed.Fragment.Length != 0 || quality is not ("low" or "high"))
            throw new PreviewException("此来源没有受支持的预览流", "preview_no_stream", 422);
        var builder = new UriBuilder(parsed);
        if (builder.Port == -1) builder.Port = parsed.Scheme == "rtmp" ? 1935 : 554;
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
            if (source is null || source.SourceType is not (MediaSourceType.RtspStream or MediaSourceType.CustomStream))
                throw new PreviewException("此来源没有预览流", "preview_no_stream", 422);
            var uri = source.Uri;
            // 仅本项目已确认Camera路径可按chn0/1调整，不猜测用户登记流的通道结构。
            if (source.SystemKey.StartsWith("camera:", StringComparison.Ordinal) && uri.EndsWith("/live/chn0", StringComparison.Ordinal) && request.Quality == "low")
                uri = uri[..^1] + "1";
            return new(uri, request.Quality, source.Id, source.SourceRevision);
        }
        var input = request.Input!;
        string address;
        if (input.Kind == "ip_stream")
        {
            address = input.IpAddress ?? string.Empty;
            if (!options.AllowedWallInputAddresses.Contains(address, StringComparer.Ordinal) &&
                !wall.Inputs.Values.Any(item => item.UnicastAddress == address))
                throw new PreviewException("此设备尚未登记预览地址", "preview_no_stream", 422);
        }
        else if (input.Kind is "window_1" or "window_2" or "laptop" && wall.Inputs.TryGetValue(input.Kind, out var descriptor) && string.IsNullOrEmpty(input.IpAddress))
            address = descriptor.UnicastAddress;
        else throw new PreviewException("此窗口暂未接入预览流", "preview_no_stream", 422);
        if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
            ip.ToString() != address || ip.GetAddressBytes()[0] is 0 or >= 224)
            throw new PreviewException("设备预览地址无效", "invalid_preview_input", 400);
        return new($"rtmp://{address}:1935/live/chn{(request.Quality == "high" ? 0 : 1)}", request.Quality);
    }

    /// <summary>:param source: 创建时源快照。:param cancellationToken: 本次取消。:returns: 登记媒体仍为原修订；不通过旧租约继续暴露已修改来源。</summary>
    public async Task<bool> IsCurrentAsync(PreviewSource source, CancellationToken cancellationToken)
    {
        if (source.SourceId is null) return true;
        await using var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await database.MediaSources.AsNoTracking().AnyAsync(item => item.Id == source.SourceId && item.SourceRevision == source.SourceRevision, cancellationToken).ConfigureAwait(false);
    }
}
