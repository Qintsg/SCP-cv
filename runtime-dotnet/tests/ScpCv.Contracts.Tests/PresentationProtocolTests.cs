/*
 * 文稿纯值协议的公开 seam 回归；原生动作仅使用外部回调计数，不启动播放器。
 * @Project : SCP-cv
 * @File : PresentationProtocolTests.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System.Globalization;
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 用户已接受不支持的统一暂停必须在原生动作前拒绝，即使调用方试图打开能力。
    /// :returns: 明确 unsupported，并保持外部 executor 调用次数为零。
    /// </summary>
    [Fact]
    public void UnsupportedUnifiedPauseNeverReachesNativeExecutor()
    {
        var request = CreateRequest("media_pause");
        var capabilities = new PresentationCapabilities { CanPauseMedia = true };
        var nativeCalls = 0;
        var response = PresentationExecutionGate.Execute(request, capabilities, CloneScope(request.Scope), Now, _ =>
        {
            nativeCalls++;
            return new PresentationResponse { Status = "succeeded" };
        });

        Assert.Equal("unsupported", response.Status);
        Assert.Equal("unsupported_media_control", response.ErrorCode);
        Assert.Equal(0, nativeCalls);
        Assert.Null(response.Observation);
    }

    /// <summary>
    /// 创建结构完整的本机显示目标请求，不证明其声明 PID/HWND 的 OS 归属。
    /// :param operation: 有限动作名称。
    /// :returns: 可用于协议和纯策略回归的合法请求。
    /// </summary>
    private static PresentationRequest CreateRequest(string operation = "open") => new()
    {
        OperationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
        ParentCommandId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"),
        ClaimToken = Guid.Parse("cccccccc-0000-0000-0000-000000000003"),
        Scope = new PresentationScope
        {
            Role = "impress-1", WindowId = 1,
            EngineInstanceId = Guid.Parse("dddddddd-0000-0000-0000-000000000004"),
            GroupEpoch = 4, OwnerEpoch = 5, EngineEpoch = 6, DocumentEpoch = 7,
            SourceId = 8, SourceRevision = 9, SourceGeneration = 10,
            SourceDigest = new string('a', 64),
        },
        Deadline = Now.AddMinutes(1).ToString("O", CultureInfo.InvariantCulture),
        Operation = operation,
        SourcePath = operation == "open" ? @"C:\media\PPT文件\原件.pptx" : string.Empty,
        TargetSlide = operation == "open" ? 1 : 0,
        ParentWindow = new PresentationParentWindow
        {
            Handle = 12345, ProcessId = 23456,
            ProcessStartTime = Now.AddHours(-1).ToString("O", CultureInfo.InvariantCulture),
            SessionId = 1, Dpi = 96, Width = 1920, Height = 1080,
        },
    };

    /// <summary>
    /// 创建不共享可变引用的纯值 scope 夹具，不冒充实际 OS/租约证明。
    /// :param scope: 原始纯值。
    /// :returns: 独立夹具快照。
    /// </summary>
    private static PresentationScope CloneScope(PresentationScope scope) => new()
    {
        Role = scope.Role, WindowId = scope.WindowId, EngineInstanceId = scope.EngineInstanceId,
        GroupEpoch = scope.GroupEpoch, OwnerEpoch = scope.OwnerEpoch, EngineEpoch = scope.EngineEpoch,
        DocumentEpoch = scope.DocumentEpoch, SourceId = scope.SourceId, SourceRevision = scope.SourceRevision,
        SourceGeneration = scope.SourceGeneration, SourceDigest = scope.SourceDigest,
    };

    /// <summary>
    /// 为当前请求创建已盖章纯值结果夹具，不代表实际原生完成。
    /// :param request: 原始请求。
    /// :param status: 有限结果状态。
    /// :returns: 用于 matcher 测试的独立结果。
    /// </summary>
    private static PresentationResponse ResponseFor(PresentationRequest request, string status = "succeeded")
    {
        var result = new PresentationResponse
        {
            OperationId = request.OperationId, Scope = CloneScope(request.Scope), Status = status,
            RequestFingerprint = PresentationFingerprints.Request(request), CompletedAt = Now.ToString("O", CultureInfo.InvariantCulture),
            Observation = new PresentationObservation
            {
                Confirmed = true, SourceId = request.Scope.SourceId, CurrentSlide = 1, TotalSlides = 9,
            },
        };
        result.ResultFingerprint = PresentationFingerprints.Response(result);
        return result;
    }

    /// <summary>
    /// 创建独立 converter 作业身份，不获取 Display 租约。
    /// :returns: 有限原件导出请求。
    /// </summary>
    private static PresentationRequest CreateConverterRequest()
    {
        var request = CreateRequest("export_slides");
        request.Scope.Role = "converter";
        request.Scope.WindowId = 0;
        request.ParentCommandId = null;
        request.ClaimToken = null;
        request.ParentJobId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000005");
        request.ParentWindow = null;
        request.SourcePath = @"C:\media\原件.pptx";
        request.OutputDirectory = @"C:\prepared\独立作业";
        return request;
    }

    /// <summary>
    /// 用生命周期 message_id 关闭自有引擎，不伪造父命令或当前文稿。
    /// :returns: 无文稿作用域的 shutdown 请求。
    /// </summary>
    private static PresentationRequest CreateShutdownRequest()
    {
        var request = CreateRequest("shutdown");
        request.ParentCommandId = null;
        request.ClaimToken = null;
        request.ParentWindow = null;
        request.ParentShutdownId = Guid.Parse("ffffffff-0000-0000-0000-000000000006");
        request.Scope.DocumentEpoch = 0;
        request.Scope.SourceId = 0;
        request.Scope.SourceRevision = 0;
        request.Scope.SourceGeneration = 0;
        request.Scope.SourceDigest = string.Empty;
        return request;
    }
}
