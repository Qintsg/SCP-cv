// 显式静态制品的畸形元数据必须可诊断地拒绝，不回退动态播放或混用文字布局。
using System.Text.Json;
using System.Text.Json.Nodes;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Tests;

public sealed class PresentationMetadataBoundaryTests
{
    [Theory]
    [InlineData("source_revision", "9223372036854775808")]
    [InlineData("source_revision", "-9223372036854775809")]
    [InlineData("source_revision", "1e999")]
    [InlineData("source_revision", "1.5")]
    [InlineData("source_revision", "\"1\"")]
    [InlineData("source_revision", "null")]
    [InlineData("page_count", "2147483648")]
    [InlineData("page_count", "-2147483649")]
    [InlineData("page_count", "1e999")]
    [InlineData("page_count", "1.5")]
    [InlineData("page_count", "\"3\"")]
    [InlineData("page_count", "null")]
    public void InvalidNumericMetadataRejectsTheRequestedStaticModeWithReadableReason(string field, string value)
    {
        using var pdf = new TemporaryPdf();
        foreach (var mode in new[] { "pdf", "slide_images" })
        foreach (var fitText in new[] { false, true })
        {
            var artifact = ValidArtifact(pdf.FilePath);
            artifact[field] = JsonNode.Parse(value);
            var source = Source(ArtifactKey(mode, fitText), artifact);

            var error = Assert.Throws<PresentationPreparationException>(() =>
                PresentationPlaybackSelector.Select(source, mode, fitText));

            Assert.Contains(mode == "pdf" ? "PDF" : "页图", error.Message, StringComparison.Ordinal);
            Assert.Equal(PlaybackMode.LibreOffice, PresentationPlaybackSelector.Select(source).Mode);
        }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("\"not-an-object\"")]
    [InlineData("{")]
    public void InvalidMetadataContainerRejectsBothLayoutsAndPreservesRequestedModeReason(string metadata)
    {
        using var pdf = new TemporaryPdf();
        var source = Source("static_pdf", ValidArtifact(pdf.FilePath));
        source.MetadataJson = metadata;
        foreach (var mode in new[] { "pdf", "slide_images" })
        foreach (var fitText in new[] { false, true })
        {
            var error = Assert.Throws<PresentationPreparationException>(() =>
                PresentationPlaybackSelector.Select(source, mode, fitText));
            Assert.Contains(mode == "pdf" ? "PDF" : "页图", error.Message, StringComparison.Ordinal);
            Assert.Equal(PlaybackMode.LibreOffice, PresentationPlaybackSelector.Select(source).Mode);
        }
    }

    [Theory]
    [InlineData("pdf", false)]
    [InlineData("pdf", true)]
    [InlineData("slide_images", false)]
    [InlineData("slide_images", true)]
    public void PreparedOppositeLayoutCannotSatisfyAnExplicitStaticRequest(string mode, bool fitText)
    {
        using var pdf = new TemporaryPdf();
        var source = Source(ArtifactKey(mode, !fitText), ValidArtifact(pdf.FilePath));
        Assert.Throws<PresentationPreparationException>(() =>
            PresentationPlaybackSelector.Select(source, mode, fitText));
    }

    [Theory]
    [InlineData("pdf", false)]
    [InlineData("pdf", true)]
    [InlineData("slide_images", false)]
    [InlineData("slide_images", true)]
    public void MatchingDigestCannotMakeAnOldSourceRevisionPlayable(string mode, bool fitText)
    {
        using var pdf = new TemporaryPdf();
        var artifact = ValidArtifact(pdf.FilePath);
        artifact["source_revision"] = 0;
        var source = Source(ArtifactKey(mode, fitText), artifact);
        Assert.Throws<PresentationPreparationException>(() =>
            PresentationPlaybackSelector.Select(source, mode, fitText));
    }

    [Theory]
    [InlineData("pdf", false)]
    [InlineData("pdf", true)]
    [InlineData("slide_images", false)]
    [InlineData("slide_images", true)]
    public void ValidNumericUpperBoundsRemainPlayableForTheRequestedLayout(string mode, bool fitText)
    {
        using var pdf = new TemporaryPdf();
        var artifact = ValidArtifact(pdf.FilePath);
        artifact["source_revision"] = long.MaxValue;
        artifact["page_count"] = 500;
        var source = Source(ArtifactKey(mode, fitText), artifact);
        source.SourceRevision = long.MaxValue;

        var selection = PresentationPlaybackSelector.Select(source, mode, fitText);

        Assert.Equal(mode == "pdf" ? PlaybackMode.Pdf : PlaybackMode.SlideImages, selection.Mode);
        Assert.Equal(500, selection.PageCount);
    }

    /// <summary>其余条件全部就绪，使数字或布局缺陷成为唯一拒绝原因。</summary>
    private static JsonObject ValidArtifact(string pdfPath) => new()
    {
        ["status"] = "ready", ["source_digest"] = "fixture-digest", ["source_revision"] = 1,
        ["page_count"] = 3, ["render_semantics"] = "raster_pages", ["path"] = pdfPath,
        ["directory"] = Path.GetDirectoryName(pdfPath),
    };

    /// <summary>默认与适配布局采用生产合同中的独立键。</summary>
    private static string ArtifactKey(string mode, bool fitText) =>
        (fitText ? "fitted_" : string.Empty) + (mode == "pdf" ? "static_pdf" : "slide_images");

    /// <summary>通过公开选择器验证，不替换其内部读取逻辑。</summary>
    private static MediaSource Source(string key, JsonObject artifact) => new()
    {
        SourceType = MediaSourceType.Presentation, Uri = "原件.pptx", SourceRevision = 1,
        ContentDigest = "fixture-digest", MetadataJson = JsonSerializer.Serialize(new JsonObject { [key] = artifact }),
    };

    /// <summary>文件存在是选择器前置；不启动PDF解析器，仅管理测试自有文件。</summary>
    private sealed class TemporaryPdf : IDisposable
    {
        public string FilePath { get; } = Path.Combine(Path.GetTempPath(), "scp-cv-metadata-" + Guid.NewGuid().ToString("N") + ".pdf");

        public TemporaryPdf() => File.WriteAllText(FilePath, "%PDF-1.4\n%%EOF\n");

        public void Dispose() => File.Delete(FilePath);
    }
}
