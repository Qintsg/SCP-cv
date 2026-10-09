// 实际VLC音频回归：只读明确夹具，单个自有实例静音，不改变系统音量或其它播放器。
using System.Security.Cryptography;
using System.Text.Json;
using ScpCv.AudioWorker.Audio;
using ScpCv.Contracts.Ipc;

namespace ScpCv.Windows.Tests;

[Trait("Category", "Physical")]
public sealed class VlcAudioAdapterPhysicalTests
{
    /// <summary>:param loop: 列表循环意图。:returns: 始终报告本曲自然结束，不能在原生层吞掉通知而循环同一曲。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListLoopStillReportsCurrentTrackFinished(bool loop)
    {
        var source = Fixture("SCP_CV_QA_AUDIO_END");
        await using var audio = new VlcAudioAdapter { IsMuted = true };
        var completed = new TaskCompletionSource<AudioFinishedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        audio.Finished += (_, finished) => completed.TrySetResult(finished);
        var executor = new AudioCommandExecutor(audio);
        await executor.ExecuteAsync(Lease("OPEN", 11, new { source_id = 101, uri = source, autoplay = true, muted = true, loop }));
        AudioFinishedEventArgs result;
        try { result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException error)
        {
            throw new InvalidOperationException($"自然结束未上报：state={audio.PlaybackState}, position={audio.PositionMs}, duration={audio.DurationMs}, loop={audio.LoopEnabled}", error);
        }
        Assert.Equal(101, result.SourceId);
        Assert.Equal(11, result.SourceGeneration);
        Assert.NotEqual(Guid.Empty, result.EventId);
    }

    /// <summary>:returns: 打开新音频但不自动播放时，不能把旧音频Playing报告为新源已播放。</summary>
    [Fact]
    public async Task OpenWithoutAutoplayRetiresPreviousNativePlayback()
    {
        var source = Fixture("SCP_CV_QA_AUDIO_MP3");
        var next = Fixture("SCP_CV_QA_AUDIO_WAV");
        var digest = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var nextDigest = SHA256.HashData(await File.ReadAllBytesAsync(next));
        await using var audio = new VlcAudioAdapter { IsMuted = true };
        var executor = new AudioCommandExecutor(audio);
        await executor.ExecuteAsync(Lease("OPEN", 1, new { source_id = 1, uri = source, autoplay = true, muted = true }));
        Assert.Equal("playing", audio.PlaybackState);
        var result = await executor.ExecuteAsync(Lease("OPEN", 2, new { source_id = 2, uri = next, autoplay = false, muted = true }));
        Assert.NotEqual("playing", result.ActualState.GetProperty("playback_state").GetString());
        await audio.DisposeAsync();
        Assert.Equal(digest, SHA256.HashData(await File.ReadAllBytesAsync(source)));
        Assert.Equal(nextDigest, SHA256.HashData(await File.ReadAllBytesAsync(next)));
        using var released = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.None);
        using var nextReleased = File.Open(next, FileMode.Open, FileAccess.Read, FileShare.None);
    }

    /// <summary>:param name: 显式实测资源环境变量。:returns: 原样只读源，不以缺样例跳过。</summary>
    private static string Fixture(string name)
    {
        var path = Environment.GetEnvironmentVariable(name);
        Assert.True(!string.IsNullOrWhiteSpace(path) && File.Exists(path), $"请提供{name}只读音频夹具");
        return Path.GetFullPath(path!);
    }

    /// <summary>:param command: 本轮明确动作。:param generation: 新源代次。:param args: 原样源与仅实例静音。:returns: 不连接现场命令队列的租约。</summary>
    private static CommandLeaseDto Lease(string command, long generation, object args) => new()
    {
        CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = command, SourceGeneration = generation,
        Args = JsonSerializer.SerializeToElement(args).EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone()),
    };
}
