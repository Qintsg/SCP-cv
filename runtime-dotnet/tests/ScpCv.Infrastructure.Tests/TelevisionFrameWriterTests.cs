// 同流顺序写入和中途故障回归；不创建真实设备连接。
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.DisplayRouting;

namespace ScpCv.Infrastructure.Tests;

public sealed class TelevisionFrameWriterTests
{
    /// <summary>:returns: 6231先于610D，两个Write使用同一个流；不夹入墙面清屏/提交包。</summary>
    [Fact]
    public async Task BothFramesUseSameStreamInRequiredOrder()
    {
        using var stream = new RecordingStream();
        var frames = TelevisionPacketCodec.Build(SiteInputCatalog.All.Single(item => item.IpSuffix == 63).Descriptor(), new());
        await TcpTelevisionController.WriteFramesAsync(stream, frames, default);
        Assert.Equal(2, stream.Writes);
        Assert.Equal(frames.SelectMany(frame => frame), stream.ToArray());
        Assert.Equal(35, stream.Length);
    }

    /// <summary>:returns: 第二次Write失败原样向上层传播，不把仅满屏成功当完整切换。</summary>
    [Fact]
    public async Task PartialWriteFailureStopsWithoutRetryingFirstFrame()
    {
        using var stream = new RecordingStream { FailAt = 2 };
        var frames = TelevisionPacketCodec.Build(SiteInputCatalog.All[0].Descriptor(), new());
        await Assert.ThrowsAsync<IOException>(() => TcpTelevisionController.WriteFramesAsync(stream, frames, default));
        Assert.Equal(2, stream.Writes); Assert.Equal(frames[0], stream.ToArray());
    }

    private sealed class RecordingStream : MemoryStream
    {
        public int Writes { get; private set; }
        public int FailAt { get; init; }
        /// <summary>:param buffer: 一帧。:param cancellationToken: 取消。:returns: 记录完整帧或故障。</summary>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Writes++;
            return Writes == FailAt ? ValueTask.FromException(new IOException("模拟写失败")) : base.WriteAsync(buffer, cancellationToken);
        }
    }
}
