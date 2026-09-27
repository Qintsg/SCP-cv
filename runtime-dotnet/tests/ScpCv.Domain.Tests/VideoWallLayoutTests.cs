// 大屏手动布局的区域冲突、输入校验与固定预设判定。
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;

namespace ScpCv.Domain.Tests;

public sealed class VideoWallLayoutTests
{
    [Fact]
    public void RecognizesOnlyTheTwoCapturedPhysicalPresets()
    {
        var fullscreen = WallLayoutPolicy.Validate(new WallLayout("窗口 1 全屏",
        [
            new WallMapping(WallRegion.Fullscreen, new WallInput(WallInputKind.Window1)),
        ]));
        var split = WallLayoutPolicy.Validate(new WallLayout("双窗",
        [
            new WallMapping(WallRegion.Left, new WallInput(WallInputKind.Window1)),
            new WallMapping(WallRegion.Right, new WallInput(WallInputKind.Window2)),
        ]));

        Assert.Equal(BigScreenMode.Single, WallLayoutPolicy.CapturedPreset(fullscreen));
        Assert.Equal(BigScreenMode.Double, WallLayoutPolicy.CapturedPreset(split));
    }

    [Fact]
    public void LaptopAndIpDraftsAreValidButHaveNoCapturedControlFrames()
    {
        var draft = WallLayoutPolicy.Validate(new WallLayout("笔记本左／直播右",
        [
            new WallMapping(WallRegion.Left, new WallInput(WallInputKind.Laptop)),
            new WallMapping(WallRegion.Right, new WallInput(WallInputKind.IpStream, "239.1.2.3")),
        ]));

        Assert.Null(WallLayoutPolicy.CapturedPreset(draft));
    }

    [Fact]
    public void FullscreenCannotBeCombinedWithSplitRegions()
    {
        var layout = new WallLayout("冲突",
        [
            new WallMapping(WallRegion.Fullscreen, new WallInput(WallInputKind.Window1)),
            new WallMapping(WallRegion.Left, new WallInput(WallInputKind.Window2)),
        ]);

        Assert.Throws<ArgumentException>(() => WallLayoutPolicy.Validate(layout));
    }

    [Fact]
    public void DuplicateRegionAndInvalidIpAreRejected()
    {
        Assert.Throws<ArgumentException>(() => WallLayoutPolicy.Validate(new WallLayout("重复",
        [
            new WallMapping(WallRegion.Left, new WallInput(WallInputKind.Window1)),
            new WallMapping(WallRegion.Left, new WallInput(WallInputKind.Window2)),
        ])));
        Assert.Throws<ArgumentException>(() => WallLayoutPolicy.Validate(new WallLayout("非法 IP",
        [
            new WallMapping(WallRegion.Right, new WallInput(WallInputKind.IpStream, "not-an-ip")),
        ])));
    }
}
