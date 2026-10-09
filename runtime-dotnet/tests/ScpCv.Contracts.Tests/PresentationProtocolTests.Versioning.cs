/*
 * 文稿协议v3与文字适配/末页无操作的版本隔离回归；仅纯值和内存流，无原生副作用。
 * @Project : SCP-cv
 * @File : PresentationProtocolTests.Versioning.cs
 * @Author : Qintsg
 * @Date : 2026-10-02
 */
using System.Text;
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    /// <summary>
    /// 新增 fit_text 改变规范字节，请求与返回都必须使用新协议版本。
    /// :returns: 默认值一致为v3，不与尚无末页保护的v1/v2宿主混用。
    /// </summary>
    [Fact]
    public void TextLayoutAndTerminalContractUseVersionThreeForBothEnvelopes()
    {
        Assert.Equal(3, PresentationRequestPolicy.ProtocolVersion);
        Assert.Equal(3, new PresentationRequest().ProtocolVersion);
        Assert.Equal(3, new PresentationResponse().ProtocolVersion);
    }

    /// <summary>
    /// 固定夹具的规范字节摘要独立计算，锁住v3 tag、字段顺序和布局布尔位。
    /// :returns: 两种布局请求及其原排版结果都有稳定的跨运行时对照值。
    /// </summary>
    [Fact]
    public void VersionThreeCanonicalFingerprintsHaveStableKnownVectors()
    {
        var request = CreateRequest();
        Assert.Equal("4F7215958DD1327CCCF7DBF9F41690477AD39CEF4796D136F03550468A6A066B",
            PresentationFingerprints.Request(request));
        var response = ResponseFor(request);
        Assert.Equal("D3DC7F867E0AEA86D87E384973DCF097B5D55218EDB0AFCC236DE1E7BFD60575", response.ResultFingerprint);
        request.FitText = true;
        Assert.Equal("761BD999D1EB94AFF64B880F03A808A5A4596458CD9ADFF113F085974866F256",
            PresentationFingerprints.Request(request));
    }

    /// <summary>
    /// 旧版和未知版不得经能力门禁进入原生回调，文字适配开关也不能放宽版本要求。
    /// :param version: 不支持的版本。
    /// :param fitText: 原排版或文字适配。
    /// :returns: 明确版本错误且零回调。
    /// </summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void DifferentProtocolVersionsCannotReachExecutor(int version, bool fitText)
    {
        var request = CreateRequest();
        request.ProtocolVersion = version;
        request.FitText = fitText;
        var nativeCalls = 0;
        Assert.Equal("unsupported_protocol_version", PresentationRequestPolicy.Validate(request, Now).Code);
        var exception = Assert.Throws<InvalidDataException>(() => PresentationExecutionGate.Execute(request,
            new PresentationCapabilities { CanOpen = true }, CloneScope(request.Scope), Now,
            _ => { nativeCalls++; return new PresentationResponse(); }));
        Assert.Equal("unsupported_protocol_version", exception.Message);
        Assert.Equal(0, nativeCalls);
    }

    /// <summary>
    /// wire 解码同样拒绝旧版请求，不能因未知字段容忍而采用旧版副作用语义。
    /// :returns: v1 原排版或携带适配字段都不能执行。
    /// </summary>
    [Fact]
    public void LegacyRequestFramesAreRejectedWithOrWithoutTextLayoutField()
    {
        var frame = PresentationWireCodec.EncodeRequest(CreateRequest());
        var body = Encoding.UTF8.GetString(frame, 4, frame.Length - 4);
        var legacy = body.Replace("\"protocol_version\":3", "\"protocol_version\":1", StringComparison.Ordinal);
        Assert.NotEqual(body, legacy);
        var exception = Assert.Throws<InvalidDataException>(() =>
            PresentationWireCodec.DecodeRequest(Frame(Encoding.UTF8.GetBytes(legacy))));
        Assert.Equal("unsupported_protocol_version", exception.Message);
        legacy = legacy.Replace(",\"fit_text\":false", string.Empty, StringComparison.Ordinal);
        exception = Assert.Throws<InvalidDataException>(() =>
            PresentationWireCodec.DecodeRequest(Frame(Encoding.UTF8.GetBytes(legacy))));
        Assert.Equal("unsupported_protocol_version", exception.Message);
    }

    /// <summary>
    /// 旧版结果既不能编码盖章，也不能从 wire 或 matcher 获得更新画面的权限。
    /// :returns: 回执必须与当前协议版本一致。
    /// </summary>
    [Fact]
    public void LegacyResponseCannotBeStampedDecodedOrApplied()
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        var frame = PresentationWireCodec.EncodeResponse(response);
        var body = Encoding.UTF8.GetString(frame, 4, frame.Length - 4);
        var legacy = body.Replace("\"protocol_version\":3", "\"protocol_version\":1", StringComparison.Ordinal);
        Assert.NotEqual(body, legacy);
        Assert.Equal("unsupported_protocol_version", Assert.Throws<InvalidDataException>(() =>
            PresentationWireCodec.DecodeResponse(Frame(Encoding.UTF8.GetBytes(legacy)))).Message);
        response.ProtocolVersion = 1;
        Assert.Equal("unsupported_protocol_version", Assert.Throws<InvalidDataException>(() =>
            PresentationWireCodec.EncodeResponse(response)).Message);
        Assert.Equal("unsupported_protocol_version", Assert.Throws<InvalidDataException>(() =>
            PresentationFingerprints.Response(response)).Message);
        var result = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.Equal("unsupported_protocol_version", result.Code);
        Assert.False(result.CanApplyObservation);
    }

    /// <summary>
    /// v2 的可选 fit_text 缺失时仍明确使用原排版，而不是沿用不兼容 v1。
    /// :returns: v2 默认关闭且规范指纹与显式 false 相同。
    /// </summary>
    [Fact]
    public void VersionThreeMissingTextLayoutFieldDefaultsToOriginal()
    {
        var request = CreateRequest();
        var frame = PresentationWireCodec.EncodeRequest(request);
        var body = Encoding.UTF8.GetString(frame, 4, frame.Length - 4);
        var omitted = body.Replace(",\"fit_text\":false", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(body, omitted);
        var restored = PresentationWireCodec.DecodeRequest(Frame(Encoding.UTF8.GetBytes(omitted)));
        Assert.Equal(3, restored.ProtocolVersion);
        Assert.False(restored.FitText);
        Assert.Equal(PresentationFingerprints.Request(request), PresentationFingerprints.Request(restored));
    }

    /// <summary>
    /// 原排版的旧回执不能用于同身份的文字适配请求。
    /// :returns: 文字适配必须属于请求指纹，不以相同文稿代次绕过。
    /// </summary>
    [Fact]
    public void ChangingTextLayoutFencesAnOtherwiseMatchingCompletion()
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        request.FitText = true;
        var result = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.Equal("request_fingerprint_fenced", result.Code);
        Assert.False(result.CanApplyObservation);
    }
}
