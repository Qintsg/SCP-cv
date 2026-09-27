// Supervisor 枚举交互桌面的显示器与大屏输出。
using System.Windows.Forms;

namespace ScpCv.Supervisor.Windows;

public sealed record DisplayTopologyItem(string DevicePath, int X, int Y, int Width, int Height, bool Primary, int Dpi);

public sealed class DisplayTopologyService
{
    public static IReadOnlyList<DisplayTopologyItem> Enumerate() => Screen.AllScreens
        .Select(screen => new DisplayTopologyItem(
            screen.DeviceName,
            screen.Bounds.X,
            screen.Bounds.Y,
            screen.Bounds.Width,
            screen.Bounds.Height,
            screen.Primary,
            96))
        .OrderByDescending(display => display.Primary)
        .ThenBy(display => display.X)
        .ThenBy(display => display.Y)
        .ToArray();

}
