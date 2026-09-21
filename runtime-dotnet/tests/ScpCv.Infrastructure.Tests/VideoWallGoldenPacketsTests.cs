using System.Text.Json;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

/// <summary>
/// 逐包校对视频墙下发序列与旧 Python 实现生成的黄金样本。
/// 样本由 <c>tools/generate_video_wall_golden.py</c> 调用 <c>scp_cv/services/video_wall.py</c>
/// 的实际结果导出，不是从本实现转抄：<see cref="VideoWallSequenceTests"/> 只断言了单屏模式第一个
/// 映射包的字节，双屏模式 50 个映射包一个字节都没有断言，逐节点的裁切区、组播地址、窗口号或
/// 校验和一旦走样都发现不了。
/// </summary>
public sealed class VideoWallGoldenPacketsTests
{
    private const string GoldenFileName = "video-wall-packets.json";
    private const int ExpectedPacketsPerSwitch = 200;
    private static readonly string[] ExpectedRuntimeModes = ["double", "single"];

    [Theory]
    [InlineData("single")]
    [InlineData("double")]
    public void WholeSequenceMatchesLegacyGoldenPackets(string bigScreenMode)
    {
        var expected = ReadGoldenPackets(bigScreenMode);
        var actual = Describe(VideoWallSequenceBuilder.Build(bigScreenMode));

        Assert.Equal(ExpectedPacketsPerSwitch, expected.Count);
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.True(
                expected[index] == actual[index],
                $"第 {index} 个控制包与旧实现不一致：期望 {expected[index]}，实际 {actual[index]}");
        }
    }

    [Fact]
    public void GoldenFixtureOnlyCoversTheTwoRuntimeModes()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GoldenPath()));

        var modes = document.RootElement.GetProperty("modes").EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal);

        // 模式名与运行态取值一一对应；样本里多出或少了模式，说明生成脚本与运行态已脱节。
        Assert.Equal(ExpectedRuntimeModes, modes);
    }

    private static List<string> ReadGoldenPackets(string bigScreenMode)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GoldenPath()));
        var mode = document.RootElement.GetProperty("modes").GetProperty(bigScreenMode);

        return
        [
            .. mode.EnumerateArray().Select(item =>
                $"{item.GetProperty("phase").GetString()} " +
                $"{item.GetProperty("ip").GetString()}:{item.GetProperty("port").GetInt32()} " +
                $"{item.GetProperty("packet").GetString()}"),
        ];
    }

    private static List<string> Describe(IReadOnlyList<VideoWallSendItem> sequence) =>
    [
        .. sequence.Select(item =>
            $"{item.Phase} {item.Ip}:{item.Port} {Convert.ToHexString(item.Packet).ToLowerInvariant()}"),
    ];

    private static string GoldenPath() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", GoldenFileName);
}
