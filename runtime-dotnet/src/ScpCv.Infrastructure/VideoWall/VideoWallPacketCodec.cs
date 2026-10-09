// 按原程序已验证字段编码映射和可选DES外层；不将兼容加密用于认证。
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;

namespace ScpCv.Infrastructure.VideoWall;

public sealed record VideoWallRectangle(int X, int Y, int Width, int Height);

public static class VideoWallPacketCodec
{
    /// <summary>编码同一节点的有序映射帧；所有参数使用实际像素尺寸。</summary>
    public static IReadOnlyList<byte[]> Mapping(
        VideoWallInputDescriptor input, VideoWallRectangle destination, VideoWallRectangle source,
        int sign, int level, int total, int fragment, int screen, VideoWallProtocolOptions options)
    {
        input.Validate();
        options.Validate();
        if (options.SendType == 3)
            return [Encode(0x6102, false), Encode(0x6160, true)];
        if (options.IgmpV3) return [Encode(0x6120, true)];
        return [Encode(options.SendType == 2 ? 0x6160 : options.CutSync ? 0x6102 : 0x6210, options.SendType == 2)];

        byte[] Encode(int command, bool unicast)
        {
            var extended = command == 0x6120;
            var packet = new byte[extended ? 60 : 45];
            packet[0] = 0xFB;
            packet[1] = 0xFC;
            Put(packet, 2, command);
            Put(packet, 6, extended ? 48 : 33);
            Put(packet, 8, sign);
            IPAddress.Parse(unicast ? input.UnicastAddress : input.MulticastAddress).GetAddressBytes().CopyTo(packet, 10);
            Put(packet, 14, extended && options.SendType == 1 ? 0 : unicast ? input.UnicastPort : input.Port);
            var rectangleOffset = 16;
            if (extended)
            {
                IPAddress.Parse(input.MulticastAddress).GetAddressBytes().CopyTo(packet, 16);
                Put(packet, 20, input.Port);
                rectangleOffset = 22;
            }
            WriteRectangle(packet, rectangleOffset, destination);
            Put(packet, rectangleOffset + 8, level);
            WriteRectangle(packet, rectangleOffset + 10, source);
            var countOffset = extended ? 40 : 34;
            Put(packet, countOffset, total);
            Put(packet, countOffset + 2, fragment);
            Put(packet, countOffset + 4, screen);
            packet[countOffset + 6] = 1;
            if (extended) packet[47] = checked((byte)options.SendType);
            Put(packet, extended ? 56 : 41, packet[2..40].Sum(value => (int)value) & 0xFFFF);
            packet[^2] = 0xFD;
            packet[^1] = 0xFE;
            return packet;
        }
    }

    /// <summary>构造本轮已恢复的清屏/提交/刷新短帧，不开放任意维护命令。</summary>
    public static byte[] Control(int command)
    {
        if (command is not (0x61FF or 0x61B8 or 0x61B9))
            throw new VideoWallException("未支持的视频墙控制命令。");
        var packet = new byte[12];
        packet[0] = 0xFB;
        packet[1] = 0xFC;
        Put(packet, 2, command);
        Put(packet, 8, packet[2..8].Sum(value => (int)value));
        packet[10] = 0xFD;
        packet[11] = 0xFE;
        return packet;
    }

    /// <summary>原设备模式的DES-ECB零填充；不是安全加密承诺。</summary>
    [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "仅用于显式开启的遗留设备协议兼容，不用于安全保护。")]
    public static byte[] Wrap(byte[] plain, VideoWallProtocolOptions options)
    {
        options.Validate();
        if (!options.UseNewProtocol) return plain;
        using var des = DES.Create();
        des.Key = Convert.FromHexString(options.DesKeyHex);
        var padded = new byte[(plain.Length + 7) / 8 * 8];
        plain.CopyTo(padded, 0);
        var cipher = des.EncryptEcb(padded, PaddingMode.None);
        var packet = new byte[cipher.Length + 6];
        packet[0] = 0xF1;
        packet[1] = 0xF2;
        Put(packet, 2, cipher.Length);
        cipher.CopyTo(packet, 4);
        packet[^2] = 0xF3;
        packet[^1] = 0xF4;
        return packet;
    }

    /// <summary>解码受限接收外层；保留零填充，由内层长度划分报文。</summary>
    [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "仅用于显式开启的遗留设备协议兼容，不用于安全保护。")]
    public static byte[] Unwrap(byte[] packet, VideoWallProtocolOptions options)
    {
        options.Validate();
        if (!options.UseNewProtocol || packet.Length < 14 || packet[0] != 0xF1 || packet[1] != 0xF2 ||
            packet[2] != 0 || packet[3] % 8 != 0 || packet[3] + 6 != packet.Length ||
            packet[^2] != 0xF3 || packet[^1] != 0xF4)
            throw new VideoWallException("视频墙加密回读帧格式无效。");
        using var des = DES.Create();
        des.Key = Convert.FromHexString(options.DesKeyHex);
        return des.DecryptEcb(packet.AsSpan(4, packet.Length - 6), PaddingMode.None);
    }

    private static void WriteRectangle(byte[] packet, int offset, VideoWallRectangle rectangle)
    {
        if (rectangle.Width < 1 || rectangle.Height < 1)
            throw new VideoWallException("视频墙矩形尺寸必须为正数。");
        Put(packet, offset, rectangle.X);
        Put(packet, offset + 2, rectangle.Y);
        Put(packet, offset + 4, rectangle.Width);
        Put(packet, offset + 6, rectangle.Height);
    }

    private static void Put(byte[] packet, int offset, int value)
    {
        if (value is < 0 or > 65535) throw new VideoWallException("视频墙字段超出16位范围。");
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset, 2), (ushort)value);
    }
}
