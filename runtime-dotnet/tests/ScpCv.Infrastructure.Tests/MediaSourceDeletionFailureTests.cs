// 单源隔离删除跨文件系统/SQLite 的失败补偿与清理残留回归。
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class MediaSourceDeletionFailureTests
{
    [Fact]
    public async Task ActiveSourceDeletionPreservesRecordAndOriginal()
    {
        using var store = await TestStore.CreateAsync();
        var source = await store.UploadAsync();
        await using (var database = store.Factory.CreateDbContext())
        {
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
            session.MediaSourceId = source.Id;
            session.PlaybackState = PlaybackState.Playing;
            await database.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<MediaServiceException>(() => store.DeleteAsync(source.Id));

        Assert.True(File.Exists(source.Uri));
        await using var check = store.Factory.CreateDbContext();
        Assert.True(await check.MediaSources.AnyAsync(item => item.Id == source.Id));
    }

    [Fact]
    public async Task LocalSourceDeletionDoesNotDeleteUnmanagedOriginal()
    {
        using var store = await TestStore.CreateAsync();
        var path = Path.Combine(store.Root, "local-original.png");
        await File.WriteAllTextAsync(path, "unmanaged-original");
        using var writes = new WriteCoordinator(store.Factory);
        var media = new MediaSourceService(store.Factory, writes, store.Factory, new MediaStorageOptions());
        var source = await media.AddLocalAsync(path, null, "image", null, false);

        await media.DeleteSourceAsync(source.Id);

        Assert.Equal("unmanaged-original", await File.ReadAllTextAsync(path));
        await using var check = store.Factory.CreateDbContext();
        Assert.False(await check.MediaSources.AnyAsync(item => item.Id == source.Id));
    }

    [Fact]
    public async Task SaveFailureAfterQuarantineRestoresFileAndSourceRecord()
    {
        using var store = await TestStore.CreateAsync();
        var source = await store.UploadAsync();
        var factory = new InterceptingFactory(store.Factory.Layout.DatabasePath, new FailDeleteSaveInterceptor());
        using var writes = new WriteCoordinator(factory);
        var media = new MediaSourceService(factory, writes, store.Factory, new MediaStorageOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => media.DeleteSourceAsync(source.Id));

        Assert.Equal("original-content", await File.ReadAllTextAsync(source.Uri));
        await using var check = store.Factory.CreateDbContext();
        Assert.True(await check.MediaSources.AnyAsync(item => item.Id == source.Id));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(store.Root, "media", ".staging")));
    }

    [Fact]
    public async Task ExceptionAfterCommitUsesPersistedDeleteAndCleansQuarantine()
    {
        using var store = await TestStore.CreateAsync();
        var source = await store.UploadAsync();
        using var observer = new DeleteCommitObserver(store.Root, failAfterCommit: true);
        var factory = new InterceptingFactory(store.Factory.Layout.DatabasePath, observer);
        using var writes = new WriteCoordinator(factory);
        var media = new MediaSourceService(factory, writes, store.Factory, new MediaStorageOptions());

        await media.DeleteSourceAsync(source.Id);

        Assert.False(File.Exists(source.Uri));
        await using var check = store.Factory.CreateDbContext();
        Assert.False(await check.MediaSources.AnyAsync(item => item.Id == source.Id));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(store.Root, "media", ".staging")));
    }

    [Fact]
    public async Task CleanupFailureReportsPartialDeleteAndKeepsRecoveryManifest()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var store = await TestStore.CreateAsync();
        var source = await store.UploadAsync();
        using var observer = new DeleteCommitObserver(store.Root, failAfterCommit: false);
        var factory = new InterceptingFactory(store.Factory.Layout.DatabasePath, observer);
        using var writes = new WriteCoordinator(factory);
        var media = new MediaSourceService(factory, writes, store.Factory, new MediaStorageOptions());

        var error = await Assert.ThrowsAsync<MediaServiceException>(() => media.DeleteSourceAsync(source.Id));

        Assert.True(error.CleanupPending);
        Assert.Contains("源记录已删除", error.Message);
        Assert.False(File.Exists(source.Uri));
        await using var check = store.Factory.CreateDbContext();
        Assert.False(await check.MediaSources.AnyAsync(item => item.Id == source.Id));
        var staging = Path.Combine(store.Root, "media", ".staging");
        var manifestPath = Assert.Single(Directory.EnumerateFiles(staging, "*.json"));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(source.Id, manifest.RootElement.GetProperty("source_id").GetInt64());
        Assert.Equal(source.Uri, manifest.RootElement.GetProperty("original_path").GetString());
        Assert.True(File.Exists(manifest.RootElement.GetProperty("quarantine_path").GetString()));
    }

    private sealed class FailDeleteSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<MediaSource>().Any(entry => entry.State == EntityState.Deleted))
                throw new InvalidOperationException("故障注入：隔离移动后写库失败");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class DeleteCommitObserver(string root, bool failAfterCommit) : DbTransactionInterceptor, IDisposable
    {
        private FileStream? _lockedQuarantine;
        public override Task TransactionCommittedAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (failAfterCommit) throw new InvalidOperationException("故障注入：事务已提交但回执异常");
            var quarantine = Directory.EnumerateFiles(Path.Combine(root, "media", ".staging"))
                .Single(path => !path.EndsWith(".json", StringComparison.Ordinal));
            _lockedQuarantine = new FileStream(quarantine, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.CompletedTask;
        }

        public void Dispose() => _lockedQuarantine?.Dispose();
    }

    private sealed class InterceptingFactory(string path, IInterceptor interceptor) : IDbContextFactory<ControlDbContext>
    {
        public ControlDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ControlDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString())
            .AddInterceptors(interceptor).Options);
    }

    private sealed class TestStore : IDisposable
    {
        private readonly WriteCoordinator _writes;
        private readonly MediaSourceService _media;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"scp-cv-source-delete-{Guid.NewGuid():N}");
        public ControlDbContextFactory Factory { get; }

        private TestStore()
        {
            Factory = new ControlDbContextFactory(new DataRootOptions { RootPath = Root }, Root);
            _writes = new WriteCoordinator(Factory);
            _media = new MediaSourceService(Factory, _writes, Factory, new MediaStorageOptions());
        }

        public static async Task<TestStore> CreateAsync()
        {
            var store = new TestStore();
            await new DatabaseInitializer(store.Factory).InitializeAsync();
            return store;
        }

        public async Task<MediaSourceDto> UploadAsync()
        {
            await using var bytes = new MemoryStream("original-content"u8.ToArray());
            return await _media.AddUploadedAsync(bytes, "original.png", null, null, null, null, false, false);
        }

        public Task DeleteAsync(long sourceId) => _media.DeleteSourceAsync(sourceId);

        public void Dispose()
        {
            _writes.Dispose();
            SqliteConnection.ClearAllPools();
            var resolved = Path.GetFullPath(Root);
            var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (resolved.StartsWith(temporary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("scp-cv-source-delete-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }
    }
}
