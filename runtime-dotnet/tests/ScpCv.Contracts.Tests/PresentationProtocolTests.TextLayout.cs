/*
 * 可选文字适配默认关闭；规范指纹、动作范围和独立快照覆盖明确选择。
 * @Project : SCP-cv
 * @File : PresentationProtocolTests.TextLayout.cs
 * @Author : Qintsg
 * @Date : 2026-10-01
 */
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    /// <summary>
    /// 默认原排版与显式文字适配必须拥有不同请求指纹并完整 wire 往返。
    /// :returns: 不静默开启适配，选择进入规范请求。
    /// </summary>
    [Fact]
    public void TextLayoutDefaultsToOriginalAndChangesRequestFingerprint()
    {
        var request = CreateRequest();
        Assert.False(request.FitText);
        var original = PresentationFingerprints.Request(request);
        request.FitText = true;
        Assert.NotEqual(original, PresentationFingerprints.Request(request));
        Assert.True(PresentationWireCodec.DecodeRequest(PresentationWireCodec.EncodeRequest(request)).FitText);
    }

    /// <summary>
    /// 文字适配只在打开和导出阶段可使用，不搭车改变其它原生动作。
    /// :param operation: 与打开原件无关的动作。
    /// :returns: 无关动作明确拒绝布局选项。
    /// </summary>
    [Theory]
    [InlineData("observe")]
    [InlineData("close")]
    [InlineData("next_effect")]
    public void TextLayoutCannotRideUnrelatedActions(string operation)
    {
        var request = CreateRequest(operation);
        request.FitText = true;
        var result = PresentationRequestPolicy.Validate(request, Now);
        Assert.False(result.Allowed);
        Assert.Equal("unexpected_text_layout", result.Code);
    }

    /// <summary>
    /// 打开和导出使用相同文字适配语义，executor 获取独立选项快照。
    /// :param converter: 是否为独立导出作业。
    /// :returns: 适配选择不随调用方修改而变化。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextLayoutSelectionIsCapturedForOpeningAndExport(bool converter)
    {
        var request = converter ? CreateConverterRequest() : CreateRequest();
        request.FitText = true;
        var capturedLayout = false;
        var calls = 0;
        PresentationExecutionGate.Execute(request,
            new PresentationCapabilities { CanOpen = true, CanExportSlides = true },
            CloneScope(request.Scope), Now, captured =>
            {
                calls++;
                request.FitText = false;
                capturedLayout = captured.FitText;
                return ResponseFor(captured);
            });
        Assert.Equal(1, calls);
        Assert.True(capturedLayout);
        Assert.False(request.FitText);
    }
}
