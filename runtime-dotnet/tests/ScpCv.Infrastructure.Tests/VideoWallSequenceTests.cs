using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

/// <summary>校对视频墙序列与 <c>scp_cv/services/video_wall.py</c> 的既有协议事实。</summary>
public sealed class VideoWallSequenceTests
{
    [Fact]
    public void TargetIpKeepsTheFieldPatchBetweenScreens45And46()
    {
        Assert.Equal("192.168.5.101", VideoWallSequenceBuilder.TargetIp(0, 0));
        Assert.Equal("192.168.5.147", VideoWallSequenceBuilder.TargetIp(9, 0));
        Assert.Equal("192.168.5.146", VideoWallSequenceBuilder.TargetIp(9, 1));
        Assert.Equal("192.168.5.150", VideoWallSequenceBuilder.TargetIp(9, 4));
        Assert.Equal(50, VideoWallSequenceBuilder.AllTargetIps().Count);
    }

    [Fact]
    public void SequenceKeepsClearMappingCommitRefreshOrderAndCounts()
    {
        var fullscreen = VideoWallSequenceBuilder.Build("single");
        Assert.Equal(200, fullscreen.Count);
        Assert.Equal(50, fullscreen.Count(item => item.Phase == "clear"));
        Assert.Equal(50, fullscreen.Count(item => item.Phase == "mapping_ws21_fullscreen"));
        Assert.Equal(50, fullscreen.Count(item => item.Phase == "commit"));
        Assert.Equal(50, fullscreen.Count(item => item.Phase == "refresh"));
        Assert.All(fullscreen, item => Assert.Equal(4830, item.Port));

        Assert.Equal(
            fullscreen.Select(item => (item.Phase, item.Ip)).ToArray(),
            VideoWallSequenceBuilder.BuildFor(VideoWallLayoutMode.FullscreenWs21)
                .Select(item => (item.Phase, item.Ip))
                .ToArray());
    }

    [Fact]
    public void SplitModeUsesWs21OnLeftHalfAndWs22OnRightHalf()
    {
        var split = VideoWallSequenceBuilder.Build("double");
        Assert.Equal(200, split.Count);
        Assert.Equal(25, split.Count(item => item.Phase == "mapping_ws21_left_half"));
        Assert.Equal(25, split.Count(item => item.Phase == "mapping_ws22_right_half"));
    }

    [Fact]
    public void FixedPacketsMatchLegacyBytesAndMappingPacketIs45Bytes()
    {
        var sequence = VideoWallSequenceBuilder.Build("single");
        Assert.Equal(Convert.FromHexString("FBFC61FF000000000160FDFE"), sequence.First(item => item.Phase == "clear").Packet);
        Assert.Equal(Convert.FromHexString("FBFC61B8000000000119FDFE"), sequence.First(item => item.Phase == "commit").Packet);
        Assert.Equal(Convert.FromHexString("FBFC61B900000000011AFDFE"), sequence.First(item => item.Phase == "refresh").Packet);

        var mapping = sequence.First(item => item.Phase == "mapping_ws21_fullscreen").Packet;
        Assert.Equal(45, mapping.Length);
        Assert.Equal([0xFB, 0xFC, 0x61, 0x02], mapping[..4]);
        Assert.Equal([0xFD, 0xFE], mapping[^2..]);
        var expectedChecksum = (mapping[2..^4].Sum(value => (int)value) - 1) & 0xFFFF;
        var actualChecksum = (mapping[^4] << 8) | mapping[^3];
        Assert.Equal(expectedChecksum, actualChecksum);
    }
}
