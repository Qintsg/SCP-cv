// 现场绑定只从受信配置读取，输入节点地址不是摄像机运动控制地址。
namespace ScpCv.Infrastructure.Devices;

public sealed class VenueOptions
{
    public const string SectionName = "Venue";
    public string ScreenAudioHost { get; set; } = "192.168.5.155";
    public int ScreenAudioPort { get; set; } = 4830;
    public int TimeoutMilliseconds { get; set; } = 3000;
    public string FixedCameraRtsp { get; set; } = string.Empty;
}

public sealed class VenueDeviceException(string message) : Exception(message);
