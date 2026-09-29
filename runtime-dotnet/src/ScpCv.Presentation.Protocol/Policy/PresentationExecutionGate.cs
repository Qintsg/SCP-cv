/*
 * 演示文稿请求的无副作用能力门禁；原生 executor 是调用方显式提供的 seam。
 * @Project : SCP-cv
 * @File : PresentationExecutionGate.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System;
using System.Globalization;
using System.IO;

namespace ScpCv.Presentation.Protocol;

public static class PresentationExecutionGate
{
    /// <summary>
    /// 校验和能力拒绝先于外部执行；未支持的统一媒体控制不得进入 executor。
    /// :param request: 带完整不可变操作身份的纯值请求。
    /// :param capabilities: 宿主经实际验证后发布的能力。
    /// :param currentScope: 可信宿主当前授权操作的 scope，不能拿 request.Scope 冒充授权。
    /// :param now: 调用方提供的当前 UTC 时间。
    /// :param execute: 仅获授权后调用的外部副作用 seam。
    /// :returns: 完整拒绝结果或 executor 返回的真实结果。
    /// </summary>
    public static PresentationResponse Execute(PresentationRequest request, PresentationCapabilities capabilities,
        PresentationScope currentScope, DateTimeOffset now, Func<PresentationRequest, PresentationResponse> execute)
    {
        // 先获取独立、有限的纯值快照，再对该快照校验并授权，原始 DTO 不是 immutable。
        var captured = PresentationWireCodec.CaptureRequest(request);
        var validation = PresentationRequestPolicy.Validate(captured, now);
        if (!validation.Allowed) throw new InvalidDataException(validation.Code);
        var nativeAction = execute ?? throw new ArgumentNullException(nameof(execute));
        var supported = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        if (!PresentationRequestPolicy.SameScope(captured.Scope, currentScope))
            return Refused(captured, "failed", "scope_fenced", now);
        if (captured.Operation == "media_pause" || captured.Operation == "media_resume" || captured.Operation == "media_seek")
            return Refused(captured, "unsupported", "unsupported_media_control", now);
        if (!Supported(captured.Operation, supported)) return Refused(captured, "unsupported", "capability_unavailable", now);
        // executor 仍须在宿主串行线程核对真实 OS/父租约；最新结果 scope 由 matcher 再验证。
        return nativeAction(captured);
    }

    /// <summary>
    /// 生成匹配当前请求的拒绝结果，不附加伪造的实际观测。
    /// :param request: 已验证请求。
    /// :param status: failed 或 unsupported。
    /// :param code: 拒绝原因。
    /// :param now: 拒绝时间。
    /// :returns: 完整指纹拒绝结果。
    /// </summary>
    private static PresentationResponse Refused(PresentationRequest request, string status, string code, DateTimeOffset now)
    {
        var response = new PresentationResponse
        {
            OperationId = request.OperationId, Scope = PresentationFingerprints.CopyScope(request.Scope),
            RequestFingerprint = PresentationFingerprints.Request(request), Status = status,
            CompletedAt = now.ToString("O", CultureInfo.InvariantCulture), ErrorCode = code,
        };
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        return response;
    }

    /// <summary>
    /// 仅按宿主已经验证的有限能力授权，未声明能力保持 false。
    /// :param operation: 有限动作名称。
    /// :param capabilities: 可信宿主能力表。
    /// :returns: 是否声明支持；三项统一媒体动作始终由更前置硬拒绝处理。
    /// </summary>
    private static bool Supported(string operation, PresentationCapabilities capabilities) => operation switch
    {
        "open" => capabilities.CanOpen,
        "close" => capabilities.CanClose,
        "next_slide" or "previous_slide" or "goto" => capabilities.CanNavigateSlides,
        "next_effect" or "previous_effect" => capabilities.CanStepEffects,
        "observe" => capabilities.CanObserve,
        "export_slides" => capabilities.CanExportSlides,
        "shutdown" => capabilities.CanShutdown,
        "media_volume" => capabilities.CanSetMediaVolume,
        "media_mute" => capabilities.CanMuteMedia,
        _ => false,
    };
}
