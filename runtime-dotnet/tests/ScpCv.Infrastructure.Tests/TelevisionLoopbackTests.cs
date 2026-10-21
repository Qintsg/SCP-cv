// 显式本机TCP回环：真实同连接字节顺序，不访问161/162或其它现场地址。
using System.Net;
using System.Net.Sockets;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.DisplayRouting;

namespace ScpCv.Infrastructure.Tests;

public sealed class TelevisionLoopbackTests
{
    /// <summary>:returns: 真实Socket上35字节=6231满屏帧后610D单源；不以网络回环冒充实体出画。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task OrderedFramesReachOneLoopbackTcpConnection()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var outgoing = new TcpClient();
        var accepted = listener.AcceptTcpClientAsync(deadline.Token);
        await outgoing.ConnectAsync(endpoint.Address, endpoint.Port, deadline.Token);
        using var incoming = await accepted;
        var frames = TelevisionPacketCodec.Build(SiteInputCatalog.All.Single(item => item.IpSuffix == 63).Descriptor(), new());
        await TcpTelevisionController.WriteFramesAsync(outgoing.GetStream(), frames, deadline.Token);
        var actual = new byte[35];
        await incoming.GetStream().ReadExactlyAsync(actual, deadline.Token);
        Assert.Equal("FBFC623100000001010094FDFEFBFC610D0000000AE001013F13880F0008700234FDFE", Convert.ToHexString(actual));
        Assert.False(listener.Pending());
    }
}
