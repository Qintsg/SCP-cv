// 登记为文稿的文件格式与公开协议接线回归；纯策略允许不冒充实际原生兼容或密码能力。
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    /// <summary>:returns: 媒体库已接受ODP，公开协议不能在原生动作前把它当未知格式拒绝。</summary>
    [Fact]
    public void RegisteredOpenDocumentExtensionIsAcceptedByPresentationPolicy()
    {
        var request = CreateRequest();
        request.SourcePath = @"C:\media\原件.odp";
        var decision = PresentationRequestPolicy.Validate(request, Now);
        Assert.True(decision.Allowed, decision.Code);
    }

    /// <summary>:param extension: 已登记非PDF文稿。:param convert: 转换/播放入口。:returns: 两公开动作使用相同格式集合。</summary>
    [Theory]
    [InlineData(".ppsx", false)]
    [InlineData(".ppsx", true)]
    [InlineData(".ppsm", false)]
    [InlineData(".pptm", true)]
    [InlineData(".potx", false)]
    [InlineData(".potm", true)]
    [InlineData(".pps", false)]
    [InlineData(".pot", true)]
    [InlineData(".ODP", true)]
    [InlineData(".ppt", true)]
    [InlineData(".PPTX", false)]
    public void RegisteredPresentationFormatsHaveEquivalentOpenAndExportPolicy(string extension, bool convert)
    {
        var request = convert ? CreateConverterRequest() : CreateRequest();
        request.SourcePath = @"C:\media\原件" + extension;
        var decision = PresentationRequestPolicy.Validate(request, Now);
        Assert.True(decision.Allowed, decision.Code);
    }

    /// <summary>:param path: 非文稿或非法源路径。:returns: 格式对齐不能放宽未知格式/伪后缀/本地路径约束。</summary>
    [Theory]
    [InlineData(@"C:\media\file.pdf")]
    [InlineData(@"C:\media\file.exe")]
    [InlineData(@"C:\media\file.pptm.exe")]
    [InlineData(@"C:\media\file.ppsx ")]
    [InlineData("https://example.invalid/file.odp")]
    [InlineData("relative/file.odp")]
    public void ExpandedPresentationFormatsDoNotAcceptOtherPaths(string path)
    {
        var request = CreateRequest();
        request.SourcePath = path;
        Assert.False(PresentationRequestPolicy.Validate(request, Now).Allowed);
    }
}
