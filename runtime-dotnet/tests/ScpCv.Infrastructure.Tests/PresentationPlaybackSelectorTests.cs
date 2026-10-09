// PPT默认LibreOffice与显式静态制品的版本/摘要选择策略。
using System.Text.Json;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Tests;

public sealed class PresentationPlaybackSelectorTests
{
    [Fact]
    public void DefaultPptSelectsLibreOfficeAndExplicitStaticModeSelectsPreparedImages()
    {
        var source = new MediaSource
        {
            SourceType = MediaSourceType.Presentation,
            Uri = "deck.pptx",
            ContentDigest = "sha256:deck",
            SourceRevision = 1,
            MetadataJson = JsonSerializer.Serialize(new
            {
                slide_images = new
                {
                    status = "ready",
                    source_digest = "sha256:deck",
                    source_revision = 1,
                    directory = "C:\\cache\\slides",
                    page_count = 3,
                },
            }),
        };

        var defaultMode = PresentationPlaybackSelector.Select(source, staticImages: false);
        var nativeMode = PresentationPlaybackSelector.Select(source, staticImages: true);

        Assert.Equal(PlaybackMode.LibreOffice, defaultMode.Mode);
        Assert.Equal("C:\\cache\\slides", nativeMode.SlidesDirectory);
        Assert.Equal(3, nativeMode.PageCount);
        Assert.Equal(PlaybackMode.SlideImages, nativeMode.Mode);
    }

    [Fact]
    public void ExplicitStaticModeNeverSilentlyFallsBackToNative()
    {
        var source = new MediaSource
        {
            SourceType = MediaSourceType.Presentation,
            Uri = "deck.pptx",
            ContentDigest = "sha256:new",
            MetadataJson = JsonSerializer.Serialize(new
            {
                slide_images = new { status = "ready", source_digest = "sha256:old", directory = "C:\\cache\\slides", page_count = 3 },
            }),
        };

        Assert.Throws<PresentationPreparationException>(() =>
            PresentationPlaybackSelector.Select(source, staticImages: true));
        Assert.Equal(PlaybackMode.LibreOffice, PresentationPlaybackSelector.Select(source, false).Mode);
    }

    [Fact]
    public void PdfPresentationRemainsPdfWithoutOffice()
    {
        var source = new MediaSource { SourceType = MediaSourceType.Presentation, Uri = "deck.pdf" };

        Assert.Equal(PlaybackMode.Pdf, PresentationPlaybackSelector.Select(source, staticImages: false).Mode);
        Assert.Equal(PlaybackMode.Pdf, PresentationPlaybackSelector.Select(source, staticImages: true).Mode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaticImagesRejectStaleRevisionEvenWhenDigestMatches(bool fitText)
    {
        var artifact = new { status = "ready", source_digest = "same", source_revision = 1,
            directory = "C:\\cache\\stale", page_count = 3 };
        var source = new MediaSource { SourceType = MediaSourceType.Presentation, Uri = "deck.pptx",
            SourceRevision = 2, ContentDigest = "same", MetadataJson = JsonSerializer.Serialize(new
            { slide_images = artifact, fitted_slide_images = artifact }) };
        Assert.Throws<PresentationPreparationException>(() =>
            PresentationPlaybackSelector.Select(source, "slide_images", fitText));
        Assert.Equal(PlaybackMode.LibreOffice, PresentationPlaybackSelector.Select(source).Mode);
    }

    [Theory]
    [InlineData(true, 1L, "new", "raster_pages")]
    [InlineData(false, 2L, "new", "raster_pages")]
    [InlineData(false, 1L, "old", "raster_pages")]
    [InlineData(false, 1L, "new", "interactive")]
    public void StaticPdfRequiresMatchingRevisionDigestAndRasterSemantics(bool accepted, long revision, string digest, string semantics)
    {
        var path = Path.Combine(Path.GetTempPath(), "scp-cv-static-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(path, "%PDF-1.4\n%%EOF");
        try
        {
            var source = new MediaSource
            {
                SourceType = MediaSourceType.Presentation, Uri = "original.pptx", SourceRevision = 1, ContentDigest = "new",
                MetadataJson = JsonSerializer.Serialize(new { static_pdf = new { status = "ready", path, source_revision = revision,
                    source_digest = digest, render_semantics = semantics, page_count = 3 } }),
            };
            if (accepted)
            {
                var selection = PresentationPlaybackSelector.Select(source, "pdf");
                Assert.Equal(PlaybackMode.Pdf, selection.Mode);
                Assert.Equal(path, selection.PlaybackUri);
            }
            else Assert.Throws<PresentationPreparationException>(() => PresentationPlaybackSelector.Select(source, "pdf"));
            Assert.Equal(PlaybackMode.LibreOffice, PresentationPlaybackSelector.Select(source).Mode);
        }
        finally { File.Delete(path); }
    }
}
