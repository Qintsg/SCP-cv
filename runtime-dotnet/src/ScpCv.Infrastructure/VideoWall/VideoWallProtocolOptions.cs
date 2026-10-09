// D4输入63/64/81与协议开关；旧D2的55/56仅为显式历史兼容配置。
using System.Net;
using System.Net.Sockets;

namespace ScpCv.Infrastructure.VideoWall;

public sealed class VideoWallProtocolOptions
{
    public int SendType { get; set; } = 1;
    public bool CutSync { get; set; } = true;
    public bool IgmpV3 { get; set; }
    public bool UseNewProtocol { get; set; }
    public string DesKeyHex { get; set; } = string.Empty;
    public bool PreserveLegacyPresets { get; set; } = true;
    public Dictionary<string, VideoWallInputDescriptor> Inputs { get; set; } = new(StringComparer.Ordinal)
    {
        ["window_1"] = Source(63),
        ["window_2"] = Source(64),
        ["laptop"] = Source(81),
    };

    /// <summary>检查协议选择；密钥仅由编码器验证，不在异常中回显。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "设备原协议只能使用DES；仅显式开启时用于兼容，不用于认证或安全保护。")]
    public void Validate()
    {
        if (SendType is < 1 or > 3 || Inputs is null)
            throw new VideoWallException("视频墙发送配置无效，未向设备发送命令。");
        if (UseNewProtocol)
        {
            try
            {
                var key = Convert.FromHexString(DesKeyHex ?? string.Empty);
                if (key.Length != 8 || System.Security.Cryptography.DES.IsWeakKey(key) ||
                    System.Security.Cryptography.DES.IsSemiWeakKey(key))
                    throw new VideoWallException("视频墙新协议需要有效的8字节DES密钥，未下发。");
            }
            catch (FormatException)
            {
                throw new VideoWallException("视频墙DES密钥格式无效，未下发。");
            }
        }
    }

    private static VideoWallInputDescriptor Source(int number) => new()
    {
        UnicastAddress = $"192.168.5.{number}",
        MulticastAddress = $"224.1.1.{number}",
        Port = 5000,
        Width = 3840,
        Height = 2160,
        UnicastPort = 59993,
    };
}

public sealed class VideoWallInputDescriptor
{
    public string UnicastAddress { get; set; } = string.Empty;
    public string MulticastAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int UnicastPort { get; set; } = 59993;

    /// <summary>输入描述预检；不根据IP自动猜测端口、分辨率或型号。</summary>
    public void Validate()
    {
        if (!IsIPv4(UnicastAddress, false) || !IsIPv4(MulticastAddress, true) ||
            Port is < 1 or > 65535 || UnicastPort is not (59993 or 60000) ||
            Width is < 1 or > 65535 || Height is < 1 or > 65535)
            throw new VideoWallException("视频墙输入地址、端口或分辨率无效，未向设备发送命令。");
    }

    private static bool IsIPv4(string value, bool multicast) =>
        IPAddress.TryParse(value, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork &&
        (ip.GetAddressBytes()[0] is >= 224 and <= 239) == multicast &&
        ip.GetAddressBytes()[0] is > 0 and < 240 && !ip.Equals(IPAddress.Broadcast);
}
