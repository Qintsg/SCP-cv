// 公开Player OPEN跨真实RTSP协议验证传输选择；无媒体数据、全零音量、不启动现场或其他服务。
using System.Text.Json;
using LibVLCSharp.Shared;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
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
