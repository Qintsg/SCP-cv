// 021逐源尺寸、别名一致性与裁剪/指纹回归；全部纯CPU，不连接设备。
using System.Buffers.Binary;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

public sealed class VideoWallSourceResolutionTests
{
    /// <summary>:param kind: D4输入种类。:param suffix: 对应输入节点。:param width: 显式主流宽度。</summary>
    [Theory]
    [InlineData(WallInputKind.Window1, 63, 3840)]
    [InlineData(WallInputKind.Window2, 64, 3840)]
    [InlineData(WallInputKind.Window3, 65, 3840)]
    [InlineData(WallInputKind.Window4, 66, 3840)]
    [InlineData(WallInputKind.Window1, 63, 1920)]
    [InlineData(WallInputKind.Window2, 64, 1920)]
    [InlineData(WallInputKind.Window3, 65, 1920)]
    [InlineData(WallInputKind.Window4, 66, 1920)]
    public void D4AliasAndIpCropExactlyEntireSourceAtBothSizes(WallInputKind kind, int suffix, int width)
    {
        var options = new VideoWallProtocolOptions();
        var descriptor = SiteInputCatalog.ByAddress($"192.168.5.{suffix}")!.Descriptor();
        descriptor.Width = width; descriptor.Height = width * 9 / 16;
        options.Inputs[kind switch { WallInputKind.Window1 => "window_1", WallInputKind.Window2 => "window_2",
            WallInputKind.Window3 => "window_3", _ => "window_4" }] = descriptor;
        foreach (var (region, grid, columns, rows) in new (WallRegion, WallRect?, int, int)[]
        {
            (WallRegion.Fullscreen, null, 10, 5), (WallRegion.Left, null, 5, 5),
            (WallRegion.Right, null, 5, 5), (WallRegion.Grid, new(2, 1, 3, 3), 3, 3),
        })
        {
            var layout = new WallLayout("尺寸", [new(region, new(kind), grid)]);
            var plan = VideoWallLayoutPlanner.Build(layout, options);
            var direct = VideoWallLayoutPlanner.Build(new("其它名称", [new(region,
                new(WallInputKind.IpStream, descriptor.UnicastAddress), grid)]), options);
            Assert.Equal(plan.Select(item => Convert.ToHexString(item.Packet)), direct.Select(item => Convert.ToHexString(item.Packet)));
            var maps = plan.Where(item => item.Phase.StartsWith("mapping_", StringComparison.Ordinal)).ToArray();
            Assert.Equal(columns * rows, maps.Length);
            Assert.Equal((long)width * descriptor.Height, maps.Sum(item => (long)Read(item.Packet, 30) * Read(item.Packet, 32)));
            Assert.Equal(width, maps.Max(item => Read(item.Packet, 26) + Read(item.Packet, 30)));
            Assert.Equal(descriptor.Height, maps.Max(item => Read(item.Packet, 28) + Read(item.Packet, 32)));
            Assert.All(maps, item =>
            {
                Assert.Equal(1920, Read(item.Packet, 20)); Assert.Equal(1080, Read(item.Packet, 22));
                Assert.Equal(item.Packet[2..40].Sum(value => (int)value) & 0xFFFF, Read(item.Packet, 41));
            });
        }
    }

    /// <summary>:returns: 入口冲突无法生成任何清屏或映射计划。</summary>
    [Fact]
    public void ConflictingDescriptorsForOneIpAreRejectedForBothEntrances()
    {
        var options = new VideoWallProtocolOptions();
        var conflicting = SiteInputCatalog.ByAddress("192.168.5.63")!.Descriptor();
        conflicting.Width = 1920; conflicting.Height = 1080;
        options.Inputs[conflicting.UnicastAddress] = conflicting;
        Assert.Null(SiteInputCatalog.ResolveDescriptor("window_1", null, options));
        Assert.Null(SiteInputCatalog.ResolveDescriptor("ip_stream", conflicting.UnicastAddress, options));
        Assert.Throws<VideoWallException>(() => VideoWallLayoutPlanner.Build(VideoWallLayoutPlanner.Preset("single"), options));
    }

    /// <summary>:param suffix: 实测1080输入。:returns: 电视源尺寸不继承D4。</summary>
    [Theory]
    [InlineData(59)] [InlineData(60)] [InlineData(62)] [InlineData(71)] [InlineData(72)] [InlineData(74)]
    [InlineData(76)] [InlineData(81)] [InlineData(85)] [InlineData(86)] [InlineData(87)] [InlineData(88)]
    public void Live1080InputsDoNotInheritD4FourK(int suffix)
    {
        var descriptor = SiteInputCatalog.ResolveDescriptor("ip_stream", $"192.168.5.{suffix}", new())!;
        Assert.Equal(1920, descriptor.Width); Assert.Equal(1080, descriptor.Height);
        var television = TelevisionPacketCodec.Build(descriptor, new())[1];
        Assert.Equal(1920, Read(television, 14)); Assert.Equal(1080, Read(television, 16));
    }

    /// <summary>:returns: 84未知不能由默认值生成映射或电视帧。</summary>
    [Fact]
    public void OfflineUnknownInputHasNoInventedSizeAndCannotDispatch()
    {
        var input = SiteInputCatalog.ByAddress("192.168.5.84")!;
        Assert.Equal(0, input.Width); Assert.Equal(0, input.Height);
        Assert.Throws<VideoWallException>(() => VideoWallLayoutPlanner.Build(new("未知",
            [new(WallRegion.Fullscreen, new(WallInputKind.IpStream, input.Address))]), new()));
        Assert.Throws<DisplayRoutingException>(() => TelevisionPacketCodec.Build(input.Descriptor(), new()));
    }

    /// <summary>:returns: 同IP改尺寸时墙及电视预案都必须重发。</summary>
    [Fact]
    public void DimensionChangeInvalidatesWallAndTelevisionFingerprints()
    {
        var options = new VideoWallProtocolOptions(); var layout = VideoWallLayoutPlanner.Preset("single");
        var beforeWall = DisplayRoutingState.WallFingerprint(layout, options);
        var beforeTv = DisplayRoutingState.TelevisionFingerprint("tv_left", TelevisionPacketCodec.Build(options.Inputs["window_1"], options));
        options.Inputs["window_1"].Width = 1920; options.Inputs["window_1"].Height = 1080;
        Assert.NotEqual(beforeWall, DisplayRoutingState.WallFingerprint(layout, options));
        Assert.NotEqual(beforeTv, DisplayRoutingState.TelevisionFingerprint("tv_left", TelevisionPacketCodec.Build(options.Inputs["window_1"], options)));
    }

    /// <summary>:returns: 配置顺序不决定尺寸，地址键不隐式换设备。</summary>
    [Fact]
    public void SameIpDuplicateConfigurationMustBeIdenticalAndKeyCannotRedirectAnotherAddress()
    {
        var options = new VideoWallProtocolOptions();
        options.Inputs["192.168.5.63"] = SiteInputCatalog.ByAddress("192.168.5.63")!.Descriptor();
        Assert.NotNull(SiteInputCatalog.ResolveDescriptor("window_1", null, options));
        options.Inputs["192.168.5.63"].Port = 5001;
        Assert.Null(SiteInputCatalog.ResolveDescriptor("window_1", null, options));
        options.Inputs["192.168.5.63"] = SiteInputCatalog.ByAddress("192.168.5.64")!.Descriptor();
        Assert.Null(SiteInputCatalog.ResolveDescriptor("ip_stream", "192.168.5.63", options));
    }

    /// <summary>:returns: 混合尺寸及重复来源各自覆盖完整范围并独立编号。</summary>
    [Fact]
    public void MixedResolutionAndRepeatedSourceKeepIndependentFullCropAreas()
    {
        var layout = new WallLayout("混合", [new(WallRegion.Grid, new(WallInputKind.Window1), new(0, 0, 5, 5)),
            new(WallRegion.Grid, new(WallInputKind.IpStream, "192.168.5.85"), new(5, 0, 3, 3)),
            new(WallRegion.Grid, new(WallInputKind.Window1), new(8, 0, 2, 2))]);
        var maps = VideoWallLayoutPlanner.Build(layout, new()).Where(item => item.Phase.StartsWith("mapping_", StringComparison.Ordinal));
        foreach (var group in maps.GroupBy(item => item.Phase))
        {
            var width = group.Key == "mapping_2" ? 1920 : 3840;
            Assert.Equal((long)width * width * 9 / 16, group.Sum(item => (long)Read(item.Packet, 30) * Read(item.Packet, 32)));
            Assert.Equal(0, Read(group.First().Packet, 26));
            Assert.Equal(0, Read(group.First().Packet, 28));
            Assert.Equal(1, Read(group.First().Packet, 36));
        }
    }

    /// <summary>:returns: 确认参数后可显式覆盖未知，而非永久屏蔽84。</summary>
    [Fact]
    public void ExplicitConfigurationCanSupplyPreviouslyUnknownInputWithoutGuessing()
    {
        var options = new VideoWallProtocolOptions();
        var descriptor = SiteInputCatalog.ByAddress("192.168.5.84")!.Descriptor();
        descriptor.Width = 1920; descriptor.Height = 1080; options.Inputs[descriptor.UnicastAddress] = descriptor;
        Assert.Equal(200, VideoWallLayoutPlanner.Build(new("明确配置",
            [new(WallRegion.Fullscreen, new(WallInputKind.IpStream, descriptor.UnicastAddress))]), options).Count);
    }

    /// <summary>:param bytes: 未加密包。:param offset: 协议偏移。:returns: 大端字段。</summary>
    private static int Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
}
