// 软件输出端点选择测试：明确ID、缺失拒绝，不能按同名HDMI或系统默认选择。
using ScpCv.Contracts.Runtime;

namespace ScpCv.Contracts.Tests;

public sealed class AudioOutputBindingTests
{
    private const string Render = "{0.0.0.00000000}.{eb7e6029-ae6b-4d15-b79a-268521bd9abd}";

    /// <summary>:returns: 未配置时不把某个枚举端点自动绑定到现场。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void UnconfiguredDoesNotSelectDefault(string? id) => Assert.Null(AudioOutputBinding.Resolve(id, [Render]));

    /// <summary>:returns: 按完整ID选择，不依赖同名设备的枚举顺序。</summary>
    [Fact]
    public void ExactIdWinsRegardlessOfOrdering() => Assert.Equal(Render,
        AudioOutputBinding.Resolve(Render.ToUpperInvariant(), ["", "{0.0.0.00000000}.{d5ff475c-6792-4d13-bc2b-2e601d097be4}", Render]));

    /// <summary>:param id: 不可信或非输出ID。:returns: 不允许默认值、名称、捕获端点或额外参数。</summary>
    [Theory]
    [InlineData("default")]
    [InlineData("HDMI-4K (NVIDIA High Definition Audio)")]
    [InlineData("{0.0.1.00000000}.{eb7e6029-ae6b-4d15-b79a-268521bd9abd}")]
    [InlineData("{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}")]
    [InlineData("{0.0.0.00000000}.{eb7e6029-ae6b-4d15-b79a-268521bd9abd}\" --other")]
    public void InvalidBindingIsRejected(string id) => Assert.Throws<InvalidOperationException>(() => AudioOutputBinding.Resolve(id, [Render]));

    /// <summary>:returns: 已配置但掉线时拒绝，不能回退默认设备。</summary>
    [Fact]
    public void MissingConfiguredEndpointIsRejected() => Assert.Throws<InvalidOperationException>(() => AudioOutputBinding.Resolve(Render, [""]));

    /// <summary>:returns: 重复ID属于不可靠枚举，不随意挑一个。</summary>
    [Fact]
    public void DuplicateEndpointIsRejected() => Assert.Throws<InvalidOperationException>(() => AudioOutputBinding.Resolve(Render, [Render, Render]));

    /// <summary>:returns: 默认输出或未知输出不能作为已绑定证据。</summary>
    [Fact]
    public void ActualBindingRequiresSameConcreteEndpoint()
    {
        Assert.True(AudioOutputBinding.Matches(Render, Render.ToUpperInvariant()));
        Assert.False(AudioOutputBinding.Matches(Render, ""));
        Assert.False(AudioOutputBinding.Matches(Render, null));
    }
}
