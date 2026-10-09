/*
 * 结果匹配与晚到围栏：受理、不确定和生命周期回执不能冒充实际画面。
 * @Project : SCP-cv
 * @File : PresentationResultPolicy.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System;

namespace ScpCv.Presentation.Protocol;

/// <summary>匹配决策不证明原生出画、OS 退出或父租约有效；这些仍由可信宿主负责。</summary>
public sealed class PresentationResultDecision
{
    /// <summary>
    /// 创建结果决策。
    /// :param matched: 当前请求/身份/指纹是否匹配。
    /// :param code: 拒绝或分类原因。
    /// :param applyObservation: 是否可采用确认的文稿观测。
    /// :param acceptArtifact: 是否可采用转换产物观测。
    /// :param recordLifecycle: 是否可记录生命周期完成回执，不是 OS 退出证明。
    /// :param duplicate: 是否为已接受的相同终态指纹。
    /// </summary>
    public PresentationResultDecision(bool matched, string code, bool applyObservation = false,
        bool acceptArtifact = false, bool recordLifecycle = false, bool duplicate = false)
    {
        Matched = matched; Code = code; CanApplyObservation = applyObservation;
        CanAcceptArtifact = acceptArtifact; CanRecordLifecycleCompletion = recordLifecycle; Duplicate = duplicate;
    }
    public bool Matched { get; }
    public string Code { get; }
    public bool CanApplyObservation { get; }
    public bool CanAcceptArtifact { get; }
    public bool CanRecordLifecycleCompletion { get; }
    public bool Duplicate { get; }
}

public static class PresentationResultPolicy
{
    /// <summary>
    /// 匹配结果与最新可信 scope；同请求不同终态指纹必须拒绝，不能重复施加观察。
    /// :param request: 调用方保留的原始请求快照。
    /// :param response: 已收到结果。
    /// :param currentScope: 可信宿主最新授权操作 scope，不得取 response.Scope 代替。
    /// :param now: 当前可信 UTC 时间。
    /// :param previousResultFingerprint: 已接受终态的指纹；未接受过时为 null。
    /// :returns: 状态分类与可采用证据类别；不执行原生操作。
    /// </summary>
    public static PresentationResultDecision Match(PresentationRequest request, PresentationResponse response,
        PresentationScope currentScope, DateTimeOffset now, string? previousResultFingerprint = null)
    {
        var requestValid = PresentationRequestPolicy.Validate(request, now);
        if (!requestValid.Allowed) return Reject(requestValid.Code);
        var responseValid = ValidateEnvelope(response, requireFingerprint: true);
        if (!responseValid.Allowed) return Reject(responseValid.Code);
        if (response.OperationId != request.OperationId) return Reject("operation_fenced");
        if (!PresentationRequestPolicy.SameScope(request.Scope, currentScope) ||
            !PresentationRequestPolicy.SameScope(request.Scope, response.Scope)) return Reject("scope_fenced");
        if (!string.Equals(PresentationFingerprints.Request(request), response.RequestFingerprint, StringComparison.OrdinalIgnoreCase))
            return Reject("request_fingerprint_fenced");
        if (!string.Equals(PresentationFingerprints.Response(response), response.ResultFingerprint, StringComparison.OrdinalIgnoreCase))
            return Reject("invalid_result_fingerprint");
        PresentationRequestPolicy.TryTimestamp(response.CompletedAt, out var completed);
        PresentationRequestPolicy.TryTimestamp(request.Deadline, out var deadline);
        if (completed > now || completed > deadline) return Reject("late_or_future_result");
        if (previousResultFingerprint != null)
        {
            if (!PresentationRequestPolicy.IsDigest(previousResultFingerprint)) return Reject("invalid_previous_fingerprint");
            return string.Equals(previousResultFingerprint, response.ResultFingerprint, StringComparison.OrdinalIgnoreCase)
                ? new PresentationResultDecision(true, "duplicate", duplicate: true) : Reject("result_conflict");
        }
        if (response.Status == "noop")
        {
            // 无播放动作仍须独立匹配请求和实际文稿；不能借此授予其它操作或省略当前画面身份。
            if (request.Operation != "next_effect" && request.Operation != "next_slide") return Reject("invalid_no_op_operation");
            var boundary = response.Observation!;
            if (boundary.SourceId != request.Scope.SourceId) return Reject("observation_source_fenced");
            return new PresentationResultDecision(true, "no_op_confirmed", applyObservation: true);
        }
        if (response.Status != "succeeded") return new PresentationResultDecision(true,
            response.Status == "accepted" ? "accepted_only" : response.Status);
        if (request.Operation == "shutdown")
            return new PresentationResultDecision(true, "lifecycle_receipt_only", recordLifecycle: true);
        var observed = response.Observation;
        if (observed == null || !observed.Confirmed) return Reject("unconfirmed_observation");
        if (request.Operation == "close")
            return observed.SourceId == null && observed.DocumentClosed && observed.CurrentSlide == 0 && observed.TotalSlides == 0
                ? new PresentationResultDecision(true, "closed", applyObservation: true) : Reject("close_not_confirmed");
        if (observed.SourceId != request.Scope.SourceId || observed.DocumentClosed || observed.EngineExited)
            return Reject("observation_source_fenced");
        if (request.Operation == "export_slides")
            return observed.TotalSlides > 0 && PresentationRequestPolicy.IsDigest(observed.ArtifactDigest)
                ? new PresentationResultDecision(true, "artifact_confirmed", acceptArtifact: true) : Reject("artifact_unconfirmed");
        if (observed.CurrentSlide < 1 || observed.TotalSlides < 1) return Reject("invalid_slide_observation");
        return new PresentationResultDecision(true, "confirmed", applyObservation: true);
    }

    /// <summary>
    /// 校验固定返回值范围。盖结果指纹前可不要求自身指纹；不能跳过匹配。
    /// :param response: 返回纯值。
    /// :param requireFingerprint: 是否已从 wire 收到完整盖章。
    /// :returns: 结构校验，不授权实际画面更新。
    /// </summary>
    internal static PresentationDecision ValidateEnvelope(PresentationResponse? response, bool requireFingerprint)
    {
        if (response == null || response.Scope == null) return new PresentationDecision(false, "invalid_response");
        if (response.ProtocolVersion != PresentationRequestPolicy.ProtocolVersion)
            return new PresentationDecision(false, "unsupported_protocol_version");
        if (response.OperationId == Guid.Empty || !PresentationRequestPolicy.IsDigest(response.RequestFingerprint) ||
            (requireFingerprint && !PresentationRequestPolicy.IsDigest(response.ResultFingerprint)))
            return new PresentationDecision(false, "invalid_response_identity");
        var scope = response.Scope;
        if (scope.EngineInstanceId == Guid.Empty || scope.GroupEpoch <= 0 || scope.OwnerEpoch <= 0 || scope.EngineEpoch <= 0 ||
            !((scope.Role == "converter" && scope.WindowId == 0) || (scope.Role == "impress-1" && scope.WindowId == 1) ||
              (scope.Role == "impress-2" && scope.WindowId == 2))) return new PresentationDecision(false, "invalid_response_scope");
        var document = scope.DocumentEpoch > 0 && scope.SourceId > 0 && scope.SourceRevision > 0 &&
            scope.SourceGeneration > 0 && PresentationRequestPolicy.IsDigest(scope.SourceDigest);
        var noDocument = scope.DocumentEpoch == 0 && scope.SourceId == 0 && scope.SourceRevision == 0 &&
            scope.SourceGeneration == 0 && scope.SourceDigest == string.Empty;
        if (!document && !noDocument) return new PresentationDecision(false, "invalid_response_scope");
        if (!PresentationRequestPolicy.TryTimestamp(response.CompletedAt, out _) || response.ErrorCode == null ||
            response.ErrorCode.Length > 128 || response.ErrorDetail == null || response.ErrorDetail.Length > 4096)
            return new PresentationDecision(false, "invalid_response_detail");
        if (response.Status != "accepted" && response.Status != "succeeded" && response.Status != "failed" &&
            response.Status != "unsupported" && response.Status != "uncertain" && response.Status != "noop") return new PresentationDecision(false, "invalid_response_status");
        if (response.Status == "succeeded" && response.ErrorCode.Length != 0)
            return new PresentationDecision(false, "contradictory_success");
        var observed = response.Observation;
        if (observed != null && (observed.CurrentSlide < 0 || observed.TotalSlides < 0 ||
            observed.TotalSlides > PresentationRequestPolicy.MaximumSlides || observed.CurrentSlide > observed.TotalSlides ||
            observed.SourceId <= 0 || observed.PositionMs < 0 || observed.DurationMs < 0 || observed.ArtifactDigest == null ||
            (observed.ArtifactDigest.Length != 0 && !PresentationRequestPolicy.IsDigest(observed.ArtifactDigest))))
            return new PresentationDecision(false, "invalid_observation");
        // 隐藏页/自定义放映的实际末页可能不等于count-1；终端事实由自有宿主确认，纯合同不猜动画状态。
        if (response.Status == "noop" && (response.ErrorCode != "presentation_at_last_slide" || observed == null ||
            !observed.Confirmed || observed.SourceId != scope.SourceId || observed.DocumentClosed || observed.EngineExited ||
            observed.CurrentSlide < 1 || observed.TotalSlides < 1 || observed.ArtifactDigest.Length != 0))
            return new PresentationDecision(false, "invalid_no_op_boundary");
        return new PresentationDecision(true, "ok");
    }

    /// <summary>
    /// 创建无实际更新权限的拒绝。
    /// :param code: 拒绝原因。
    /// :returns: 不采用迟到或无效观测。
    /// </summary>
    private static PresentationResultDecision Reject(string code) => new PresentationResultDecision(false, code);
}
