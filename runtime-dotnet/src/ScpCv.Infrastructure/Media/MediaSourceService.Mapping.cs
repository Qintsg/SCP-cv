// 媒体与文件夹的 REST 投影，显式区分页图准备状态。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    private static MediaFolderDto ToFolderDto(MediaFolder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        ParentId = folder.ParentId,
        CreatedAt = folder.CreatedAt.ToString("O"),
        UpdatedAt = folder.UpdatedAt.ToString("O"),
    };

    /// <summary>
    /// 投影默认页图模式的媒体状态，不写回旧库中的可用标记。
    /// :param source: 持久媒体源。
    /// :returns: 默认打开能力与准备状态。
    /// </summary>
    public static MediaSourceDto ToSourceDto(MediaSource source) => ToSourceDto(source, experimentalEnabled: false);

    /// <summary>
    /// 将 PPT 可尝试打开能力与当前实验设置对齐；不代表已连通或实际出画。
    /// :param source: 持久媒体源。
    /// :param experimentalEnabled: 后续打开使用的持久原生放映门禁。
    /// :returns: 页图准备信息与当前可尝试打开能力。
    /// </summary>
    public static MediaSourceDto ToSourceDto(MediaSource source, bool experimentalEnabled)
    {
        ArgumentNullException.ThrowIfNull(source);
        var metadata = ParseMetadata(source.MetadataJson);
        var playbackMode = SourceTypeName(source.SourceType) == "ppt"
            ? (Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "slide_images")
            : string.Empty;
        var slideImages = metadata.TryGetValue("slide_images", out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
        var preparationState = slideImages.ValueKind == JsonValueKind.Object
            ? ReadString(slideImages, "status") ?? string.Empty
            : string.Empty;
        var pageCount = slideImages.ValueKind == JsonValueKind.Object &&
                        slideImages.TryGetProperty("page_count", out var pages) &&
                        pages.ValueKind == JsonValueKind.Number && pages.TryGetInt32(out var count)
            ? count
            : 0;
        var isAvailable = source.IsAvailable;
        if (playbackMode == "slide_images")
        {
            var prepared = SelectPresentation(source, experimentalEnabled: false);
            pageCount = prepared?.PageCount ?? 0;
            // 旧 ready 可能属于旧摘要或残缺清单；不宣称准备完成，让现有编辑入口可显式重试。
            if (prepared is null && preparationState == "ready") preparationState = string.Empty;
            // false 可能仅代表页图转换排队/失败；明确实验模式仍可尝试原件。
            isAvailable = experimentalEnabled
                ? SelectPresentation(source, experimentalEnabled: true) is not null
                : source.IsAvailable && prepared is not null;
        }
        var (previewUrl, previewKind, previewLabel) = Preview(source);
        return new MediaSourceDto
        {
            Id = source.Id,
            SourceType = SourceTypeName(source.SourceType),
            Name = source.Name,
            Uri = source.Uri,
            IsAvailable = isAvailable,
            StreamIdentifier = source.StreamIdentifier,
            FolderId = source.FolderId,
            OriginalFilename = source.OriginalFilename,
            FileSize = source.FileSize,
            MimeType = source.MimeType,
            IsTemporary = source.IsTemporary,
            ExpiresAt = source.ExpiresAt?.ToString("O"),
            Metadata = metadata,
            KeepAlive = source.KeepAlive,
            PreheatEnabled = source.KeepAlive,
            PlaybackMode = playbackMode,
            PreparationState = preparationState,
            PageCount = pageCount,
            PreviewUrl = previewUrl,
            ThumbnailUrl = previewUrl,
            PreviewKind = previewKind,
            PreviewLabel = previewLabel,
            CreatedAt = source.CreatedAt.ToString("O"),
        };
    }

    /// <summary>
    /// 在当前事务读取和 OPEN 相同的持久设置，保持列表及写入响应一致。
    /// :param source: 持久媒体源。
    /// :param database: 当前读取或写入事务的数据库。
    /// :param cancellationToken: 取消令牌。
    /// :returns: 当前门禁下的媒体投影。
    /// </summary>
    internal static async Task<MediaSourceDto> ProjectSourceAsync(
        MediaSource source,
        ControlDbContext database,
        CancellationToken cancellationToken)
    {
        var experimental = source.SourceType == MediaSourceType.Presentation &&
                           !Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase) &&
                           await database.RuntimeStates.AsNoTracking()
                               .Select(state => state.ExperimentalPowerPointEnabled)
                               .SingleAsync(cancellationToken).ConfigureAwait(false);
        return ToSourceDto(source, experimental);
    }

    /// <summary>
    /// PPT 可尝试播放必须通过实际打开入口相同的模式选择规则。
    /// :param source: 持久媒体源。
    /// :param experimentalEnabled: 后续打开使用的持久原生放映门禁。
    /// :returns: 当前可选择模式；默认页图未准备时返回空。
    /// </summary>
    private static PresentationSelection? SelectPresentation(MediaSource source, bool experimentalEnabled)
    {
        try
        {
            return PresentationPlaybackSelector.Select(source, experimentalEnabled);
        }
        catch (PresentationPreparationException)
        {
            return null;
        }
    }

    public static string SourceTypeName(MediaSourceType value) => value switch
    {
        MediaSourceType.Presentation => "ppt",
        MediaSourceType.Video => "video",
        MediaSourceType.Audio => "audio",
        MediaSourceType.Image => "image",
        MediaSourceType.Web => "web",
        MediaSourceType.CustomStream => "custom_stream",
        MediaSourceType.RtspStream => "rtsp_stream",
        MediaSourceType.SrtStream => "srt_stream",
        _ => string.Empty,
    };

    private static (string Url, string Kind, string Label) Preview(MediaSource source)
    {
        if (source.SourceType == MediaSourceType.Presentation)
        {
            var first = source.PptResources.OrderBy(resource => resource.PageIndex).FirstOrDefault()?.SlideImage ?? string.Empty;
            return first.Length > 0
                ? (first, "image", "演示文稿第一页缩略图")
                : (string.Empty, "icon", "演示文稿暂无缩略图");
        }

        if (source.SourceType == MediaSourceType.Image)
        {
            return ($"/api/sources/{source.Id}/preview/", "image", "图片缩略图");
        }

        if (source.SourceType == MediaSourceType.Video)
        {
            return ($"/api/sources/{source.Id}/preview/", "video", "视频封面");
        }

        return (string.Empty, "icon", "无预览");
    }

    private static Dictionary<string, JsonElement> ParseMetadata(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadString(JsonElement value, string key) =>
        value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
}
