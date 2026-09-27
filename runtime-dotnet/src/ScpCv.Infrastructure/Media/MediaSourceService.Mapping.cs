// 媒体与文件夹的 REST 投影，显式区分页图准备状态。
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

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

    public static MediaSourceDto ToSourceDto(MediaSource source)
    {
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
                        slideImages.TryGetProperty("page_count", out var pages) && pages.TryGetInt32(out var count)
            ? count
            : 0;
        var (previewUrl, previewKind, previewLabel) = Preview(source);
        return new MediaSourceDto
        {
            Id = source.Id,
            SourceType = SourceTypeName(source.SourceType),
            Name = source.Name,
            Uri = source.Uri,
            IsAvailable = source.IsAvailable,
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
