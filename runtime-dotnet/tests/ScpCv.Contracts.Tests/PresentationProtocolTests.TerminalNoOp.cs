// 末页无播放动作是独立、可围栏的协议结果，不冒充成功执行、失败或动画完成计数。
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    /// <summary>:returns: 末页无操作语义必须与旧宿主隔离，同版调用方/宿主使用v3。</summary>
    [Fact]
    public void TerminalHoldContractUsesVersionThree()
    {
        Assert.Equal(3, PresentationRequestPolicy.ProtocolVersion);
        Assert.Equal(3, new PresentationRequest().ProtocolVersion);
        Assert.Equal(3, new PresentationResponse().ProtocolVersion);
    }

    /// <summary>:returns: 已匹配的实际末页无操作回执可保持当前观测，并带明确信息边界；不证明宿主原生判断本身已正确。</summary>
    [Fact]
    public async Task TerminalNoOpHasFencedConfirmedCurrentDocumentObservation()
    {
        var request = CreateRequest("next_effect");
        var response = new PresentationResponse
        {
            OperationId = request.OperationId, Scope = CloneScope(request.Scope),
            RequestFingerprint = PresentationFingerprints.Request(request), CompletedAt = Now.ToString("O"),
            Status = "noop", ErrorCode = "presentation_at_last_slide",
            Observation = new PresentationObservation { Confirmed = true, SourceId = request.Scope.SourceId,
                CurrentSlide = 7, TotalSlides = 7 },
        };
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        using var stream = new MemoryStream(PresentationWireCodec.EncodeResponse(response));
        var decoded = await PresentationWireCodec.ReadResponseAsync(stream);
        Assert.NotNull(decoded);
        var matched = PresentationResultPolicy.Match(request, decoded, request.Scope, Now);
        Assert.True(matched.Matched);
        Assert.True(matched.CanApplyObservation);
        Assert.Equal("no_op_confirmed", matched.Code);
    }

    /// <summary>:param operation: 非前向边界操作。:returns: 无操作回执不能替代关闭/打开/转换或撤销的证明。</summary>
    [Theory]
    [InlineData("open")]
    [InlineData("close")]
    [InlineData("goto")]
    [InlineData("previous_effect")]
    public void TerminalNoOpCannotGrantUnrelatedOperationObservation(string operation)
    {
        var request = CreateRequest(operation);
        if (operation == "goto") request.TargetSlide = 1;
        var response = NoOpResponse(request);
        Assert.Equal("invalid_no_op_operation", PresentationResultPolicy.Match(request, response, request.Scope, Now).Code);
    }

    /// <summary>:returns: 尾部隐藏页时真实终端可在列表中间，纯合同不能假称current==total才是末页。</summary>
    [Fact]
    public void TerminalNoOpAllowsTrustedHiddenPageTerminalWithinList()
    {
        var request = CreateRequest("next_effect");
        var response = NoOpResponse(request);
        response.Observation!.CurrentSlide = 6;
        response.Observation.TotalSlides = 7;
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        Assert.Equal("no_op_confirmed", PresentationResultPolicy.Match(request, response, request.Scope, Now).Code);
    }

    /// <summary>:param corruption: 未确认/错误身份或生命周期。:returns: 错误边界不能盖章或解码，不丢原校验。</summary>
    [Theory]
    [InlineData("unconfirmed")]
    [InlineData("source")]
    [InlineData("closed")]
    [InlineData("exited")]
    [InlineData("code")]
    [InlineData("missing")]
    public void MalformedTerminalNoOpCannotBeStamped(string corruption)
    {
        var response = NoOpResponse(CreateRequest("next_effect"));
        switch (corruption)
        {
            case "unconfirmed": response.Observation!.Confirmed = false; break;
            case "source": response.Observation!.SourceId++; break;
            case "closed": response.Observation!.DocumentClosed = true; break;
            case "exited": response.Observation!.EngineExited = true; break;
            case "code": response.ErrorCode = "ok"; break;
            case "missing": response.Observation = null; break;
        }
        Assert.Equal("invalid_no_op_boundary", Assert.Throws<InvalidDataException>(() => PresentationFingerprints.Response(response)).Message);
    }

    /// <summary>:returns: null摘要走明确校验而非NullReference，零native副作用。</summary>
    [Fact]
    public void TerminalNoOpWithNullArtifactDigestIsExplicitlyRejected()
    {
        var response = NoOpResponse(CreateRequest("next_effect"));
        response.Observation!.ArtifactDigest = null!;
        Assert.Equal("invalid_observation", Assert.Throws<InvalidDataException>(() => PresentationFingerprints.Response(response)).Message);
    }

    /// <summary>:param request: 合法当前请求。:returns: 受控已盖章无操作；不是原生耗尽证据。</summary>
    private static PresentationResponse NoOpResponse(PresentationRequest request)
    {
        var response = new PresentationResponse
        {
            OperationId = request.OperationId, Scope = CloneScope(request.Scope), RequestFingerprint = PresentationFingerprints.Request(request),
            CompletedAt = Now.ToString("O"), Status = "noop", ErrorCode = "presentation_at_last_slide",
            Observation = new PresentationObservation { Confirmed = true, SourceId = request.Scope.SourceId, CurrentSlide = 7, TotalSlides = 7 },
        };
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        return response;
    }
}
