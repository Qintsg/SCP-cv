// PPT 根据持久实验开关选择页图或原生放映，拒绝缺失和过期页图。
using System.Text.Json;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Presentations;

public sealed record PresentationSelection(PlaybackMode Mode, string SlidesDirectory = "", int PageCount = 0);

public static class PresentationPlaybackSelector
{
    public static PresentationSelection Select(MediaSource source, bool experimentalEnabled)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceType != MediaSourceType.Presentation)
            throw new ArgumentException("只有演示文稿源可选择页图播放模式。", nameof(source));
        if (Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return new PresentationSelection(PlaybackMode.Pdf);
        if (experimentalEnabled)
            return new PresentationSelection(PlaybackMode.PowerPoint);

        try
        {
            using var metadata = JsonDocument.Parse(source.MetadataJson);
            if (metadata.RootElement.TryGetProperty("slide_images", out var images) &&
                images.ValueKind == JsonValueKind.Object &&
                ReadString(images, "status") == "ready" &&
                string.Equals(ReadString(images, "source_digest"), source.ContentDigest, StringComparison.OrdinalIgnoreCase) &&
                images.TryGetProperty("page_count", out var count) && count.TryGetInt32(out var pages) &&
                pages is >= 1 and <= 500)
            {
                var directory = ReadString(images, "directory");
                if (!string.IsNullOrWhiteSpace(directory))
                    return new PresentationSelection(PlaybackMode.SlideImages, directory, pages);
            }
        }
        catch (JsonException) { }
        throw new PresentationPreparationException("PPT 逐页图片尚未准备完成；默认播放不会自动启动 PowerPoint。");
    }

    private static string ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
}

public sealed class PresentationPreparationException(string message) : Exception(message);
