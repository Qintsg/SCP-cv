// 文稿预览按明确布局选择制品，备注/媒体资源保持既有DTO语义。
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
        CancellationToken cancellationToken = default) =>
        await GetPptSlideImageAsync(sourceId, page, false, cancellationToken).ConfigureAwait(false);

    /// <summary>:param sourceId: 文稿标识。:param page: 页码。:param fitText: 布局。:param cancellationToken: 取消令牌。:returns: 所选布局的PNG。</summary>
    public async Task<MediaFileResult> GetPptSlideImageAsync(
        long sourceId,
        int page,
        bool fitText,
        CancellationToken cancellationToken = default)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (source.SourceType != MediaSourceType.Presentation)
            throw new MediaServiceException("只有演示文稿源支持逐页图片。", isNotFound: true);
        var artifact = PresentationPreviewArtifact.TrySelect(source, PreviewArtifactRoot, fitText)
            ?? throw new MediaServiceException("所选文字布局的演示文稿逐页图片尚未准备完成。", isNotFound: true);
        if (page < 1 || page > artifact.PageCount)
            throw new MediaServiceException($"演示文稿第 {page} 页不存在。", isNotFound: true);
        var file = artifact.PagePath(page);
        if (!File.Exists(file)) throw new MediaServiceException("页图文件已缺失。", isNotFound: true);
        return new MediaFileResult(file, "image/png", Path.GetFileName(file), Download: false);
    }

    public async Task<IReadOnlyList<PptResourceDto>> GetPptResourcesAsync(
        long sourceId,
        CancellationToken cancellationToken = default) =>
        await GetPptResourcesAsync(sourceId, false, cancellationToken).ConfigureAwait(false);

    /// <summary>:param sourceId: 文稿标识。:param fitText: 布局。:param cancellationToken: 取消令牌。:returns: 所选布局的预览及原有备注/媒体。</summary>
    public async Task<IReadOnlyList<PptResourceDto>> GetPptResourcesAsync(
        long sourceId,
        bool fitText,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var resources = await database.PptResources.AsNoTracking()
            .Where(resource => resource.SourceId == sourceId)
            .OrderBy(resource => resource.PageIndex)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var source = await database.MediaSources.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sourceId, cancellationToken).ConfigureAwait(false)
            ?? throw new MediaServiceException($"媒体源 id={sourceId} 不存在", isNotFound: true);
        var artifact = PresentationPreviewArtifact.TrySelect(source, PreviewArtifactRoot, fitText);
        if (artifact is null) return resources.Select(resource =>
            ToPptResourceDto(resource, null) with { SlideImage = string.Empty, NextSlideImage = string.Empty }).ToArray();
        var byPage = resources.ToDictionary(resource => resource.PageIndex);
        return Enumerable.Range(1, artifact.PageCount).Select(page =>
        {
            var resource = byPage.GetValueOrDefault(page) ?? new PptResource
            {
                SourceId = sourceId, PageIndex = page, CreatedAt = source.CreatedAt,
            };
            return ToPptResourceDto(resource, null) with
            {
                SlideImage = PresentationPreviewArtifact.PageUrl(sourceId, page, fitText),
                NextSlideImage = page < artifact.PageCount ? PresentationPreviewArtifact.PageUrl(sourceId, page + 1, fitText) : string.Empty,
            };
        }).ToArray();
    }

    private string PreviewArtifactRoot => Path.Combine(controlDbFactory.Layout.RootPath, "cache", "artifacts");

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
