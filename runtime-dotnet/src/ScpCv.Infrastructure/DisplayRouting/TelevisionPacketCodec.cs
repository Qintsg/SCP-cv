// 原6742B0/65C7C0矩阵帧；满屏后单路是项目明确组合，不是50屏映射。
using System.Buffers.Binary;
using System.Net;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.DisplayRouting;

public sealed class DisplayRoutingException(string message, string code = "display_routing_invalid", Exception? inner = null)
    : Exception(message, inner)
{
    public string Code { get; } = code;
}

public static class TelevisionPacketCodec
{
    /// <summary>:param input: 已登记源的真实尺寸和组播参数。:param options: 显式协议。:returns: 同连接依次写出的满屏与单源帧。</summary>
    public static IReadOnlyList<byte[]> Build(VideoWallInputDescriptor input, VideoWallProtocolOptions options)
    {
        try { input.Validate(); options.Validate(); }
        catch (VideoWallException exception) { throw new DisplayRoutingException(exception.Message, "protocol_unavailable", exception); }
        if (options.SendType != 1 || options.IgmpV3)
            throw new DisplayRoutingException("电视仅接入已恢复的组播单路协议。", "protocol_unavailable");
        var full = Convert.FromHexString("FBFC623100000001010094FDFE");
        var route = new byte[22];
        route[0] = 0xFB; route[1] = 0xFC; route[2] = 0x61; route[3] = 0x0D;
        route[7] = 10; // 两台电视channelNum均0；Position1/2不是帧通道。
        IPAddress.Parse(input.MulticastAddress).GetAddressBytes().CopyTo(route, 8);
        Put(route, 12, input.Port); Put(route, 14, input.Width); Put(route, 16, input.Height);
        Put(route, 18, route[2..14].Sum(value => (int)value) & 0xFFFF);
        route[20] = 0xFD; route[21] = 0xFE;
        return [VideoWallPacketCodec.Wrap(full, options), VideoWallPacketCodec.Wrap(route, options)];
    }

    /// <summary>:param packet: 自有包。:param offset: 字段位置。:param value: 预检后的16位值。</summary>
    private static void Put(byte[] packet, int offset, int value) =>
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset, 2), checked((ushort)value));
}
