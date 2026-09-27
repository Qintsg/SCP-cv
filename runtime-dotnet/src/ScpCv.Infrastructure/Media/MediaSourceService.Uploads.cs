// 受管理媒体文件上传与原件、准备作业的同事务登记。
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    public async Task<MediaSourceDto> AddUploadedAsync(
        Stream content,
        string fileName,
        string? contentType,
        string? displayName,
        string? sourceType,
        long? folderId,
        bool isTemporary,
        bool preheatEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var safeFileName = Path.GetFileName(fileName);
        if (safeFileName.Length == 0 || !string.Equals(safeFileName, fileName, StringComparison.Ordinal))
            throw new MediaServiceException("上传文件名不能包含路径，且不能为空。");
        MediaPathResolver.ValidateSegment(safeFileName);
        var parsedType = string.IsNullOrWhiteSpace(sourceType)
            ? DetectSourceType(safeFileName)
            : ParseSourceType(sourceType);
        var stagingRoot = Path.Combine(_mediaRoot, ".staging");
        Directory.CreateDirectory(stagingRoot);
        var staging = Path.Combine(stagingRoot, $"{Guid.NewGuid():N}.part");
        string? destination = null;
        string digest;
        long length;
        try
        {
            await using (var target = new FileStream(
                             staging,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                length = 0;
                while (true)
                {
                    var read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    hasher.AppendData(buffer, 0, read);
                    length += read;
                }

                digest = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }
            if (length == 0) throw new MediaServiceException("上传文件不能为空。");

            return await writes.ExecuteAsync(async (database, token) =>
            {
                var effectiveFolder = await OptionalFolderIdAsync(database, folderId, token).ConfigureAwait(false);
                var folders = await database.MediaFolders.AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                var folderPath = _paths.FolderPath(effectiveFolder, folders);
                _paths.EnsureNoReparsePoint(folderPath);
                Directory.CreateDirectory(folderPath);
                _paths.EnsureNoReparsePoint(folderPath);
                destination = _paths.NextAvailableFilePath(folderPath, safeFileName);
                File.Move(staging, destination);

                var source = new MediaSource
                {
                    SourceType = parsedType,
                    Name = string.IsNullOrWhiteSpace(displayName)
                        ? Path.GetFileNameWithoutExtension(safeFileName)
                        : ValidateName(displayName, "显示名称不能为空"),
                    Uri = destination,
                    UploadedFile = destination,
                    IsAvailable = parsedType != MediaSourceType.Presentation ||
                                  Path.GetExtension(safeFileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase),
                    FolderId = effectiveFolder,
                    OriginalFilename = safeFileName,
                    FileSize = length,
                    MimeType = string.IsNullOrWhiteSpace(contentType) ? GuessMimeType(safeFileName) : contentType,
                    IsTemporary = isTemporary,
                    ExpiresAt = isTemporary ? _timeProvider.GetUtcNow().AddDays(1) : null,
                    KeepAlive = preheatEnabled,
                    SourceRevision = 1,
                    ContentDigest = digest,
                    CreatedAt = _timeProvider.GetUtcNow(),
                };
                if (source.SourceType == MediaSourceType.Presentation && !source.IsAvailable)
                    source.MetadataJson = JsonSerializer.Serialize(new
                    {
                        slide_images = new { status = "queued", source_digest = digest, page_count = 0 },
                    });
                database.MediaSources.Add(source);
                await database.SaveChangesAsync(token).ConfigureAwait(false);
                if (source.SourceType == MediaSourceType.Presentation && !source.IsAvailable)
                    database.MediaPreparationJobs.Add(new MediaPreparationJob
                    {
                        JobId = Guid.NewGuid(),
                        SourceId = source.Id,
                        SourceRevision = source.SourceRevision,
                        SourceDigest = digest,
                        Kind = PreparationJobKind.PptImages,
                        Priority = 10,
                        RecipeVersion = "ppt-images-v1",
                    });
                return ToSourceDto(source);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (destination is not null && File.Exists(destination))
            {
                // 提交异常可能发生在数据库已接受写入之后；先核对唯一存储路径再决定清理。
                await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
                var committed = await check.MediaSources.AsNoTracking()
                    .Include(source => source.PptResources)
                    .SingleOrDefaultAsync(source => source.UploadedFile == destination, CancellationToken.None)
                    .ConfigureAwait(false);
                if (committed is not null) return ToSourceDto(committed);
                TryDeleteFile(destination);
            }
            TryDeleteFile(staging);
            throw;
        }
    }

    private Task<MediaSourceDto> CreateFileSourceAsync(
        string uri,
        string uploadedFile,
        string originalFilename,
        string? contentType,
        string? displayName,
        MediaSourceType sourceType,
        long? folderId,
        bool isTemporary,
        bool preheatEnabled,
        long fileSize,
        string digest,
        CancellationToken cancellationToken) =>
        writes.ExecuteAsync(
            async (database, token) =>
            {
                var effectiveFolder = await OptionalFolderIdAsync(database, folderId, token).ConfigureAwait(false);
                var source = new MediaSource
                {
                    SourceType = sourceType,
                    Name = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(originalFilename) : ValidateName(displayName, "显示名称不能为空"),
                    Uri = uri,
                    UploadedFile = uploadedFile,
                    IsAvailable = sourceType != MediaSourceType.Presentation ||
                                  Path.GetExtension(originalFilename).Equals(".pdf", StringComparison.OrdinalIgnoreCase),
                    FolderId = effectiveFolder,
                    OriginalFilename = originalFilename,
                    FileSize = fileSize,
                    MimeType = string.IsNullOrWhiteSpace(contentType) ? GuessMimeType(originalFilename) : contentType,
                    IsTemporary = isTemporary,
                    ExpiresAt = isTemporary ? _timeProvider.GetUtcNow().AddDays(1) : null,
                    KeepAlive = preheatEnabled,
                    SourceRevision = 1,
                    ContentDigest = digest,
                    CreatedAt = _timeProvider.GetUtcNow(),
                };
                if (source.SourceType == MediaSourceType.Presentation && !source.IsAvailable)
                {
                    source.MetadataJson = JsonSerializer.Serialize(new
                    {
                        slide_images = new { status = "queued", source_digest = digest, page_count = 0 },
                    });
                }
                database.MediaSources.Add(source);
                await database.SaveChangesAsync(token).ConfigureAwait(false);
                if (source.SourceType == MediaSourceType.Presentation && !source.IsAvailable)
                {
                    database.MediaPreparationJobs.Add(new MediaPreparationJob
                    {
                        JobId = Guid.NewGuid(),
                        SourceId = source.Id,
                        SourceRevision = source.SourceRevision,
                        SourceDigest = digest,
                        Kind = PreparationJobKind.PptImages,
                        Priority = 10,
                        RecipeVersion = "ppt-images-v1",
                    });
                }
                return ToSourceDto(source);
            },
            cancellationToken);
}
