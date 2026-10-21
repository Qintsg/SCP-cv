// 文件描述：仅回环的自有编码器成熟RTMP接收器；项目：SCP-cv；文件：PreviewRtmpReceiver.cs；作者：Qintsg；日期：2026-10-21。
using System.Net;
using System.Threading.Channels;
using LiveStreamingServerNet;
using LiveStreamingServerNet.Networking.Server.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Auth.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace ScpCv.ControlHost.StreamPreview;

public sealed partial class PreviewRtmpReceiver : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Channel<FlvTag> _media = Channel.CreateBounded<FlvTag>(new BoundedChannelOptions(16)
    { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
    private readonly TaskCompletionSource<int> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _path = "/scp-preview/" + Guid.NewGuid().ToString("N");
    private readonly object _sync = new();
    private ILiveStreamingServer? _server;
    private Task? _running, _closing;
    private ITcpListener? _listener;
    private uint? _publisher;
    private int _connections;
    public string PublishUri { get; private set; } = string.Empty;
    public ChannelReader<FlvTag> Reader => _media.Reader;

    private PreviewRtmpReceiver() { }

    /// <summary>:param cancellationToken: 创建寿命。:returns: 已实际绑定的私有端口；不探测或拉取媒体。</summary>
    public static async Task<PreviewRtmpReceiver> StartAsync(CancellationToken cancellationToken)
    {
        var receiver = new PreviewRtmpReceiver();
        try
        {
            receiver._server = LiveStreamingServerBuilder.Create()
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureRtmpServer(options =>
                {
                    options.Configure(settings => { settings.MaxStreamsPerClient = 1; settings.MaxInChunkSize = 65536;
                        settings.EnableGopCaching = false; settings.MediaPacketBatchWindow = TimeSpan.Zero; })
                    .ConfigureMediaStreaming(settings => { settings.MaxOutstandingMediaPacketsCount = 16;
                        settings.MaxOutstandingMediaPacketsSize = 2 * 1024 * 1024; settings.MaxGroupOfPicturesCacheSize = 2 * 1024 * 1024; });
                    // 接收器由本模块拥有；实例注册不把其释放职责交给内部Host。
                    options.Services.AddSingleton<IAuthorizationHandler>(receiver);
                    options.Services.AddSingleton<IRtmpMediaMessageInterceptor>(receiver);
                    options.Services.AddSingleton<IRtmpServerStreamEventHandler>(receiver);
                })
                .ConfigureServer(options =>
                {
                    options.ConfigureNetwork(settings => settings.NoDelay = true).ConfigureDataBufferPool(settings => settings.MaxPoolSize = 8);
                    options.Services.AddSingleton<IServerEventHandler>(receiver);
                })
                .Build();
            receiver._running = receiver._server.RunAsync(new IPEndPoint(IPAddress.Loopback, 0), receiver._lifetime.Token);
            var port = await receiver._ready.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            receiver.PublishUri = $"rtmp://127.0.0.1:{port}{receiver._path}";
            return receiver;
        }
        catch { await receiver.DisposeAsync().ConfigureAwait(false); throw; }
    }

    /// <summary>:returns: 编码器结束后关闭本轮媒体读取，不保留永远等待的读者。</summary>
    public void EncoderExited() => _media.Writer.TryComplete(new IOException("预览编码器已结束"));

    /// <summary>:returns: 幂等取消本接收器，等待精确会话及监听结束；不确定时明确失败。</summary>
    public ValueTask DisposeAsync()
    { lock (_sync) return new(_closing ??= CloseAsync()); }

    /// <summary>:returns: 只关闭本接收器的会话、监听及有界队列。</summary>
    private async Task CloseAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false); _media.Writer.TryComplete();
        if (_server is null) { _lifetime.Dispose(); return; }
        foreach (var client in _server.Clients) client.Disconnect();
        if (_running is not null)
        {
            try { await _running.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
            catch (TimeoutException) { throw new PreviewException("预览接收器退出尚未确认", "preview_cleanup_pending"); }
        }
        await _server.DisposeAsync().ConfigureAwait(false); _lifetime.Dispose();
    }
}
