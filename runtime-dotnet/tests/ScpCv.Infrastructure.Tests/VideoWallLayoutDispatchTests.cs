// 同节点双帧严格有序、阶段失败中止及真实回环接收诊断。
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

public sealed class VideoWallLayoutDispatchTests
{
    private static readonly string[] NodeSequence = ["61FF", "6102", "6160", "61B8", "61B9"];
    [Fact]
    public async Task DualProtocolFramesKeepNodeOrderAndStageBarriers()
    {
        var transport = new OrderedTransport();
        using var controller = new TcpVideoWallController(transport, Fast(), protocol: new() { SendType = 3 });
        var receipt = await controller.DispatchLayoutAsync(new("重复", [new(WallRegion.Left, new(WallInputKind.Window2)), new(WallRegion.Right, new(WallInputKind.Window2))]));
        Assert.True(receipt.IsHardware);
        Assert.Equal(250, receipt.PacketCount);
        foreach (var node in transport.Sent.GroupBy(item => item.Ip))
            Assert.Equal(NodeSequence, node.Select(item => item.Command));
        var global = transport.Sent.ToArray();
        Assert.All(global[..50], item => Assert.Equal("61FF", item.Command));
        Assert.All(global[150..200], item => Assert.Equal("61B8", item.Command));
    }

    [Fact]
    public async Task FailedMappingNeverCommitsAndInvalidProfileNeverClears()
    {
        var transport = new OrderedTransport { FailMappings = true };
        using var controller = new TcpVideoWallController(transport, Fast());
        await Assert.ThrowsAsync<VideoWallException>(() => controller.DispatchLayoutAsync(VideoWallLayoutPlanner.Preset("double")));
        Assert.DoesNotContain(transport.Sent, item => item.Command == "61B8");
        var invalid = new OrderedTransport();
        using var invalidController = new TcpVideoWallController(invalid, Fast(), protocol: new() { SendType = 0 });
        await Assert.ThrowsAsync<VideoWallException>(() => invalidController.DispatchLayoutAsync(VideoWallLayoutPlanner.Preset("single")));
        Assert.Empty(invalid.Sent);
    }

    [Fact]
    public async Task TcpTransportCollectsActualReadbackButDoesNotClaimVerified()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var observations = new VideoWallObservationStore();
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var request = new byte[12];
            await stream.ReadExactlyAsync(request);
            Assert.Equal(VideoWallPacketCodec.Control(0x61FF), request);
            var reply = Convert.FromHexString("FCFD6102000000160001E0010151138800000000078004380002000100050000FEFF");
            await stream.WriteAsync(reply.AsMemory(0, 10));
            await stream.WriteAsync(reply.AsMemory(10));
        });
        await new TcpVideoWallTransport(observations).SendAsync("127.0.0.1", port, VideoWallPacketCodec.Control(0x61FF), CancellationToken.None);
        await peer;
        Assert.Single(observations.Snapshot());
    }

    private static VideoWallDispatchOptions Fast() => new()
    {
        RetryAttempts = 1,
        Wait = static (_, _) => Task.CompletedTask,
    };

    private sealed class OrderedTransport : IVideoWallTransport
    {
        public ConcurrentQueue<(string Ip, string Command)> Sent { get; } = new();
        public bool FailMappings { get; init; }

        public async Task SendAsync(string ip, int port, byte[] packet, CancellationToken cancellationToken)
        {
            var command = Convert.ToHexString(packet[2..4]);
            if (command == "6102") await Task.Yield();
            if (FailMappings && command == "6102") throw new VideoWallException("受控映射失败");
            Sent.Enqueue((ip, command));
        }
    }
}
