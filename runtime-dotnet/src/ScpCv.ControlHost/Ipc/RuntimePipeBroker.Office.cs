// 旧PowerPoint协议只保留明确退役响应，不再转发或生成任何Office副作用。
using System.Text.Json;
using ScpCv.Contracts.Ipc;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    public bool CanConvertSlides => false;

    /// <summary>:returns: 旧转换接口明确失败，不调用已登记的历史Office连接。</summary>
    public Task<OfficeResultDto> ConvertSlidesAsync(Guid jobId, long sourceRevision, string path,
        string outputDirectory, CancellationToken cancellationToken = default) =>
        Task.FromResult(RetiredOfficeResult(jobId));

    private static Task<IpcFrameDto> ForwardOfficeRequestAsync(RuntimeConnection player,
        IpcFrameDto frame, CancellationToken cancellationToken)
    {
        var request = frame.Payload.Deserialize<OfficeRequestDto>()
            ?? throw new InvalidDataException("OfficeRequest payload 无效。");
        return Task.FromResult(Response(frame, "office_result", RetiredOfficeResult(request.OfficeOperationId)));
    }

    private static Task<IpcFrameDto> CompleteOfficeResultAsync(RuntimeConnection office,
        IpcFrameDto frame, CancellationToken cancellationToken) =>
        Task.FromResult(Response(frame, "office_result_accepted", new { accepted = false, reason = "powerpoint_retired" }));

    private static OfficeResultDto RetiredOfficeResult(Guid operationId) => new()
    {
        OfficeOperationId = operationId, Status = "failed", ErrorCode = "powerpoint_retired",
        ErrorDetail = "PowerPoint已退役；请使用LibreOffice。",
        ResultFingerprint = "failed:powerpoint_retired",
        Result = JsonSerializer.SerializeToElement(new { code = "powerpoint_retired" }),
    };
}
