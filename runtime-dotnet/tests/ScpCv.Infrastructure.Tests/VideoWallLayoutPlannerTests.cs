// 三种输入的完整/单侧组合与旧预设兼容，不访问现场节点。
using System.Buffers.Binary;
using System.Net;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

public sealed class VideoWallLayoutPlannerTests
{
    [Fact]
    public void CurrentIp81DefaultMatchesLive1080SpsButExplicit4kStaysIndependent()
    {
        var options = new VideoWallProtocolOptions();
        Assert.Equal(1920, options.Inputs["laptop"].Width);
        Assert.Equal(1080, options.Inputs["laptop"].Height);
        options.Inputs["laptop"].Width = 3840; options.Inputs["laptop"].Height = 2160;
        options.Inputs["laptop"].Validate();
        Assert.Equal(3840, options.Inputs["laptop"].Width);
        Assert.Equal(1920, options.Inputs["window_1"].Width);
    }

    public static IEnumerable<object[]> Combinations()
    {
        var kinds = new[] { WallInputKind.Window1, WallInputKind.Window2, WallInputKind.Laptop };
        foreach (var kind in kinds)
        {
            yield return [kind, null!];
            foreach (var right in kinds) yield return [kind, right];
        }
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void TwelveCombinationsHaveIndependentWindowsAndCorrectRowMajorFragments(
        WallInputKind left, WallInputKind? right)
    {
        var layout = new WallLayout("组合", right is null
            ? [new(WallRegion.Fullscreen, new(left))]
            : [new(WallRegion.Left, new(left)), new(WallRegion.Right, new(right.Value))]);
        var plan = VideoWallLayoutPlanner.Build(layout, new() { PreserveLegacyPresets = false });
        Assert.Equal(200, plan.Count);
        var mappings = plan.Where(item => item.Phase.StartsWith("mapping_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(50, mappings.Length);
        foreach (var item in mappings)
        {
            var packet = item.Packet;
            var screen = U16(packet, 38) - 1;
            var row = screen / 10;
            var col = screen % 10;
            var source = right is null || col < 5 ? left : right.Value;
            var sourceNumber = source switch { WallInputKind.Window1 => 63, WallInputKind.Window2 => 64, _ => 81 };
            Assert.Equal(IPAddress.Parse($"224.1.1.{sourceNumber}").GetAddressBytes(), packet[10..14]);
            Assert.Equal(VideoWallSequenceBuilder.TargetIp(col, row), item.Ip);
            Assert.Equal(right is null || col < 5 ? 1 : 2, U16(packet, 8));
            Assert.Equal(U16(packet, 8), U16(packet, 24));
            Assert.Equal(right is null ? 1 : 2, U16(packet, 34));
            Assert.Equal(right is null ? row * 10 + col + 1 : row * 5 + col % 5 + 1, U16(packet, 36));
            // 独立于产品默认值：63/64为用户/实测确认，81由本轮实际SPS确认1080P。
            var sourceWidth = 1920;
            var sourceHeight = 1080;
            var width = sourceWidth / (right is null ? 10 : 5);
            var height = sourceHeight / 5;
            Assert.Equal((right is null ? col : col % 5) * width, U16(packet, 26));
            Assert.Equal(row * height, U16(packet, 28));
            Assert.Equal(width, U16(packet, 30));
            Assert.Equal(height, U16(packet, 32));
            Assert.InRange(U16(packet, 26) + width, 1, sourceWidth);
            Assert.InRange(U16(packet, 28) + height, 1, sourceHeight);
        }
    }

    [Theory]
    [InlineData(WallRegion.Left, WallInputKind.Window1)]
    [InlineData(WallRegion.Left, WallInputKind.Window2)]
    [InlineData(WallRegion.Left, WallInputKind.Laptop)]
    [InlineData(WallRegion.Right, WallInputKind.Window1)]
    [InlineData(WallRegion.Right, WallInputKind.Window2)]
    [InlineData(WallRegion.Right, WallInputKind.Laptop)]
    public void SingleSideStillClearsCommitsAndRefreshesAllFiftyNodes(WallRegion region, WallInputKind kind)
    {
        var plan = VideoWallLayoutPlanner.Build(new("单侧", [new(region, new(kind))]), new());
        Assert.Equal(175, plan.Count);
        Assert.Equal(50, plan.Count(item => item.Phase == "clear"));
        Assert.Equal(25, plan.Count(item => item.Phase.StartsWith("mapping_", StringComparison.Ordinal)));
        Assert.Equal(50, plan.Count(item => item.Phase == "commit"));
        Assert.Equal(50, plan.Count(item => item.Phase == "refresh"));
    }

    [Theory]
    [InlineData("single")]
    [InlineData("double")]
    public void ExplicitHistoricalD2ProfileKeepsEveryGoldenPacket(string mode)
    {
        var layout = VideoWallLayoutPlanner.Preset(mode);
        var historical = new VideoWallProtocolOptions();
        historical.Inputs["window_1"].UnicastAddress = "192.168.5.55";
        historical.Inputs["window_1"].MulticastAddress = "224.1.1.55";
        historical.Inputs["window_2"].UnicastAddress = "192.168.5.56";
        historical.Inputs["window_2"].MulticastAddress = "224.1.1.56";
        foreach (var key in new[] { "window_1", "window_2" })
        {
            historical.Inputs[key].Width = 3840;
            historical.Inputs[key].Height = 2160;
        }
        var actual = VideoWallLayoutPlanner.Build(layout, historical);
        var expected = VideoWallSequenceBuilder.Build(mode);
        Assert.Equal(expected.Select(item => Convert.ToHexString(item.Packet)),
            actual.Select(item => Convert.ToHexString(item.Packet)));
        Assert.Equal(expected.Select(item => item.Ip), actual.Select(item => item.Ip));
    }

    [Theory]
    [InlineData("single")]
    [InlineData("double")]
    public void DefaultPresetsUseD4InputsEvenWithLegacyCompatibilityEnabled(string mode)
    {
        var options = new VideoWallProtocolOptions();
        Assert.True(options.PreserveLegacyPresets);
        Assert.Equal("192.168.5.63", options.Inputs["window_1"].UnicastAddress);
        Assert.Equal("192.168.5.64", options.Inputs["window_2"].UnicastAddress);
        var plan = VideoWallLayoutPlanner.Build(VideoWallLayoutPlanner.Preset(mode), options);
        var mappings = plan.Where(item => item.Phase.StartsWith("mapping_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(50, mappings.Length);
        foreach (var item in mappings)
        {
            var number = mode == "double" && item.Phase == "mapping_2" ? 64 : 63;
            Assert.Equal(IPAddress.Parse($"224.1.1.{number}").GetAddressBytes(), item.Packet[10..14]);
        }
    }

    [Fact]
    public void InvalidConfigurationOrUndescribedIpCannotProduceAPlan()
    {
        var layout = new WallLayout("未登记", [new(WallRegion.Fullscreen, new(WallInputKind.IpStream, "239.1.2.3"))]);
        Assert.Throws<VideoWallException>(() => VideoWallLayoutPlanner.Build(layout, new()));
        Assert.Throws<VideoWallException>(() => VideoWallLayoutPlanner.Build(VideoWallLayoutPlanner.Preset("single"), new() { SendType = 4 }));
        Assert.Throws<VideoWallException>(() => VideoWallLayoutPlanner.Build(VideoWallLayoutPlanner.Preset("single"), new() { UseNewProtocol = true }));
    }

    private static int U16(byte[] packet, int offset) => BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(offset, 2));
}
