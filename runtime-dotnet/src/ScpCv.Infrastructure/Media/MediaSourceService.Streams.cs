// 直播源登记与 URL 验证，登记不冒充已经连通或实际出画。
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Diagnostics;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    private static readonly string[] CustomStreamSchemes = ["http", "https", "udp", "rtp", "rtmp"];

    public Task<MediaSourceDto> AddStreamAsync(
        string sourceType,
        string url,
        string? displayName,
        long? folderId,
        bool preheatEnabled,
        CancellationToken cancellationToken = default)
    {
        var (kind, scheme) = sourceType.Trim().ToLowerInvariant() switch
        {
            "rtsp_stream" => (MediaSourceType.RtspStream, "rtsp"),
            "srt_stream" => (MediaSourceType.SrtStream, "srt"),
            "custom_stream" => (MediaSourceType.CustomStream, string.Empty),
            _ => throw new MediaServiceException("仅支持 RTSP、SRT 或自定义媒体流。"),
        };
        var normalized = ValidateStreamUrl(url, scheme);
        return writes.ExecuteAsync(async (database, token) =>
        {
            var effectiveFolder = await OptionalFolderIdAsync(database, folderId, token).ConfigureAwait(false);
            var source = new MediaSource
            {
                SourceType = kind,
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? SafeStreamName(normalized)
                    : ValidateName(displayName, "显示名称不能为空"),
                Uri = normalized,
                IsAvailable = true,
                MetadataJson = "{\"stream_status\":\"unverified\"}",
                FolderId = effectiveFolder,
                KeepAlive = preheatEnabled,
                SourceRevision = 1,
                CreatedAt = _timeProvider.GetUtcNow(),
            };
            database.MediaSources.Add(source);
            await database.SaveChangesAsync(token).ConfigureAwait(false);
            return ToSourceDto(source);
        }, cancellationToken);
    }

    private static string ValidateStreamUrl(string url, string scheme)
    {
        var trimmed = url.Trim();
        if (trimmed.Any(char.IsControl) || !Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) ||
            !(scheme.Length == 0
                ? CustomStreamSchemes.Contains(parsed.Scheme, StringComparer.OrdinalIgnoreCase)
                : parsed.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(parsed.Host))
            throw new MediaServiceException(scheme.Length == 0
                ? "自定义媒体流仅支持 http(s)、udp、rtp 或 rtmp 主机地址。"
                : $"直播源地址必须是有效的 {scheme}:// 主机地址。");
        if (parsed.UserInfo.Length > 0 &&
            (parsed.Scheme is not ("rtsp" or "http" or "https" or "rtmp") || parsed.UserInfo[0] == ':'))
            throw new MediaServiceException("该直播协议不支持内嵌凭据，或凭据缺少用户名。RTSP、HTTP(S)、RTMP可使用用户名:密码@主机。");
        return parsed.AbsoluteUri;
    }

    /// <summary>:param address: 真实保存地址。:returns: 仅生成名称时脱敏，不修改播放器收到的URI。</summary>
    private static string SafeStreamName(string address)
    {
        var label = (string)LogRedaction.Value("source_uri", address)!;
        return label[..Math.Min(label.Length, 80)];
    }
}
