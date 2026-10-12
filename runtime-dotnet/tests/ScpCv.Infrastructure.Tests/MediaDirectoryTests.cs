// 公开目录路径必须对应受管真实文件名，不泄漏物理根；重名不覆盖。
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class MediaDirectoryTests
{
    [Fact]
    public async Task PathsStartAtRootAndUseActualSavedLeaf()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-directory-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("examplefolder", null);
            await using var bytes = new MemoryStream("one"u8.ToArray());
            await using var again = new MemoryStream("two"u8.ToArray());
            var first = await media.AddUploadedAsync(bytes, "example2.png", null, null, null, folder.Id, false, false);
            var second = await media.AddUploadedAsync(again, "example2.png", null, null, null, folder.Id, false, false);
            Assert.Equal("/examplefolder", folder.RelativePath);
            Assert.Equal("/examplefolder/example2.png", PublicPath(first));
            Assert.Equal("/examplefolder/example2 (2).png", PublicPath(second));
            var moved = await media.MoveSourceAsync(first.Id, null);
            Assert.Equal("/example2.png", PublicPath(moved));
            Assert.Equal("one", await File.ReadAllTextAsync(Path.Combine(root, "media", "example2.png")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string? PublicPath(object source) => JsonSerializer.SerializeToElement(source).GetProperty("path").GetString();
}
