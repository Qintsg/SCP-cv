// 主序重复保护的纯值回归；原生模型接线和旧输出保持另做实际Player验证。
using ScpCv.LibreOfficeHost.Uno;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationAnimationSafetyTests
{
    /// <summary>:returns: 两轮主序不能进入当前已复现越界的原生生命周期。</summary>
    [Fact]
    public void TwoMainSequenceIterationsAreRejected() =>
        Assert.True(PresentationAnimationSafety.RepeatsMainSequence(2.0, false));

    /// <summary>:returns: 无限重复同样会进入第二轮；不能把无数值当单轮。</summary>
    [Fact]
    public void IndefiniteMainSequenceIsRejected() =>
        Assert.True(PresentationAnimationSafety.RepeatsMainSequence(null, true));

    /// <summary>:param count: 原生主序不会进入第二轮的次数。:returns: 无次数、单轮及未达到第二轮的分数不被泛拒。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(1.999)]
    [InlineData(0.0)]
    public void CountsWithoutASecondIterationRemainAllowed(double? count) =>
        Assert.False(PresentationAnimationSafety.RepeatsMainSequence(count, false));
}
