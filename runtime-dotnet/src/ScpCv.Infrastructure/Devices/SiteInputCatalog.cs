// 现场输入唯一目录：用户2026-10-21设备表；不探测网络，不把空端口推断为设备。
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Devices;

public sealed record SiteInputDefinition(string Id, string Name, string Group, int IpSuffix, int Width, int Height, string SystemKey)
{
    public string Address => $"192.168.5.{IpSuffix}";
    public string MulticastAddress => $"224.1.1.{IpSuffix}";
    public string PreviewKind => IpSuffix switch { 63 => "window_1", 64 => "window_2", 65 => "window_3", 66 => "window_4", 81 => "laptop", _ => "ip_stream" };
    public string DefaultPreviewUri => $"{(Group == "camera" ? "rtsp" : "rtmp")}://{Address}:{(Group == "camera" ? 554 : 1935)}/live/chn0";

    /// <summary>:returns: 目录登记描述；0/0保留未知，调用方必须Validate，不能猜4K。</summary>
    public VideoWallInputDescriptor Descriptor() => new() { UnicastAddress = Address, MulticastAddress = MulticastAddress, Port = 5000, UnicastPort = 59993, Width = Width, Height = Height };
}

public static class SiteInputCatalog
{
    public static IReadOnlyList<SiteInputDefinition> All { get; } = Build();

    /// <summary>:param address: 完整规范单播地址。:returns: 已登记项或null，75等空端口不生成。</summary>
    public static SiteInputDefinition? ByAddress(string? address) => All.FirstOrDefault(item => item.Address.Equals(address, StringComparison.Ordinal));

    /// <summary>:param kind: 输入种类。:param ip: ip_stream地址。:param options: 受信配置。:returns: 同一地址的唯一描述；未知或配置冲突返回null，调用者不得发包。</summary>
    public static VideoWallInputDescriptor? ResolveDescriptor(string kind, string? ip, VideoWallProtocolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Inputs is null) return null;
        string address;
        if (kind is "window_1" or "window_2" or "window_3" or "window_4" or "laptop")
        {
            if (!string.IsNullOrEmpty(ip)) return null;
            var suffix = kind switch { "window_1" => 63, "window_2" => 64, "window_3" => 65, "window_4" => 66, _ => 81 };
            address = options.Inputs.TryGetValue(kind, out var configured) ? configured.UnicastAddress : $"192.168.5.{suffix}";
        }
        else if (kind == "ip_stream" && !string.IsNullOrWhiteSpace(ip)) address = ip;
        else return null;
        // 地址键不能暗中重定向；窗口别名与完整IP必须通过相同的一致性检查。
        if (options.Inputs.TryGetValue(address, out var keyed) && keyed.UnicastAddress != address) return null;
        var candidates = options.Inputs.Values.Where(item => item.UnicastAddress == address).ToArray();
        if (candidates.Length == 0) return ByAddress(address)?.Descriptor();
        var first = candidates[0];
        return candidates.All(item => item.MulticastAddress == first.MulticastAddress && item.Port == first.Port &&
            item.Width == first.Width && item.Height == first.Height && item.UnicastPort == first.UnicastPort) ? first : null;
    }

    /// <summary>:param input: 领域输入。:param options: 受信配置。:returns: 同一解析规则，不让矩阵与墙面各自猜源。</summary>
    public static VideoWallInputDescriptor? ResolveDescriptor(WallInput input, VideoWallProtocolOptions options) =>
        ResolveDescriptor(input.Kind switch
        {
            WallInputKind.Window1 => "window_1", WallInputKind.Window2 => "window_2", WallInputKind.Window3 => "window_3",
            WallInputKind.Window4 => "window_4", WallInputKind.Laptop => "laptop", _ => "ip_stream",
        }, string.IsNullOrEmpty(input.IpAddress) ? null : input.IpAddress, options);

    /// <summary>:returns: 2026-10-21 07:01UTC匹配主组播的611A尺寸；D4已4K，84无回读则未知。不是实时扫描，显式配置可覆盖。</summary>
    private static SiteInputDefinition[] Build()
    {
        var result = Enumerable.Range(51, 24).Select(suffix =>
        {
            var name = $"D{(suffix - 51) / 4 + 1}-{(suffix - 51) % 4 + 1}";
            var width = suffix is 59 or 60 or 62 or 71 or 72 or 74 ? 1920 : 3840;
            return new SiteInputDefinition("workstation:" + name, name, "workstation", suffix,
                width, width * 9 / 16, "workstation:" + name);
        }).ToList();
        result.AddRange([
            new("camera:rear", "后摄像机", "camera", 84, 0, 0, "camera:rear"),
            new("camera:center1", "中摄像机1", "camera", 85, 1920, 1080, "camera:center1"),
            new("camera:center2", "中摄像机2", "camera", 86, 1920, 1080, "camera:center2"),
            new("camera:left", "左摄像机", "camera", 87, 1920, 1080, "camera:left"),
            new("camera:right", "右摄像机", "camera", 88, 1920, 1080, "camera:right"),
            new("camera:fixed", "监控01", "camera", 76, 1920, 1080, "camera:fixed"),
            new("site:laptop", "现场输入", "site", 81, 1920, 1080, string.Empty),
        ]);
        return result.ToArray();
    }
}
