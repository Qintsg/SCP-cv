// 自然结束迟到事件不能控制新源或新代次。
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class VlcEndPolicyTests
{
    [Fact]
    public void LateGenerationIsIgnored()
    {
        var player = new object();
        Assert.Equal(VlcEndAction.Ignore, VlcEndPolicy.Decide(player, player, 2, 1, true));
    }

    [Fact]
    public void PreviousPlayerIsIgnored()
    {
        Assert.Equal(VlcEndAction.Ignore, VlcEndPolicy.Decide(new object(), new object(), 2, 2, true));
    }

    [Theory]
    [InlineData(true, VlcEndAction.Replay)]
    [InlineData(false, VlcEndAction.Stop)]
    public void CurrentPlayerFollowsLoopIntent(bool loopEnabled, VlcEndAction expected)
    {
        var player = new object();
        Assert.Equal(expected, VlcEndPolicy.Decide(player, player, 2, 2, loopEnabled));
    }
}
