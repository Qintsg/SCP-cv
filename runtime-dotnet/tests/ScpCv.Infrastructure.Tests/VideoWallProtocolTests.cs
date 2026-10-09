// 原逆向向量、独立校验及设备回读流解析；所有网络测试仅回环。
using System.Buffers.Binary;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

public sealed class VideoWallProtocolTests
{
    [Theory]
    [InlineData(1, false, false, "FBFC6210000000210007E0010137138800000000078004380003000000000F00087000010001000101039EFDFE")]
    [InlineData(1, true, false, "FBFC6102000000210007E0010137138800000000078004380003000000000F00087000010001000101038FFDFE")]
    public void MappingMatchesOriginalBusinessVectors(int sendType, bool cutSync, bool igmp, string expected)
    {
        var profile = new VideoWallProtocolOptions { SendType = sendType, CutSync = cutSync, IgmpV3 = igmp };
        var source = profile.Inputs["window_1"];
        var packet = VideoWallPacketCodec.Mapping(source, new(0, 0, 1920, 1080), new(0, 0, 3840, 2160),
            7, 3, 1, 1, 1, profile).Single();
        Assert.Equal(expected, Convert.ToHexString(packet));
    }

    [Theory]
    [InlineData(1, true, 59993, "FBFC6120000000300007C00002640000E0010137138800000000078004380003000000000F0008700001000100010101000000000000000004DFFDFE")]
    [InlineData(2, false, 59993, "FBFC6160000000210007C0000264EA5900000000078004380003000000000F0008700001000100010104A2FDFE")]
    [InlineData(2, false, 60000, "FBFC6160000000210007C0000264EA6000000000078004380003000000000F0008700001000100010104A9FDFE")]
    [InlineData(2, true, 60000, "FBFC6120000000300007C0000264EA60E0010137138800000000078004380003000000000F000870000100010001010200000000000000000629FDFE")]
    public void ExtendedAndUnicastFramesMatchOriginalVectors(int sendType, bool igmp, int port, string expected)
    {
        // 对照layout-branch-verification.json的原业务合成节点，不是实际现场输入地址。
        var profile = new VideoWallProtocolOptions { SendType = sendType, IgmpV3 = igmp };
        var source = profile.Inputs["window_1"];
        source.UnicastAddress = "192.0.2.100";
        source.UnicastPort = port;
        var packet = VideoWallPacketCodec.Mapping(source, new(0, 0, 1920, 1080), new(0, 0, 3840, 2160),
            7, 3, 1, 1, 1, profile).Single();
        Assert.Equal(expected, Convert.ToHexString(packet));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void SelectedFormatHasCorrectAddressesChecksumAndOrder(int sendType, bool igmp)
    {
        var profile = new VideoWallProtocolOptions { SendType = sendType, IgmpV3 = igmp };
        var frames = VideoWallPacketCodec.Mapping(profile.Inputs["laptop"], new(0, 0, 1920, 1080),
            new(0, 0, 384, 432), 1, 2, 2, 3, 4, profile);
        Assert.Equal(sendType == 3 ? 2 : 1, frames.Count);
        foreach (var frame in frames)
        {
            var extended = frame.Length == 60;
            var checksumOffset = extended ? 56 : 41;
            Assert.Equal(frame[2..40].Sum(value => (int)value),
                BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(checksumOffset, 2)));
            Assert.Equal(extended ? 48 : 33, BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(6, 2)));
        }
        if (sendType == 3)
        {
            Assert.Equal(0x6102, BinaryPrimitives.ReadUInt16BigEndian(frames[0].AsSpan(2, 2)));
            Assert.Equal(0x6160, BinaryPrimitives.ReadUInt16BigEndian(frames[1].AsSpan(2, 2)));
        }
        if (igmp && sendType != 3)
        {
            Assert.Equal(new byte[] { 192, 168, 5, 81 }, frames[0][10..14]);
            Assert.Equal(new byte[] { 224, 1, 1, 81 }, frames[0][16..20]);
            Assert.Equal(sendType == 1 ? 0 : 59993, BinaryPrimitives.ReadUInt16BigEndian(frames[0].AsSpan(14, 2)));
        }
    }

    [Fact]
    public void EncryptionUsesZeroPaddingAndDoesNotAddAnExtraBlockToAlignedData()
    {
        // 合成公开测试密钥，不是现场密钥。
        var options = new VideoWallProtocolOptions { UseNewProtocol = true, DesKeyHex = "133457799BBCDFF1" };
        var plain = Convert.FromHexString("0123456789ABCDEF");
        var wrapped = VideoWallPacketCodec.Wrap(plain, options);
        Assert.Equal("F1F2000885E813540F0AB405F3F4", Convert.ToHexString(wrapped));
        var shortFrame = VideoWallPacketCodec.Control(0x61FF);
        Assert.Equal(22, VideoWallPacketCodec.Wrap(shortFrame, options).Length);
        Assert.Equal(shortFrame, VideoWallPacketCodec.Unwrap(VideoWallPacketCodec.Wrap(shortFrame, options), options)[..12]);
    }

    [Fact]
    public void WindowReadbackHandlesSplitCoalescedAndWrongLengthVariants()
    {
        var frame = Convert.FromHexString("FCFD6102000000160001E0010151138800000000078004380002000100050000FEFF");
        var decoder = new VideoWallReplyDecoder("192.168.5.101", new());
        Assert.Empty(decoder.Feed(frame[..8]));
        var replies = decoder.Feed([.. frame[8..], .. frame]);
        Assert.Equal(2, replies.Count);
        Assert.All(replies, reply =>
        {
            Assert.Equal("192.168.5.101", reply.NodeIp);
            Assert.Equal("224.1.1.81", reply.StreamIp);
            Assert.Equal(1920, reply.Width);
            Assert.Equal(5, reply.ScreenId);
        });
        Assert.Empty(decoder.Feed(Convert.FromHexString("FCFD61020000000AE0010137C0A8053713880000FEFF")));
        Assert.Empty(decoder.Feed([0xFC, 0xFD, 0x61, 0x02, 0, 0, 0xFF, 0xFF]));
        Assert.Single(decoder.Feed(frame));
    }
}
