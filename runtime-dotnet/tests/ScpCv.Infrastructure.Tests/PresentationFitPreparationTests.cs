// 文字适配版制品必须与默认原排版隔离，失败也不能破坏默认资源。
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Tests;

public sealed class PresentationFitPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangingLayoutCannotReplayAnUncertainConversion(bool firstFit)
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-fit-preparation-" + Guid.NewGuid().ToString("N"));
        var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
        try
        {
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            await using var bytes = new MemoryStream("不确定保护"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "文稿.pptx", null, null, null, null, false, false);
            var preparation = new MediaPreparationService(factory, writes, new DataRootOptions { RootPath = root });
            var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(job);
            if (firstFit)
            {
                await preparation.PublishSlideImagesAsync(job.JobId, await StageAsync(root, job.JobId), 1);
                await preparation.RetryPptImagesAsync(source.Id, true);
                job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
                Assert.NotNull(job);
            }
            await preparation.MarkUncertainSlideImagesAsync(job.JobId, "转换是否完成未知");
            await Assert.ThrowsAsync<MediaServiceException>(() => preparation.RetryPptImagesAsync(source.Id, !firstFit));
            Assert.Null(await preparation.ClaimNextAsync(PreparationJobKind.PptImages));
        }
        finally { DeleteRoot(factory, root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitFitPreparationNeverReplacesDefault(bool fail)
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-fit-preparation-" + Guid.NewGuid().ToString("N"));
        var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
        try
        {
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            await using var bytes = new MemoryStream("原件保留"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "文稿.pptx", null, null, null, null, false, false);
            var preparation = new MediaPreparationService(factory, writes, new DataRootOptions { RootPath = root });
            var original = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(original);
            var originalStage = await StageAsync(root, original.JobId);
            await preparation.PublishSlideImagesAsync(original.JobId, originalStage, 1);
            await preparation.RetryPptImagesAsync(source.Id, true);
            var fitted = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(fitted);
            Assert.StartsWith("ppt-images-fit-", fitted.RecipeVersion);
            if (fail) await preparation.FailSlideImagesAsync(fitted.JobId, "显式适配失败");
            else await preparation.PublishSlideImagesAsync(fitted.JobId, await StageAsync(root, fitted.JobId), 1);
            await using var database = factory.CreateDbContext();
            var current = await database.MediaSources.Include(item => item.PptResources).SingleAsync(item => item.Id == source.Id);
            var defaultMode = PresentationPlaybackSelector.Select(current, "slide_images");
            Assert.Contains(original.JobId.ToString("N"), defaultMode.SlidesDirectory);
            if (fail) Assert.Throws<PresentationPreparationException>(() => PresentationPlaybackSelector.Select(current, "slide_images", true));
            else Assert.Contains(fitted.JobId.ToString("N"), PresentationPlaybackSelector.Select(current, "slide_images", true).SlidesDirectory);
            Assert.Single(current.PptResources);
            Assert.Equal("原件保留", await File.ReadAllTextAsync(source.Uri));
            using var metadata = JsonDocument.Parse(current.MetadataJson);
            Assert.Equal("ready", metadata.RootElement.GetProperty("slide_images").GetProperty("status").GetString());
        }
        finally { DeleteRoot(factory, root); }
    }

    private static async Task<string> StageAsync(string root, Guid job)
    {
        var directory = Path.Combine(root, "cache", "staging", job.ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, "page-0001.png"), [137, 80, 78, 71, 13, 10, 26, 10, 1]);
        return directory;
    }

    private static void DeleteRoot(ControlDbContextFactory factory, string root)
    {
        using var database = factory.CreateDbContext();
        using var connection = new SqliteConnection(database.Database.GetConnectionString());
        SqliteConnection.ClearPool(connection);
        var full = Path.GetFullPath(root);
        if (Path.GetDirectoryName(full) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("测试目录越界");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}
