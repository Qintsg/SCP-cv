// 公开Player OPEN跨真实RTSP协议验证传输选择；无媒体数据、全零音量、不启动现场或其他服务。
using System.Text.Json;
using LibVLCSharp.Shared;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 5秒观察内错误认证不进入SETUP/假报播放；不把连接重试当已确认错误，也不承诺错误时限。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task RtspRejectedCredentialsDoNotClaimPlayingOrReachSetup()
    {
        await using var server = new RecordingRtspTransportServer("fixture-user:correct-fixture-pass");
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            await using var host = new PlayerRuntimeHost(window, 1, libVlcFactory: () => new LibVLC("--vout=dummy", "--no-interact"));
            try
            {
                var uri = server.Url.AbsoluteUri.Replace("rtsp://", "rtsp://fixture-user:wrong-fixture-pass@", StringComparison.Ordinal);
                var lease = OpenLease(uri);
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("rtsp");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(0);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                var accepted = await host.ExecuteAsync(lease, CancellationToken.None);
                Assert.False(await server.Authentication.WaitAsync(TimeSpan.FromSeconds(8)));
                Assert.Equal("completed", accepted.Status);
                var observed = accepted.ActualState;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
                do
                {
                    var sample = await host.SampleProgressAsync(CancellationToken.None);
                    if (sample is not null) observed = sample.State;
                    Assert.NotEqual("playing", observed.GetProperty("playback_state").GetString());
                    if (observed.GetProperty("playback_state").GetString() == "error") break;
                    await Task.Delay(25);
                } while (DateTimeOffset.UtcNow < deadline);
                Assert.False(server.Transport.IsCompletedSuccessfully);
                Assert.DoesNotContain("wrong-fixture-pass", observed.GetProperty("error_message").GetString()!, StringComparison.Ordinal);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:param password: 虚构密码（含需转义字符）。:returns: 真正原生RTSP认证后才可进入SETUP，无现场媒体或声音。</summary>
    [Theory]
    [InlineData("fixture-pass")]
    [InlineData("p:a#ss%@word")]
    [Trait("Category", "Physical")]
    public async Task RtspCredentialsReachNativeAuthentication(string password)
    {
        await using var server = new RecordingRtspTransportServer("fixture-user:" + password);
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            await using var host = new PlayerRuntimeHost(window, 1, libVlcFactory: () => new LibVLC("--vout=dummy", "--no-interact"));
            try
            {
                var uri = server.Url.AbsoluteUri.Replace("rtsp://", "rtsp://fixture-user:" + Uri.EscapeDataString(password) + "@", StringComparison.Ordinal);
                var lease = OpenLease(uri);
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("rtsp");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(0);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(lease, CancellationToken.None);
                Assert.True(await server.Authentication.WaitAsync(TimeSpan.FromSeconds(8)));
                Assert.Contains("RTP/AVP/TCP", await server.Transport.WaitAsync(TimeSpan.FromSeconds(8)), StringComparison.OrdinalIgnoreCase);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:param sourceType: RTSP或自定义流登记类型。:returns: RTSP URI首次SETUP即使用TCP，不能等UDP丢流后才长时间回退。</summary>
    [Theory]
    [InlineData("rtsp")]
    [InlineData("custom_stream")]
    [Trait("Category", "Physical")]
    public async Task RtspOpenRequestsReliableInterleavedTransport(string sourceType)
    {
        await using var server = new RecordingRtspTransportServer();
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--vout=dummy"));
            try
            {
                var lease = OpenLease(server.Url.AbsoluteUri);
                lease.Args["source_type"] = JsonSerializer.SerializeToElement(sourceType);
                lease.Args["volume"] = JsonSerializer.SerializeToElement(0);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(lease, CancellationToken.None);
                var transport = await server.Transport.WaitAsync(TimeSpan.FromSeconds(8));
                Assert.Contains("RTP/AVP/TCP", transport, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("interleaved=", transport, StringComparison.OrdinalIgnoreCase);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }
}
