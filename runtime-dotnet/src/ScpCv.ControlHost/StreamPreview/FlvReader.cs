// 无声AVC预览的有界FLV解析；只接受完整tag，不把任意字节块作为可丢弃帧。
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace ScpCv.ControlHost.StreamPreview;

public sealed class FlvTag
{
    public byte[] Bytes { get; }
    public byte Type => Bytes[0];
    public uint Timestamp => (uint)(Bytes[4] << 16 | Bytes[5] << 8 | Bytes[6] | Bytes[7] << 24);
    public bool IsConfiguration => Type == 9 && Bytes[12] == 0;
    public bool IsKeyframe => Type == 9 && Bytes[11] >> 4 == 1 && Bytes[12] == 1;

    /// <summary>:param bytes: 本模块拥有且之后不可修改的完整tag。:returns: 通过结构校验的tag。</summary>
    public FlvTag(byte[] bytes) { FlvReader.ValidateTag(bytes); Bytes = bytes; }

    /// <summary>:param timestamp: 输出毫秒。:returns: 独立且不可变使用的重映射tag，不修改共享原件。</summary>
    public FlvTag WithTimestamp(uint timestamp)
    {
        var copy = (byte[])Bytes.Clone();
        copy[4] = (byte)(timestamp >> 16); copy[5] = (byte)(timestamp >> 8); copy[6] = (byte)timestamp; copy[7] = (byte)(timestamp >> 24);
        return new(copy);
    }
}

public static class FlvReader
{
    public const int MaximumTagBytes = 512 * 1024;
    private static readonly byte[] FileHeader = [0x46, 0x4c, 0x56, 1, 1, 0, 0, 0, 9, 0, 0, 0, 0];
    public static ReadOnlyMemory<byte> Header => FileHeader;

    /// <summary>:param stream: 自有转换器输出。:param cancellationToken: 代次取消。:returns: 完整并校验过的tag；截断明确失败。</summary>
    public static async IAsyncEnumerable<FlvTag> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var file = new byte[13];
        await stream.ReadExactlyAsync(file, cancellationToken).ConfigureAwait(false);
        if (!file.AsSpan().SequenceEqual(FileHeader)) throw new InvalidDataException("预览不是受支持的无声FLV输出");
        var head = new byte[11];
        while (await stream.ReadAsync(head.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) != 0)
        {
            await stream.ReadExactlyAsync(head.AsMemory(1), cancellationToken).ConfigureAwait(false);
            var size = PayloadLength(head);
            if (size > MaximumTagBytes - 15) throw new InvalidDataException("预览单帧超过安全上限");
            var bytes = new byte[checked(size + 15)]; head.CopyTo(bytes, 0);
            await stream.ReadExactlyAsync(bytes.AsMemory(11), cancellationToken).ConfigureAwait(false);
            yield return new FlvTag(bytes);
        }
    }

    /// <summary>:param bytes: 完整tag。:returns: 非法类型、长度或AVC包明确失败。</summary>
    internal static void ValidateTag(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 15 || bytes.Length > MaximumTagBytes || bytes.Length != PayloadLength(bytes) + 15 ||
            bytes[8] != 0 || bytes[9] != 0 || bytes[10] != 0 ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(bytes.Length - 4)) != bytes.Length - 4)
            throw new InvalidDataException("预览FLV帧长度无效");
        if (bytes[0] != 18 && (bytes[0] != 9 || bytes.Length < 20 || (bytes[11] & 15) != 7 || bytes[12] > 2))
            throw new InvalidDataException("预览FLV编码不受支持");
    }

    private static int PayloadLength(ReadOnlySpan<byte> bytes) => bytes[1] << 16 | bytes[2] << 8 | bytes[3];
}

public sealed class FlvTimeline
{
    private ulong _last, _epoch;
    private uint _first;
    private bool _hasTimestamp, _started;

    /// <summary>:returns: 新编码进程从上一代时间戳之后接续，不重置浏览器时间线。</summary>
    public void BeginEpoch() { _epoch = _hasTimestamp ? _last + 1 : 0; _started = false; }

    /// <summary>:param source: 编码器原时间戳。:returns: 不倒退的FLV毫秒，32位回绕遵循FLV字段范围。</summary>
    public uint Map(uint source)
    {
        if (!_started) { _first = source; _started = true; }
        var delta = unchecked((int)(source - _first));
        var next = _epoch + (ulong)Math.Max(delta, 0);
        _last = _hasTimestamp ? Math.Max(next, _last) : next; _hasTimestamp = true;
        return unchecked((uint)_last);
    }
}
