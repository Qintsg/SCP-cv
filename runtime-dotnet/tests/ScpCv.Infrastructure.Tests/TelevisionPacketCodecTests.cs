// 电视矩阵黄金帧来自原6742B0/65C7C0纯CPU差分；不连接现场。
using System.Buffers.Binary;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Tests;

public sealed class TelevisionPacketCodecTests
{
    /// <summary>:returns: 同一D4输入的窗口别名和自定义IP产生同一软件静音策略，不因标签不同静音当前来源。</summary>
    [Fact]
    public void EquivalentInputAddressesPreserveSameWindowMutePolicy()
    {
        var layout = new WallLayout("同一输入", [new(WallRegion.Fullscreen, new(WallInputKind.IpStream, "192.168.5.63"))]);
        Assert.Equal([2], VideoWallStateProjection.MutedWindows(layout, new()));
        var configured = new VideoWallProtocolOptions();
        configured.Inputs["window_1"].UnicastAddress = "192.168.5.55";
        configured.Inputs["window_1"].MulticastAddress = "224.1.1.55";
        layout = new("显式配置", [new(WallRegion.Fullscreen, new(WallInputKind.IpStream, "192.168.5.55"))]);
        Assert.Equal([2], VideoWallStateProjection.MutedWindows(layout, configured));
    }
    /// <summary>:param suffix: 固定输入。:param expected: 独立保存的原构造黄金帧。</summary>
    [Theory]
    [InlineData(63, "FBFC610D0000000AE001013F13880F0008700234FDFE")]
    [InlineData(64, "FBFC610D0000000AE001014013880F0008700235FDFE")]
    [InlineData(81, "FBFC610D0000000AE00101511388078004380246FDFE")]
    public void SingleSourceMatchesOriginalConstructor(int suffix, string expected)
    {
        var input = SiteInputCatalog.ResolveDescriptor("ip_stream", $"192.168.5.{suffix}", new())!;
        var frames = TelevisionPacketCodec.Build(input, new());
        Assert.Equal(2, frames.Count);
        Assert.Equal("FBFC623100000001010094FDFE", Convert.ToHexString(frames[0]));
        Assert.Equal(expected, Convert.ToHexString(frames[1]));
    }

    /// <summary>:returns: 所有受信源均按输入尺寸编码；尺寸不错误进入短帧校验。</summary>
    [Fact]
    public void AllCatalogInputsHaveBoundedFramesAndSourceGeometry()
    {
        foreach (var item in SiteInputCatalog.All)
        {
            var packet = TelevisionPacketCodec.Build(item.Descriptor(), new())[1];
            Assert.Equal(22, packet.Length);
            Assert.Equal(0, packet[5]);
            Assert.Equal(item.Width, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(14, 2)));
            Assert.Equal(item.Height, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(16, 2)));
            Assert.Equal(packet[2..14].Sum(value => (int)value), BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(18, 2)));
        }
    }

    /// <summary>:returns: 未恢复的矩阵型号/IGMP分支不能套用墙面协议。</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    public void UnsupportedMatrixModesRejectBeforeSending(int sendType, bool igmp)
    {
        Assert.Throws<DisplayRoutingException>(() => TelevisionPacketCodec.Build(
            SiteInputCatalog.All[0].Descriptor(), new() { SendType = sendType, IgmpV3 = igmp }));
    }
}
