// 自定义矩形包级回归：4K源、源间独立分片、空白清理与未知规则拒绝。
using System.Buffers.Binary;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

public sealed class VideoWallGridPlannerTests
{
    [Fact]
    public void SeparateRectanglesOfSameInputUseIndependentSignsAndFragments()
    {
        var plan = VideoWallLayoutPlanner.Build(new("测试",
            [new(WallRegion.Grid, new(WallInputKind.Window1), new(1, 1, 3, 3)),
             new(WallRegion.Grid, new(WallInputKind.Window1), new(6, 2, 2, 2))]), new());
        Assert.Equal(50, plan.Count(item => item.Phase == "clear"));
        Assert.Equal(50, plan.Count(item => item.Phase == "refresh"));
        var first = plan.Where(item => item.Phase == "mapping_1").ToArray();
        var second = plan.Where(item => item.Phase == "mapping_2").ToArray();
        Assert.Equal(9, first.Length); Assert.Equal(4, second.Length);
        Assert.Equal(0, Number(first[0], 26)); Assert.Equal(0, Number(first[0], 28));
        Assert.Equal(1280, Number(first[0], 30)); Assert.Equal(720, Number(first[0], 32));
        Assert.Equal(12, Number(first[0], 38)); Assert.Equal(1, Number(second[0], 36));
        Assert.Equal(1920, Number(second[0], 30)); Assert.Equal(1080, Number(second[0], 32));
        Assert.All(first, item => Assert.Equal(1, Number(item, 8)));
        Assert.All(second, item => Assert.Equal(2, Number(item, 8)));
    }

    [Theory]
    [InlineData(WallInputKind.Window1, 7, 5)]
    [InlineData(WallInputKind.Window2, 9, 5)]
    [InlineData(WallInputKind.Laptop, 5, 4)]
    public void UnverifiedRemainderCannotProducePlan(WallInputKind input, int columns, int rows) =>
        Assert.Throws<VideoWallException>(() => VideoWallLayoutPlanner.Build(new("拒发",
            [new(WallRegion.Grid, new(input), new(0, 0, columns, rows))]), new()));

    /// <summary>:param input: D4后两路受信输入。:returns: 允许矩阵映射，不新增PlayerWorker。</summary>
    [Theory]
    [InlineData(WallInputKind.Window3)]
    [InlineData(WallInputKind.Window4)]
    public void RegisteredRearOutputsCanMapWithoutAddingNativeWindows(WallInputKind input)
    {
        var plan = VideoWallLayoutPlanner.Build(new("后路", [new(WallRegion.Grid, new(input), new(0, 0, 5, 5))]), new());
        Assert.Equal(25, plan.Count(item => item.Phase == "mapping_1"));
        Assert.Equal(2, WindowId.Maximum);
    }

    private static int Number(VideoWallSendItem item, int offset) => BinaryPrimitives.ReadUInt16BigEndian(item.Packet.AsSpan(offset, 2));
}
