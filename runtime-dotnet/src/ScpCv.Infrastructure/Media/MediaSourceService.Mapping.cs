// 媒体与文件夹的 REST 投影，显式区分页图准备状态。
using System.Text.Json;
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
    /// 投影默认LibreOffice原件能力，静态准备失败不会伪装原件缺失。
    /// :param source: 持久媒体源。
    /// :returns: 默认打开能力与准备状态。
    /// </summary>
    public static MediaSourceDto ToSourceDto(MediaSource source) => ToSourceDto(source, experimentalEnabled: false);

    /// <summary>
    /// 历史调用签名兼容；实验设置已退役且不会改变默认模式。
    /// :param source: 持久媒体源。
    /// :param experimentalEnabled: 历史字段，忽略此值；不产生PowerPoint副作用。
    /// :returns: 页图准备信息与当前可尝试打开能力。
    /// </summary>
    public static MediaSourceDto ToSourceDto(MediaSource source, bool experimentalEnabled) =>
        ToSourceDtoCore(source, artifactRoot: null);

    /// <summary>:param source: 当前媒体源。:param artifactRoot: 当前主机显式受管理制品根。:returns: 同一文件门禁下的预览与准备投影。</summary>
    public static MediaSourceDto ToSourceDto(MediaSource source, string artifactRoot) =>
        ToSourceDtoCore(source, artifactRoot);

    /// <summary>:param source: 持久媒体源。:param artifactRoot: 空表示仅纯元数据兼容投影，不宣称物理预览可用。:returns: 不信任历史资源行图地址的DTO。</summary>
    private static MediaSourceDto ToSourceDtoCore(MediaSource source, string? artifactRoot)
    {
        ArgumentNullException.ThrowIfNull(source);
        var metadata = ParseMetadata(source.MetadataJson);
        var playbackMode = SourceTypeName(source.SourceType) == "ppt"
            ? (Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "libreoffice")
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
        PresentationPreviewArtifact? preview = null;
        if (playbackMode == "libreoffice")
        {
            if (artifactRoot is null)
            {
                // 兼容纯策略调用的元数据页数，不把缺少主机根的投影当物理文件验收。
                pageCount = SelectPresentation(source, experimentalEnabled: true)?.PageCount ?? 0;
            }
            else
            {
                preview = PresentationPreviewArtifact.TrySelect(source, artifactRoot, fitText: false);
                pageCount = preview?.PageCount ?? 0;
            }
            // 旧 ready 可能属于旧摘要或残缺清单；不宣称准备完成，让现有编辑入口可显式重试。
            if (pageCount == 0 && preparationState == "ready") preparationState = string.Empty;
            // false可能仅代表页图转换排队/失败；原件能力独立计算。
            isAvailable = File.Exists(source.Uri) && !string.IsNullOrWhiteSpace(source.ContentDigest);
        }
        var (previewUrl, previewKind, previewLabel) = Preview(source, preview);
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
    /// 默认模式不依赖已退役的实验设置；保持列表与写入投影一致。
    /// :param source: 持久媒体源。
    /// :param database: 当前读取或写入事务的数据库。
    /// :param cancellationToken: 取消令牌。
    /// :returns: 当前门禁下的媒体投影。
    /// </summary>
    private Task<MediaSourceDto> ProjectSourceAsync(
        MediaSource source,
        ControlDbContext database,
        CancellationToken cancellationToken) =>
        ProjectSourceAsync(source, database, _artifactRoot, cancellationToken);

    /// <summary>:param source: 媒体源。:returns: 当前实例明确根下的列表投影，不推导URI或连接串。</summary>
    private MediaSourceDto ProjectManagedSource(MediaSource source) => ToSourceDto(source, _artifactRoot);

    /// <summary>:param source: 持久媒体源。:param database: 当前事务。:param cancellationToken: 寿命。:param artifactRoot: 调用服务显式根。:returns: 根已贯穿的预览投影。</summary>
    internal static Task<MediaSourceDto> ProjectSourceAsync(
        MediaSource source,
        ControlDbContext database,
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ToSourceDto(source, artifactRoot));
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

    private static (string Url, string Kind, string Label) Preview(MediaSource source, PresentationPreviewArtifact? artifact)
    {
        if (source.SourceType == MediaSourceType.Presentation)
        {
            return artifact is not null
                ? (PresentationPreviewArtifact.PageUrl(source.Id, 1, fitText: false), "image", "演示文稿原排版第一页缩略图")
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
