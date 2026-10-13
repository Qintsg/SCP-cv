// TPSimulator触控Join协议：客户端10，现场滑杆SQL Lock=19量程；不含设备状态读回。
using System.Buffers.Binary;

namespace ScpCv.Infrastructure.Devices;

public static class VenueJoinProtocol
{
    /// <summary>:param join: 原工程绑定。:param pressed: 按下或释放。:returns: 完整九字节帧的十六进制。</summary>
    public static string Digital(int join, bool pressed) => Frame(1, join, pressed ? 1 : 0);

    /// <summary>:param join: 滑杆绑定。:param percent: 百分比。:returns: 原工程19满量程整除结果，共0–19二十档。</summary>
    public static string Analog(int join, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        return Frame(2, join, percent * 19 / 100);
    }

    private static string Frame(byte kind, int join, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(join, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(join, ushort.MaxValue);
        Span<byte> bytes = [0xff, 6, kind, 10, 0, 0, 0, 0, 0xfa];
        BinaryPrimitives.WriteUInt16BigEndian(bytes[4..], checked((ushort)join));
        BinaryPrimitives.WriteUInt16BigEndian(bytes[6..], checked((ushort)value));
        return Convert.ToHexString(bytes);
    }
}
