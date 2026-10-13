// 显式软件声音绑定的原生回归；只读夹具、隐藏自有STA、实例静音，不修改系统声卡。
using System.Text.Json;
using LibVLCSharp.Shared;
using ScpCv.Contracts.Runtime;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:param windowId: 大屏逻辑目标。:returns: 首次OPEN与停止重播均等待真实端点，不把启动空ID判为错绑并禁用音轨。</summary>
    [Theory]
    [Trait("Category", "Physical")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BoundAudioHasConcreteOutputBeforeOpenAndReplayComplete(int windowId)
    {
        var endpoint = Environment.GetEnvironmentVariable("SCP_CV_QA_AUDIO_OUTPUT_DEVICE_ID");
        var path = Environment.GetEnvironmentVariable("SCP_CV_QA_AUDIO_WAV");
        Assert.False(string.IsNullOrWhiteSpace(endpoint), "请显式提供本机完整Render端点ID");
        Assert.True(!string.IsNullOrWhiteSpace(path) && File.Exists(path), "请显式提供有限只读WAV夹具");
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null;
            await using var host = new PlayerRuntimeHost(window, windowId, audioOutputDeviceId: endpoint,
                configureVlcPlayer: value => { player = value; player.Mute = true; });
            try
            {
                var lease = OpenLease(path!);
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                await host.ExecuteAsync(lease, CancellationToken.None);
                Assert.True(AudioOutputBinding.Matches(endpoint!, player!.OutputDevice));
                await host.ExecuteAsync(lease with { Command = "STOP" }, CancellationToken.None);
                await host.ExecuteAsync(lease with { Command = "PLAY" }, CancellationToken.None);
                Assert.True(AudioOutputBinding.Matches(endpoint!, player.OutputDevice));
                Assert.True(player.Mute);
                Assert.Equal("playing", (await host.SampleProgressAsync(CancellationToken.None))!.State.GetProperty("playback_state").GetString());
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }
}
