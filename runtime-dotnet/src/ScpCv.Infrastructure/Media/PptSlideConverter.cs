// PPT 页图转换器边界：Office COM 只能在独立宿主执行。
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public sealed record SlideConversionResult(OperationStatus Status, int PageCount, string Code, string Detail);

public interface IPptSlideConverter
{
    bool IsAvailable { get; }

    Task<SlideConversionResult> ConvertAsync(
        Guid jobId,
        long sourceRevision,
        string sourcePath,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailablePptSlideConverter : IPptSlideConverter
{
    public bool IsAvailable => false;

    public Task<SlideConversionResult> ConvertAsync(
        Guid jobId,
        long sourceRevision,
        string sourcePath,
        string outputDirectory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SlideConversionResult(OperationStatus.Failed, 0, "office_unavailable",
            "当前未连接可用的独立 Office 宿主。"));
}
