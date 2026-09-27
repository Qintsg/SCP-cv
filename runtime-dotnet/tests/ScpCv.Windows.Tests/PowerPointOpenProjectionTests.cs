// PowerPoint 打开结果的页码必须与实际放映的 1-based 页码一致。
using ScpCv.PowerPointHost.Interop;

namespace ScpCv.Windows.Tests;

public sealed class PowerPointOpenProjectionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(12, 9)]
    public void OpenProjectsValidOneBasedSlide(int observed, int expected)
    {
        var opened = new PowerPointOpenResult(
            true, "ok", 1, 1, 9, CurrentSlide: observed);

        Assert.Equal(expected, opened.ProjectedCurrentSlide);
    }
}
