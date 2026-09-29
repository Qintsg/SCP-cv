/*
 * 请求/结果/current-scope 和终态指纹策略回归，不伪造原生效果或退出证明。
 * @Project : SCP-cv
 * @File : PresentationProtocolResultTests.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    /// <summary>
    /// accepted/uncertain/failed/unsupported 即使携带确认观测也不能更新画面。
    /// :param status: 非完成状态。
    /// :returns: 匹配身份不等于采用实际画面。
    /// </summary>
    [Theory]
    [InlineData("accepted", "accepted_only")]
    [InlineData("uncertain", "uncertain")]
    [InlineData("failed", "failed")]
    [InlineData("unsupported", "unsupported")]
    public void NonSuccessStatesNeverAuthorizePictureUpdates(string status, string code)
    {
        var request = CreateRequest();
        var response = ResponseFor(request, status);
        var decision = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.True(decision.Matched);
        Assert.Equal(code, decision.Code);
        Assert.False(decision.CanApplyObservation);
        Assert.False(decision.CanAcceptArtifact);
        Assert.False(decision.CanRecordLifecycleCompletion);
    }

    /// <summary>
    /// 旧结果不能覆盖最新文稿、进程、目标、所有者或源摘要。
    /// :param fence: 最新当前状态已改变的维度。
    /// :returns: 丢弃旧结果并保留可诊断围栏原因。
    /// </summary>
    [Theory]
    [InlineData("role")]
    [InlineData("window")]
    [InlineData("instance")]
    [InlineData("group")]
    [InlineData("owner")]
    [InlineData("engine")]
    [InlineData("document")]
    [InlineData("source")]
    [InlineData("revision")]
    [InlineData("generation")]
    [InlineData("digest")]
    public void LateResultsCannotUpdateAnyReplacementScope(string fence)
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        var replacement = CloneScope(request.Scope);
        ChangeFence(replacement, fence);
        var decision = PresentationResultPolicy.Match(request, response, replacement, Now);
        Assert.False(decision.Matched);
        Assert.Equal("scope_fenced", decision.Code);
        Assert.False(decision.CanApplyObservation);
    }

    /// <summary>
    /// 当前 scope 相同仍须匹配 operation、请求参数指纹及结果内容指纹。
    /// :param mismatch: 被改写的结果/请求维度。
    /// :param code: 期望诊断码。
    /// :returns: 任意错配都无画面更新权限。
    /// </summary>
    [Theory]
    [InlineData("operation", "operation_fenced")]
    [InlineData("request_hash", "request_fingerprint_fenced")]
    [InlineData("result_hash", "invalid_result_fingerprint")]
    [InlineData("changed_parameters", "request_fingerprint_fenced")]
    public void MatchingEpochsDoNotBypassOperationAndFingerprintChecks(string mismatch, string code)
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        if (mismatch == "operation") response.OperationId = Guid.NewGuid();
        else if (mismatch == "request_hash") response.RequestFingerprint = new string('b', 64);
        else if (mismatch == "result_hash") response.ResultFingerprint = new string('b', 64);
        else request.SourcePath = @"C:\media\另一个原件.pptx";
        var decision = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.Equal(code, decision.Code);
        Assert.False(decision.CanApplyObservation);
    }

    /// <summary>
    /// 相同终态可幂等确认，但不得再应用；同请求的另一个终态指纹必须冲突。
    /// :returns: 幂等或明确冲突，不重复施加外部副作用。
    /// </summary>
    [Fact]
    public void DuplicateTerminalResultsAreIdempotentAndConflictingResultsAreRejected()
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        var accepted = response.ResultFingerprint;
        var duplicate = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now, accepted);
        Assert.True(duplicate.Matched);
        Assert.True(duplicate.Duplicate);
        Assert.False(duplicate.CanApplyObservation);
        var changed = response.Observation ?? throw new InvalidOperationException("夹具缺少观测");
        changed.CurrentSlide = 2;
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        var conflict = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now, accepted);
        Assert.False(conflict.Matched);
        Assert.Equal("result_conflict", conflict.Code);
    }

    /// <summary>
    /// 结构完整结果也不能更新已过期请求或未来完成时间。
    /// :returns: 期限不能作为“必定完成”的承诺。
    /// </summary>
    [Fact]
    public void ExpiredRequestsAndFutureCompletionTimesCannotAuthorizeObservation()
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        Assert.Equal("deadline_expired", PresentationResultPolicy.Match(request, response,
            CloneScope(request.Scope), Now.AddMinutes(2)).Code);
        response.CompletedAt = Now.AddSeconds(1).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        Assert.Equal("late_or_future_result", PresentationResultPolicy.Match(request, response,
            CloneScope(request.Scope), Now).Code);
    }

    /// <summary>
    /// 成功标签但没有确认观测或观测源错误，不能冒充实际画面。
    /// :param fault: 缺失/未确认/错误源/已关闭的观测。
    /// :returns: 拒绝实际状态应用。
    /// </summary>
    [Theory]
    [InlineData("missing", "unconfirmed_observation")]
    [InlineData("unconfirmed", "unconfirmed_observation")]
    [InlineData("wrong_source", "observation_source_fenced")]
    [InlineData("closed", "observation_source_fenced")]
    public void SuccessLabelsDoNotReplaceActualObservationEvidence(string fault, string code)
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        if (fault == "missing") response.Observation = null;
        else
        {
            var observed = response.Observation ?? throw new InvalidOperationException("夹具缺少观测");
            if (fault == "unconfirmed") observed.Confirmed = false;
            else if (fault == "wrong_source") observed.SourceId++;
            else observed.DocumentClosed = true;
        }
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        var decision = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.Equal(code, decision.Code);
        Assert.False(decision.CanApplyObservation);
    }

    /// <summary>
    /// 自有引擎 shutdown 无文稿观测时只能记生命周期回执，不能伪造当前出画清空。
    /// :returns: 回执可记，不是 OS 进程退出证明，也无画面更新权限。
    /// </summary>
    [Fact]
    public void ShutdownReceiptWithoutObservationDoesNotFabricatePictureOrOsExit()
    {
        var request = CreateShutdownRequest();
        var response = new PresentationResponse
        {
            OperationId = request.OperationId, Scope = CloneScope(request.Scope),
            RequestFingerprint = PresentationFingerprints.Request(request), Status = "succeeded",
            CompletedAt = Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        };
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        var decision = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.True(decision.CanRecordLifecycleCompletion);
        Assert.False(decision.CanApplyObservation);
        Assert.False(decision.CanAcceptArtifact);
        Assert.Null(response.Observation);
    }

    /// <summary>
    /// 已确认 close 可采用无源观测，不能仅靠 succeeded 清掉最后实际源。
    /// :returns: 只有文稿关闭和页数归零证据成立才应用。
    /// </summary>
    [Fact]
    public void CloseRequiresConfirmedClosedDocumentObservation()
    {
        var request = CreateRequest("close");
        var response = ResponseFor(request);
        var observed = response.Observation ?? throw new InvalidOperationException("夹具缺少观测");
        observed.SourceId = null;
        observed.CurrentSlide = 0;
        observed.TotalSlides = 0;
        observed.DocumentClosed = true;
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        Assert.True(PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now).CanApplyObservation);
        observed.DocumentClosed = false;
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        Assert.Equal("close_not_confirmed", PresentationResultPolicy.Match(request, response,
            CloneScope(request.Scope), Now).Code);
    }

    /// <summary>
    /// Converter 产物观测必须匹配请求源，不能冒充 Display 实际出画。
    /// :returns: 只授权产物记录，缺产物摘要时拒绝。
    /// </summary>
    [Fact]
    public void ConverterCompletionAuthorizesOnlyConfirmedArtifactMetadata()
    {
        var request = CreateConverterRequest();
        var response = ResponseFor(request);
        var observed = response.Observation ?? throw new InvalidOperationException("夹具缺少观测");
        observed.CurrentSlide = 0;
        observed.ArtifactDigest = new string('b', 64);
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        var decision = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.True(decision.CanAcceptArtifact);
        Assert.False(decision.CanApplyObservation);
        observed.ArtifactDigest = string.Empty;
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        Assert.Equal("artifact_unconfirmed", PresentationResultPolicy.Match(request, response,
            CloneScope(request.Scope), Now).Code);
    }
}
