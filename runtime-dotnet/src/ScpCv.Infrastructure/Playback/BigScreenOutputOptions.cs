// D4 大屏两个播放窗口的物理显示器绑定。
namespace ScpCv.Infrastructure.Playback;

/// <summary>Hardware 模式只允许窗口 1/2 落在显式配置的两块大屏。</summary>
public sealed class BigScreenOutputOptions
{
    public const string SectionName = "BigScreenOutputs";

    public string Window1 { get; set; } = string.Empty;

    public string Window2 { get; set; } = string.Empty;

    public bool HardwareBindingRequired { get; set; }

    public void ValidateHardware()
    {
        if (!HardwareBindingRequired) return;
        if (string.IsNullOrWhiteSpace(Window1) || string.IsNullOrWhiteSpace(Window2))
            throw new InvalidOperationException("Hardware 模式必须配置两块大屏的显示器设备名。");
        if (Window1.Contains('"', StringComparison.Ordinal) || Window2.Contains('"', StringComparison.Ordinal))
            throw new InvalidOperationException("显示器设备名不能包含引号。");
        if (string.Equals(Window1.Trim(), Window2.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("窗口 1 和窗口 2 不能绑定同一块显示器。");
    }

    public bool IsAllowed(int windowId, string displayName)
    {
        if (windowId is < 1 or > 2 || string.IsNullOrWhiteSpace(displayName)) return false;
        if (!HardwareBindingRequired) return true;
        var expected = windowId == 1 ? Window1 : Window2;
        return !string.IsNullOrWhiteSpace(expected) &&
               string.Equals(expected.Trim(), displayName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public string RoleFor(string displayName)
    {
        if (string.Equals(Window1.Trim(), displayName, StringComparison.OrdinalIgnoreCase)) return "big_left";
        if (string.Equals(Window2.Trim(), displayName, StringComparison.OrdinalIgnoreCase)) return "big_right";
        return string.Empty;
    }

    public string ForWindow(int windowId) => windowId switch
    {
        1 => Window1.Trim(),
        2 => Window2.Trim(),
        _ => throw new ArgumentOutOfRangeException(nameof(windowId), windowId, "只支持大屏窗口 1 和 2。"),
    };
}
