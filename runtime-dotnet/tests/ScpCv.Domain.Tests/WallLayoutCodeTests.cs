// 前后端共享短码黄金向量、矩形重叠与预留来源往返回归。
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;

namespace ScpCv.Domain.Tests;

public sealed class WallLayoutCodeTests
{
    [Theory]
    [InlineData("1IHdoxVLz", 1)]
    [InlineData("1TVQvzf6NxUXc", 2)]
    [InlineData("64GrUV8JBI1DxU", 1)]
    public void FrontendGoldenVectorsRoundTrip(string code, int count)
    {
        var layout = WallLayoutCode.Decode(code);
        Assert.Equal(count, layout.Mappings.Count);
        Assert.Equal(code, WallLayoutCode.Encode(layout));
    }

    [Fact]
    public void FiftyCellsAndReservedInputsRoundTrip()
    {
        var blocks = Enumerable.Range(0, 50).Select(i => new WallMapping(WallRegion.Grid,
            new(i % 2 == 0 ? WallInputKind.Window3 : WallInputKind.Window4), new(i % 10, i / 10, 1, 1))).ToArray();
        var code = WallLayoutCode.Encode(new("测试", blocks));
        Assert.Equal(50, WallLayoutCode.Decode(code).Mappings.Count);
        Assert.Equal(code, WallLayoutCode.Encode(new("名称无关", blocks.Reverse().ToArray())));
    }

    [Theory]
    [InlineData("")]
    [InlineData("000")]
    [InlineData("1IHdoxVLA")]
    [InlineData("1IHdoxVL!")]
    public void InvalidCodesReject(string code) => Assert.Throws<ArgumentException>(() => WallLayoutCode.Decode(code));

    [Fact]
    public void OverlappingCellsReject() => Assert.Throws<ArgumentException>(() => WallLayoutPolicy.Validate(new("重叠",
        [new(WallRegion.Grid, new(WallInputKind.Window1), new(0, 0, 2, 2)), new(WallRegion.Grid, new(WallInputKind.Window2), new(1, 1, 2, 2))])));
}
