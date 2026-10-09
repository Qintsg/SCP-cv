// 放映失败或关闭在途不等于原件已释放；源/目录的写操作必须保留原件。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class PresentationOriginalUsageTests
{
    /// <summary>:param operation: 文件操作。:param state: 不活跃投影。:returns: 实际源占用在确认关闭前始终阻止移动/删除。</summary>
    [Theory]
    [InlineData("move", PlaybackState.Error)]
    [InlineData("move", PlaybackState.Idle)]
    [InlineData("delete", PlaybackState.Error)]
    [InlineData("delete", PlaybackState.Idle)]
    [InlineData("rename_folder", PlaybackState.Error)]
    [InlineData("rename_folder", PlaybackState.Idle)]
    [InlineData("delete_folder", PlaybackState.Error)]
    [InlineData("delete_folder", PlaybackState.Idle)]
    public async Task ActualDocumentRemainsReservedUntilConfirmedClose(string operation, PlaybackState state)
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-presentation-usage-" + Guid.NewGuid().ToString("N"));
        var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
        try
        {
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = await media.CreateFolderAsync("中文原文稿", null);
            var target = await media.CreateFolderAsync("移动目标", null);
            await using var bytes = new MemoryStream("保留原件"u8.ToArray());
            var source = await media.AddUploadedAsync(bytes, "保持.pptx", null, null, null, folder.Id, false, false);
            await using (var database = factory.CreateDbContext())
            {
                var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
                session.MediaSourceId = null; session.ActualSourceId = source.Id;
                session.PlaybackState = state; session.PendingCommand = string.Empty;
                var group = await database.RuntimeGroupControls.SingleAsync();
                group.State = RuntimeGroupState.Armed;
                await database.SaveChangesAsync();
            }
            Task Invoke() => operation switch
            {
                "move" => media.MoveSourceAsync(source.Id, target.Id),
                "delete" => media.DeleteSourceAsync(source.Id),
                "rename_folder" => media.UpdateFolderAsync(folder.Id, "不应改名", null, false),
                _ => media.DeleteFolderAsync(folder.Id, true),
            };
            await Assert.ThrowsAsync<MediaServiceException>(Invoke);
            Assert.Equal("保留原件", await File.ReadAllTextAsync(source.Uri));
            await using (var database = factory.CreateDbContext())
            {
                (await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).ActualSourceId = null;
                await database.SaveChangesAsync();
            }
            await Invoke();
        }
        finally
        {
            using var database = factory.CreateDbContext();
            using var connection = new SqliteConnection(database.Database.GetConnectionString());
            SqliteConnection.ClearPool(connection);
            DeleteRoot(root);
        }
    }

    private static void DeleteRoot(string root)
    {
        var full = Path.GetFullPath(root);
        if (Path.GetDirectoryName(full) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("测试路径越界");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}
