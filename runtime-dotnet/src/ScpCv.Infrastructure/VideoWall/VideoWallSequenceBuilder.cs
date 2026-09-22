using System.Net;

namespace ScpCv.Infrastructure.VideoWall;

/// <summary>视频墙物理布局模式。</summary>
public enum VideoWallLayoutMode
{
    /// <summary>工作站 2-1 铺满整墙（运行态 single）。</summary>
    FullscreenWs21,

    /// <summary>左半墙工作站 2-1、右半墙工作站 2-2（运行态 double）。</summary>
    SplitWs21Ws22,
}

/// <summary>单个视频墙控制包发送项。</summary>
public sealed record VideoWallSendItem(string Phase, string Ip, int Port, byte[] Packet);

/// <summary>
/// 视频墙控制包序列构造。自清理前提交 <c>e822be9</c> 的旧实现 1:1 迁移，协议逻辑不变：
/// 清屏 → 映射 → 提交 → 刷新，节点 IP 仍为 <c>192.168.5.101~150:4830</c>（含现场 45/46 互换补丁）。
/// </summary>
public static class VideoWallSequenceBuilder
{
    public const int TcpPort = 4830;
    public const int WallColumns = 10;
    public const int WallRows = 5;
    public const int ScreenWidth = 1920;
    public const int ScreenHeight = 1080;
    public const int SourceWidth = 3840;
    public const int SourceHeight = 2160;
    public const string Ws21MulticastAddress = "224.1.1.55";
    public const string Ws22MulticastAddress = "224.1.1.56";

    private static readonly byte[] ClearPacket = Convert.FromHexString("FBFC61FF000000000160FDFE");
    private static readonly byte[] CommitPacket = Convert.FromHexString("FBFC61B8000000000119FDFE");
    private static readonly byte[] RefreshPacket = Convert.FromHexString("FBFC61B900000000011AFDFE");
    private static readonly byte[] FixedFlags = Convert.FromHexString("000900010001");

    /// <summary>按运行态大屏模式构造下发序列。</summary>
    public static IReadOnlyList<VideoWallSendItem> Build(string bigScreenMode) =>
        bigScreenMode.Trim().ToLowerInvariant() switch
        {
            "single" => BuildFor(VideoWallLayoutMode.FullscreenWs21),
            "double" => BuildFor(VideoWallLayoutMode.SplitWs21Ws22),
            // Python 的 build_sequence 同样以 VideoWallError 报未知模式，这里统一到视频墙异常类型，
            // 调用方无需再区分 ArgumentOutOfRangeException。
            _ => throw new VideoWallException($"未知的视频墙模式：{bigScreenMode}"),
        };

    /// <summary>构造完整的视频墙下发序列。</summary>
    public static IReadOnlyList<VideoWallSendItem> BuildFor(VideoWallLayoutMode mode)
    {
        var items = new List<VideoWallSendItem>();
        foreach (var ip in AllTargetIps()) items.Add(new VideoWallSendItem("clear", ip, TcpPort, ClearPacket));
        items.AddRange(mode == VideoWallLayoutMode.FullscreenWs21 ? FullscreenMappings() : SplitMappings());
        foreach (var ip in AllTargetIps()) items.Add(new VideoWallSendItem("commit", ip, TcpPort, CommitPacket));
        foreach (var ip in AllTargetIps()) items.Add(new VideoWallSendItem("refresh", ip, TcpPort, RefreshPacket));
        return items;
    }

    /// <summary>返回视频墙全部目标节点 IP（列优先）。</summary>
    public static IReadOnlyList<string> AllTargetIps() =>
    [
        .. from column in Enumerable.Range(0, WallColumns)
           from row in Enumerable.Range(0, WallRows)
           select TargetIp(column, row),
    ];

    /// <summary>按列行坐标计算目标节点 IP，并处理现场 45/46 互换补丁。</summary>
    public static string TargetIp(int column, int row)
    {
        var screenId = column * WallRows + row;
        if (screenId == 45) screenId = 46;
        else if (screenId == 46) screenId = 45;
        return $"192.168.5.{101 + screenId}";
    }

    /// <summary>生成单个 61 02 窗口映射包（45 字节，校验和 = (sum(packet[2:]) - 1) &amp; 0xFFFF）。</summary>
    public static byte[] MappingPacket(
        string multicastAddress,
        int windowId,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight)
    {
        var packet = new List<byte>();
        packet.AddRange([0xFB, 0xFC, 0x61, 0x02]);
        packet.AddRange([0x00, 0x00, 0x00, 0x21]);
        packet.AddRange(U16(windowId));
        packet.AddRange(IPAddress.Parse(multicastAddress).GetAddressBytes());
        packet.AddRange(U16(5000));
        packet.AddRange(U16(0));
        packet.AddRange(U16(0));
        packet.AddRange(U16(ScreenWidth));
        packet.AddRange(U16(ScreenHeight));
        packet.AddRange(U16(windowId));
        packet.AddRange(U16(sourceX));
        packet.AddRange(U16(sourceY));
        packet.AddRange(U16(sourceWidth));
        packet.AddRange(U16(sourceHeight));
        packet.AddRange(FixedFlags);
        packet.Add(0x01);
        var checksum = (packet.Skip(2).Sum(value => (int)value) - 1) & 0xFFFF;
        packet.AddRange(U16(checksum));
        packet.AddRange([0xFD, 0xFE]);
        if (packet.Count != 45)
        {
            throw new InvalidOperationException($"窗口映射包长度异常：{packet.Count}");
        }

        return [.. packet];
    }

    private static IEnumerable<VideoWallSendItem> FullscreenMappings()
    {
        var sourceWidth = SourceWidth / WallColumns;
        var sourceHeight = SourceHeight / WallRows;
        for (var column = 0; column < WallColumns; column++)
        {
            for (var row = 0; row < WallRows; row++)
            {
                yield return new VideoWallSendItem(
                    "mapping_ws21_fullscreen",
                    TargetIp(column, row),
                    TcpPort,
                    MappingPacket(Ws21MulticastAddress, 1, column * sourceWidth, row * sourceHeight, sourceWidth, sourceHeight));
            }
        }
    }

    private static IEnumerable<VideoWallSendItem> SplitMappings()
    {
        var halfColumns = WallColumns / 2;
        var sourceWidth = SourceWidth / halfColumns;
        var sourceHeight = SourceHeight / WallRows;
        for (var column = 0; column < WallColumns; column++)
        {
            for (var row = 0; row < WallRows; row++)
            {
                if (column < halfColumns)
                {
                    yield return new VideoWallSendItem(
                        "mapping_ws21_left_half",
                        TargetIp(column, row),
                        TcpPort,
                        MappingPacket(Ws21MulticastAddress, 1, column * sourceWidth, row * sourceHeight, sourceWidth, sourceHeight));
                }
                else
                {
                    yield return new VideoWallSendItem(
                        "mapping_ws22_right_half",
                        TargetIp(column, row),
                        TcpPort,
                        MappingPacket(
                            Ws22MulticastAddress,
                            2,
                            (column - halfColumns) * sourceWidth,
                            row * sourceHeight,
                            sourceWidth,
                            sourceHeight));
                }
            }
        }
    }

    private static byte[] U16(int value)
    {
        if (value is < 0 or > 0xFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "u16 超出范围。");
        }

        return [(byte)(value >> 8), (byte)(value & 0xFF)];
    }
}
