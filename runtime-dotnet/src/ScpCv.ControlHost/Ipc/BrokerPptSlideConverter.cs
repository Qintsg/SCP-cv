// 上传转换作业经受管管道发送至独立 PowerPointHost STA。
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;

namespace ScpCv.ControlHost.Ipc;

public sealed class BrokerPptSlideConverter(RuntimePipeBroker broker) : IPptSlideConverter
{
    public bool IsAvailable => broker.CanConvertSlides;

    public async Task<SlideConversionResult> ConvertAsync(
        Guid jobId,
        long sourceRevision,
        string sourcePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await broker.ConvertSlidesAsync(jobId, sourceRevision, sourcePath, outputDirectory, cancellationToken)
            .ConfigureAwait(false);
        var status = result.Status switch
        {
            "succeeded" => OperationStatus.Succeeded,
            "uncertain" => OperationStatus.Uncertain,
            _ => OperationStatus.Failed,
        };
        var pages = result.Result.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    result.Result.TryGetProperty("page_count", out var count) && count.TryGetInt32(out var parsed)
            ? parsed
            : 0;
        return new SlideConversionResult(status, pages, result.ErrorCode, result.ErrorDetail);
    }
}
