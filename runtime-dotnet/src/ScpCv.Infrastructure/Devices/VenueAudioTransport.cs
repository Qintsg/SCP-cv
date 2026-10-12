// 屏幕音量读写有界网络传输；仿真与真实传输不能混用。
using System.Net.Sockets;

namespace ScpCv.Infrastructure.Devices;

public interface IVenueAudioTransport
{
    bool IsSimulation { get; }
    Task<int> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(int volume, CancellationToken cancellationToken);
}

public sealed class SimulationVenueAudioTransport : IVenueAudioTransport
{
    private int _volume = 100;
    public bool IsSimulation => true;
    public int WriteCount { get; private set; }
    public Task<int> ReadAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_volume); }
    public Task WriteAsync(int volume, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = VenueProtocol.ScreenVolume(volume);
        _volume = volume;
        WriteCount++;
        return Task.CompletedTask;
    }
}

public sealed class TcpVenueAudioTransport(VenueOptions options) : IVenueAudioTransport
{
    public bool IsSimulation => false;
    public async Task<int> ReadAsync(CancellationToken cancellationToken) =>
        VenueProtocol.ReadScreenVolume(await ExchangeAsync(VenueProtocol.ScreenVolumeQuery(), true, cancellationToken).ConfigureAwait(false));
    public async Task WriteAsync(int volume, CancellationToken cancellationToken) =>
        _ = await ExchangeAsync(VenueProtocol.ScreenVolume(volume), false, cancellationToken).ConfigureAwait(false);

    private async Task<byte[]> ExchangeAsync(byte[] bytes, bool read, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ScreenAudioHost) || options.ScreenAudioPort is < 1 or > 65535)
            throw new VenueDeviceException("屏幕音频端点配置无效，未访问设备。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Math.Clamp(options.TimeoutMilliseconds, 100, 10000));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(options.ScreenAudioHost, options.ScreenAudioPort, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            await stream.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            if (!read) return [];
            var response = new byte[13];
            await stream.ReadExactlyAsync(response, timeout.Token).ConfigureAwait(false);
            return response;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            throw new VenueDeviceException(read ? "屏幕音量读回失败或超时。" : "屏幕音量发送失败或超时；实际状态未确认。");
        }
    }
}
