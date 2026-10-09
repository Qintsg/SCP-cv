// 音频公开执行器的只读进度与源身份围栏回归，不启动声卡或修改播放器控制。
using ScpCv.AudioWorker.Audio;

namespace ScpCv.Windows.Tests;

public sealed partial class AudioCommandExecutorTests
{
    /// <summary>:returns: 未打开、停止和采样取消均不执行新控制；暂停仍可报告稳定时钟。</summary>
    [Fact]
    public async Task UnknownStoppedPausedAndCancelledSamplesRespectBoundary()
    {
        var audio = new FakeAudioAdapter();
        var executor = new AudioCommandExecutor(audio);
        Assert.Null(await executor.SampleProgressAsync(CancellationToken.None));
        await executor.ExecuteAsync(Lease("OPEN", 7, new { source_id = 42, uri = "test.mp3" }));
        await executor.ExecuteAsync(Lease("PAUSE", 7, new { }));
        Assert.Equal("paused", (await executor.SampleProgressAsync(CancellationToken.None))!.State.GetProperty("playback_state").GetString());
        await executor.ExecuteAsync(Lease("STOP", 7, new { }));
        Assert.Null(await executor.SampleProgressAsync(CancellationToken.None));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.SampleProgressAsync(cancelled.Token));
        Assert.Equal(1, audio.PlayCalls); Assert.Equal(1, audio.StopCalls);
    }

    /// <summary>:returns: 适配器源身份外部改变后不沿用旧身份；新成功OPEN重建有效代次。</summary>
    [Fact]
    public async Task ChangedSourceIsNotReportedUntilSuccessfulNewOpen()
    {
        var audio = new FakeAudioAdapter(); var executor = new AudioCommandExecutor(audio);
        await executor.ExecuteAsync(Lease("OPEN", 7, new { source_id = 42, uri = "test.mp3" }));
        await audio.OpenAsync(43, "next.mp3", 8); await audio.PlayAsync();
        Assert.Null(await executor.SampleProgressAsync(CancellationToken.None));
        await executor.ExecuteAsync(Lease("OPEN", 8, new { source_id = 43, uri = "next.mp3" }));
        Assert.Equal(8, (await executor.SampleProgressAsync(CancellationToken.None))!.SourceGeneration);
    }

    /// <summary>:returns: 同源新OPEN失败也使旧成功身份失效，不能在后续采样假报旧代次。</summary>
    [Fact]
    public async Task FailedSameSourceOpenDoesNotSamplePreviousSuccessfulIdentity()
    {
        var audio = new FakeAudioAdapter();
        var executor = new AudioCommandExecutor(audio);
        await executor.ExecuteAsync(Lease("OPEN", 7, new { source_id = 42, uri = "test.mp3" }));
        await Assert.ThrowsAsync<InvalidDataException>(() => executor.ExecuteAsync(Lease("OPEN", 8, new { source_id = 42 })));
        Assert.Null(await executor.SampleProgressAsync(CancellationToken.None));
    }

    /// <summary>:returns: 空闲样本携带已完成OPEN代次和最新真实时钟，采样不执行播放控制。</summary>
    [Fact]
    public async Task IdleSampleReportsActualProgressWithoutExecutingControl()
    {
        var audio = new FakeAudioAdapter();
        var executor = new AudioCommandExecutor(audio);
        await executor.ExecuteAsync(Lease("OPEN", 7, new { source_id = 42, uri = "test.mp3" }));
        await audio.SeekAsync(321);
        var sample = await executor.SampleProgressAsync(CancellationToken.None);
        Assert.NotNull(sample);
        Assert.Equal(7, sample.SourceGeneration);
        Assert.Equal(42, sample.State.GetProperty("source_id").GetInt64());
        Assert.Equal(321, sample.State.GetProperty("position_ms").GetInt64());
        Assert.Equal("playing", sample.State.GetProperty("playback_state").GetString());
        Assert.Equal(1, audio.PlayCalls);
        Assert.Equal(0, audio.StopCalls);
    }
}
