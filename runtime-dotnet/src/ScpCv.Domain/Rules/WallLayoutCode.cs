// 无数据库布局码v1，与前端Base62/矩形14bit/CRC16-CCITT合同一致，不携带凭据。
using System.Numerics;
using ScpCv.Domain.Model;

namespace ScpCv.Domain.Rules;

public static class WallLayoutCode
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly WallInputKind[] Kinds = [WallInputKind.Window1, WallInputKind.Window2,
        WallInputKind.Laptop, WallInputKind.IpStream, WallInputKind.Window3, WallInputKind.Window4];

    public static string Encode(WallLayout layout)
    {
        layout = WallLayoutPolicy.Validate(layout);
        var bytes = new List<byte> { 1, (byte)layout.Mappings.Count };
        foreach (var mapping in layout.Mappings.OrderBy(item => WallLayoutPolicy.Rectangle(item).Row)
            .ThenBy(item => WallLayoutPolicy.Rectangle(item).Column))
        {
            var rect = WallLayoutPolicy.Rectangle(mapping);
            var packed = rect.Column | rect.Row << 4 | (rect.Columns - 1) << 7 | (rect.Rows - 1) << 11;
            bytes.Add((byte)(packed >> 8)); bytes.Add((byte)packed);
            bytes.Add((byte)Array.IndexOf(Kinds, mapping.Input.Kind));
            if (mapping.Input.Kind == WallInputKind.IpStream)
                bytes.AddRange(System.Net.IPAddress.Parse(mapping.Input.IpAddress).GetAddressBytes());
        }
        var crc = Checksum(bytes); bytes.Add((byte)(crc >> 8)); bytes.Add((byte)crc);
        var value = new BigInteger(bytes.ToArray(), isUnsigned: true, isBigEndian: true);
        var code = string.Empty;
        do { code = Alphabet[(int)(value % 62)] + code; value /= 62; } while (value > 0);
        return code;
    }

    public static WallLayout Decode(string code, string name = "自定义布局")
    {
        if (string.IsNullOrEmpty(code) || code.Length > 480 || code[0] == '0')
            throw new ArgumentException("布局代码格式无效。", nameof(code));
        var value = BigInteger.Zero;
        foreach (var character in code)
        {
            var digit = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (digit < 0) throw new ArgumentException("布局代码格式无效。", nameof(code));
            value = value * 62 + digit;
        }
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length < 4 || bytes[0] != 1 || bytes[1] > 50 || bytes[1] == 0)
            throw new ArgumentException("布局代码版本或内容无效。", nameof(code));
        var body = bytes[..^2];
        if (Checksum(body) != ((bytes[^2] << 8) | bytes[^1]))
            throw new ArgumentException("布局代码不完整或已损坏。", nameof(code));
        var mappings = new List<WallMapping>(); var offset = 2;
        for (var i = 0; i < bytes[1]; i++)
        {
            if (offset + 3 > body.Length) throw new ArgumentException("布局代码不完整。", nameof(code));
            var rect = (body[offset++] << 8) | body[offset++]; var kind = body[offset++];
            if (rect >= 16384 || kind >= Kinds.Length) throw new ArgumentException("布局代码包含未知内容。", nameof(code));
            var ip = string.Empty;
            if (Kinds[kind] == WallInputKind.IpStream)
            {
                if (offset + 4 > body.Length) throw new ArgumentException("布局代码缺少IP。", nameof(code));
                ip = new System.Net.IPAddress(body[offset..(offset + 4)]).ToString(); offset += 4;
            }
            mappings.Add(new(WallRegion.Grid, new(Kinds[kind], ip),
                new(rect & 15, (rect >> 4) & 7, ((rect >> 7) & 15) + 1, ((rect >> 11) & 7) + 1)));
        }
        if (offset != body.Length) throw new ArgumentException("布局代码含多余内容。", nameof(code));
        return WallLayoutPolicy.Validate(new(name, mappings));
    }

    private static int Checksum(IEnumerable<byte> bytes)
    {
        var crc = 0xffff;
        foreach (var value in bytes)
        {
            crc ^= value << 8;
            for (var i = 0; i < 8; i++) crc = ((crc << 1) ^ ((crc & 0x8000) != 0 ? 0x1021 : 0)) & 0xffff;
        }
        return crc;
    }
}
