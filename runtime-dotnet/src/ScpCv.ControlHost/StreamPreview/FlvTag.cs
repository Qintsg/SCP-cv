// 文件描述：成熟库提供的媒体分类与FLV封装；项目：SCP-cv；文件：FlvTag.cs；作者：Qintsg；日期：2026-10-21。
using System.Buffers;
using LiveStreamingServerNet.Rtmp;
using LiveStreamingServerNet.Rtmp.Utilities.Containers;
using LiveStreamingServerNet.Utilities.Buffers;
using LiveStreamingServerNet.Utilities.Buffers.Configurations;
using LiveStreamingServerNet.Utilities.Contracts;
using Microsoft.Extensions.Options;

namespace ScpCv.ControlHost.StreamPreview;

public sealed class FlvTag
{
    public const int MaximumBytes = 512 * 1024;
    private readonly byte[] _payload;
    private byte[]? _bytes;
    public byte Type { get; }
    public uint Timestamp { get; }
    public bool IsConfiguration { get; }
    public bool IsKeyframe { get; }
    public byte[] Bytes => _bytes ??= FlvSerializer.Tag((FlvTagType)Type, Timestamp, _payload);

    /// <summary>:param type: 库返回的媒体种类。:param timestamp: 已解析毫秒。:param payload: 库提供的有界载荷。:returns: 独立拥有数据的媒体对象，不解析容器。</summary>
    public FlvTag(byte type, uint timestamp, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaximumBytes - 15 || type is not (9 or 18)) throw new InvalidDataException("预览媒体超出支持范围");
        Type = type; Timestamp = timestamp; _payload = payload.ToArray();
        if (type != 9) return;
        if (payload.Length < FlvVideoTagHeader.Size) throw new InvalidDataException("预览视频载荷不完整");
        var header = FlvParser.ParseVideoTagHeader(payload);
        if (header.VideoCodec != VideoCodec.AVC || !Enum.IsDefined(header.AVCPacketType) || !Enum.IsDefined(header.FrameType))
            throw new InvalidDataException("预览视频编码不受支持");
        IsConfiguration = header.AVCPacketType == AVCPacketType.SequenceHeader;
        IsKeyframe = header.FrameType == VideoFrameType.KeyFrame && header.AVCPacketType == AVCPacketType.NALU;
    }

    /// <summary>:param timestamp: 当前业务时间线毫秒。:returns: 通过库重新封装的独立对象，不修改共享原件。</summary>
    public FlvTag WithTimestamp(uint timestamp) => new(Type, timestamp, _payload);
}

public static class FlvSerializer
{
    private static readonly DataBufferPool Buffers = new(Options.Create(new DataBufferPoolConfiguration { MaxPoolSize = 8 }));
    public static ReadOnlyMemory<byte> Header { get; } = Serialize(static writer => writer.WriteHeaderAsync(false, true, CancellationToken.None));

    /// <summary>:param type: 已验证种类。:param timestamp: 毫秒。:param payload: 完整媒体载荷。:returns: 由成熟writer生成的完整标签。</summary>
    public static byte[] Tag(FlvTagType type, uint timestamp, byte[] payload) =>
        Serialize(writer => writer.WriteTagAsync(type, timestamp, buffer => buffer.Write(payload), CancellationToken.None));

    /// <summary>:param operation: 有界内存封装。:returns: 完整不可共享修改的输出副本。</summary>
    private static byte[] Serialize(Func<FlvWriter, ValueTask> operation)
    {
        var output = new MemoryWriter();
        var writer = new FlvWriter(output, Buffers);
        try { operation(writer).AsTask().GetAwaiter().GetResult(); return output.Buffer.WrittenSpan.ToArray(); }
        finally { writer.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private sealed class MemoryWriter : IStreamWriter
    {
        public ArrayBufferWriter<byte> Buffer { get; } = new();
        /// <summary>:param buffer: 库封装的数据。:param cancellationToken: 当前寿命。:returns: 仅向本次内存写入。</summary>
        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Buffer.Write(buffer.Span); return ValueTask.CompletedTask;
        }
    }
}
