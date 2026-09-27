using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    public async Task<MediaFileResult> GetPptSlideImageAsync(
        long sourceId,
        int page,
        CancellationToken cancellationToken = default)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (source.SourceType != MediaSourceType.Presentation)
            throw new MediaServiceException("只有演示文稿源支持逐页图片。", isNotFound: true);
        JsonElement images;
        try
        {
            using var metadata = JsonDocument.Parse(source.MetadataJson);
            if (!metadata.RootElement.TryGetProperty("slide_images", out var parsed) ||
                parsed.ValueKind != JsonValueKind.Object ||
                !parsed.TryGetProperty("status", out var status) || status.GetString() != "ready" ||
                !parsed.TryGetProperty("source_digest", out var digest) ||
                digest.GetString() != source.ContentDigest ||
                !parsed.TryGetProperty("page_count", out var pages) || pages.ValueKind != JsonValueKind.Number ||
                !parsed.TryGetProperty("directory", out var location) || location.ValueKind != JsonValueKind.String)
                throw new MediaServiceException("演示文稿逐页图片尚未准备完成。", isNotFound: true);
            images = parsed.Clone();
        }
        catch (JsonException)
        {
            throw new MediaServiceException("演示文稿逐页图片元数据无效。", isNotFound: true);
        }
        var pageCount = images.GetProperty("page_count").GetInt32();
        if (page < 1 || page > pageCount)
            throw new MediaServiceException($"演示文稿第 {page} 页不存在。", isNotFound: true);
        var directory = Path.GetFullPath(images.GetProperty("directory").GetString() ?? string.Empty);
        var artifactRoot = Path.Combine(controlDbFactory.Layout.RootPath, "cache", "artifacts");
        if (!IsWithinRoot(directory, artifactRoot))
            throw new MediaServiceException("页图目录超出受管理制品范围。");
        var file = Path.Combine(directory, $"page-{page:0000}.png");
        if (!File.Exists(file)) throw new MediaServiceException("页图文件已缺失。", isNotFound: true);
        return new MediaFileResult(file, "image/png", Path.GetFileName(file), Download: false);
    }

    public async Task<IReadOnlyList<PptResourceDto>> GetPptResourcesAsync(
        long sourceId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var resources = await database.PptResources.AsNoTracking()
            .Where(resource => resource.SourceId == sourceId)
            .OrderBy(resource => resource.PageIndex)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (!await database.MediaSources.AnyAsync(source => source.Id == sourceId, cancellationToken).ConfigureAwait(false))
        {
            throw new MediaServiceException($"媒体源 id={sourceId} 不存在", isNotFound: true);
        }

        return resources.Select((resource, index) => ToPptResourceDto(resource, resources.ElementAtOrDefault(index + 1))).ToArray();
    }

    public async Task<IReadOnlyList<PptResourceDto>> ReplacePptResourcesAsync(
        long sourceId,
        IReadOnlyList<PptResourceInput> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return await writes.ExecuteAsync(
            async (database, token) =>
            {
                var source = await database.MediaSources
                    .Include(item => item.PptResources)
                    .SingleOrDefaultAsync(item => item.Id == sourceId, token).ConfigureAwait(false)
                    ?? throw new MediaServiceException($"媒体源 id={sourceId} 不存在", isNotFound: true);
                if (source.SourceType != MediaSourceType.Presentation)
                {
                    throw new MediaServiceException("只有演示文稿媒体源支持 PPT 资源", isNotFound: false);
                }

                var normalized = inputs.Select(NormalizePptResource).ToArray();
                if (normalized.Select(item => item.PageIndex).Distinct().Count() != normalized.Length)
                {
                    throw new MediaServiceException("page_index 不能重复");
                }

                database.PptResources.RemoveRange(source.PptResources);
                source.PptResources.Clear();
                foreach (var item in normalized)
                {
                    source.PptResources.Add(new PptResource
                    {
                        SourceId = sourceId,
                        PageIndex = item.PageIndex,
                        SlideImage = item.SlideImage ?? string.Empty,
                        SpeakerNotes = item.SpeakerNotes ?? string.Empty,
                        MediaItemsJson = JsonSerializer.Serialize(item.MediaItems),
                        CreatedAt = _timeProvider.GetUtcNow(),
                    });
                }

                await database.SaveChangesAsync(token).ConfigureAwait(false);
                var saved = await database.PptResources.AsNoTracking()
                    .Where(item => item.SourceId == sourceId)
                    .OrderBy(item => item.PageIndex)
                    .ToListAsync(token).ConfigureAwait(false);
                return saved
                    .Select((resource, index) => ToPptResourceDto(resource, saved.ElementAtOrDefault(index + 1)))
                    .ToArray();
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static PptResourceInput NormalizePptResource(PptResourceInput value)
    {
        if (value.PageIndex < 1) throw new MediaServiceException("page_index 必须从 1 开始");
        return value with
        {
            SlideImage = value.SlideImage?.Trim() ?? string.Empty,
            SpeakerNotes = value.SpeakerNotes?.Trim() ?? string.Empty,
            MediaItems = value.MediaItems ?? [],
        };
    }

    private static PptResourceDto ToPptResourceDto(PptResource resource, PptResource? next)
    {
        List<PptMediaItemDto> mediaItems;
        try
        {
            mediaItems = JsonSerializer.Deserialize<List<PptMediaItemDto>>(resource.MediaItemsJson) ?? [];
        }
        catch (JsonException)
        {
            mediaItems = [];
        }

        return new PptResourceDto
        {
            Id = resource.Id,
            SourceId = resource.SourceId,
            PageIndex = resource.PageIndex,
            SlideImage = resource.SlideImage,
            NextSlideImage = next?.SlideImage ?? string.Empty,
            SpeakerNotes = resource.SpeakerNotes,
            HasMedia = mediaItems.Count > 0,
            MediaItems = mediaItems,
            CreatedAt = resource.CreatedAt.ToString("O"),
        };
    }
}

public sealed record PptResourceInput
{
    [JsonPropertyName("page_index")]
    public int PageIndex { get; init; }
    [JsonPropertyName("slide_image")]
    public string? SlideImage { get; init; }
    [JsonPropertyName("speaker_notes")]
    public string? SpeakerNotes { get; init; }
    [JsonPropertyName("media_items")]
    public IReadOnlyList<PptMediaItemDto>? MediaItems { get; init; }
}
