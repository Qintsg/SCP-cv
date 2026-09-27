// 直播源登记与 URL 验证，登记不冒充已经连通或实际出画。
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

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
                    ? normalized[..Math.Min(normalized.Length, 80)]
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
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) ||
            !(scheme.Length == 0
                ? CustomStreamSchemes.Contains(parsed.Scheme, StringComparer.OrdinalIgnoreCase)
                : parsed.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(parsed.Host) || parsed.UserInfo.Length > 0)
            throw new MediaServiceException(scheme.Length == 0
                ? "自定义媒体流仅支持 http(s)、udp、rtp 或 rtmp 主机地址，且不能内嵌凭据。"
                : $"直播源地址必须是无内嵌凭据的 {scheme}:// 主机地址。");
        return parsed.AbsoluteUri;
    }
}
