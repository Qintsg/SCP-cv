// PPT 默认LibreOffice；显式静态制品校验摘要、修订及页数，并保留所请求模式的诊断。
using System.Text.Json;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Presentations;

public sealed record PresentationSelection(PlaybackMode Mode, string SlidesDirectory = "", int PageCount = 0, string PlaybackUri = "");

public static class PresentationPlaybackSelector
{
    public static PresentationSelection Select(MediaSource source, bool staticImages) => Select(source, staticImages ? "slide_images" : null);

    public static PresentationSelection Select(MediaSource source, string? requestedMode = null, bool fitText = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceType != MediaSourceType.Presentation)
            throw new ArgumentException("只有演示文稿源可选择页图播放模式。", nameof(source));
        if (Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return new PresentationSelection(PlaybackMode.Pdf);
        if (requestedMode is null or "" or "libreoffice")
            return new PresentationSelection(PlaybackMode.LibreOffice);

        try
        {
            using var metadata = JsonDocument.Parse(source.MetadataJson);
            if (requestedMode == "pdf")
            {
                if (metadata.RootElement.ValueKind == JsonValueKind.Object &&
                    metadata.RootElement.TryGetProperty(fitText ? "fitted_static_pdf" : "static_pdf", out var pdf) &&
                    pdf.ValueKind == JsonValueKind.Object &&
                    ReadString(pdf, "status") == "ready" && ReadString(pdf, "render_semantics") == "raster_pages" &&
                    !string.IsNullOrWhiteSpace(source.ContentDigest) &&
                    string.Equals(ReadString(pdf, "source_digest"), source.ContentDigest, StringComparison.OrdinalIgnoreCase) &&
                    pdf.TryGetProperty("source_revision", out var revision) && revision.ValueKind == JsonValueKind.Number &&
                    revision.TryGetInt64(out var revisionValue) && revisionValue == source.SourceRevision &&
                    pdf.TryGetProperty("page_count", out var pdfPages) && pdfPages.ValueKind == JsonValueKind.Number &&
                    pdfPages.TryGetInt32(out var pageCount) && pageCount is >= 1 and <= 500 &&
                    File.Exists(ReadString(pdf, "path")))
                    return new PresentationSelection(PlaybackMode.Pdf, PageCount: pageCount, PlaybackUri: ReadString(pdf, "path"));
                throw new PresentationPreparationException("同源静态PDF尚未准备完成，请使用LibreOffice或重新准备静态页。");
            }
            if (metadata.RootElement.ValueKind == JsonValueKind.Object &&
                metadata.RootElement.TryGetProperty(fitText ? "fitted_slide_images" : "slide_images", out var images) &&
                images.ValueKind == JsonValueKind.Object &&
                ReadString(images, "status") == "ready" &&
                !string.IsNullOrWhiteSpace(source.ContentDigest) &&
                string.Equals(ReadString(images, "source_digest"), source.ContentDigest, StringComparison.OrdinalIgnoreCase) &&
                images.TryGetProperty("source_revision", out var imageRevision) &&
                imageRevision.ValueKind == JsonValueKind.Number && imageRevision.TryGetInt64(out var imageRevisionValue) &&
                imageRevisionValue == source.SourceRevision &&
                images.TryGetProperty("page_count", out var count) && count.ValueKind == JsonValueKind.Number &&
                count.TryGetInt32(out var pages) &&
                pages is >= 1 and <= 500)
            {
                var directory = ReadString(images, "directory");
                if (!string.IsNullOrWhiteSpace(directory))
                    return new PresentationSelection(PlaybackMode.SlideImages, directory, pages);
            }
        }
        catch (JsonException) { }
        throw new PresentationPreparationException(requestedMode == "pdf"
            ? "同源静态PDF尚未准备完成，请使用LibreOffice或重新准备静态页。"
            : "PPT 静态页图尚未准备完成；请选择LibreOffice动态模式或准备页图。");
    }

    private static string ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
}

public sealed class PresentationPreparationException(string message) : Exception(message);
