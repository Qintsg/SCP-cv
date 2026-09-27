// 文件夹删除先移动或隔离实体文件，数据库提交失败时按原路径补偿恢复。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    public async Task DeleteFolderAsync(
        long folderId,
        bool deleteContents,
        CancellationToken cancellationToken = default)
    {
        string? folderPath = null;
        string? quarantinePath = null;
        string? extraPath = null;
        var deletionStarted = false;
        var relocated = new List<(string From, string To)>();
        try
        {
            await writes.ExecuteAsync(async (database, token) =>
            {
                var folders = await database.MediaFolders.ToListAsync(token).ConfigureAwait(false);
                if (folders.All(folder => folder.Id != folderId))
                    throw new MediaServiceException($"文件夹 id={folderId} 不存在", isNotFound: true);
                deletionStarted = true;
                folderPath = _paths.FolderPath(folderId, folders);
                _paths.EnsureNoReparsePoint(folderPath);
                var subtree = CollectSubtree(folderId, folders);
                var sources = await database.MediaSources.Where(source => source.FolderId != null &&
                    subtree.Contains(source.FolderId.Value)).ToListAsync(token).ConfigureAwait(false);
                await EnsureSourcesIdleAsync(database, sources.Select(source => source.Id).ToArray(), token)
                    .ConfigureAwait(false);

                if (deleteContents)
                {
                    var staging = Path.Combine(_mediaRoot, ".staging");
                    Directory.CreateDirectory(staging);
                    quarantinePath = Path.Combine(staging, $"deleted-folder-{Guid.NewGuid():N}");
                    extraPath = Path.Combine(staging, $"deleted-files-{Guid.NewGuid():N}");
                    if (Directory.Exists(folderPath)) Directory.Move(folderPath, quarantinePath);
                    foreach (var source in sources)
                    {
                        var managed = ManagedFileOrNull(source.UploadedFile);
                        if (managed is null || IsWithinRoot(managed, folderPath) || !File.Exists(managed)) continue;
                        Directory.CreateDirectory(extraPath);
                        var target = Path.Combine(extraPath, $"{source.Id}-{Guid.NewGuid():N}");
                        File.Move(managed, target);
                        relocated.Add((managed, target));
                    }
                    database.MediaSources.RemoveRange(sources);
                }
                else
                {
                    var managedFiles = sources.Select(source => ManagedFileOrNull(source.UploadedFile))
                        .Where(path => path is not null && IsWithinRoot(path, folderPath))
                        .Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (Directory.Exists(folderPath) && EnumerateSafeFiles(folderPath)
                            .Any(path => !managedFiles.Contains(path)))
                        throw new MediaServiceException("文件夹含有未登记文件；请先整理，避免默认删除时丢失内容。");
                    foreach (var source in sources)
                    {
                        var managed = ManagedFileOrNull(source.UploadedFile);
                        if (managed is not null && IsWithinRoot(managed, folderPath))
                        {
                            if (!File.Exists(managed))
                                throw new MediaServiceException("受管理媒体原件不存在，不能安全搬出文件夹。");
                            var name = source.OriginalFilename.Length > 0
                                ? source.OriginalFilename : Path.GetFileName(managed);
                            var target = _paths.NextAvailableFilePath(_mediaRoot, name);
                            File.Move(managed, target);
                            relocated.Add((managed, target));
                            source.Uri = target;
                            source.UploadedFile = target;
                        }
                        source.FolderId = null;
                    }
                }

                var parentById = folders.ToDictionary(folder => folder.Id, folder => folder.ParentId);
                foreach (var folder in folders.Where(folder => subtree.Contains(folder.Id))
                             .OrderByDescending(folder => GetDepth(folder.Id, parentById)))
                    database.MediaFolders.Remove(folder);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!deletionStarted) throw;
            await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
            var folderStillExists = await check.MediaFolders.AsNoTracking()
                .AnyAsync(folder => folder.Id == folderId, CancellationToken.None).ConfigureAwait(false);
            if (folderStillExists)
            {
                if (quarantinePath is not null && folderPath is not null && Directory.Exists(quarantinePath))
                    Directory.Move(quarantinePath, folderPath);
                foreach (var (from, to) in relocated.AsEnumerable().Reverse())
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(from)!);
                    File.Move(to, from);
                }
                if (extraPath is not null && Directory.Exists(extraPath)) Directory.Delete(extraPath);
                throw;
            }
            // 提交结果不明时，以持久化状态为准；已提交的删除继续清理隔离区。
        }

        if (quarantinePath is not null && Directory.Exists(quarantinePath))
            Directory.Delete(quarantinePath, recursive: true);
        if (extraPath is not null && Directory.Exists(extraPath))
            Directory.Delete(extraPath, recursive: true);
        if (!deleteContents && folderPath is not null && Directory.Exists(folderPath))
            DeleteEmptyTree(folderPath);
    }

    private static IEnumerable<string> EnumerateSafeFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new MediaServiceException("文件夹内有符号链接或重解析点，已拒绝删除。");
                if (Directory.Exists(entry)) pending.Push(entry);
                else yield return entry;
            }
        }
    }

    private static void DeleteEmptyTree(string root)
    {
        var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length).ToArray();
        foreach (var directory in directories)
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
        if (!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
    }
}
