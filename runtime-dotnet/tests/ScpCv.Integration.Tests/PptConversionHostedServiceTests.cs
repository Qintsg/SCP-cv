// 后台 PPT 作业用可替代转换器发布页图，不依赖真实 Office。
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScpCv.ControlHost.Media;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class PptConversionHostedServiceTests
{
    [Fact]
    public async Task QueuedPptBecomesAvailableOnlyAfterFakeConverterPublishesAllPages()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var options = new DataRootOptions { RootPath = fixture.TemporaryRoot };
        var media = new MediaSourceService(fixture.Database, fixture.Writes, fixture.Database, new MediaStorageOptions());
        await using var upload = new MemoryStream("fake-ppt"u8.ToArray());
        var source = await media.AddUploadedAsync(upload, "deck.pptx", null, null, null, null, false, false);
        var preparation = new MediaPreparationService(fixture.Database, fixture.Writes, options);
        using var hosted = new PptConversionHostedService(
            preparation,
            new FakeSlideConverter(),
            fixture.Database,
            options,
            NullLogger<PptConversionHostedService>.Instance);

        await hosted.StartAsync(CancellationToken.None);
        try
        {
            var ready = false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!timeout.IsCancellationRequested)
            {
                await using var database = fixture.Database.CreateDbContext();
                var current = await database.MediaSources.Include(item => item.PptResources)
                    .SingleAsync(item => item.Id == source.Id);
                if (current.IsAvailable && current.PptResources.Count == 2) { ready = true; break; }
                await Task.Delay(100, timeout.Token);
            }
            Assert.True(ready, "后台准备作业未在预算内发布两页图片。");
        }
        finally { await hosted.StopAsync(CancellationToken.None); }
    }

    private sealed class FakeSlideConverter : IPptSlideConverter
    {
        public bool IsAvailable => true;

        public async Task<SlideConversionResult> ConvertAsync(
            Guid jobId,
            long sourceRevision,
            string sourcePath,
            string outputDirectory,
            CancellationToken cancellationToken = default)
        {
            byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];
            await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "page-0001.png"), png, cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "page-0002.png"), png, cancellationToken);
            return new SlideConversionResult(OperationStatus.Succeeded, 2, "ok", string.Empty);
        }
    }
}
