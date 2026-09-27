// PPT 默认页图、实验性原生模式与准备失败的选择策略。
using System.Text.Json;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Tests;

public sealed class PresentationPlaybackSelectorTests
{
    [Fact]
    public void DefaultPptSelectsPreparedSlideImagesAndExperimentalSelectsNative()
    {
        var source = new MediaSource
        {
            SourceType = MediaSourceType.Presentation,
            Uri = "deck.pptx",
            ContentDigest = "sha256:deck",
            MetadataJson = JsonSerializer.Serialize(new
            {
                slide_images = new
                {
                    status = "ready",
                    source_digest = "sha256:deck",
                    directory = "C:\\cache\\slides",
                    page_count = 3,
                },
            }),
        };

        var defaultMode = PresentationPlaybackSelector.Select(source, experimentalEnabled: false);
        var nativeMode = PresentationPlaybackSelector.Select(source, experimentalEnabled: true);

        Assert.Equal(PlaybackMode.SlideImages, defaultMode.Mode);
        Assert.Equal("C:\\cache\\slides", defaultMode.SlidesDirectory);
        Assert.Equal(3, defaultMode.PageCount);
        Assert.Equal(PlaybackMode.PowerPoint, nativeMode.Mode);
    }

    [Fact]
    public void MissingOrStaleImagesCannotSilentlyOpenNativePowerPoint()
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
            PresentationPlaybackSelector.Select(source, experimentalEnabled: false));
    }

    [Fact]
    public void PdfPresentationRemainsPdfWithoutOffice()
    {
        var source = new MediaSource { SourceType = MediaSourceType.Presentation, Uri = "deck.pdf" };

        Assert.Equal(PlaybackMode.Pdf, PresentationPlaybackSelector.Select(source, experimentalEnabled: false).Mode);
        Assert.Equal(PlaybackMode.Pdf, PresentationPlaybackSelector.Select(source, experimentalEnabled: true).Mode);
    }
}
