// 有界增量窗口回读解析；旧帧无操作版本，观测不能作为本次布局完成ACK。
using System.Buffers.Binary;
using System.Net;

namespace ScpCv.Infrastructure.VideoWall;

public sealed record VideoWallWindowReply(
    string NodeIp, int WindowSign, string StreamIp, int Port,
    int X, int Y, int Width, int Height, int Level, int TotalCount, int ScreenId);

public sealed class VideoWallReplyDecoder(string nodeIp, VideoWallProtocolOptions options)
{
    private readonly List<byte> _buffer = [];

    /// <summary>消费半包/粘包，非法长度逐字节重同步，不按一次TCP读取划帧。</summary>
    public IReadOnlyList<VideoWallWindowReply> Feed(byte[] bytes)
    {
        if (bytes.Length > 65536) throw new VideoWallException("视频墙诊断接收超过预算。");
        var result = new List<VideoWallWindowReply>();
        foreach (var value in bytes)
        {
            if (_buffer.Count >= 512) _buffer.RemoveAt(0);
            _buffer.Add(value);
            Drain(result);
        }
        return result;
    }

    private void Drain(List<VideoWallWindowReply> result)
    {
        while (_buffer.Count >= 8)
        {
            var encrypted = options.UseNewProtocol;
            if (_buffer[0] != (encrypted ? 0xF1 : 0xFC) || _buffer[1] != (encrypted ? 0xF2 : 0xFD))
            {
                _buffer.RemoveAt(0);
                continue;
            }
            var payload = encrypted ? _buffer[2] * 256 + _buffer[3] : _buffer[6] * 256 + _buffer[7];
            if (encrypted ? payload is < 8 or > 248 || payload % 8 != 0 : payload is < 1 or > 84)
            {
                _buffer.RemoveAt(0);
                continue;
            }
            var length = payload + (encrypted ? 6 : 12);
            if (_buffer.Count < length) return;
            if (_buffer[length - 2] != (encrypted ? 0xF3 : 0xFE) || _buffer[length - 1] != (encrypted ? 0xF4 : 0xFF))
            {
                _buffer.RemoveAt(0);
                continue;
            }
            var frame = _buffer.GetRange(0, length).ToArray();
            _buffer.RemoveRange(0, length);
            if (encrypted)
            {
                var inner = new VideoWallReplyDecoder(nodeIp, new());
                result.AddRange(inner.Feed(VideoWallPacketCodec.Unwrap(frame, options)));
            }
            else if (frame[2] == 0x61 && frame[3] == 0x02 && frame[4] == 0 && frame[5] == 0 && payload == 22)
            {
                result.Add(new(nodeIp, Read(8), new IPAddress(frame[10..14]).ToString(), Read(14),
                    Read(16), Read(18), Read(20), Read(22), Read(24), Read(26), Read(28)));
            }
            int Read(int offset) => BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(offset, 2));
        }
    }
}

public sealed class VideoWallObservationStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Node, int Sign), VideoWallWindowReply> _windows = [];

    /// <summary>保存有界最近观测；不触发布局verified或自动重放。</summary>
    public void Record(VideoWallWindowReply reply)
    {
        lock (_gate)
        {
            var key = (reply.NodeIp, reply.WindowSign);
            if (_windows.Count >= 256 && !_windows.ContainsKey(key)) return;
            _windows[key] = reply;
        }
    }

    public IReadOnlyList<VideoWallWindowReply> Snapshot()
    {
        lock (_gate) return _windows.Values.ToArray();
    }
}
