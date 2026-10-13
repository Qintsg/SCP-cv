// 屏幕音量读写有界网络传输；仿真与真实传输不能混用。
using System.Net.Sockets;

namespace ScpCv.Infrastructure.Devices;

public interface IVenueAudioTransport
{
    bool IsSimulation { get; }
    bool SupportsSourceSelection => false;
    Task<int> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(int volume, CancellationToken cancellationToken);

    /// <summary>:param source: 已预检的二选一来源。:returns: 只有真实装配可发送，旧替身不能静默假成功。</summary>
    Task SelectSourceAsync(string source, CancellationToken cancellationToken) =>
        throw new VenueDeviceException("选源传输未装配，未访问任何设备。");
}

public sealed class SimulationVenueAudioTransport : IVenueAudioTransport
{
    private int _volume = 100;
    public bool IsSimulation => true;
    public bool SupportsSourceSelection => true;
    public int WriteCount { get; private set; }
    public int SourceWriteCount { get; private set; }
    public string? SelectedSource { get; private set; }
    public Task<int> ReadAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_volume); }
    public Task WriteAsync(int volume, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = VenueProtocol.ScreenVolume(volume);
        _volume = volume;
        WriteCount++;
        return Task.CompletedTask;
    }

    public Task SelectSourceAsync(string source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = VenueAudioSourceProtocol.Select(source);
        SourceWriteCount++;
        SelectedSource = source;
        return Task.CompletedTask;
    }
}

public sealed class TcpVenueAudioTransport(VenueOptions options) : IVenueAudioTransport
{
    public bool IsSimulation => false;
    public bool SupportsSourceSelection => true;
    public async Task<int> ReadAsync(CancellationToken cancellationToken) =>
        VenueProtocol.ReadScreenVolume(await ExchangeAsync(VenueProtocol.ScreenVolumeQuery(), true, cancellationToken).ConfigureAwait(false));
    public async Task WriteAsync(int volume, CancellationToken cancellationToken) =>
        _ = await ExchangeAsync(VenueProtocol.ScreenVolume(volume), false, cancellationToken).ConfigureAwait(false);

    /// <summary>:param source: 固定二选一来源。:returns: 有界单次网络写出；完整软件门禁由服务层预检。</summary>
    public async Task SelectSourceAsync(string source, CancellationToken cancellationToken) =>
        _ = await ExchangeAsync(VenueAudioSourceProtocol.Select(source), false, cancellationToken).ConfigureAwait(false);

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
            throw new VenueDeviceException(read ? "屏幕音量读回失败或超时。" : "现场音频配置发送失败或超时；实际状态未确认。");
        }
    }
}
