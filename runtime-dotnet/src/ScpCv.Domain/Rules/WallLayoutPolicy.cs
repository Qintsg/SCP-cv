// 验证大屏区域布局，并仅识别已有抓包证据的两个固定预设。
using System.Net;
using System.Net.Sockets;
using ScpCv.Domain.Model;

namespace ScpCv.Domain.Rules;

public static class WallLayoutPolicy
{
    public static WallLayout Validate(WallLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (string.IsNullOrWhiteSpace(layout.Name) || layout.Mappings is null)
            throw new ArgumentException("布局名称和映射内容不能为空。", nameof(layout));
        var name = layout.Name.Trim();
        if (name.Length is < 1 or > 64)
            throw new ArgumentException("布局名称必须为 1 到 64 个字符。", nameof(layout));
        if (layout.Mappings.Count is < 1 or > 2)
            throw new ArgumentException("大屏布局必须包含一项全屏或至多两项左右映射。", nameof(layout));

        var regions = new HashSet<WallRegion>();
        foreach (var mapping in layout.Mappings)
        {
            if (mapping.Input is null)
                throw new ArgumentException("大屏输入不能为空。", nameof(layout));
            if (!Enum.IsDefined(mapping.Region) || !regions.Add(mapping.Region))
                throw new ArgumentException("大屏目标区域无效或重复。", nameof(layout));
            if (!Enum.IsDefined(mapping.Input.Kind))
                throw new ArgumentException("大屏输入类型无效。", nameof(layout));
            if (mapping.Input.Kind == WallInputKind.IpStream)
            {
                if (!IPAddress.TryParse(mapping.Input.IpAddress, out var address) ||
                    address.AddressFamily != AddressFamily.InterNetwork)
                    throw new ArgumentException("自定义 IP 流需要有效的 IPv4 地址。", nameof(layout));
            }
            else if (!string.IsNullOrWhiteSpace(mapping.Input.IpAddress))
            {
                throw new ArgumentException("仅自定义 IP 流可以设置 IP 地址。", nameof(layout));
            }
        }

        if (regions.Contains(WallRegion.Fullscreen) && regions.Count != 1)
            throw new ArgumentException("全屏映射不能与左右分区同时使用。", nameof(layout));
        return layout with { Name = name };
    }

    public static BigScreenMode? CapturedPreset(WallLayout layout)
    {
        var normalized = Validate(layout);
        if (normalized.Mappings.Count == 1 &&
            normalized.Mappings[0] is { Region: WallRegion.Fullscreen, Input.Kind: WallInputKind.Window1 })
            return BigScreenMode.Single;
        if (normalized.Mappings.Count != 2) return null;
        return normalized.Mappings.Any(mapping => mapping is { Region: WallRegion.Left, Input.Kind: WallInputKind.Window1 }) &&
               normalized.Mappings.Any(mapping => mapping is { Region: WallRegion.Right, Input.Kind: WallInputKind.Window2 })
            ? BigScreenMode.Double
            : null;
    }
}
