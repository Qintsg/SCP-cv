using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using ScpCv.Infrastructure.VideoWall;
using Xunit.Abstractions;

namespace ScpCv.Infrastructure.Tests;

/// <summary>
/// 真实 TCP 下发。把 192.168.5.101~150 挂成本机别名（<c>runtime-dotnet/scripts/videowall-loopback.ps1</c>）
/// 之后，假节点在真实端口上收包，于是「2 秒超时、5 次重试退避、失败即中止、取消原样抛出」这些
/// 原本只能靠读代码判断的行为，在这里有实测数字和逐包字节。别名没挂的机器上按各自前置条件跳过，
/// 默认 <c>dotnet test</c>（乃至 <c>--filter "Category!=Physical"</c>）都不受影响。
/// </summary>
[Trait("Category", "Physical")]
[Trait("Requires", "VideoWallLoopback")]
public sealed class VideoWallLoopbackTests(ITestOutputHelper output)
{
    /// <summary>别名挂着但没人监听：现场最常见的一种故障，用来造「端口拒连」。</summary>
    private const string RefusingNode = "192.168.5.101";

    /// <summary>故意不挂别名的节点：连出去没人应答，用来造「节点掉线」。</summary>
    private const string DeadNode = "192.168.5.150";

    private static readonly byte[] PhaseMarkers = [0xFF, 0x02, 0xB8, 0xB9];
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

    /// <summary>节点地址 → 墙上的列行坐标（含现场 45/46 互换补丁）。</summary>
    private static readonly Dictionary<string, (int Column, int Row)> NodePositions =
        (from column in Enumerable.Range(0, VideoWallSequenceBuilder.WallColumns)
         from row in Enumerable.Range(0, VideoWallSequenceBuilder.WallRows)
         select (Ip: VideoWallSequenceBuilder.TargetIp(column, row), Column: column, Row: row))
        .ToDictionary(item => item.Ip, item => (item.Column, item.Row), StringComparer.Ordinal);

    [RequiresVideoWallAliases]
    public async Task SingleModeReachesEveryNodeByteForByteWithinThreeSeconds()
    {
        await using var nodes = await FakeVideoWallNodes.StartAsync(VideoWallSequenceBuilder.AllTargetIps());
        var logger = new RecordingLogger<TcpVideoWallController>();

        var elapsed = await DispatchAsync(nodes, "single", logger, 4);

        Assert.Equal(200, nodes.Nodes.Sum(node => node.Received.Length));
        Assert.All(nodes.Nodes, node => AssertSequence(node, "single"));
        var completion = Assert.Single(logger.Messages);
        Assert.Contains("视频墙下发完成：模式 single，控制包 200 个", completion, StringComparison.Ordinal);

        output.WriteLine($"single 全节点：50 个节点各收 4 个包，实测 {elapsed:0} ms");
        // SC-005 常态侧：回环上的实测值要把「整墙切换一次要多久」钉成一个真实数字，而不是推算。
        Assert.True(elapsed < 3000, $"全节点下发耗时 {elapsed:0} ms");
    }

    [RequiresVideoWallAliases]
    public async Task DoubleModeSendsEachHalfWallItsOwnMulticastWindowAndSourceRectangle()
    {
        await using var nodes = await FakeVideoWallNodes.StartAsync(VideoWallSequenceBuilder.AllTargetIps());
        var logger = new RecordingLogger<TcpVideoWallController>();

        var elapsed = await DispatchAsync(nodes, "double", logger, 4);

        Assert.All(nodes.Nodes, node => AssertSequence(node, "double"));
        // 帧内容现场最容易配错。直接从收到的字节里解出组播地址、窗口号和源矩形，按列坐标核对左右半墙。
        Assert.All(nodes.Nodes, AssertHalfWallMapping);

        output.WriteLine($"double 全节点：50 个节点各收 4 个包，实测 {elapsed:0} ms");
    }

    [RequiresVideoWallAliases(Absent = DeadNode)]
    public async Task UnreachableNodeBurnsFiveTwoSecondTimeoutsThenAbortsAtTheClearPhase()
    {
        var listening = VideoWallSequenceBuilder.AllTargetIps().Where(ip => ip != DeadNode);
        await using var nodes = await FakeVideoWallNodes.StartAsync(listening);
        var logger = new RecordingLogger<TcpVideoWallController>();
        using var controller = Controller(logger);

        var stopwatch = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<VideoWallException>(() => controller.DispatchAsync("single"));
        stopwatch.Stop();

        // 掉线节点在每个阶段都会被联系一次，所以最早那个阶段就把它挡住：中止于 clear，点名到节点。
        Assert.Contains($"{DeadNode}:4830 phase=clear", error.Message, StringComparison.Ordinal);
        // 其余 49 个节点只收到清屏包：映射、提交、刷新一个都没发出去。
        AssertEveryNodeReceived(await nodes.WaitForAsync(1, ReceiveTimeout), "其余节点连清屏包都没收到");
        Assert.All(nodes.Nodes, node =>
        {
            Assert.Single(node.Received);
            Assert.Equal(0xFF, node.Received[0][3]);
        });

        var failure = Assert.Single(logger.MessagesAt(LogLevel.Error));
        Assert.Contains("中止于阶段 clear", failure, StringComparison.Ordinal);
        Assert.Contains("失败节点 1 个", failure, StringComparison.Ordinal);
        Assert.Empty(logger.MessagesAt(LogLevel.Information));

        output.WriteLine($"192.168.5.150 掉线：实测 {stopwatch.Elapsed.TotalSeconds:0.0} 秒后中止");
        // FR-005 的 2 秒超时 × 5 次尝试，加 0.2+0.4+0.8+1.0 秒退避 = 12.4 秒，实测 12.6 秒。
        // 也就是说：单个节点掉线的整墙切换最坏耗时就在这个量级，SC-005 的 90 秒上界很宽松。
        Assert.InRange(stopwatch.Elapsed.TotalSeconds, 11.5, 20);
    }

    [RequiresVideoWallAliases]
    public async Task NodeRefusingConnectionsAbortsAtTheClearPhaseBeforeAnythingElseGoesOut()
    {
        var listening = VideoWallSequenceBuilder.AllTargetIps().Where(ip => ip != RefusingNode);
        await using var nodes = await FakeVideoWallNodes.StartAsync(listening);
        var logger = new RecordingLogger<TcpVideoWallController>();
        using var controller = Controller(logger);

        var stopwatch = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<VideoWallException>(() => controller.DispatchAsync("single"));
        stopwatch.Stop();

        Assert.Contains($"{RefusingNode}:4830 phase=clear", error.Message, StringComparison.Ordinal);
        // 其余 49 个节点只收到清屏包：映射、提交、刷新一个都没发出去，半套映射不会落到墙上。
        AssertEveryNodeReceived(await nodes.WaitForAsync(1, ReceiveTimeout), "其余节点连清屏包都没收到");
        Assert.All(nodes.Nodes, node =>
        {
            Assert.Single(node.Received);
            Assert.Equal(0xFF, node.Received[0][3]);
        });

        var failure = Assert.Single(logger.MessagesAt(LogLevel.Error));
        Assert.Contains("中止于阶段 clear", failure, StringComparison.Ordinal);

        output.WriteLine($"192.168.5.101 拒连：实测 {stopwatch.Elapsed.TotalSeconds:0.0} 秒后中止");
        // 下界是退避本身：5 次尝试至少退避 0.2+0.4+0.8+1.0=2.4 秒，与「秒拒还是等满超时」无关；
        // 本机别名上的拒连要约 2 秒才回来，所以实测远高于下界（12.5 秒）。上界对齐 SC-005。
        Assert.InRange(stopwatch.Elapsed.TotalSeconds, 2.4, 20);
    }

    [RequiresVideoWallAliases]
    public async Task CancellingMidDispatchPropagatesCancellationWithoutLoggingSuccessOrFailure()
    {
        var listening = VideoWallSequenceBuilder.AllTargetIps().Where(ip => ip != RefusingNode);
        await using var nodes = await FakeVideoWallNodes.StartAsync(listening);
        var logger = new RecordingLogger<TcpVideoWallController>();
        using var controller = Controller(logger);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.DispatchAsync("single", cancellation.Token));
        stopwatch.Stop();
        await FakeVideoWallNodes.SettleAsync();

        // FR-017：取消原样抛出，不被折成节点失败。2 秒超时都还没到就返回，就是证据。
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1.5),
            $"取消后 {stopwatch.Elapsed.TotalMilliseconds:0} ms 才返回，像是等超时而不是即时取消");
        // FR-018：取消既不算节点失败，也不记成功，两条日志一条都不该出现。
        Assert.Empty(logger.Entries);
        // 取消发生在清屏阶段：收到的只该是 12 字节的清屏包，映射/提交/刷新一个都没出去。
        Assert.All(nodes.Nodes.SelectMany(node => node.Received), packet =>
        {
            Assert.Equal(12, packet.Length);
            Assert.Equal(0xFF, packet[3]);
        });

        output.WriteLine($"400 ms 时取消：{stopwatch.Elapsed.TotalMilliseconds:0} ms 抛出，未记任何下发日志");
    }

    private static TcpVideoWallController Controller(RecordingLogger<TcpVideoWallController> logger) =>
        new(new TcpVideoWallTransport(), new VideoWallDispatchOptions(), logger);

    /// <summary>真机下发一次并等各节点收满，返回实测耗时（毫秒）。</summary>
    private static async Task<double> DispatchAsync(
        FakeVideoWallNodes nodes,
        string mode,
        RecordingLogger<TcpVideoWallController> logger,
        int packetsPerNode)
    {
        using var controller = Controller(logger);
        var stopwatch = Stopwatch.StartNew();
        await controller.DispatchAsync(mode);
        stopwatch.Stop();

        AssertEveryNodeReceived(
            await nodes.WaitForAsync(packetsPerNode, ReceiveTimeout),
            $"有节点没收满 {packetsPerNode} 个控制包");
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>到达顺序就是阶段顺序，逐包字节与构造出的序列完全相同。</summary>
    private static void AssertSequence(FakeVideoWallNode node, string mode)
    {
        var markers = node.Received.Select(packet => packet[3]).ToArray();
        Assert.Equal(PhaseMarkers, markers);
        Assert.Equal(
            VideoWallSequenceBuilder.Build(mode)
                .Where(item => item.Ip == node.Ip)
                .Select(item => Convert.ToHexString(item.Packet)),
            node.Received.Select(Convert.ToHexString));
    }

    /// <summary>按列坐标核对该节点收到的映射包：左半墙工作站 2-1、右半墙工作站 2-2。</summary>
    private static void AssertHalfWallMapping(FakeVideoWallNode node)
    {
        var (column, row) = NodePositions[node.Ip];
        var mapping = node.Received[1];
        var halfColumns = VideoWallSequenceBuilder.WallColumns / 2;
        var leftHalf = column < halfColumns;
        var windowId = ReadU16(mapping, 8);

        Assert.Equal(
            leftHalf ? VideoWallSequenceBuilder.Ws21MulticastAddress : VideoWallSequenceBuilder.Ws22MulticastAddress,
            new IPAddress(mapping[10..14]).ToString());
        Assert.Equal(leftHalf ? 1 : 2, windowId);
        // 同一个窗口号在包内出现两次，两个字段必须一致。
        Assert.Equal(windowId, ReadU16(mapping, 24));

        var halfWidth = VideoWallSequenceBuilder.SourceWidth / halfColumns;
        var rowHeight = VideoWallSequenceBuilder.SourceHeight / VideoWallSequenceBuilder.WallRows;
        Assert.Equal((leftHalf ? column : column - halfColumns) * halfWidth, ReadU16(mapping, 26));
        Assert.Equal(row * rowHeight, ReadU16(mapping, 28));
        Assert.Equal(halfWidth, ReadU16(mapping, 30));
        Assert.Equal(rowHeight, ReadU16(mapping, 32));
    }

    private static int ReadU16(byte[] packet, int offset) => (packet[offset] << 8) | packet[offset + 1];

    private static void AssertEveryNodeReceived(string[] stillShort, string what) =>
        Assert.True(stillShort.Length == 0, $"{what}，仍缺：{string.Join('、', stillShort)}");
}
