// 退出证据不能被断桥/launcher单独退出/空job替代；不启动Office或其它进程。
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationNativeExitPolicyTests
{
    [Theory]
    [InlineData(null, null, true, true, false)]
    [InlineData(0, null, true, true, false)]
    [InlineData(null, 0, true, true, false)]
    [InlineData(0, 0, false, true, false)]
    [InlineData(0, 0, true, false, false)]
    [InlineData(2, 0, true, true, false)]
    [InlineData(0, -1073741819, true, true, false)]
    [InlineData(0, 0, true, true, true)]
    public void AllIndependentNormalExitFactsAreRequired(int? launcher, int? engine, bool empty, bool closed, bool expected)
    {
        Assert.Equal(expected, PresentationNativeExitPolicy.Confirmed(launcher, engine, empty, closed));
    }
}
