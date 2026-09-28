// 新上传原件按页面文件夹的实体路径保存，重名不覆盖。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class MediaStorageLayoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleSourceDeletionRejectsLockedOrReadOnlyFileWithoutRemovingRecord(bool readOnly)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        string? path = null;
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            await using var bytes = new MemoryStream("preserve-original"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "locked.png", null, null, null, null, false, false);
            path = source.Uri;
            using var fileLock = readOnly ? null : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (readOnly) File.SetAttributes(path, FileAttributes.ReadOnly);

            await Assert.ThrowsAsync<MediaServiceException>(() => media.DeleteSourceAsync(source.Id));

            Assert.True(File.Exists(path));
            Assert.Equal("preserve-original", await File.ReadAllTextAsync(path));
            await using var check = factory.CreateDbContext();
            Assert.Equal(path, (await check.MediaSources.SingleAsync(item => item.Id == source.Id)).UploadedFile);
        }
        finally
        {
            if (path is not null && File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task RootAndNamedFolderUploadsUseVisiblePathsAndNeverOverwrite()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("PPT文件", null);
            await using var a = new MemoryStream("root-original"u8.ToArray());
            await using var b = new MemoryStream("folder-original"u8.ToArray());
            await using var duplicate = new MemoryStream("new-content"u8.ToArray());

            var rootSource = await media.AddUploadedAsync(a, "a.pptx", null, null, null, null, false, false);
            var folderSource = await media.AddUploadedAsync(b, "b.pptx", null, null, null, folder.Id, false, false);
            var second = await media.AddUploadedAsync(duplicate, "b.pptx", null, null, null, folder.Id, false, false);

            await using var database = factory.CreateDbContext();
            var paths = await database.MediaSources.OrderBy(item => item.Id).Select(item => item.UploadedFile).ToArrayAsync();
            Assert.Equal(Path.Combine(root, "media", "a.pptx"), paths[0]);
            Assert.Equal(Path.Combine(root, "media", "PPT文件", "b.pptx"), paths[1]);
            Assert.Equal(Path.Combine(root, "media", "PPT文件", "b (2).pptx"), paths[2]);
            Assert.Equal("folder-original", await File.ReadAllTextAsync(paths[1]));
            Assert.Equal("new-content", await File.ReadAllTextAsync(paths[2]));
            Assert.Equal("queued", rootSource.PreparationState);
            Assert.Equal(folder.Id, folderSource.FolderId);
            Assert.Equal(folder.Id, second.FolderId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(full).StartsWith("scp-cv-media-layout-", StringComparison.Ordinal) &&
                Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }
    }

    [Fact]
    public async Task MovingUploadedMediaChangesPhysicalPathAndKeepsBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var first = await media.CreateFolderAsync("原文件夹", null);
            var second = await media.CreateFolderAsync("目标文件夹", null);
            await using var bytes = new MemoryStream("original-bytes"u8.ToArray());
            var uploaded = await media.AddUploadedAsync(bytes, "image.png", null, null, null, first.Id, false, false);
            var oldPath = Path.Combine(root, "media", "原文件夹", "image.png");

            var moved = await media.MoveSourceAsync(uploaded.Id, second.Id);

            var newPath = Path.Combine(root, "media", "目标文件夹", "image.png");
            Assert.False(File.Exists(oldPath));
            Assert.Equal("original-bytes", await File.ReadAllTextAsync(newPath));
            Assert.Equal(second.Id, moved.FolderId);
            await using var check = factory.CreateDbContext();
            var persisted = await check.MediaSources.SingleAsync(item => item.Id == uploaded.Id);
            Assert.Equal(newPath, persisted.Uri);
            Assert.Equal(newPath, persisted.UploadedFile);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ActivePlaybackBlocksPhysicalMoveWithoutChangingSource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("目标", null);
            await using var bytes = new MemoryStream("on-air"u8.ToArray());
            var uploaded = await media.AddUploadedAsync(bytes, "on-air.png", null, null, null, null, false, false);
            await using (var database = factory.CreateDbContext())
            {
                var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
                session.MediaSourceId = uploaded.Id;
                session.PlaybackState = ScpCv.Domain.Model.PlaybackState.Playing;
                await database.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<MediaServiceException>(() => media.MoveSourceAsync(uploaded.Id, folder.Id));

            Assert.True(File.Exists(Path.Combine(root, "media", "on-air.png")));
            Assert.False(File.Exists(Path.Combine(root, "media", "目标", "on-air.png")));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task RenamingAndMovingFolderUpdatesNestedPhysicalFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var parent = await media.CreateFolderAsync("PPT文件", null);
            var child = await media.CreateFolderAsync("早会", parent.Id);
            await using var bytes = new MemoryStream("nested-original"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "a.png", null, null, null, child.Id, false, false);

            await media.UpdateFolderAsync(parent.Id, "演示资料", null, updateParent: false);
            var renamed = Path.Combine(root, "media", "演示资料", "早会", "a.png");
            Assert.True(File.Exists(renamed));
            Assert.False(File.Exists(Path.Combine(root, "media", "PPT文件", "早会", "a.png")));

            await media.UpdateFolderAsync(child.Id, null, null, updateParent: true);
            var moved = Path.Combine(root, "media", "早会", "a.png");
            Assert.True(File.Exists(moved));
            Assert.False(File.Exists(renamed));
            await using var check = factory.CreateDbContext();
            Assert.Equal(moved, (await check.MediaSources.SingleAsync(item => item.Id == source.Id)).Uri);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task DuplicateSiblingFolderNamesAreRejectedCaseInsensitively()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            await media.CreateFolderAsync("PPT文件", null);

            await Assert.ThrowsAsync<MediaServiceException>(() => media.CreateFolderAsync("ppt文件", null));

            Assert.True(Directory.Exists(Path.Combine(root, "media", "PPT文件")));
            await using var check = factory.CreateDbContext();
            Assert.Single(await check.MediaFolders.ToArrayAsync());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task DeletingFolderWithoutContentsMovesManagedFilesToRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("待删除", null);
            await using var bytes = new MemoryStream("keep-me"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "keep.png", null, null, null, folder.Id, false, false);

            await media.DeleteFolderAsync(folder.Id, deleteContents: false);

            var relocated = Path.Combine(root, "media", "keep.png");
            Assert.Equal("keep-me", await File.ReadAllTextAsync(relocated));
            Assert.False(Directory.Exists(Path.Combine(root, "media", "待删除")));
            await using var check = factory.CreateDbContext();
            var persisted = await check.MediaSources.SingleAsync(item => item.Id == source.Id);
            Assert.Null(persisted.FolderId);
            Assert.Equal(relocated, persisted.UploadedFile);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task DeletingFolderWithContentsRemovesTrackedSourcesAndDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("待删除", null);
            await using var bytes = new MemoryStream("remove-me"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "remove.png", null, null, null, folder.Id, false, false);

            await media.DeleteFolderAsync(folder.Id, deleteContents: true);

            Assert.False(Directory.Exists(Path.Combine(root, "media", "待删除")));
            await using var check = factory.CreateDbContext();
            Assert.False(await check.MediaSources.AnyAsync(item => item.Id == source.Id));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task FolderRenameFailureAfterPhysicalMoveRestoresOriginalPathAndDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("原目录", null);
            await using var bytes = new MemoryStream("keep-original"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "a.png", null, null, null, folder.Id, false, false);
            var failing = new MediaSourceService(factory, writes, factory, new MediaStorageOptions(), new FailingTimeProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() => failing.UpdateFolderAsync(
                folder.Id, "新目录", null, updateParent: false));

            var original = Path.Combine(root, "media", "原目录", "a.png");
            Assert.Equal("keep-original", await File.ReadAllTextAsync(original));
            Assert.False(Directory.Exists(Path.Combine(root, "media", "新目录")));
            await using var check = factory.CreateDbContext();
            Assert.Equal("原目录", (await check.MediaFolders.SingleAsync(item => item.Id == folder.Id)).Name);
            Assert.Equal(original, (await check.MediaSources.SingleAsync(item => item.Id == source.Id)).Uri);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task FolderDeletionWithUntrackedFileIsRejectedAndPreservesAllFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-layout-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("有额外文件", null);
            var unmanaged = Path.Combine(root, "media", "有额外文件", "not-registered.txt");
            await File.WriteAllTextAsync(unmanaged, "keep");

            await Assert.ThrowsAsync<MediaServiceException>(() => media.DeleteFolderAsync(folder.Id, deleteContents: false));

            Assert.Equal("keep", await File.ReadAllTextAsync(unmanaged));
            await using var check = factory.CreateDbContext();
            Assert.True(await check.MediaFolders.AnyAsync(item => item.Id == folder.Id));
        }
        finally { DeleteRoot(root); }
    }

    private sealed class FailingTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("故障注入：目录已移动但未写库");
    }

    private static void DeleteRoot(string root)
    {
        SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(full).StartsWith("scp-cv-media-layout-", StringComparison.Ordinal) &&
            Directory.Exists(full))
            Directory.Delete(full, recursive: true);
    }
}
