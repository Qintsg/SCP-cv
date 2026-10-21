// 文件描述：成熟媒体对象的组边界与资源约束回归；项目：SCP-cv；文件：StreamPreviewFlvTests.cs；作者：Qintsg；日期：2026-10-21。
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewFlvTests
{
    /// <summary>:returns: 库分类与重新封装保持原对象时间戳。</summary>
    [Fact]
    public void LibraryObjectsPreserveIndependentTimestampAndClassification()
    {
        var original = Tag(9, 42, [0x17, 1, 0, 0, 0, 1]);
        Assert.True(original.IsKeyframe); Assert.False(original.IsConfiguration);
        var moved = original.WithTimestamp(100);
        Assert.Equal(42u, original.Timestamp); Assert.Equal(100u, moved.Timestamp);
        Assert.Equal(original.Bytes.Length, moved.Bytes.Length);
        Assert.NotEqual(original.Bytes, moved.Bytes);
    }

    /// <summary>:returns: 完整组从关键帧开始，携带当前配置。</summary>
    [Fact]
    public void GopStartsAtKeyframeAndCarriesCurrentDecoderConfiguration()
    {
        var buffer = new FlvGopBuffer();
        Assert.Null(buffer.Push(Tag(9, 0, [0x17, 0, 0, 0, 0, 1])));
        Assert.Null(buffer.Push(Tag(9, 10, [0x27, 1, 0, 0, 0, 2])));
        Assert.Null(buffer.Push(Tag(9, 20, [0x17, 1, 0, 0, 0, 3])));
        Assert.Null(buffer.Push(Tag(9, 30, [0x27, 1, 0, 0, 0, 4])));
        var gop = buffer.Push(Tag(9, 40, [0x17, 1, 0, 0, 0, 5]));
        Assert.NotNull(gop); Assert.Equal(20u, gop.Timestamp); Assert.Equal(3, gop.TagCount);
        Assert.Equal(63, gop.Bytes.Length);
    }

    /// <summary>:returns: 编码代次时间线向前接续，旧配置不能跨代使用。</summary>
    [Fact]
    public void NewEncoderEpochKeepsTimestampForwardAndDiscardsUndecodableTail()
    {
        var clock = new FlvTimeline();
        clock.BeginEpoch(); Assert.Equal(0u, clock.Map(400)); Assert.Equal(100u, clock.Map(500));
        clock.BeginEpoch(); Assert.Equal(101u, clock.Map(0)); Assert.Equal(201u, clock.Map(100));
        var buffer = new FlvGopBuffer();
        buffer.Push(Tag(9, 0, [0x17, 0, 0, 0, 0, 1]));
        buffer.Push(Tag(9, 0, [0x17, 1, 0, 0, 0, 2]));
        buffer.BeginEpoch();
        Assert.Null(buffer.Push(Tag(9, 1000, [0x27, 1, 0, 0, 0, 3])));
        Assert.Null(buffer.Push(Tag(9, 1000, [0x17, 1, 0, 0, 0, 4])));
    }

    /// <summary>:returns: 完整组超过上限立即拒绝。</summary>
    [Fact]
    public void OversizeGopFailsInsteadOfGrowingWithoutBound()
    {
        var buffer = new FlvGopBuffer(80);
        buffer.Push(Tag(9, 0, [0x17, 0, 0, 0, 0, 1]));
        buffer.Push(Tag(9, 0, [0x17, 1, 0, 0, 0, 2]));
        Assert.Throws<InvalidDataException>(() => buffer.Push(Tag(9, 1, [0x27, 1, .. new byte[64]])));
    }

    /// <summary>:returns: 未完整分类及超限媒体不能复制进入共享缓存。</summary>
    [Fact]
    public void IncompleteUnsupportedOrOversizedPayloadIsRejectedBeforeCopy()
    {
        Assert.Throws<InvalidDataException>(() => Tag(9, 0, [0x17, 1, 0]));
        Assert.Throws<InvalidDataException>(() => Tag(9, 0, [0x1c, 1, 0, 0, 0]));
        Assert.Throws<InvalidDataException>(() => Tag(9, 0, new byte[FlvTag.MaximumBytes]));
    }

    /// <summary>:param type: 库的种类。:param timestamp: 媒体毫秒。:param payload: 分类边界数据。:returns: 交给成熟库分类和封装的对象；实际解码由真实媒体验证覆盖。</summary>
    private static FlvTag Tag(byte type, uint timestamp, byte[] payload) => new(type, timestamp, payload);
}
