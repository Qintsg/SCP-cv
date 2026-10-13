// 仅受信触控工程绑定可发包；客户端不能指定IP、端口、Join或原始字节。
namespace ScpCv.Infrastructure.Devices;

public sealed class VenueJoinDispatcher(IDeviceCommandTransport transport)
{
    public bool IsSimulation => transport.IsSimulation;

    /// <summary>:param id: 五路固定摄像机。:returns: 工程控制端点，不使用任意RTSP主机。</summary>
    public static string CameraHost(string id) => id switch
    {
        "rear" => "192.168.5.84", "center1" => "192.168.5.85", "center2" => "192.168.5.86",
        "left" => "192.168.5.87", "right" => "192.168.5.88",
        _ => throw new VenueDeviceException("固定监控或未知摄像机不支持云台控制。"),
    };

    /// <summary>:param id: 摄像机。:param join: 已核实的运动/模式/位置绑定。:param pressed: 按下或释放。</summary>
    public Task DigitalAsync(string id, int join, bool pressed, CancellationToken token)
    {
        if (join is not (>= 31 and <= 36 or 38 or 39 or >= 45 and <= 50))
            throw new VenueDeviceException("摄像机Join不在工程白名单中。");
        return SendAsync("camera:" + id, CameraHost(id), [VenueJoinProtocol.Digital(join, pressed)], TimeSpan.Zero, token);
    }

    /// <summary>:param join: 头戴1、手持2、电脑3。:param percent: 0–100请求值，不冒充实际DSP增益。</summary>
    public Task AnalogAsync(int join, int percent, CancellationToken token)
    {
        if (join is < 1 or > 3) throw new VenueDeviceException("音频Join不在工程白名单中。");
        return SendAsync("venue_audio", "192.168.5.10", [VenueJoinProtocol.Analog(join, percent)], TimeSpan.Zero, token);
    }

    private async Task SendAsync(string type, string host, IReadOnlyList<string> frames, TimeSpan delay, CancellationToken token)
    {
        try { await transport.SendAsync(new DeviceCommand(type, host, 8889, frames, delay), token).ConfigureAwait(false); }
        catch (DeviceServiceException exception) { throw new VenueDeviceException("触控指令发送未确认：" + exception.Message); }
    }
}
