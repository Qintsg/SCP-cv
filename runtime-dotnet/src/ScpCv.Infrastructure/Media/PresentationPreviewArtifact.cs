// 文稿预览制品选择；显式接收受管理根，不从原件路径推导缓存归属。
using System.Globalization;
using System.Text.Json;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Media;

public sealed record PresentationPreviewArtifact(string Directory, int PageCount)
{
    /// <summary>:param page: 已校验范围的页码。:returns: 制品中的连续PNG路径。</summary>
    public string PagePath(int page) => Path.Combine(Directory, $"page-{page:0000}.png");

    /// <summary>
    /// :param source: 当前原件及其制品元数据。
    /// :param artifactRoot: 当前ControlHost明确配置的受管理制品根。
    /// :param fitText: 显式选择的文字布局。
    /// :returns: 当前布局的可选择制品；未准备或版本不匹配时为空。
    /// </summary>
    public static PresentationPreviewArtifact? TrySelect(MediaSource source, string artifactRoot, bool fitText)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        if (source.SourceType != MediaSourceType.Presentation ||
            Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return null;
        PresentationSelection selected;
        try { selected = PresentationPlaybackSelector.Select(source, "slide_images", fitText); }
        catch (PresentationPreparationException) { return null; }
        if (!MatchesDeclaredTextLayout(source.MetadataJson, fitText)) return null;
        try
        {
            var root = Path.GetFullPath(artifactRoot);
            var directory = Path.GetFullPath(selected.SlidesDirectory);
            var relative = Path.GetRelativePath(root, directory);
            if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                Path.IsPathRooted(relative) || !HasPlainDirectoryChain(root, directory)) return null;
            var artifact = new PresentationPreviewArtifact(directory, selected.PageCount);
            return HasAllPublishedPages(artifact) ? artifact : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 坏路径、消失/占用文件及重解析点均不代表制品就绪，不泄漏元数据中的路径值。
            return null;
        }
    }

    /// <summary>:param sourceId: 文稿标识。:param page: 页码。:param fitText: 布局。:returns: 不混用布局的HTTP URL。</summary>
    public static string PageUrl(long sourceId, int page, bool fitText) =>
        $"/api/sources/{sourceId.ToString(CultureInfo.InvariantCulture)}/slides/{page.ToString(CultureInfo.InvariantCulture)}/" +
        (fitText ? "?fit_text=true" : string.Empty);

    /// <summary>:param metadataJson: 已通过选择器的制品元数据。:param fitText: 所选布局。:returns: 存在的布局声明必须一致；历史缺声明按已有键契约兼容。</summary>
    private static bool MatchesDeclaredTextLayout(string metadataJson, bool fitText)
    {
        try
        {
            using var metadata = JsonDocument.Parse(metadataJson);
            if (metadata.RootElement.ValueKind != JsonValueKind.Object ||
                !metadata.RootElement.TryGetProperty(fitText ? "fitted_slide_images" : "slide_images", out var images) ||
                images.ValueKind != JsonValueKind.Object) return false;
            return !images.TryGetProperty("text_layout", out var declared) ||
                declared.ValueKind == JsonValueKind.String &&
                string.Equals(declared.GetString(), fitText ? "fit" : "original", StringComparison.Ordinal);
        }
        catch (JsonException) { return false; }
    }

    /// <summary>:param root: 明确制品根。:param directory: 已验证词法范围的目录。:returns: 每层目录均存在且无重解析点。</summary>
    private static bool HasPlainDirectoryChain(string root, string directory)
    {
        DirectoryInfo? current = new(directory);
        while (current is not null)
        {
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            if (string.Equals(current.FullName, root, StringComparison.OrdinalIgnoreCase)) return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>:param artifact: 所选布局制品。:returns: 所有连续页通过与正式发布相同的长度/PNG签名门禁；不冒称完整图像解码。</summary>
    private static bool HasAllPublishedPages(PresentationPreviewArtifact artifact)
    {
        // 只枚举当前目录且最多读取N+1条：数量或连续文件名漂移均不宣称完整，不遍历子目录。
        var files = System.IO.Directory.EnumerateFiles(artifact.Directory, "page-*.png", SearchOption.TopDirectoryOnly)
            .Take(artifact.PageCount + 1).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (files.Length != artifact.PageCount) return false;
        ReadOnlySpan<byte> pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        Span<byte> prefix = stackalloc byte[8];
        for (var page = 1; page <= artifact.PageCount; page++)
        {
            var path = artifact.PagePath(page);
            if (!string.Equals(files[page - 1], path, StringComparison.OrdinalIgnoreCase)) return false;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length <= pngSignature.Length) return false;
            input.ReadExactly(prefix);
            if (!prefix.SequenceEqual(pngSignature)) return false;
        }
        return true;
    }
}
