// 后台 PPT 转页图作业：在无演出占用时经独立 OfficeHost 运行，失败如实落库。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Media;

public sealed partial class PptConversionHostedService(
    MediaPreparationService preparation,
    IPptSlideConverter converter,
    IDbContextFactory<ControlDbContext> contextFactory,
    DataRootOptions dataRoot,
    ILogger<PptConversionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await preparation.ReconcileInterruptedSlideJobsAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (converter.IsAvailable)
                {
                    var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages, stoppingToken)
                        .ConfigureAwait(false);
                    if (job is not null) await ProcessAsync(job, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                LogLoopError(logger, exception);
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ProcessAsync(MediaPreparationJob job, CancellationToken cancellationToken)
    {
        try
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var source = await database.MediaSources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == job.SourceId, cancellationToken)
                .ConfigureAwait(false);
            if (source is null || source.SourceRevision != job.SourceRevision ||
                !string.Equals(source.ContentDigest, job.SourceDigest, StringComparison.Ordinal))
            {
                await preparation.FailSlideImagesAsync(job.JobId, "文稿版本已变化或原件记录不存在。", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            if (!File.Exists(source.Uri))
            {
                await preparation.FailSlideImagesAsync(job.JobId, "文稿原件文件不存在。", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var layout = dataRoot.Resolve(AppContext.BaseDirectory);
            var staging = Path.Combine(layout.RootPath, "cache", "staging", job.JobId.ToString("N"));
            if (Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any())
            {
                await preparation.MarkUncertainSlideImagesAsync(job.JobId,
                    "暂存目录已有上次输出；未重放可能已发生的 Office 转换。", cancellationToken).ConfigureAwait(false);
                return;
            }
            Directory.CreateDirectory(staging);
            var result = await converter.ConvertAsync(job.JobId, job.SourceRevision, source.Uri, staging, cancellationToken)
                .ConfigureAwait(false);
            if (result.Status == OperationStatus.Succeeded && result.PageCount > 0)
            {
                await preparation.PublishSlideImagesAsync(job.JobId, staging, result.PageCount, cancellationToken)
                    .ConfigureAwait(false);
                LogCompleted(logger, job.SourceId, result.PageCount);
                return;
            }
            if (result.Status == OperationStatus.Uncertain)
            {
                await preparation.MarkUncertainSlideImagesAsync(job.JobId, result.Detail, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            if (result.Code is "office_unavailable" or "group_not_armed" or "office_slot_busy" or "office_conversion_busy")
            {
                await preparation.RequeueSlideImagesAsync(job.JobId, result.Detail, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            await preparation.FailSlideImagesAsync(job.JobId,
                result.Detail.Length == 0 ? result.Code : result.Detail, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await preparation.MarkUncertainSlideImagesAsync(job.JobId,
                "转换期间 ControlHost 停止；未盲目重试 Office 副作用。", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            LogJobError(logger, exception, job.SourceId);
            await preparation.FailSlideImagesAsync(job.JobId, exception.Message, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [LoggerMessage(4201, LogLevel.Error, "PPT 逐页图片作业循环异常；下一轮继续检查。")]
    private static partial void LogLoopError(ILogger logger, Exception exception);

    [LoggerMessage(4202, LogLevel.Information, "PPT 源 {SourceId} 已导出 {PageCount} 页图片。")]
    private static partial void LogCompleted(ILogger logger, long sourceId, int pageCount);

    [LoggerMessage(4203, LogLevel.Error, "PPT 源 {SourceId} 逐页图片作业失败。")]
    private static partial void LogJobError(ILogger logger, Exception exception, long sourceId);
}
