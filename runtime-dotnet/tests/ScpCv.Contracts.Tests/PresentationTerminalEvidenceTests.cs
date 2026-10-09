// 末页事实模块的纯值行为，真实UNO绑定/回调与画面另作实际Player验证。
using ScpCv.LibreOfficeHost.Runtime;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationTerminalEvidenceTests
{
    /// <summary>:returns: 本轮实际主序开始、最后组开始/结束才能确认耗尽。</summary>
    [Fact]
    public void MatchingPlaybackAndFinalGroupCompletionConfirmExhaustion()
    {
        var evidence = new PresentationTerminalEvidence(7, 6);
        evidence.Apply(7, 0, PresentationTerminalEvent.Transition);
        evidence.Apply(7, 6, PresentationTerminalEvent.MainBegin);
        evidence.Apply(7, 6, PresentationTerminalEvent.LastBegin);
        evidence.Apply(7, 6, PresentationTerminalEvent.LastEnd);
        Assert.True(evidence.Exhausted);
    }

    /// <summary>:returns: 丢事件之后不能把随后单个结束回调当完整耗尽事实。</summary>
    [Fact]
    public void LostEventsCannotGrantExhaustion()
    {
        var evidence = new PresentationTerminalEvidence(7, 6);
        evidence.Apply(7, 6, PresentationTerminalEvent.MainBegin);
        evidence.Apply(7, 6, PresentationTerminalEvent.LastBegin);
        evidence.Apply(7, 0, PresentationTerminalEvent.Lost);
        evidence.Apply(7, 6, PresentationTerminalEvent.LastEnd);
        Assert.False(evidence.Exhausted);
    }

    /// <summary>:param epoch: 事件轮身份。:param model: 节点模型身份。:returns: 旧listener或其它模型的结束不能确认当前轮。</summary>
    [Theory]
    [InlineData(6, 6)]
    [InlineData(7, 5)]
    public void ForeignCompletionCannotGrantCurrentExhaustion(long epoch, long model)
    {
        var evidence = new PresentationTerminalEvidence(7, 6);
        evidence.Apply(7, 6, PresentationTerminalEvent.MainBegin);
        evidence.Apply(7, 6, PresentationTerminalEvent.LastBegin);
        evidence.Apply(epoch, model, PresentationTerminalEvent.LastEnd);
        Assert.False(evidence.Exhausted);
    }

    /// <summary>:returns: 同轮孤立end、没有主序begin或没有最后组begin均无耗尽证明。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IncompleteLifecycleDoesNotGrantExhaustion(bool main, bool last)
    {
        var evidence = new PresentationTerminalEvidence(7, 6);
        if (main) evidence.Apply(7, 6, PresentationTerminalEvent.MainBegin);
        if (last) evidence.Apply(7, 6, PresentationTerminalEvent.LastBegin);
        evidence.Apply(7, 6, PresentationTerminalEvent.LastEnd);
        Assert.False(evidence.Exhausted);
    }

    /// <summary>:returns: 倒退新轮不继承旧耗尽，旧listener晚end被拒；新最后组明确begin/end后仍可确认。</summary>
    [Fact]
    public void RewindRejectsOldCompletionButAllowsNewFinalGroup()
    {
        var old = new PresentationTerminalEvidence(7, 6);
        old.Apply(7, 6, PresentationTerminalEvent.MainBegin);
        old.Apply(7, 6, PresentationTerminalEvent.LastBegin);
        old.Apply(7, 6, PresentationTerminalEvent.LastEnd);
        var current = old.RenewForRewind(8);
        current.Apply(7, 6, PresentationTerminalEvent.LastEnd);
        Assert.False(current.Exhausted);
        current.Apply(8, 6, PresentationTerminalEvent.LastBegin);
        current.Apply(8, 6, PresentationTerminalEvent.LastEnd);
        Assert.True(current.Exhausted);
    }

    /// <summary>:returns: 模型0只用于全轮过渡/丢失，不能当未知节点的动画完成。</summary>
    [Fact]
    public void UnknownModelDoesNotGrantCompletion()
    {
        var current = new PresentationTerminalEvidence(7, 6);
        current.Apply(7, 6, PresentationTerminalEvent.MainBegin);
        current.Apply(7, 6, PresentationTerminalEvent.LastBegin);
        current.Apply(7, 0, PresentationTerminalEvent.LastEnd);
        Assert.False(current.Exhausted);
    }

    /// <summary>:returns: 过渡/新主序及已结束的主序均撤销原耗尽，不能复用旧末组end。</summary>
    [Theory]
    [InlineData(PresentationTerminalEvent.Transition)]
    [InlineData(PresentationTerminalEvent.MainBegin)]
    [InlineData(PresentationTerminalEvent.MainEnd)]
    internal void PlaybackRestartInvalidatesPreviousFinalGroup(PresentationTerminalEvent reset)
    {
        var current = new PresentationTerminalEvidence(7, 6);
        current.Apply(7, 6, PresentationTerminalEvent.MainBegin);
        current.Apply(7, 6, PresentationTerminalEvent.LastBegin);
        current.Apply(7, 6, PresentationTerminalEvent.LastEnd);
        current.Apply(7, reset == PresentationTerminalEvent.Transition ? 0 : 6, reset);
        current.Apply(7, 6, PresentationTerminalEvent.LastEnd);
        Assert.False(current.Exhausted);
    }
}
