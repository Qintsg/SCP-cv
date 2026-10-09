// 活动直播无数据时不能永远playing；纯值/单调时钟回归，不启动播放器或网络。
using LibVLCSharp.Shared;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class VlcLiveInputProgressTests
{
    /// <summary>:returns: 文件播放不使用直播停滞规则，旧回调和新尝试仍各自隔离。</summary>
    [Fact]
    public void OnlyLiveAttemptLatchesProgressFailureAndFreshAttemptResets()
    {
        var file = new VlcPlaybackAttempt(7);
        var live = new VlcPlaybackAttempt(7, true);
        var stats = new VlcInputCounters(100, 100, 10, 10);
        foreach (var attempt in new[] { file, live })
        {
            attempt.RecordPlaying();
            attempt.ObserveInputProgress(stats, VLCState.Playing, "playing", 0);
            attempt.ObserveInputProgress(stats, VLCState.Playing, "playing", 5000);
        }
        Assert.Equal("playing", file.ResolveState(VLCState.Playing, "playing"));
        Assert.Equal("error", live.ResolveState(VLCState.Playing, "playing"));
        live.ObserveInputProgress(new(200, 200, 20, 20), VLCState.Playing, "playing", 5001);
        Assert.True(live.HasError);
        var reopened = new VlcPlaybackAttempt(8, true);
        Assert.False(reopened.HasError);
        Assert.True(reopened.IsLiveInput);
    }

    /// <summary>:returns: 计数溢出/重置但值确有变化时仍按活动处理，不因符号推断断流。</summary>
    [Fact]
    public void NativeCounterWrapIsStillProgress()
    {
        var progress = new VlcLiveInputProgress();
        progress.Observe(new(int.MaxValue, 100, 10, 10), VLCState.Playing, "playing", 0);
        Assert.False(progress.Observe(new(int.MinValue, 100, 10, 10), VLCState.Playing, "playing", 5000));
    }
    /// <summary>:returns: 重连中的Playing只有停滞计数不能授予直播成功。</summary>
    [Fact]
    public void FrozenNativeCountersAreReportedAfterFiveSeconds()
    {
        var progress = new VlcLiveInputProgress();
        var stats = new VlcInputCounters(100, 100, 10, 10);
        Assert.False(progress.Observe(stats, VLCState.Playing, "playing", 0));
        Assert.False(progress.Observe(stats, VLCState.Playing, "playing", 4999));
        Assert.True(progress.Observe(stats, VLCState.Playing, "playing", 5000));
    }

    /// <summary>:returns: 任一输入/解码/呈现计数推进都重置健康窗口，不比较图像内容变化。</summary>
    [Theory]
    [InlineData(101, 100, 10, 10)]
    [InlineData(100, 101, 10, 10)]
    [InlineData(100, 100, 11, 10)]
    [InlineData(100, 100, 10, 11)]
    public void CounterProgressResetsGrace(int read, int demux, int decoded, int displayed)
    {
        var progress = new VlcLiveInputProgress();
        progress.Observe(new(100, 100, 10, 10), VLCState.Playing, "playing", 0);
        var next = new VlcInputCounters(read, demux, decoded, displayed);
        Assert.False(progress.Observe(next, VLCState.Playing, "playing", 4999));
        Assert.False(progress.Observe(next, VLCState.Playing, "playing", 9998));
        Assert.True(progress.Observe(next, VLCState.Playing, "playing", 9999));
    }

    /// <summary>:returns: 明确暂停、停止或缓冲不算正在播出的停滞；恢复时给新的窗口。</summary>
    [Theory]
    [InlineData(VLCState.Paused, "paused")]
    [InlineData(VLCState.Stopped, "stopped")]
    [InlineData(VLCState.Buffering, "playing")]
    public void NonPlayingDoesNotTriggerStall(VLCState state, string desired)
    {
        var progress = new VlcLiveInputProgress();
        var stats = new VlcInputCounters(100, 100, 10, 10);
        progress.Observe(stats, VLCState.Playing, "playing", 0);
        Assert.False(progress.Observe(stats, state, desired, 20000));
        Assert.False(progress.Observe(stats, VLCState.Playing, "playing", 20001));
        Assert.False(progress.Observe(stats, VLCState.Playing, "playing", 25000));
        Assert.True(progress.Observe(stats, VLCState.Playing, "playing", 25001));
    }
}
