// 现场SendType=1、输出通道1和原音频默认分类1的选源帧；编码通过不等于软件输出或实体路由已确认。
using System.Buffers.Binary;

namespace ScpCv.Infrastructure.Devices;

public static class VenueAudioSourceProtocol
{
    /// <summary>:param source: 仅IP81或软件公共声音总线63。:returns: 原675640构造器核对的23字节610D帧，不接受客户端原始地址。</summary>
    public static byte[] Select(string source)
    {
        var suffix = source switch
        {
            "ip81" => (byte)81,
            "scp_cv" => (byte)63,
            _ => throw new VenueDeviceException("现场音频来源只能是IP81或SCP-cv，未构造任何配置。"),
        };
        byte[] bytes = [0xfb, 0xfc, 0x61, 0x0d, 0, 1, 0, 11, 224, 1, 1, suffix,
            0x13, 0x88, 0x07, 0x80, 0x04, 0x38, 1, 0, 0, 0xfd, 0xfe];
        // 本变体校验只累加[2:15]；不能套用全部payload或6102的几何校验范围。
        var checksum = 0;
        for (var index = 2; index < 15; index++) checksum += bytes[index];
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(19, 2), checked((ushort)checksum));
        return bytes;
    }
}
