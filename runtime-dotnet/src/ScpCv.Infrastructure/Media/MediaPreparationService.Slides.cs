// PPT 逐页图片制品发布，原件与旧版本制品均不被覆盖。
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaPreparationService
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public async Task<MediaSourceDto> RetryPptImagesAsync(long sourceId, CancellationToken cancellationToken = default)
    {
        var digestSnapshot = await ReadLegacyDigestAsync(sourceId, cancellationToken).ConfigureAwait(false);
        return await writes.ExecuteAsync(async (database, token) =>
        {
            var source = await database.MediaSources.Include(item => item.PptResources)
                .SingleOrDefaultAsync(item => item.Id == sourceId, token).ConfigureAwait(false)
                ?? throw new MediaServiceException($"媒体源 id={sourceId} 不存在", isNotFound: true);
            if (source.SourceType != MediaSourceType.Presentation ||
                Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new MediaServiceException("仅 PPT/PPTX 原件可以重新准备逐页图片。");
            if (!File.Exists(source.Uri))
                throw new MediaServiceException("文稿原件缺失，无法安全重试转换。");
            if (digestSnapshot is not null &&
                (source.Uri != digestSnapshot.Uri || source.SourceRevision != digestSnapshot.SourceRevision))
            {
                // 同源并发请求可复用另一个请求已提交的摘要/排队作业，不把正常补版本误报为原件变化。
                var concurrentlyQueued = source.Uri == digestSnapshot.Uri &&
                    source.SourceRevision == Math.Max(1, checked(digestSnapshot.SourceRevision + 1)) &&
                    string.Equals(source.ContentDigest, digestSnapshot.Digest, StringComparison.OrdinalIgnoreCase) &&
                    await database.MediaPreparationJobs.AnyAsync(item =>
                        item.SourceId == sourceId && item.Kind == PreparationJobKind.PptImages &&
                        item.SourceRevision == source.SourceRevision && item.SourceDigest == source.ContentDigest &&
                        (item.Status == OperationStatus.Queued || item.Status == OperationStatus.Running), token)
                        .ConfigureAwait(false);
                if (!concurrentlyQueued)
                    throw new MediaServiceException("读取摘要期间文稿路径或版本已变化；请核查原件后重新重试。");
            }
            if (string.IsNullOrWhiteSpace(source.ContentDigest))
            {
                if (digestSnapshot is null)
                    throw new MediaServiceException("读取状态后文稿摘要已变化；请核查原件后重新重试。");
                var unresolved = await database.MediaPreparationJobs.AnyAsync(item =>
                    item.SourceId == sourceId && item.Kind == PreparationJobKind.PptImages &&
                    (item.Status == OperationStatus.Running || item.Status == OperationStatus.Uncertain), token)
                    .ConfigureAwait(false);
                if (unresolved)
                    throw new MediaServiceException("旧文稿存在运行中或结果不确定的 Office 作业；请先核查，不能盲目重试。");
                source.ContentDigest = digestSnapshot.Digest;
                source.SourceRevision = Math.Max(1, checked(source.SourceRevision + 1));
            }
            else if (digestSnapshot is not null &&
                     !string.Equals(source.ContentDigest, digestSnapshot.Digest, StringComparison.OrdinalIgnoreCase))
                throw new MediaServiceException("读取摘要期间文稿摘要已变化；请核查原件后重新重试。");

            var latest = await database.MediaPreparationJobs
                .Where(item => item.SourceId == sourceId && item.Kind == PreparationJobKind.PptImages &&
                               item.SourceDigest == source.ContentDigest)
                .OrderByDescending(item => item.Id).FirstOrDefaultAsync(token).ConfigureAwait(false);
            if (latest?.Status == OperationStatus.Uncertain)
                throw new MediaServiceException("上次 Office 转换结果不确定；请先核查暂存文件与 Office 状态，不能盲目重试。");
            if (latest?.Status is OperationStatus.Queued or OperationStatus.Running ||
                latest?.Status == OperationStatus.Succeeded && HasPreparedSlideImages(source))
                return await MediaSourceService.ProjectSourceAsync(source, database, token).ConfigureAwait(false);

            database.MediaPreparationJobs.Add(new MediaPreparationJob
            {
                JobId = Guid.NewGuid(),
                SourceId = source.Id,
                SourceRevision = source.SourceRevision,
                SourceDigest = source.ContentDigest,
                Kind = PreparationJobKind.PptImages,
                Priority = 10,
                RecipeVersion = latest is null ? "ppt-images-v1" : $"ppt-images-v1-retry-{Guid.NewGuid():N}",
            });
            SetSourceSlideStatus(source, "queued", string.Empty, source.ContentDigest);
            return await MediaSourceService.ProjectSourceAsync(source, database, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 仅为显式重试的缺摘要旧原件读取文件，整个外部 I/O 位于写门禁之外。
    /// :param sourceId: 已登记原件标识。
    /// :param cancellationToken: 取消令牌。
    /// :returns: 路径、版本和新摘要快照；已有摘要时为空且不读取原件。
    /// </summary>
    private async Task<LegacyDigestSnapshot?> ReadLegacyDigestAsync(long sourceId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var source = await database.MediaSources.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sourceId, cancellationToken).ConfigureAwait(false)
            ?? throw new MediaServiceException($"媒体源 id={sourceId} 不存在", isNotFound: true);
        if (!string.IsNullOrWhiteSpace(source.ContentDigest)) return null;
        if (source.SourceType != MediaSourceType.Presentation ||
            Path.GetExtension(source.Uri).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new MediaServiceException("仅 PPT/PPTX 原件可以重新准备逐页图片。");
        if (!File.Exists(source.Uri))
            throw new MediaServiceException("文稿原件缺失，无法安全重试转换。");
        var unresolved = await database.MediaPreparationJobs.AnyAsync(item =>
            item.SourceId == sourceId && item.Kind == PreparationJobKind.PptImages &&
            (item.Status == OperationStatus.Running || item.Status == OperationStatus.Uncertain), cancellationToken)
            .ConfigureAwait(false);
        if (unresolved)
            throw new MediaServiceException("旧文稿存在运行中或结果不确定的 Office 作业；请先核查，不能盲目重试。");
        try
        {
            var digest = await _originalDigestReader.ReadSha256Async(source.Uri, cancellationToken).ConfigureAwait(false);
            return new LegacyDigestSnapshot(source.Uri, source.SourceRevision, digest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new MediaServiceException($"读取文稿原件摘要失败，重试未排队：{exception.Message}");
        }
    }

    private sealed record LegacyDigestSnapshot(string Uri, long SourceRevision, string Digest);

    /// <summary>
    /// 成功作业仅在当前原件仍可选择页图时复用，否则允许用户明确重建清单。
    /// :param source: 当前原件与制品元数据。
    /// :returns: 默认页图选择器是否接受当前清单。
    /// </summary>
    private static bool HasPreparedSlideImages(MediaSource source)
    {
        try
        {
            PresentationPlaybackSelector.Select(source, experimentalEnabled: false);
            return true;
        }
        catch (PresentationPreparationException)
        {
            return false;
        }
    }

    public async Task<ArtifactManifest> PublishSlideImagesAsync(
        Guid jobId,
        string stagingDirectory,
        int pageCount,
        CancellationToken cancellationToken = default)
    {
        var layout = dataRootOptions.Resolve(AppContext.BaseDirectory);
        var stagingRoot = Path.Combine(layout.RootPath, "cache", "staging");
        var cacheRoot = Path.Combine(layout.RootPath, "cache", "artifacts");
        var staging = Path.GetFullPath(stagingDirectory);
        if (!IsWithin(staging, stagingRoot) || !Directory.Exists(staging))
            throw new InvalidOperationException("逐页图片暂存目录不存在或越界。");
        if (pageCount is < 1 or > 500) throw new InvalidOperationException("逐页图片页数无效。");

        var files = Directory.GetFiles(staging, "page-*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (files.Length != pageCount) throw new InvalidOperationException("逐页 PNG 数量与文稿页数不一致。");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long totalSize = 0;
        for (var index = 1; index <= pageCount; index++)
        {
            var expected = Path.Combine(staging, $"page-{index:0000}.png");
            if (!string.Equals(files[index - 1], expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"第 {index} 页 PNG 文件名不连续。");
            var bytes = await File.ReadAllBytesAsync(expected, cancellationToken).ConfigureAwait(false);
            if (bytes.Length <= PngSignature.Length || !bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
                throw new InvalidOperationException($"第 {index} 页不是有效 PNG。");
            hash.AppendData(bytes);
            totalSize += bytes.Length;
        }
        var digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var job = await context.MediaPreparationJobs.Include(item => item.Source)
            .SingleOrDefaultAsync(item => item.JobId == jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("逐页图片准备作业不存在。");
        if (job.Kind != PreparationJobKind.PptImages || job.Status != OperationStatus.Running)
            throw new InvalidOperationException("逐页图片准备作业不是运行中状态。");
        if (job.Source.SourceRevision != job.SourceRevision ||
            !string.Equals(job.Source.ContentDigest, job.SourceDigest, StringComparison.Ordinal))
            throw new InvalidOperationException("文稿原件版本已变化，拒绝发布旧页图。");

        var destination = Path.Combine(cacheRoot, job.SourceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SafePathSegment(job.SourceDigest), $"slides-{jobId:N}");
        if (!IsWithin(destination, cacheRoot) || Directory.Exists(destination))
            throw new InvalidOperationException("逐页图片目标目录越界或已存在。");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.Move(staging, destination);
        try
        {
            return await writes.ExecuteAsync(async (database, token) =>
            {
                var currentJob = await database.MediaPreparationJobs.Include(item => item.Source)
                    .ThenInclude(source => source.PptResources)
                    .SingleAsync(item => item.JobId == jobId, token).ConfigureAwait(false);
                if (currentJob.Status != OperationStatus.Running ||
                    currentJob.Source.SourceRevision != currentJob.SourceRevision ||
                    !string.Equals(currentJob.Source.ContentDigest, currentJob.SourceDigest, StringComparison.Ordinal))
                    throw new InvalidOperationException("文稿准备作业已过期，拒绝发布页图。");

                var source = currentJob.Source;
                var metadata = JsonNode.Parse(source.MetadataJson) as JsonObject ?? new JsonObject();
                metadata["slide_images"] = JsonSerializer.SerializeToNode(new
                {
                    status = "ready",
                    directory = destination,
                    page_count = pageCount,
                    source_digest = currentJob.SourceDigest,
                });
                source.MetadataJson = metadata.ToJsonString();
                source.IsAvailable = true;
                database.PptResources.RemoveRange(source.PptResources);
                source.PptResources.Clear();
                for (var index = 1; index <= pageCount; index++)
                {
                    source.PptResources.Add(new PptResource
                    {
                        PageIndex = index,
                        SlideImage = $"/api/sources/{source.Id}/slides/{index}/",
                        CreatedAt = _timeProvider.GetUtcNow(),
                    });
                }

                var manifest = new ArtifactManifest
                {
                    SourceId = source.Id,
                    SourceDigest = currentJob.SourceDigest,
                    RecipeVersion = currentJob.RecipeVersion,
                    RelativePath = Path.GetRelativePath(cacheRoot, destination).Replace('\\', '/'),
                    Sha256 = digest,
                    FileSize = totalSize,
                    PageCount = pageCount,
                    Status = OperationStatus.Succeeded,
                };
                database.ArtifactManifests.Add(manifest);
                currentJob.Status = OperationStatus.Succeeded;
                currentJob.ArtifactManifestJson = JsonSerializer.Serialize(new
                {
                    directory = destination,
                    page_count = pageCount,
                    sha256 = digest,
                });
                return manifest;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 提交结果不明时不能把已发布页图搬回暂存区，否则数据库会指向失效目录。
            await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
            var committed = await check.ArtifactManifests.AsNoTracking().SingleOrDefaultAsync(item =>
                item.SourceId == job.SourceId && item.SourceDigest == job.SourceDigest &&
                item.RelativePath == Path.GetRelativePath(cacheRoot, destination).Replace('\\', '/'),
                CancellationToken.None).ConfigureAwait(false);
            if (committed is not null) return committed;
            if (Directory.Exists(destination) && !Directory.Exists(staging))
                Directory.Move(destination, staging);
            throw;
        }
    }

    public Task FailSlideImagesAsync(Guid jobId, string error, CancellationToken cancellationToken = default) =>
        writes.ExecuteAsync(async (database, token) =>
        {
            var job = await database.MediaPreparationJobs.Include(item => item.Source)
                .SingleAsync(item => item.JobId == jobId, token).ConfigureAwait(false);
            if (job.Status != OperationStatus.Running) return;
            job.Status = OperationStatus.Failed;
            job.ErrorMessage = error;
            if (job.Source.SourceRevision != job.SourceRevision ||
                !string.Equals(job.Source.ContentDigest, job.SourceDigest, StringComparison.Ordinal)) return;
            var metadata = JsonNode.Parse(job.Source.MetadataJson) as JsonObject ?? new JsonObject();
            metadata["slide_images"] = JsonSerializer.SerializeToNode(new
            {
                status = "failed",
                error,
                source_digest = job.SourceDigest,
                page_count = 0,
            });
            job.Source.MetadataJson = metadata.ToJsonString();
            job.Source.IsAvailable = false;
        }, cancellationToken);

    public Task RequeueSlideImagesAsync(Guid jobId, string reason, CancellationToken cancellationToken = default) =>
        SetSlideJobStateAsync(jobId, OperationStatus.Queued, "queued", reason, cancellationToken);

    public Task MarkUncertainSlideImagesAsync(Guid jobId, string reason, CancellationToken cancellationToken = default) =>
        SetSlideJobStateAsync(jobId, OperationStatus.Uncertain, "uncertain", reason, cancellationToken);

    public Task ReconcileInterruptedSlideJobsAsync(CancellationToken cancellationToken = default) =>
        writes.ExecuteAsync(async (database, token) =>
        {
            var interrupted = await database.MediaPreparationJobs.Include(item => item.Source)
                .Where(item => item.Kind == PreparationJobKind.PptImages && item.Status == OperationStatus.Running)
                .ToListAsync(token).ConfigureAwait(false);
            foreach (var job in interrupted)
            {
                job.Status = OperationStatus.Uncertain;
                job.ErrorMessage = "ControlHost 上次退出时转换仍在运行；未盲目重试 Office 副作用。";
                if (job.Source.SourceRevision != job.SourceRevision ||
                    !string.Equals(job.Source.ContentDigest, job.SourceDigest, StringComparison.Ordinal)) continue;
                SetSourceSlideStatus(job.Source, "uncertain", job.ErrorMessage, job.SourceDigest);
            }
        }, cancellationToken);

    private Task SetSlideJobStateAsync(
        Guid jobId,
        OperationStatus jobStatus,
        string sourceStatus,
        string reason,
        CancellationToken cancellationToken) =>
        writes.ExecuteAsync(async (database, token) =>
        {
            var job = await database.MediaPreparationJobs.Include(item => item.Source)
                .SingleAsync(item => item.JobId == jobId, token).ConfigureAwait(false);
            if (job.Status != OperationStatus.Running) return;
            job.Status = jobStatus;
            job.ErrorMessage = reason;
            if (job.Source.SourceRevision != job.SourceRevision ||
                !string.Equals(job.Source.ContentDigest, job.SourceDigest, StringComparison.Ordinal)) return;
            SetSourceSlideStatus(job.Source, sourceStatus, reason, job.SourceDigest);
        }, cancellationToken);

    private static void SetSourceSlideStatus(MediaSource source, string status, string error, string sourceDigest)
    {
        JsonObject metadata;
        try
        {
            metadata = JsonNode.Parse(source.MetadataJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            // 显式准备请求可以重建损坏的状态清单；只读投影不会改写历史元数据。
            metadata = new JsonObject();
        }
        metadata["slide_images"] = JsonSerializer.SerializeToNode(new
        {
            status,
            error,
            source_digest = sourceDigest,
            page_count = 0,
        });
        source.MetadataJson = metadata.ToJsonString();
        source.IsAvailable = false;
    }
}
