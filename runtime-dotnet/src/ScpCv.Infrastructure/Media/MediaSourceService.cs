// 媒体源上传、文件夹、移动和播放元数据管理。
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService(
    IDbContextFactory<ControlDbContext> contextFactory,
    WriteCoordinator writes,
    ControlDbContextFactory controlDbFactory,
    MediaStorageOptions storageOptions,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly string _mediaRoot = Path.GetFullPath(Path.Combine(controlDbFactory.Layout.RootPath, "media"));
    private readonly MediaPathResolver _paths = new(Path.Combine(controlDbFactory.Layout.RootPath, "media"));
    private readonly string[] _allowedRoots = BuildAllowedRoots(controlDbFactory, storageOptions);

    public async Task<IReadOnlyList<MediaSourceDto>> ListSourcesAsync(
        string? sourceType,
        long? folderId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = database.MediaSources.AsNoTracking().Include(source => source.PptResources).AsQueryable();
        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            var parsedType = ParseSourceType(sourceType);
            query = query.Where(source => source.SourceType == parsedType);
        }

        if (folderId is not null)
        {
            query = folderId < 0
                ? query.Where(source => source.FolderId == null)
                : query.Where(source => source.FolderId == folderId);
        }

        var sources = await query.OrderBy(source => source.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return sources.Select(ToSourceDto).ToArray();
    }

    public async Task<MediaSourceDto> AddLocalAsync(
        string path,
        string? displayName,
        string? sourceType,
        long? folderId,
        bool preheatEnabled,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveAllowedFile(path);
        var parsedType = string.IsNullOrWhiteSpace(sourceType)
            ? DetectSourceType(resolved)
            : ParseSourceType(sourceType);
        var info = new FileInfo(resolved);
        var digest = await ComputeDigestAsync(resolved, cancellationToken).ConfigureAwait(false);
        return await CreateFileSourceAsync(
            resolved,
            string.Empty,
            info.Name,
            null,
            displayName,
            parsedType,
            folderId,
            false,
            preheatEnabled,
            info.Length,
            digest,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<MediaSourceDto> AddWebAsync(
        string url,
        string? displayName,
        long? folderId,
        bool preheatEnabled,
        CancellationToken cancellationToken = default)
    {
        var normalizedUrl = NormalizeWebUrl(url);
        if (normalizedUrl.Length == 0)
        {
            throw new MediaServiceException("URL 不能为空");
        }

        return writes.ExecuteAsync(
            async (database, token) =>
            {
                var effectiveFolder = await OptionalFolderIdAsync(database, folderId, token).ConfigureAwait(false);
                var source = new MediaSource
                {
                    SourceType = MediaSourceType.Web,
                    Name = string.IsNullOrWhiteSpace(displayName) ? normalizedUrl[..Math.Min(80, normalizedUrl.Length)] : ValidateName(displayName, "显示名称不能为空"),
                    Uri = normalizedUrl,
                    IsAvailable = true,
                    MimeType = "text/html",
                    FolderId = effectiveFolder,
                    KeepAlive = preheatEnabled,
                    SourceRevision = 1,
                    CreatedAt = _timeProvider.GetUtcNow(),
                };
                database.MediaSources.Add(source);
                await database.SaveChangesAsync(token).ConfigureAwait(false);
                return ToSourceDto(source);
            },
            cancellationToken);
    }

    public async Task<MediaSourceDto> MoveSourceAsync(
        long sourceId,
        long? folderId,
        CancellationToken cancellationToken = default)
    {
        string? originalPath = null;
        string? targetPath = null;
        try
        {
            return await writes.ExecuteAsync(async (database, token) =>
            {
                var source = await FindSourceAsync(database, sourceId, token).ConfigureAwait(false);
                var effectiveFolder = await OptionalFolderIdAsync(database, folderId, token).ConfigureAwait(false);
                var managed = ManagedFileOrNull(source.UploadedFile);
                if (managed is not null)
                {
                    var activeDisplay = await database.PlaybackSessions.AnyAsync(session =>
                        session.WindowId <= WindowId.Maximum && session.MediaSourceId == sourceId &&
                        (session.PendingCommand != string.Empty ||
                         session.PlaybackState == PlaybackState.Loading ||
                         session.PlaybackState == PlaybackState.Playing ||
                         session.PlaybackState == PlaybackState.Paused), token).ConfigureAwait(false);
                    var activeAudio = await database.BackgroundAudioStates.AnyAsync(audio =>
                        audio.CurrentSourceId == sourceId &&
                        (audio.PlaybackState == PlaybackState.Loading ||
                         audio.PlaybackState == PlaybackState.Playing ||
                         audio.PlaybackState == PlaybackState.Paused), token).ConfigureAwait(false);
                    var converting = await database.MediaPreparationJobs.AnyAsync(job =>
                        job.SourceId == sourceId && job.Status == OperationStatus.Running, token).ConfigureAwait(false);
                    if (activeDisplay || activeAudio || converting)
                        throw new MediaServiceException("媒体源正在播放或转换中，停止后才能移动原件。");
                    if (!File.Exists(managed)) throw new MediaServiceException("受管理媒体原件不存在，不能移动。");

                    var folders = await database.MediaFolders.AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                    var targetFolder = _paths.FolderPath(effectiveFolder, folders);
                    _paths.EnsureNoReparsePoint(targetFolder);
                    if (source.FolderId == effectiveFolder &&
                        string.Equals(Path.GetDirectoryName(managed), targetFolder, StringComparison.OrdinalIgnoreCase))
                        return ToSourceDto(source);
                    Directory.CreateDirectory(targetFolder);
                    var originalName = source.OriginalFilename.Length > 0
                        ? source.OriginalFilename
                        : Path.GetFileName(managed);
                    var selected = _paths.NextAvailableFilePath(targetFolder, originalName);
                    if (!string.Equals(managed, selected, StringComparison.OrdinalIgnoreCase))
                    {
                        originalPath = managed;
                        targetPath = selected;
                        File.Move(originalPath, targetPath);
                        source.Uri = targetPath;
                        source.UploadedFile = targetPath;
                    }
                }
                source.FolderId = effectiveFolder;
                return ToSourceDto(source);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (originalPath is not null && targetPath is not null && File.Exists(targetPath) && !File.Exists(originalPath))
            {
                await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
                var committed = await check.MediaSources.AsNoTracking().Include(source => source.PptResources)
                    .SingleOrDefaultAsync(source => source.Id == sourceId, CancellationToken.None).ConfigureAwait(false);
                if (committed?.UploadedFile == targetPath) return ToSourceDto(committed);
                File.Move(targetPath, originalPath);
            }
            throw;
        }
    }

    public Task<MediaSourceDto> UpdateSourceAsync(
        long sourceId,
        string? name,
        string? uri,
        bool? preheatEnabled,
        CancellationToken cancellationToken = default) =>
        writes.ExecuteAsync(
            async (database, token) =>
            {
                var source = await FindSourceAsync(database, sourceId, token).ConfigureAwait(false);
                if (name is not null)
                {
                    source.Name = ValidateName(name, "显示名称不能为空");
                }

                if (uri is not null && source.SourceType == MediaSourceType.Web)
                {
                    var normalized = NormalizeWebUrl(uri);
                    if (normalized.Length == 0)
                    {
                        throw new MediaServiceException("网页 URL 不能为空");
                    }

                    source.Uri = normalized;
                    source.SourceRevision = checked(source.SourceRevision + 1);
                }

                if (preheatEnabled is not null)
                {
                    source.KeepAlive = preheatEnabled.Value;
                }

                return ToSourceDto(source);
            },
            cancellationToken);

    public async Task DeleteSourceAsync(long sourceId, CancellationToken cancellationToken = default)
    {
        var managedFile = await writes.ExecuteAsync(
            async (database, token) =>
            {
                var source = await FindSourceAsync(database, sourceId, token).ConfigureAwait(false);
                var path = ManagedFileOrNull(source.UploadedFile);
                database.MediaSources.Remove(source);
                return path;
            },
            cancellationToken).ConfigureAwait(false);
        if (managedFile is not null)
        {
            TryDeleteFile(managedFile);
        }
    }

    public async Task<MediaFileResult> GetDownloadAsync(long sourceId, CancellationToken cancellationToken = default)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
        var path = ResolveAllowedFile(source.Uri);
        return new MediaFileResult(
            path,
            source.MimeType.Length == 0 ? GuessMimeType(path) : source.MimeType,
            source.OriginalFilename.Length == 0 ? Path.GetFileName(path) : source.OriginalFilename,
            Download: true);
    }

    public async Task<MediaFileResult> GetPreviewAsync(long sourceId, CancellationToken cancellationToken = default)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (source.SourceType is not (MediaSourceType.Image or MediaSourceType.Video))
        {
            throw new MediaServiceException("仅图片和视频源支持文件预览");
        }

        var path = ResolveAllowedFile(source.Uri);
        return new MediaFileResult(
            path,
            source.MimeType.Length == 0 ? GuessMimeType(path) : source.MimeType,
            Path.GetFileName(path),
            Download: false);
    }


}

public sealed record MediaFileResult(string Path, string ContentType, string FileName, bool Download);

public sealed class MediaServiceException(string message, bool isNotFound = false) : Exception(message)
{
    public bool IsNotFound { get; } = isNotFound;
}
