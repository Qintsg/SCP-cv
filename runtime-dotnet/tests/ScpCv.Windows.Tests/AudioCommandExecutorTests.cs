// 验证音频命令只在真实适配器状态到达可观察值后回报完成。
using System.Text.Json;
using ScpCv.AudioWorker.Audio;
using ScpCv.Contracts.Ipc;

namespace ScpCv.Windows.Tests;

public sealed class AudioCommandExecutorTests
{
    [Fact]
    public async Task OpenWaitsForDelayedPlaybackStateBeforeReporting()
    {
        var audio = new FakeAudioAdapter { HoldPlaybackTransition = true };
        var executor = new AudioCommandExecutor(audio);
        var execution = executor.ExecuteAsync(Lease("OPEN", 7, new
        {
            source_id = 42,
            uri = "file:///C:/media/test.mp3",
            autoplay = true,
        }));

        Assert.False(execution.IsCompleted);
        audio.CompletePlaybackTransition();

        var result = await execution;
        Assert.Equal("playing", result.ActualState.GetProperty("playback_state").GetString());
    }

    [Fact]
    public async Task OpenFailsClearlyWhenPlaybackNeverLeavesLoading()
    {
        var audio = new FakeAudioAdapter { HoldPlaybackTransition = true };
        var executor = new AudioCommandExecutor(audio, TimeSpan.FromMilliseconds(100));

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            executor.ExecuteAsync(Lease("OPEN", 7, new
            {
                source_id = 42,
                uri = "file:///C:/media/test.mp3",
                autoplay = true,
            })));

        Assert.Contains("音频", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PauseWaitsForActualPausedStateBeforeReporting()
    {
        var audio = new FakeAudioAdapter { HoldPauseTransition = true };
        var executor = new AudioCommandExecutor(audio);
        await executor.ExecuteAsync(Lease("PLAY", 1, new { }));

        var execution = executor.ExecuteAsync(Lease("PAUSE", 1, new { }));
        Assert.False(execution.IsCompleted);
        audio.CompletePauseTransition();

        var result = await execution;
        Assert.Equal("paused", result.ActualState.GetProperty("playback_state").GetString());
    }

    [Fact]
    public async Task PauseFailsWhenNativePlayerKeepsPlaying()
    {
        var audio = new FakeAudioAdapter { HoldPauseTransition = true };
        var executor = new AudioCommandExecutor(audio, TimeSpan.FromMilliseconds(100));
        await executor.ExecuteAsync(Lease("PLAY", 1, new { }));

        await Assert.ThrowsAsync<TimeoutException>(() => executor.ExecuteAsync(Lease("PAUSE", 1, new { })));
    }

    [Fact]
    public async Task OpenAppliesPlaybackSettingsAndReportsActualState()
    {
        var audio = new FakeAudioAdapter();
        var executor = new AudioCommandExecutor(audio);
        var lease = Lease("OPEN", 7, new
        {
            source_id = 42,
            uri = "file:///C:/media/test.mp3",
            volume = 35,
            muted = true,
            loop = true,
            autoplay = true,
        });

        var result = await executor.ExecuteAsync(lease);

        Assert.Equal("completed", result.Status);
        Assert.Equal(42, audio.SourceId);
        Assert.Equal(7, audio.Generation);
        Assert.Equal(35, audio.Volume);
        Assert.True(audio.IsMuted);
        Assert.True(audio.LoopEnabled);
        Assert.Equal(1, audio.PlayCalls);
        Assert.Equal(42, result.ActualState.GetProperty("source_id").GetInt64());
        Assert.Equal(7, result.ActualState.GetProperty("source_generation").GetInt64());
        Assert.Equal("playing", result.ActualState.GetProperty("playback_state").GetString());
    }

    [Theory]
    [InlineData("PLAY")]
    [InlineData("PAUSE")]
    [InlineData("STOP")]
    public async Task TransportCommandsExecuteExactlyOnce(string command)
    {
        var audio = new FakeAudioAdapter();
        var executor = new AudioCommandExecutor(audio);

        await executor.ExecuteAsync(Lease(command, 1, new { }));

        Assert.Equal(command == "PLAY" ? 1 : 0, audio.PlayCalls);
        Assert.Equal(command == "PAUSE" ? 1 : 0, audio.PauseCalls);
        Assert.Equal(command == "STOP" ? 1 : 0, audio.StopCalls);
    }

    [Fact]
    public async Task UnsupportedCommandFailsClosed()
    {
        var executor = new AudioCommandExecutor(new FakeAudioAdapter());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(Lease("NEXT", 1, new { })));
    }

    [Fact]
    public void DuplicateNaturalEndCallbacksReuseEventIdUntilSourceChanges()
    {
        var tracker = new AudioFinishedEventTracker();
        var first = tracker.GetOrCreate(42, 7);

        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, tracker.GetOrCreate(42, 7));
        Assert.NotEqual(first, tracker.GetOrCreate(43, 8));

        tracker.Reset();
        Assert.NotEqual(first, tracker.GetOrCreate(42, 7));
    }

    private static CommandLeaseDto Lease(string command, long generation, object args)
    {
        var json = JsonSerializer.SerializeToElement(args);
        return new CommandLeaseDto
        {
            Command = command,
            SourceGeneration = generation,
            Args = json.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone()),
        };
    }

    private sealed class FakeAudioAdapter : IAudioPlaybackAdapter
    {
        public long SourceId { get; private set; }
        public long Generation { get; private set; }
        public int Volume { get; set; } = 70;
        public bool LoopEnabled { get; set; }
        public bool IsMuted { get; set; }
        public long PositionMs { get; private set; }
        public long DurationMs => 1000;
        public string PlaybackState { get; private set; } = "stopped";
        public int PlayCalls { get; private set; }
        public int PauseCalls { get; private set; }
        public int StopCalls { get; private set; }
        public bool HoldPlaybackTransition { get; init; }
        public bool HoldPauseTransition { get; init; }

        public void CompletePlaybackTransition() => PlaybackState = "playing";
        public void CompletePauseTransition() => PlaybackState = "paused";

        public Task OpenAsync(long sourceId, string uri, long generation, CancellationToken cancellationToken = default)
        {
            SourceId = sourceId;
            Generation = generation;
            PlaybackState = "loading";
            return Task.CompletedTask;
        }

        public Task PlayAsync(CancellationToken cancellationToken = default)
        {
            PlayCalls++;
            if (!HoldPlaybackTransition) PlaybackState = "playing";
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            PauseCalls++;
            if (!HoldPauseTransition) PlaybackState = "paused";
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            PlaybackState = "stopped";
            return Task.CompletedTask;
        }

        public Task SeekAsync(long positionMs, CancellationToken cancellationToken = default)
        {
            PositionMs = positionMs;
            return Task.CompletedTask;
        }
    }
}
