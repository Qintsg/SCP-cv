// 按显式设备名选择大屏，不依赖 Windows 的显示器枚举顺序。
namespace ScpCv.PlayerWorker;

public sealed record PlayerDisplay(string Name, int X, int Y, int Width, int Height);

public static class PlayerDisplaySelection
{
    public static PlayerDisplay Select(string displayName, IEnumerable<PlayerDisplay> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("缺少大屏显示器设备名，拒绝创建播放器窗口。");

        return displays.FirstOrDefault(display =>
                   string.Equals(display.Name, displayName, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"指定的大屏显示器不存在：{displayName}。");
    }
}
