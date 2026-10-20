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
        if (layout.Mappings.Count is < 1 or > 50)
            throw new ArgumentException("大屏布局必须包含1到50项映射。", nameof(layout));
        if (layout.Mappings.Any(mapping => mapping is null))
            throw new ArgumentException("大屏映射不能为空。", nameof(layout));
        if (layout.Mappings.Any(mapping => mapping.Region == WallRegion.Grid) &&
            layout.Mappings.Any(mapping => mapping.Region != WallRegion.Grid))
            throw new ArgumentException("网格与兼容区域不能混合，请统一为网格坐标。", nameof(layout));

        var occupied = new HashSet<int>();
        foreach (var mapping in layout.Mappings)
        {
            if (mapping.Input is null)
                throw new ArgumentException("大屏输入不能为空。", nameof(layout));
            if (!Enum.IsDefined(mapping.Region) || (mapping.Region != WallRegion.Grid && mapping.Rect is not null))
                throw new ArgumentException("大屏目标区域无效。", nameof(layout));
            var rect = Rectangle(mapping);
            if (rect.Column < 0 || rect.Row < 0 || rect.Columns < 1 || rect.Rows < 1 ||
                rect.Column > 9 || rect.Row > 4 || rect.Columns > 10 - rect.Column || rect.Rows > 5 - rect.Row)
                throw new ArgumentException("矩形必须位于10列5行网格内。", nameof(layout));
            for (var row = rect.Row; row < rect.Row + rect.Rows; row++)
                for (var column = rect.Column; column < rect.Column + rect.Columns; column++)
                    if (!occupied.Add(row * 10 + column))
                        throw new ArgumentException("大屏显示块不能重叠。", nameof(layout));
            if (!Enum.IsDefined(mapping.Input.Kind))
                throw new ArgumentException("大屏输入类型无效。", nameof(layout));
            if (mapping.Input.Kind == WallInputKind.IpStream)
            {
                if (!IPAddress.TryParse(mapping.Input.IpAddress, out var address) ||
                    address.AddressFamily != AddressFamily.InterNetwork || address.ToString() != mapping.Input.IpAddress)
                    throw new ArgumentException("自定义 IP 流需要有效的 IPv4 地址。", nameof(layout));
            }
            else if (!string.IsNullOrWhiteSpace(mapping.Input.IpAddress))
            {
                throw new ArgumentException("仅自定义 IP 流可以设置 IP 地址。", nameof(layout));
            }
        }

        return layout with { Name = name };
    }

    /// <summary>将兼容区域转换为网格矩形，不产生设备动作。</summary>
    public static WallRect Rectangle(WallMapping mapping) => mapping.Region switch
    {
        WallRegion.Fullscreen => new(0, 0, 10, 5),
        WallRegion.Left => new(0, 0, 5, 5),
        WallRegion.Right => new(5, 0, 5, 5),
        WallRegion.Grid when mapping.Rect is not null => mapping.Rect,
        _ => throw new ArgumentException("布局缺少有效矩形坐标。", nameof(mapping)),
    };

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
