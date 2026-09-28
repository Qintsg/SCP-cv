// VLC 播放尝试的纯状态回归；不初始化原生 VLC、不创建窗口或音视频输出。
using LibVLCSharp.Shared;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class VlcPlaybackAttemptTests
{
    /// <summary>事件只能更新本次尝试的观测，不得改变创建时固定的源代次。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void SourceGenerationRemainsFixedWhenSignalsArrive()
    {
        var attempt = new VlcPlaybackAttempt(37);

        attempt.RecordPlaying();
        attempt.RecordEnded();
        attempt.RecordError();

        Assert.Equal(37, attempt.SourceGeneration);
        Assert.True(attempt.HasError);
    }

    /// <summary>同次失败被锁存，随后自然结束或迟到 Playing 都不能伪报成功。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void RecordedErrorSurvivesEndedAndLatePlayingSignals()
    {
        var attempt = new VlcPlaybackAttempt(38);
        attempt.RecordError();
        attempt.RecordEnded();
        attempt.RecordPlaying();

        Assert.True(attempt.HasError);
        Assert.Equal("error", attempt.ResolveState(VLCState.Ended, "playing"));
        Assert.Equal("error", attempt.ResolveState(VLCState.Playing, "playing"));
        Assert.Equal("error", attempt.ResolveState(VLCState.Paused, "paused"));
    }

    /// <summary>尚未收到错误事件时，真实原生 Error 也不能被成功观测或暂停意图覆盖。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void NativeErrorOverridesPlayingEvidenceAndPausedIntent()
    {
        var attempt = new VlcPlaybackAttempt(39);
        attempt.RecordPlaying();

        Assert.Equal("error", attempt.ResolveState(VLCState.Error, "playing"));
        Assert.Equal("error", attempt.ResolveState(VLCState.Error, "paused"));
    }

    /// <summary>旧回调只持有旧尝试；同代次循环和新代次重开都不会继承其失败或完成标记。</summary>
    /// <remarks>:param newGeneration: 新尝试的源代次，相同值模拟循环重播。:returns: 无返回值。</remarks>
    [Theory]
    [InlineData(42L)]
    [InlineData(43L)]
    public void LateCallbacksCannotPolluteAnotherAttempt(long newGeneration)
    {
        var previous = new VlcPlaybackAttempt(42);
        var current = new VlcPlaybackAttempt(newGeneration);

        previous.RecordError();
        previous.RecordEnded();
        previous.RecordPlaying();

        Assert.True(previous.HasError);
        Assert.False(current.HasError);
        Assert.Equal(newGeneration, current.SourceGeneration);
        Assert.Equal("loading", current.ResolveState(VLCState.NothingSpecial, "playing"));

        current.RecordPlaying();
        Assert.Equal("playing", current.ResolveState(VLCState.NothingSpecial, "playing"));
        Assert.Equal("error", previous.ResolveState(VLCState.Playing, "playing"));
    }

    /// <summary>旧 Playing 观测不能恢复用户明确暂停或停止的媒体。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void PlayingEvidenceCannotOverrideExplicitPauseOrStop()
    {
        var attempt = new VlcPlaybackAttempt(44);
        attempt.RecordPlaying();

        Assert.Equal("paused", attempt.ResolveState(VLCState.Playing, "paused"));
        Assert.Equal("stopped", attempt.ResolveState(VLCState.Playing, "stopped"));
    }

    /// <summary>已完成的显式停止优先投影 stopped，同时不得抹掉原错误证据。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void ExplicitStopTakesPriorityWithoutClearingErrorEvidence()
    {
        var attempt = new VlcPlaybackAttempt(45);
        attempt.RecordError();
        attempt.RecordPlaying();
        attempt.RecordEnded();

        Assert.Equal("stopped", attempt.ResolveState(VLCState.Error, "stopped"));
        Assert.True(attempt.HasError);
        Assert.Equal("error", attempt.ResolveState(VLCState.Ended, "playing"));
    }

    /// <summary>连接或缓冲期间仍是 loading，不能因早先 Playing 事件伪称已稳定播出。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void OpeningAndBufferingRemainLoadingAfterEarlierPlayingSignal()
    {
        var attempt = new VlcPlaybackAttempt(46);
        attempt.RecordPlaying();

        Assert.Equal("loading", attempt.ResolveState(VLCState.Opening, "playing"));
        Assert.Equal("loading", attempt.ResolveState(VLCState.Buffering, "playing"));
    }

    /// <summary>正常播放和暂停由真实原生状态观测，不依赖进度时间是否非零。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void NativePlayingAndPausedStatesSupplyActualPlaybackEvidence()
    {
        var attempt = new VlcPlaybackAttempt(47);

        Assert.Equal("playing", attempt.ResolveState(VLCState.Playing, "playing"));
        attempt.RecordPlaying();
        Assert.Equal("paused", attempt.ResolveState(VLCState.Paused, "playing"));
    }

    /// <summary>无错误的原生结束或结束信号均投影 stopped，晚 Playing 不复活同次播放。</summary>
    /// <remarks>:returns: 无返回值。</remarks>
    [Fact]
    public void EndedStateOrSignalStopsTheCurrentAttempt()
    {
        var nativeEnded = new VlcPlaybackAttempt(48);
        Assert.Equal("stopped", nativeEnded.ResolveState(VLCState.Ended, "playing"));

        var signalledEnded = new VlcPlaybackAttempt(48);
        signalledEnded.RecordEnded();
        signalledEnded.RecordPlaying();
        Assert.Equal("stopped", signalledEnded.ResolveState(VLCState.Playing, "playing"));
        Assert.False(signalledEnded.HasError);
    }
}
