// 正式Host的共享释放证据只在自有Dispose成功后产生；外部原生IO替身不当实际WebView证明。
using System.Text.Json;
using System.Windows;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    [Fact]
    public async Task ShareReleaseEvidenceWaitsForNativeDisposalCompletion()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(new ReleaseControlledShare(identity, started, finished.Task)) };
            try
            {
                var lease = ShareLease(1, 1);
                var opening = await host.ExecuteAsync(lease, CancellationToken.None);
                Assert.Empty(opening.ActualState.GetProperty("released_screen_shares").EnumerateArray());
                var closing = host.ExecuteAsync(lease with { Command = "CLOSE", SourceGeneration = 2 }, CancellationToken.None);
                await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(closing.IsCompleted);
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                finished.SetResult();
                var closed = await closing;
                var proof = Assert.Single(closed.ActualState.GetProperty("released_screen_shares").EnumerateArray());
                Assert.Equal(lease.Args["share_id"].GetGuid(), proof.GetProperty("share_id").GetGuid());
                Assert.Equal(1, proof.GetProperty("source_generation").GetInt64());
                Assert.Equal("idle", closed.ActualState.GetProperty("playback_state").GetString());
                Assert.NotNull(await host.SampleProgressAsync(CancellationToken.None));
            }
            finally { finished.TrySetResult(); await host.DisposeAsync(); window.Close(); }
        });
    }

    [Fact]
    public async Task FailedShareDisposalNeverProducesReleaseProofOrRetriesNativeClose()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ReleaseControlledShare? native = null;
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(native = new ReleaseControlledShare(identity, started, Task.FromException(new IOException("fixture_disposal_failed")))) };
            try
            {
                var lease = ShareLease(1, 1);
                await host.ExecuteAsync(lease, CancellationToken.None);
                await Assert.ThrowsAsync<IOException>(() => host.ExecuteAsync(lease with { Command = "CLOSE", SourceGeneration = 2 }, CancellationToken.None));
                var sample = await host.SampleProgressAsync(CancellationToken.None);
                Assert.NotNull(sample);
                Assert.Empty(sample.State.GetProperty("released_screen_shares").EnumerateArray());
                Assert.Equal(lease.Args["share_id"].GetGuid(), sample.State.GetProperty("share_id").GetGuid());
                await Assert.ThrowsAsync<IOException>(() => host.DisposeAsync().AsTask());
                Assert.Equal(1, native!.Closes);
            }
            finally { window.Close(); }
        });
    }

    private sealed class ReleaseControlledShare(ScreenShareReceiverIdentity identity, TaskCompletionSource started, Task disposal) : IScreenShareSurface
    {
        private readonly ControlledShare _prepared = new(identity);
        public FrameworkElement Surface => _prepared.Surface;
        public ScreenSharePlaybackState State => _prepared.State;
        internal int Closes { get; private set; }
        public Task ActivateAsync(int volume, bool muted, CancellationToken token) => _prepared.ActivateAsync(volume, muted, token);
        /// <summary>:param volume: 专用音量。:param muted: 明确静音。:param token: 控制寿命。:returns: 仅委托已有IO替身，不改变释放验证语义。</summary>
        public Task ApplyAudioAsync(int volume, bool muted, CancellationToken token) => _prepared.ApplyAudioAsync(volume, muted, token);

        /// <summary>:returns: 退役媒体不代表控制器已退出，测试显式控制真实Dispose完成/失败的边界。</summary>
        public ValueTask DisposeAsync()
        {
            Closes++; State.Retire(); started.TrySetResult(); return new(disposal);
        }
    }
}
