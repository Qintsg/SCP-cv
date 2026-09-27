// 大屏映射的输入来源、目标区域与布局意图。
namespace ScpCv.Domain.Model;

public enum WallRegion
{
    Fullscreen,
    Left,
    Right,
}

public enum WallInputKind
{
    Window1,
    Window2,
    Laptop,
    IpStream,
}

public sealed record WallInput(WallInputKind Kind, string IpAddress = "");

public sealed record WallMapping(WallRegion Region, WallInput Input);

public sealed record WallLayout(string Name, IReadOnlyList<WallMapping> Mappings);
