// 触控工程独立黄金字节：SQL加载Lock=19为模拟量最大值，非法值不能被截断接受。
using ScpCv.Infrastructure.Devices;

namespace ScpCv.Infrastructure.Tests;

public sealed class VenueJoinProtocolTests
{
    [Theory]
    [InlineData(1, true, "FF06010A00010001FA")]
    [InlineData(31, true, "FF06010A001F0001FA")]
    [InlineData(31, false, "FF06010A001F0000FA")]
    [InlineData(50, true, "FF06010A00320001FA")]
    public void DigitalMatchesOriginalButtonEncoding(int join, bool pressed, string expected) =>
        Assert.Equal(expected, VenueJoinProtocol.Digital(join, pressed));

    [Theory]
    [InlineData(0, "FF06020A00020000FA")]
    [InlineData(1, "FF06020A00020000FA")]
    [InlineData(50, "FF06020A00020009FA")]
    [InlineData(100, "FF06020A00020013FA")]
    public void AnalogUsesActualSiteSqlRange19(int percent, string expected) =>
        Assert.Equal(expected, VenueJoinProtocol.Analog(2, percent));

    [Fact]
    public void InvalidInputsAreRejectedBeforeConstructingFrame()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VenueJoinProtocol.Analog(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => VenueJoinProtocol.Analog(2, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => VenueJoinProtocol.Digital(0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => VenueJoinProtocol.Digital(65536, true));
    }
}
