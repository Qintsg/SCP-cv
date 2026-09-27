// 播放窗口启动时必须按设备名落在明确绑定的大屏。
using ScpCv.PlayerWorker;

namespace ScpCv.Windows.Tests;

public sealed class PlayerDisplaySelectionTests
{
    [Fact]
    public void SelectsNamedBigScreenInsteadOfFirstEnumeratedMonitor()
    {
        PlayerDisplay[] screens =
        [
            new(@"\\.\DISPLAY1", 0, 0, 1920, 1200),
            new(@"\\.\DISPLAY2", 1920, 0, 1920, 1080),
            new(@"\\.\DISPLAY3", 3840, 0, 1920, 1080),
        ];

        Assert.Equal(@"\\.\DISPLAY3", PlayerDisplaySelection.Select(@"\\.\DISPLAY3", screens).Name);
        Assert.Throws<InvalidOperationException>(() => PlayerDisplaySelection.Select(@"\\.\DISPLAY4", screens));
        Assert.Throws<InvalidOperationException>(() => PlayerDisplaySelection.Select(string.Empty, screens));
    }
}
