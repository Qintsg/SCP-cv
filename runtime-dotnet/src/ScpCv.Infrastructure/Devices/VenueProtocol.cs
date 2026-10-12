// 逆向及155实机查询确认的6190/6191帧；不同族的帧不可套用校验或通道号。
namespace ScpCv.Infrastructure.Devices;

public static class VenueProtocol
{
    /// <summary>:param volume: 0–100。:returns: 屏幕音量配置帧。</summary>
    public static byte[] ScreenVolume(int volume)
    {
        if (volume is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(volume));
        var bytes = Convert.FromHexString("FBFC619000000001000000FDFE");
        bytes[8] = checked((byte)volume);
        return bytes;
    }

    /// <summary>:returns: 只读屏幕音量查询帧。</summary>
    public static byte[] ScreenVolumeQuery() => Convert.FromHexString("FBFC6191000000000000FDFE");

    /// <summary>:param bytes: 完整响应。:returns: 已读回音量，不接受请求回显或其它族。</summary>
    public static int ReadScreenVolume(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 13 || bytes[0] != 0xFC || bytes[1] != 0xFD || bytes[2] != 0x61 || bytes[3] != 0x91 ||
            bytes[11] != 0xFE || bytes[12] != 0xFF || bytes[8] > 100)
            throw new VenueDeviceException("屏幕音量响应格式无效，未确认实际音量。");
        return bytes[8];
    }
}
