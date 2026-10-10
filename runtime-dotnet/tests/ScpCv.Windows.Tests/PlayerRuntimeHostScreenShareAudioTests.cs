// 屏幕共享专用音频控制必须核当前身份/代次且保留原生回执，不借用VLC；隐藏STA测试不计真实出声。
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 专用命令只改变当前共享音频，实际代次不因控制而推进。</summary>
    [Fact]
    public async Task DedicatedShareAudioUsesReceiverReadbackAndPreservesGeneration()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            ControlledShare? resource = null;
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(resource = new ControlledShare(identity)) };
            try
            {
                var opened = ShareLease(1, 7);
                await host.ExecuteAsync(opened, CancellationToken.None);
                var result = await host.ExecuteAsync(ShareAudioLease(opened, 29, false), CancellationToken.None);
                Assert.Equal("completed", result.Status);
                Assert.Equal("ok", result.ResultCode);
                Assert.Equal(7, result.ActualState.GetProperty("source_generation").GetInt64());
                Assert.Equal(29, result.ActualState.GetProperty("actual_share_volume").GetInt32());
                Assert.True(result.ActualState.GetProperty("actual_share_muted").GetBoolean());
                Assert.False(result.ActualState.GetProperty("actual_audio").GetBoolean());
                Assert.Equal("playing", result.ActualState.GetProperty("playback_state").GetString());
                Assert.Equal(1, resource!.AudioCalls);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:param invalid: 畸形参数分类。:returns: 严格拒绝且原生零调用，未开始动作时保留既有回执。</summary>
    [Theory]
    [InlineData("negative")]
    [InlineData("large")]
    [InlineData("fraction")]
    [InlineData("text")]
    [InlineData("mute_text")]
    [InlineData("missing")]
    [InlineData("extra")]
    public async Task DedicatedShareAudioRejectsInvalidValuesWithoutCallingNative(string invalid)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); ControlledShare? resource = null;
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(resource = new(identity)) };
            try
            {
                var opened = ShareLease(1, 7);
                await host.ExecuteAsync(opened, CancellationToken.None);
                var control = ShareAudioLease(opened, 29, false);
                if (invalid == "missing") control.Args.Remove("volume");
                else if (invalid == "extra") control.Args.Add("uri", JsonSerializer.SerializeToElement("never-used"));
                else control.Args[invalid == "mute_text" ? "muted" : "volume"] = invalid switch
                {
                    "negative" => JsonSerializer.SerializeToElement(-1), "large" => JsonSerializer.SerializeToElement(101),
                    "fraction" => JsonSerializer.SerializeToElement(2.5), _ => JsonSerializer.SerializeToElement("29"),
                };
                await Assert.ThrowsAsync<InvalidDataException>(() => host.ExecuteAsync(control, CancellationToken.None));
                Assert.Equal(0, resource!.AudioCalls);
                Assert.Equal(100, resource.State.Audio!.Volume);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 有音轨的成功回读与控制失败分别表示；失败不伪装旧静音仍已确认，也不影响视频身份。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DedicatedShareAudioOnlyPublishesConfirmedProperties(bool fail)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); ControlledShare? resource = null;
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(resource = new(identity, hasAudio: true)
                { AudioFailure = fail ? new IOException("fixture_audio_failed") : null }) };
            try
            {
                var opened = ShareLease(1, 7);
                await host.ExecuteAsync(opened, CancellationToken.None);
                if (fail)
                {
                    await Assert.ThrowsAsync<IOException>(() => host.ExecuteAsync(ShareAudioLease(opened, 29, false), CancellationToken.None));
                    Assert.Null(resource!.State.Audio);
                }
                else
                {
                    var result = await host.ExecuteAsync(ShareAudioLease(opened, 29, false), CancellationToken.None);
                    Assert.False(result.ActualState.GetProperty("actual_share_muted").GetBoolean());
                    Assert.Equal(29, result.ActualState.GetProperty("actual_share_volume").GetInt32());
                }
                Assert.Equal(1, resource!.AudioCalls);
                Assert.True(resource.State.IsPlaying);
                Assert.Equal(7, (await host.SampleProgressAsync(CancellationToken.None))!.SourceGeneration);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 过期专用控制同步拒绝，不受已完成Task影响且不调用原生音频。</summary>
    [Fact]
    public async Task ExpiredDedicatedShareAudioDoesNotTouchNative()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); ControlledShare? resource = null;
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(resource = new(identity)) };
            try
            {
                var opened = ShareLease(1, 7);
                await host.ExecuteAsync(opened, CancellationToken.None);
                var expired = ShareAudioLease(opened, 29, false) with { Deadline = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O") };
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.ExecuteAsync(expired, CancellationToken.None));
                Assert.Equal(0, resource!.AudioCalls);
                Assert.Equal(100, resource.State.Audio!.Volume);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:param mismatch: 旧分享、旧代次、伪目标均不得触及当前接收资源。</summary>
    [Theory]
    [InlineData("id")]
    [InlineData("generation")]
    [InlineData("window")]
    public async Task DedicatedShareAudioRejectsMismatchedIdentity(string mismatch)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { ScreenShareFactory = (identity, _) => Task.FromResult<IScreenShareSurface>(new ControlledShare(identity)) };
            try
            {
                var opened = ShareLease(1, 7);
                await host.ExecuteAsync(opened, CancellationToken.None);
                var control = ShareAudioLease(opened, 29, false);
                if (mismatch == "generation") control = control with { SourceGeneration = 8 };
                else control.Args[mismatch == "id" ? "share_id" : "window_id"] = mismatch == "id"
                    ? JsonSerializer.SerializeToElement(Guid.NewGuid()) : JsonSerializer.SerializeToElement(2);
                await Assert.ThrowsAsync<InvalidDataException>(() => host.ExecuteAsync(control, CancellationToken.None));
                var sample = (await host.SampleProgressAsync(CancellationToken.None))!;
                Assert.Equal(7, sample.SourceGeneration);
                Assert.Equal(opened.Args["share_id"].GetGuid(), sample.State.GetProperty("share_id").GetGuid());
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:param opened: 原共享OPEN。:param volume: 专用音量。:param muted: 明确静音。:returns: 不包含cap/URI、原OPEN代次的控制。</summary>
    private static CommandLeaseDto ShareAudioLease(CommandLeaseDto opened, int volume, bool muted) => opened with
    {
        CommandId = Guid.NewGuid(), Command = "SCREEN_SHARE_AUDIO",
        Args = JsonSerializer.SerializeToElement(new { share_id = opened.Args["share_id"].GetGuid(), window_id = 1, volume, muted })
            .EnumerateObject().ToDictionary(item => item.Name, item => item.Value),
    };
}
