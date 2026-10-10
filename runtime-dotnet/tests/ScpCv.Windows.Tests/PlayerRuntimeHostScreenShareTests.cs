// 正式Host的共享OPEN、后帧失败、换源与实际关闭回归；隐藏STA不启动原生浏览器。
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 已过期OPEN即使adapter同步就绪也必须零原生创建，不能让已完成Task绕过取消。</summary>
    [Fact]
    public async Task ExpiredShareLeaseCannotOpenSynchronouslyReadyResource()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var calls = 0; var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => { calls++; return Task.FromResult<IScreenShareSurface>(new ControlledShare(identity)); } };
            try
            {
                var lease = ShareLease(1, 1) with { Deadline = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O") };
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.ExecuteAsync(lease, CancellationToken.None));
                Assert.Equal(0, calls);
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 没有首帧的候选有限等待并释放；显式取消不产生实际共享资源。</summary>
    [Fact]
    public async Task ShareWithoutFirstFrameHonorsCancellationAndReleasesCandidate()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); ControlledShare? resource = null;
            var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => { resource = new(identity, prepare: false); created.TrySetResult(); return Task.FromResult<IScreenShareSurface>(resource); } };
            using var cancel = new CancellationTokenSource();
            try
            {
                var opening = host.ExecuteAsync(ShareLease(1, 1), cancel.Token);
                await created.Task.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.False(opening.IsCompleted);
                cancel.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
                Assert.Equal(1, resource!.CloseCalls);
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 实际接收失败须由当前资源采样成error，保留分享归属，不假报idle/已释放。</summary>
    [Fact]
    public async Task CurrentShareFailureIsNotHiddenByPreviousPlayingState()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); ControlledShare? resource = null;
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(resource = new(identity)) };
            try
            {
                var lease = ShareLease(1, 1);
                await host.ExecuteAsync(lease, CancellationToken.None);
                resource!.State.Fail("share_peer_disconnected");
                var sample = await host.SampleProgressAsync(CancellationToken.None);
                Assert.NotNull(sample);
                Assert.Equal("error", sample.State.GetProperty("playback_state").GetString());
                Assert.Equal(lease.Args["share_id"].GetString(), sample.State.GetProperty("share_id").GetString());
                Assert.Equal(0, resource.CloseCalls);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 正式共享OPEN不需要永久源或普通uri，结果等待后帧并保留明确分享身份。</summary>
    [Fact]
    public async Task ScreenShareOpenUsesNullSourceAndActualPostActivationFrame()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            ControlledShare? resource = null;
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(resource = new(identity)) };
            try
            {
                var lease = ShareLease(1, 1);
                var result = await host.ExecuteAsync(lease, CancellationToken.None);
                Assert.Equal("completed", result.Status);
                Assert.Equal(JsonValueKind.Null, result.ActualState.GetProperty("source_id").ValueKind);
                Assert.Equal("screen_share", result.ActualState.GetProperty("adapter_kind").GetString());
                Assert.Equal(lease.Args["share_id"].GetString(), result.ActualState.GetProperty("share_id").GetString());
                Assert.Equal("playing", result.ActualState.GetProperty("playback_state").GetString());
                Assert.Equal(2, result.ActualState.GetProperty("actual_frames").GetInt64());
                Assert.Equal(JsonValueKind.Null, result.ActualState.GetProperty("position_ms").ValueKind);
                var observed = await host.SampleProgressAsync(CancellationToken.None);
                Assert.NotNull(observed);
                Assert.Equal(1, observed.SourceGeneration);
                await host.ExecuteAsync(lease with { Command = "CLOSE", SourceGeneration = 2 }, CancellationToken.None);
                Assert.Equal(1, resource!.CloseCalls);
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:param stage: 预备帧或后帧失败。:returns: 失败共享不能覆盖仍然存活的旧图，也不变成warm缓存。</summary>
    [Theory]
    [InlineData("prepare")]
    [InlineData("activate")]
    public async Task FailedScreenShareLeavesPreviousImageAndDisposesCandidate(string stage)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            ControlledShare? failed = null;
            var host = new PlayerRuntimeHost(window, 1)
            {
                ScreenShareFactory = (identity, _) =>
                {
                    failed = new(identity, prepare: stage != "prepare", activate: stage != "activate");
                    if (stage == "prepare") failed.State.Fail("share_negotiation_failed");
                    return Task.FromResult<IScreenShareSurface>(failed);
                },
            };
            try
            {
                // 图片不依赖文稿包/原生引擎，使用既有图片adapter的自有最小PNG。
                var root = Path.GetDirectoryName(CreatePackage())!;
                var imagePath = Path.Combine(root, "shared-original.png");
                var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null, new byte[] { 0, 80, 255, 255 }, 4);
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var file = File.Open(imagePath, FileMode.CreateNew)) png.Save(file);
                var original = OpenLease(imagePath);
                original.Args["source_type"] = JsonSerializer.SerializeToElement("image");
                await host.ExecuteAsync(original, CancellationToken.None);
                var surfaces = Assert.IsType<Grid>(window.FindName("SurfaceHost"));
                var oldSurface = Assert.Single(surfaces.Children.Cast<object>());
                await Assert.ThrowsAsync<IOException>(() => host.ExecuteAsync(ShareLease(1, 2), CancellationToken.None));
                Assert.Same(oldSurface, Assert.Single(surfaces.Children.Cast<object>()));
                Assert.Equal(1, failed!.CloseCalls);
                var retained = await host.ExecuteAsync(original with { Command = "SET_VOLUME" }, CancellationToken.None);
                Assert.Equal(101, retained.ActualState.GetProperty("source_id").GetInt64());
                Assert.Equal("image", retained.ActualState.GetProperty("adapter_kind").GetString());
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 旧资源失败/结束和旧共享CLOSE均不能清理已发布的另一分享。</summary>
    [Fact]
    public async Task OldShareCallbackAndConditionalCloseCannotAffectTheNextShare()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var resources = new List<ControlledShare>();
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => { var item = new ControlledShare(identity); resources.Add(item); return Task.FromResult<IScreenShareSurface>(item); } };
            try
            {
                var first = ShareLease(1, 1); var next = ShareLease(1, 2);
                await host.ExecuteAsync(first, CancellationToken.None);
                await host.ExecuteAsync(next, CancellationToken.None);
                Assert.Equal(1, resources[0].CloseCalls);
                resources[0].State.Fail("share_peer_disconnected");
                var stale = first with { Command = "CLOSE", SourceGeneration = 3 };
                stale.Args["close_source_generation"] = JsonSerializer.SerializeToElement(1);
                var noOp = await host.ExecuteAsync(stale, CancellationToken.None);
                Assert.Equal("share_no_longer_current", noOp.ResultCode);
                Assert.Equal(next.Args["share_id"].GetString(), noOp.ActualState.GetProperty("share_id").GetString());
                Assert.Equal(2, noOp.ActualState.GetProperty("source_generation").GetInt64());
                Assert.Equal("playing", noOp.ActualState.GetProperty("playback_state").GetString());
                Assert.Equal(0, resources[1].CloseCalls);
            }
            finally { await host.DisposeAsync(); window.Close(); }
            Assert.All(resources, item => Assert.Equal(1, item.CloseCalls));
        });
    }

    /// <summary>:returns: 共享不能用普通媒体动作伪成功；内容音频桥在T013实测接通前也明确拒绝。</summary>
    [Theory]
    [InlineData("PLAY")]
    [InlineData("PAUSE")]
    [InlineData("SEEK")]
    [InlineData("SET_VOLUME")]
    [InlineData("SET_MUTE")]
    [InlineData("NEXT")]
    public async Task UnsupportedShareControlIsNotSuccessful(string command)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(new ControlledShare(identity)) };
            try
            {
                var lease = ShareLease(1, 1);
                await host.ExecuteAsync(lease, CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(lease with { Command = command }, CancellationToken.None));
                Assert.Equal("playing", (await host.SampleProgressAsync(CancellationToken.None))!.State.GetProperty("playback_state").GetString());
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:param window: 目标。:param generation: 独立代次。:returns: 仅lease携带cap、不带普通媒体uri的正式参数。</summary>
    private static CommandLeaseDto ShareLease(int window, long generation)
    {
        var id = Guid.NewGuid();
        return new()
        {
            CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = "OPEN", GroupEpoch = 1, OwnerEpoch = 1,
            SourceGeneration = generation,
            Args = JsonSerializer.SerializeToElement(new { source_id = 0, source_type = "screen_share", share_id = id,
                receiver_uri = $"http://127.0.0.1:5207/api/screen-share/receiver/{id:D}/?window_id={window}&source_generation={generation}#{new string('a', 64)}" })
                .EnumerateObject().ToDictionary(p => p.Name, p => p.Value),
        };
    }

    /// <summary>仅替代原生外部IO；Host自身必须等待预备及激活后帧并管理Dispose。</summary>
    private sealed class ControlledShare : IScreenShareSurface
    {
        private readonly bool _activate;
        internal int CloseCalls { get; private set; }
        public FrameworkElement Surface { get; } = new Grid();
        public ScreenSharePlaybackState State { get; }
        internal ControlledShare(ScreenShareReceiverIdentity identity, bool prepare = true, bool activate = true)
        {
            State = new(identity); _activate = activate;
            if (prepare) { Emit("ready", new { frames = 0 }); Emit("frame", new { frames = 1, activated = false, has_audio = false, media_time = 1d }); }
        }
        /// <summary>:param volume: 未测试音频。:param muted: 未测试音频。:param token: OPEN寿命。:returns: 实际后帧或明确失败，由Host处理。</summary>
        public Task ActivateAsync(int volume, bool muted, CancellationToken token)
        { State.BeginActivation(); if (_activate) Emit("frame", new { frames = 2, activated = true, has_audio = false, media_time = 2d }); else State.Fail("share_video_play_failed"); return Task.CompletedTask; }
        /// <summary>:returns: 每次外部调用计数，退役之后拒绝晚消息。</summary>
        public ValueTask DisposeAsync() { CloseCalls++; State.Retire(); return ValueTask.CompletedTask; }
        /// <summary>:param type: 正式消息类型。:param fields: 实际字段。:returns: 相同来源/身份的纯值消息。</summary>
        private void Emit(string type, object fields)
        {
            var identity = State.Identity;
            var values = JsonSerializer.SerializeToElement(fields).EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            values.Add("type", JsonSerializer.SerializeToElement(type)); values.Add("share_id", JsonSerializer.SerializeToElement(identity.ShareId));
            values.Add("window_id", JsonSerializer.SerializeToElement(identity.WindowId)); values.Add("source_generation", JsonSerializer.SerializeToElement(identity.SourceGeneration));
            State.Accept(identity.Address.AbsoluteUri, JsonSerializer.Serialize(values));
        }
    }
}
