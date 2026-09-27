// 仅允许两块大屏显示器作为播放目标的配置回归。
using ScpCv.Infrastructure.Playback;

namespace ScpCv.Infrastructure.Tests;

public sealed class BigScreenOutputOptionsTests
{
    [Fact]
    public void HardwareBindingRejectsMissingOrDuplicateDisplays()
    {
        var missing = new BigScreenOutputOptions { HardwareBindingRequired = true };
        Assert.Throws<InvalidOperationException>(missing.ValidateHardware);

        var duplicate = new BigScreenOutputOptions
        {
            HardwareBindingRequired = true,
            Window1 = @"\\.\DISPLAY2",
            Window2 = @"\\.\DISPLAY2",
        };
        Assert.Throws<InvalidOperationException>(duplicate.ValidateHardware);
    }

    [Fact]
    public void HardwareBindingMatchesOnlyAssignedBigScreenDisplay()
    {
        var options = new BigScreenOutputOptions
        {
            HardwareBindingRequired = true,
            Window1 = @"\\.\DISPLAY2",
            Window2 = @"\\.\DISPLAY3",
        };
        options.ValidateHardware();

        Assert.True(options.IsAllowed(1, @"\\.\DISPLAY2"));
        Assert.True(options.IsAllowed(2, @"\\.\DISPLAY3"));
        Assert.False(options.IsAllowed(1, @"\\.\DISPLAY3"));
        Assert.False(options.IsAllowed(2, @"\\.\DISPLAY4"));
        Assert.Equal("big_left", options.RoleFor(@"\\.\DISPLAY2"));
        Assert.Equal("big_right", options.RoleFor(@"\\.\DISPLAY3"));
        Assert.Equal(string.Empty, options.RoleFor(@"\\.\DISPLAY4"));
    }
}
