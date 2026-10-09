// 每个准备作业拥有独立只读LibreOffice转换实例，不占用两块大屏播放引擎。
using System.Security.Cryptography;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Contracts.Presentations;
using ScpCv.Presentation.Protocol;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;
using ScpCv.Runtime.Presentations;
using System.Xml;

namespace ScpCv.ControlHost.Media;

public sealed class LibreOfficeSlideConverter(IDbContextFactory<ControlDbContext> contexts) : IPptSlideConverter
{
    public bool IsAvailable
    {
        get
        {
            try { return File.Exists(PresentationRuntimePaths.HostExecutable()) && File.Exists(Path.Combine(PresentationRuntimePaths.OfficeProgram, "uno.ini")); }
            catch (FileNotFoundException) { return false; }
        }
    }

    /// <summary>:param jobId: 独立转换作业。:param sourceRevision: 原件修订。:param sourcePath: 已核受管理原件。:param outputDirectory: 空暂存目录。:param cancellationToken: 等待寿命。:returns: 真实页图及原件保持结果。</summary>
    public async Task<SlideConversionResult> ConvertAsync(Guid jobId, long sourceRevision, string sourcePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        // 转换由ControlHost准备作业拥有，不依赖两块大屏是否已启动。
        using var parent = Process.GetCurrentProcess();
        var lifetimeEpoch = parent.StartTime.ToUniversalTime().Ticks;
        await using var stream = File.OpenRead(sourcePath);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        await using var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var job = await database.MediaPreparationJobs.AsNoTracking().SingleAsync(item => item.JobId == jobId, cancellationToken).ConfigureAwait(false);
        if (job.Status != OperationStatus.Running || job.SourceRevision != sourceRevision ||
            !string.Equals(job.SourceDigest, digest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("转换作业状态/原件修订或摘要不匹配");
        // 纯包拒绝尚无引擎/导出副作用，不能经侧车fatal退出误记为uncertain。
        try { _ = new PresentationPackageMetadata(sourcePath); }
        catch (Exception error) when (error is InvalidDataException or XmlException or ArgumentException or UriFormatException)
        { return new SlideConversionResult(OperationStatus.Failed, 0, "presentation_package_rejected", error.Message); }
        var scope = new PresentationScope
        {
            Role = "converter", EngineInstanceId = Guid.NewGuid(), GroupEpoch = lifetimeEpoch, OwnerEpoch = lifetimeEpoch, EngineEpoch = 1,
            DocumentEpoch = 1, SourceId = job.SourceId, SourceRevision = sourceRevision, SourceGeneration = sourceRevision, SourceDigest = digest,
        };
        var client = new PresentationProcessClient(PresentationRuntimePaths.HostExecutable(), PresentationRuntimePaths.OfficeProgram,
            PresentationRuntimePaths.EngineProfile(scope.EngineInstanceId), scope);
        SlideConversionResult converted;
        try
        {
            converted = await ConvertWithClientAsync(client, scope, job, sourcePath, outputDirectory, stream, digest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception operationFailure)
        {
            try { await client.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup)
            {
                throw new PresentationTransportException("转换动作失败，且自有侧车退出也未确认；保留两原因，不可盲重试。",
                    new AggregateException(operationFailure, cleanup));
            }
            throw;
        }
        try { await client.DisposeAsync().ConfigureAwait(false); }
        catch (Exception cleanup) when (converted.Status != OperationStatus.Succeeded)
        {
            // 返回的Failed/Uncertain也是原操作证据，不只保留抛异常的动作失败。
            throw new PresentationTransportException("转换返回未成功结果，且自有侧车退出也未确认；保留响应与退出原因。",
                new AggregateException(new InvalidOperationException("未成功转换响应：" + converted.Code + "；" + converted.Detail), cleanup));
        }
        return converted;
    }

    /// <summary>:param client: 本次真实stdio实例。:param scope: 已验证归属。:param job: 当前作业。:param sourcePath: 原件。:param outputDirectory: stage。:param stream: 保持打开的原件。:param digest: 原摘要。:param cancellationToken: 等待寿命。:returns: 原转换行为；外层负责单次精确退出及双失败诊断。</summary>
    private static async Task<SlideConversionResult> ConvertWithClientAsync(PresentationProcessClient client, PresentationScope scope,
        MediaPreparationJob job, string sourcePath, string outputDirectory, FileStream stream, string digest, CancellationToken cancellationToken)
    {
        var jobId = job.JobId;
        var request = new PresentationRequest
        {
            OperationId = jobId, ParentJobId = jobId, Scope = scope, Operation = "export_slides", SourcePath = Path.GetFullPath(sourcePath),
            OutputDirectory = Path.GetFullPath(outputDirectory), Deadline = DateTimeOffset.UtcNow.AddMinutes(4).ToString("O"),
            FitText = job.RecipeVersion.StartsWith("ppt-images-fit-", StringComparison.Ordinal),
        };
        var response = await client.SendAsync(request, scope, cancellationToken).ConfigureAwait(false);
        var result = PresentationResultPolicy.Match(request, response, scope, DateTimeOffset.UtcNow);
        if (!result.CanAcceptArtifact) return new SlideConversionResult(OperationStatus.Failed, 0, response.ErrorCode, response.ErrorDetail);
        using var artifactHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var page = 1; page <= response.Observation!.TotalSlides; page++)
            artifactHash.AppendData(await File.ReadAllBytesAsync(Path.Combine(outputDirectory, $"page-{page:0000}.png"), cancellationToken).ConfigureAwait(false));
        artifactHash.AppendData(await File.ReadAllBytesAsync(Path.Combine(outputDirectory, "presentation.pdf"), cancellationToken).ConfigureAwait(false));
        if (!string.Equals(Convert.ToHexString(artifactHash.GetHashAndReset()), response.Observation.ArtifactDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("实际PNG/PDF制品摘要与宿主完成证据不匹配");
        stream.Position = 0;
        if (Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)) != digest)
            throw new InvalidDataException("LibreOffice转换后原件摘要变化");
        var shutdownScope = new PresentationScope { Role = "converter", EngineInstanceId = scope.EngineInstanceId, GroupEpoch = scope.GroupEpoch, OwnerEpoch = scope.OwnerEpoch, EngineEpoch = scope.EngineEpoch };
        await client.SendAsync(new PresentationRequest
        {
            Scope = shutdownScope, OperationId = Guid.NewGuid(), ParentShutdownId = Guid.NewGuid(), Operation = "shutdown",
            Deadline = DateTimeOffset.UtcNow.AddSeconds(20).ToString("O"),
        }, shutdownScope, cancellationToken).ConfigureAwait(false);
        return new SlideConversionResult(OperationStatus.Succeeded, response.Observation?.TotalSlides ?? 0, "ok", string.Empty);
    }
}
