// 用户文件夹的实体目录创建、重命名、移动与安全删除。
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    public async Task<IReadOnlyList<MediaFolderDto>> ListFoldersAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var folders = await database.MediaFolders.AsNoTracking().OrderBy(folder => folder.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return folders.Select(folder => FolderDto(folder, folders)).ToArray();
    }

    public Task<MediaFolderDto> CreateFolderAsync(
        string name,
        long? parentId,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = MediaPathResolver.ValidateSegment(ValidateName(name, "文件夹名称不能为空"));
        return CreateFolderWithDirectoryAsync(normalizedName, parentId, cancellationToken);
    }

    private async Task<MediaFolderDto> CreateFolderWithDirectoryAsync(
        string normalizedName,
        long? parentId,
        CancellationToken cancellationToken)
    {
        string? createdPath = null;
        try
        {
            return await writes.ExecuteAsync(async (database, token) =>
            {
                var folders = await database.MediaFolders.ToListAsync(token).ConfigureAwait(false);
                if (parentId is not null && folders.All(folder => folder.Id != parentId.Value))
                    throw new MediaServiceException($"父文件夹 id={parentId.Value} 不存在");
                if (folders.Any(folder => folder.ParentId == parentId &&
                                          string.Equals(folder.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
                    throw new MediaServiceException("同一目录下已存在同名文件夹。");
                var parentPath = _paths.FolderPath(parentId, folders);
                _paths.EnsureNoReparsePoint(parentPath);
                var directory = Path.Combine(parentPath, normalizedName);
                _paths.EnsureWithinRoot(directory);
                if (File.Exists(directory) || Directory.Exists(directory))
                    throw new MediaServiceException("实体媒体目录已存在同名项，未覆盖。");
                Directory.CreateDirectory(directory);
                createdPath = directory;

                var now = _timeProvider.GetUtcNow();
                var folder = new MediaFolder
                {
                    Name = normalizedName,
                    ParentId = parentId,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                database.MediaFolders.Add(folder);
                await database.SaveChangesAsync(token).ConfigureAwait(false);
                folders.Add(folder);
                return FolderDto(folder, folders);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (createdPath is not null && Directory.Exists(createdPath) &&
                !Directory.EnumerateFileSystemEntries(createdPath).Any())
            {
                await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
                var committed = await check.MediaFolders.AsNoTracking().AnyAsync(folder =>
                    folder.ParentId == parentId && folder.Name == normalizedName, CancellationToken.None).ConfigureAwait(false);
                if (!committed) Directory.Delete(createdPath);
            }
            throw;
        }
    }

    public async Task<MediaFolderDto> UpdateFolderAsync(
        long folderId,
        string? name,
        long? parentId,
        bool updateParent,
        CancellationToken cancellationToken = default)
    {
        string? originalPath = null;
        string? targetPath = null;
        var movedDirectory = false;
        var createdDirectory = false;
        var normalizedName = name is null ? null : MediaPathResolver.ValidateSegment(ValidateName(name, "文件夹名称不能为空"));
        string? expectedName = null;
        try
        {
            return await writes.ExecuteAsync(async (database, token) =>
            {
                var folders = await database.MediaFolders.ToListAsync(token).ConfigureAwait(false);
                var folder = folders.SingleOrDefault(candidate => candidate.Id == folderId)
                    ?? throw new MediaServiceException($"文件夹 id={folderId} 不存在", isNotFound: true);
                originalPath = _paths.FolderPath(folderId, folders);
                var nextParentId = updateParent ? parentId : folder.ParentId;
                var nextName = normalizedName ?? folder.Name;
                expectedName = nextName;

                if (nextParentId == folderId)
                    throw new MediaServiceException("不能将文件夹设为自己的子文件夹");
                if (nextParentId is not null && updateParent)
                {
                    var ancestors = await LoadAncestorIdsAsync(database, nextParentId.Value, token).ConfigureAwait(false);
                    if (ancestors is null) throw new MediaServiceException($"父文件夹 id={nextParentId.Value} 不存在");
                    if (ancestors.Contains(folderId))
                        throw new MediaServiceException("不能将文件夹移动到自己的子文件夹");
                }
                if (folders.Any(candidate => candidate.Id != folderId && candidate.ParentId == nextParentId &&
                                             string.Equals(candidate.Name, nextName, StringComparison.OrdinalIgnoreCase)))
                    throw new MediaServiceException("同一目录下已存在同名文件夹。");

                folder.Name = nextName;
                folder.ParentId = nextParentId;
                targetPath = _paths.FolderPath(folderId, folders);
                var changedPath = !string.Equals(originalPath, targetPath, StringComparison.OrdinalIgnoreCase);
                if (changedPath)
                {
                    _paths.EnsureNoReparsePoint(originalPath);
                    _paths.EnsureNoReparsePoint(Path.GetDirectoryName(targetPath)!);
                    if (File.Exists(targetPath) || Directory.Exists(targetPath))
                        throw new MediaServiceException("目标实体媒体目录已存在同名项，未覆盖。");

                    var subtree = CollectSubtree(folderId, folders);
                    var sourceIds = await database.MediaSources.Where(source => source.FolderId != null &&
                        subtree.Contains(source.FolderId.Value)).Select(source => source.Id).ToArrayAsync(token).ConfigureAwait(false);
                    await EnsureSourcesIdleAsync(database, sourceIds, token).ConfigureAwait(false);
                    var sources = await database.MediaSources.Where(source => source.FolderId != null &&
                        subtree.Contains(source.FolderId.Value)).ToListAsync(token).ConfigureAwait(false);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    if (Directory.Exists(originalPath))
                    {
                        Directory.Move(originalPath, targetPath);
                        movedDirectory = true;
                    }
                    else
                    {
                        Directory.CreateDirectory(targetPath);
                        createdDirectory = true;
                    }

                    foreach (var source in sources)
                    {
                        var managed = ManagedFileOrNull(source.UploadedFile);
                        if (!movedDirectory || managed is null || !IsWithinRoot(managed, originalPath)) continue;
                        var relative = Path.GetRelativePath(originalPath, managed);
                        var relocated = Path.Combine(targetPath, relative);
                        source.Uri = relocated;
                        source.UploadedFile = relocated;
                    }
                }
                folder.UpdatedAt = _timeProvider.GetUtcNow();
                return FolderDto(folder, folders);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (targetPath is not null && originalPath is not null && (movedDirectory || createdDirectory))
            {
                await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
                var committed = await check.MediaFolders.AsNoTracking().SingleOrDefaultAsync(
                    folder => folder.Id == folderId, CancellationToken.None).ConfigureAwait(false);
                if (committed is not null && committed.Name == expectedName &&
                    (!updateParent || committed.ParentId == parentId))
                {
                    var folders = await check.MediaFolders.AsNoTracking().ToListAsync(CancellationToken.None).ConfigureAwait(false);
                    if (string.Equals(_paths.FolderPath(folderId, folders), targetPath, StringComparison.OrdinalIgnoreCase))
                        return FolderDto(committed, folders);
                }
                if (movedDirectory && Directory.Exists(targetPath) && !Directory.Exists(originalPath))
                    Directory.Move(targetPath, originalPath);
                if (createdDirectory && Directory.Exists(targetPath) &&
                    !Directory.EnumerateFileSystemEntries(targetPath).Any())
                    Directory.Delete(targetPath);
            }
            throw;
        }
    }

    private static async Task EnsureSourcesIdleAsync(
        Persistence.ControlDbContext database,
        long[] sourceIds,
        CancellationToken cancellationToken)
    {
        if (sourceIds.Length == 0) return;
        var activeDisplay = await database.PlaybackSessions.AnyAsync(session =>
            session.WindowId <= WindowId.Maximum && session.MediaSourceId != null &&
            sourceIds.Contains(session.MediaSourceId.Value) &&
            (session.PendingCommand != string.Empty || session.PlaybackState == PlaybackState.Loading ||
             session.PlaybackState == PlaybackState.Playing || session.PlaybackState == PlaybackState.Paused), cancellationToken)
            .ConfigureAwait(false);
        var activeAudio = await database.BackgroundAudioStates.AnyAsync(audio =>
            audio.CurrentSourceId != null && sourceIds.Contains(audio.CurrentSourceId.Value) &&
            (audio.PlaybackState == PlaybackState.Loading || audio.PlaybackState == PlaybackState.Playing ||
             audio.PlaybackState == PlaybackState.Paused), cancellationToken).ConfigureAwait(false);
        var converting = await database.MediaPreparationJobs.AnyAsync(job =>
            sourceIds.Contains(job.SourceId) && job.Status == OperationStatus.Running, cancellationToken)
            .ConfigureAwait(false);
        if (activeDisplay || activeAudio || converting)
            throw new MediaServiceException("文件夹内有媒体正在播放或转换，停止后才能移动或删除。");
    }

    private MediaFolderDto FolderDto(MediaFolder folder, IReadOnlyCollection<MediaFolder> folders)
    {
        var relative = Path.GetRelativePath(_mediaRoot, _paths.FolderPath(folder.Id, folders));
        return ToFolderDto(folder) with
        {
            RelativePath = relative == "." ? string.Empty : relative.Replace('\\', '/'),
        };
    }
}
