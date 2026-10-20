// 预览FLV纯协议回归：不访问设备、MediaMTX或原生解码器。
using System.Buffers.Binary;
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewFlvTests
{
    [Fact]
    public async Task ReadsCompleteTagsAndRejectsIncorrectPreviousSizeAsync()
    {
        var raw = Tag(9, 42, [0x17, 1, 0, 0, 0, 1]);
        await using var stream = new MemoryStream([.. FlvReader.Header.ToArray(), .. raw]);
        var tags = new List<FlvTag>();
        await foreach (var tag in FlvReader.ReadAsync(stream, CancellationToken.None)) tags.Add(tag);
        Assert.Single(tags); Assert.Equal(42u, tags[0].Timestamp);
        raw[^1] ^= 1;
        await using var broken = new MemoryStream([.. FlvReader.Header.ToArray(), .. raw]);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var unused in FlvReader.ReadAsync(broken, CancellationToken.None)) { _ = unused; }
        });
    }

    [Fact]
    public void GopStartsAtKeyframeAndCarriesCurrentDecoderConfiguration()
    {
        var buffer = new FlvGopBuffer();
        Assert.Null(buffer.Push(new(Tag(9, 0, [0x17, 0, 0, 0, 0, 1]))));
        Assert.Null(buffer.Push(new(Tag(9, 10, [0x27, 1, 0, 0, 0, 2]))));
        Assert.Null(buffer.Push(new(Tag(9, 20, [0x17, 1, 0, 0, 0, 3]))));
        Assert.Null(buffer.Push(new(Tag(9, 30, [0x27, 1, 0, 0, 0, 4]))));
        var gop = buffer.Push(new(Tag(9, 40, [0x17, 1, 0, 0, 0, 5])));
        Assert.NotNull(gop); Assert.Equal(20u, gop.Timestamp); Assert.Equal(3, gop.TagCount);
        Assert.Equal(20u, new FlvTag(gop.Bytes[..21]).Timestamp);
        Assert.Equal(0, gop.Bytes[12]);
        Assert.Equal(1, gop.Bytes[21 + 12]);
    }

    [Fact]
    public void NewEncoderEpochKeepsTimestampForwardAndDiscardsUndecodableTail()
    {
        var clock = new FlvTimeline();
        clock.BeginEpoch(); Assert.Equal(0u, clock.Map(400)); Assert.Equal(100u, clock.Map(500));
        clock.BeginEpoch(); Assert.Equal(101u, clock.Map(0)); Assert.Equal(201u, clock.Map(100));
        var buffer = new FlvGopBuffer();
        buffer.Push(new(Tag(9, 0, [0x17, 0, 0, 0, 0, 1])));
        buffer.Push(new(Tag(9, 0, [0x17, 1, 0, 0, 0, 2])));
        buffer.BeginEpoch();
        Assert.Null(buffer.Push(new(Tag(9, 1000, [0x27, 1, 0, 0, 0, 3]))));
        Assert.Null(buffer.Push(new(Tag(9, 1000, [0x17, 1, 0, 0, 0, 4]))));
    }

    [Fact]
    public void OversizeGopFailsInsteadOfGrowingWithoutBound()
    {
        var buffer = new FlvGopBuffer(80);
        buffer.Push(new(Tag(9, 0, [0x17, 0, 0, 0, 0, 1])));
        buffer.Push(new(Tag(9, 0, [0x17, 1, 0, 0, 0, 2])));
        Assert.Throws<InvalidDataException>(() => buffer.Push(new(Tag(9, 1, [0x27, 1, .. new byte[64]]))));
    }

    [Fact]
    public async Task TruncatedOrOversizedTagIsRejectedBeforeAllocationAsync()
    {
        var header = Tag(9, 0, [0x17, 1, 0]);
        header[1] = 0x7f;
        await using var stream = new MemoryStream([.. FlvReader.Header.ToArray(), .. header]);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var unused in FlvReader.ReadAsync(stream, CancellationToken.None)) { _ = unused; }
        });
    }

    /// <summary>:param type: FLV类型。:param timestamp: 毫秒。:param payload: 有界测试负载。:returns: 独立构造的合法完整tag。</summary>
    private static byte[] Tag(byte type, uint timestamp, byte[] payload)
    {
        var bytes = new byte[15 + payload.Length]; bytes[0] = type;
        bytes[1] = (byte)(payload.Length >> 16); bytes[2] = (byte)(payload.Length >> 8); bytes[3] = (byte)payload.Length;
        bytes[4] = (byte)(timestamp >> 16); bytes[5] = (byte)(timestamp >> 8); bytes[6] = (byte)timestamp; bytes[7] = (byte)(timestamp >> 24);
        payload.CopyTo(bytes, 11); BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(bytes.Length - 4), (uint)payload.Length + 11); return bytes;
    }
}
