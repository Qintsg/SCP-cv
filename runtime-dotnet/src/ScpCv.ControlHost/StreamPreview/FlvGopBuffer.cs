// 完整关键帧组缓存：所有订阅共享不可变字节，慢消费者只跳完整GOP。
namespace ScpCv.ControlHost.StreamPreview;

public sealed record PreviewGop(byte[] Bytes, uint Timestamp, int TagCount, PreviewEncoding? Encoding = null);

public sealed class FlvGopBuffer(int maximumBytes = 2 * 1024 * 1024)
{
    private FlvTag? _configuration, _metadata;
    private readonly List<FlvTag> _current = [];
    private int _size;

    /// <summary>:returns: 新编码代次不复用旧配置或旧未完成GOP。</summary>
    public void BeginEpoch() { _configuration = null; _metadata = null; _current.Clear(); _size = 0; }

    /// <summary>:param tag: 时间戳已映射的完整tag。:returns: 新关键帧开始时发布上一完整组；未有配置与关键帧时不发布。</summary>
    public PreviewGop? Push(FlvTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag.Type == 18) { _metadata = tag; return null; }
        if (tag.IsConfiguration) { _configuration = tag; _current.Clear(); _size = 0; return null; }
        if (_configuration is null) return null;
        PreviewGop? finished = null;
        if (tag.IsKeyframe) finished = Complete();
        if (_current.Count == 0 && !tag.IsKeyframe) return null;
        var size = checked(_size + tag.Bytes.Length);
        if (size + _configuration.Bytes.Length + (_metadata?.Bytes.Length ?? 0) > maximumBytes)
            throw new InvalidDataException("预览关键帧组超过安全上限");
        _current.Add(tag); _size = size;
        return finished;
    }

    /// <summary>:returns: 仅从关键帧开始的完整组，初始化tag绑定该组起始时间戳；清空当前组。</summary>
    public PreviewGop? Complete()
    {
        if (_current.Count == 0 || _configuration is null) return null;
        var timestamp = _current[0].Timestamp;
        var size = checked(_size + _configuration.Bytes.Length + (_metadata?.Bytes.Length ?? 0));
        if (size > maximumBytes) throw new InvalidDataException("预览关键帧组超过安全上限");
        var bytes = new byte[size]; var offset = 0;
        if (_metadata is not null) Append(_metadata.WithTimestamp(timestamp));
        Append(_configuration.WithTimestamp(timestamp));
        foreach (var tag in _current) Append(tag);
        var count = _current.Count + 1 + (_metadata is null ? 0 : 1);
        _current.Clear(); _size = 0;
        return new(bytes, timestamp, count);

        void Append(FlvTag tag) { tag.Bytes.CopyTo(bytes, offset); offset += tag.Bytes.Length; }
    }
}
