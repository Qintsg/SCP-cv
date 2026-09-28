// 单源删除先隔离受管理原件，再提交数据库；失败可补偿，清理残留有清单而非静默遗失。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    public async Task DeleteSourceAsync(long sourceId, CancellationToken cancellationToken = default)
    {
        string? originalPath = null;
        string? quarantinePath = null;
        string? manifestPath = null;
        var deletionStarted = false;
        try
        {
            await writes.ExecuteAsync(async (database, token) =>
            {
                var source = await FindSourceAsync(database, sourceId, token).ConfigureAwait(false);
                deletionStarted = true;
                await EnsureSourcesIdleAsync(database, [sourceId], token).ConfigureAwait(false);
                originalPath = ManagedFileOrNull(source.UploadedFile);
                if (originalPath is not null)
                {
                    _paths.EnsureNoReparsePoint(originalPath);
                    if (Directory.Exists(originalPath))
                        throw new MediaServiceException("媒体原件路径是目录，已拒绝删除。");
                    if (File.Exists(originalPath))
                    {
                        if ((File.GetAttributes(originalPath) & FileAttributes.ReadOnly) != 0)
                            throw new MediaServiceException("媒体原件为只读，删除未生效；请先解除只读属性。");
                        var staging = Path.Combine(_mediaRoot, ".staging");
                        _paths.EnsureNoReparsePoint(staging);
                        Directory.CreateDirectory(staging);
                        quarantinePath = Path.Combine(staging, $"deleted-source-{sourceId}-{Guid.NewGuid():N}");
                        manifestPath = quarantinePath + ".json";
                        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(new
                        {
                            source_id = sourceId,
                            original_path = originalPath,
                            quarantine_path = quarantinePath,
                            started_at = _timeProvider.GetUtcNow(),
                        }), token).ConfigureAwait(false);
                        File.Move(originalPath, quarantinePath);
                    }
                }
                database.MediaSources.Remove(source);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (!deletionStarted) throw;
            bool sourceStillExists;
            try
            {
                await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
                sourceStillExists = await check.MediaSources.AsNoTracking()
                    .AnyAsync(source => source.Id == sourceId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception checkException)
            {
                throw new MediaServiceException(
                    $"无法确认删除事务结果；隔离原件和清单已保留，需检查后再清理：{checkException.Message}", cleanupPending: true);
            }

            if (sourceStillExists)
            {
                try
                {
                    if (quarantinePath is not null && originalPath is not null && File.Exists(quarantinePath))
                        File.Move(quarantinePath, originalPath);
                    if (manifestPath is not null && File.Exists(manifestPath)) File.Delete(manifestPath);
                }
                catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
                {
                    throw new MediaServiceException(
                        $"删除未提交，但原件恢复失败；隔离文件和清单已保留：{restoreException.Message}", cleanupPending: true);
                }
                if (exception is IOException or UnauthorizedAccessException)
                    throw new MediaServiceException($"媒体原件被占用或没有删除权限，删除未生效：{exception.Message}");
                throw;
            }
            // Commit 的异常可能发生在提交之后；持久化记录已删除时不再把原件搬回旧路径。
        }

        try
        {
            if (quarantinePath is not null && File.Exists(quarantinePath)) File.Delete(quarantinePath);
            if (manifestPath is not null && File.Exists(manifestPath)) File.Delete(manifestPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new MediaServiceException(
                $"源记录已删除，但隔离原件清理未完成；清单保留在 media/.staging，请维护人员检查：{exception.Message}", cleanupPending: true);
        }
    }
}
