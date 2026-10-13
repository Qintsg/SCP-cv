// 原TPSimulator构造器的独立黄金帧与真实回环传输；不将本机回环当实体路由或可听声音验收。
using System.Net;
using System.Net.Sockets;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.Infrastructure.Tests;

public sealed class VenueAudioSourceProtocolTests
{
    [Theory]
    [InlineData("scp_cv", "FBFC610D0001000BE001013F13880780043801023DFDFE")]
    [InlineData("ip81", "FBFC610D0001000BE001015113880780043801024FFDFE")]
    public void NativeConstructorActualSiteParametersMatchExactly(string source, string expected)
    {
        Assert.Equal(expected, Convert.ToHexString(VenueAudioSourceProtocol.Select(source)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("window_2")]
    [InlineData("192.168.5.155")]
    [InlineData("224.1.1.64")]
    public void UnknownOrArbitrarySourcesAreRejectedBeforeConstructingPacket(string source)
    {
        Assert.Throws<VenueDeviceException>(() => VenueAudioSourceProtocol.Select(source));
    }

    [Theory]
    [InlineData("scp_cv", "FBFC610D0001000BE001013F13880780043801023DFDFE")]
    [InlineData("ip81", "FBFC610D0001000BE001015113880780043801024FFDFE")]
    public async Task HardwareTransportWritesExactlyOneNativeFrameToExplicitEndpoint(string source, string expected)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var transport = new TcpVenueAudioTransport(new VenueOptions
        { ScreenAudioHost = "127.0.0.1", ScreenAudioPort = ((IPEndPoint)listener.LocalEndpoint).Port });
        var sending = transport.SelectSourceAsync(source, budget.Token);
        using var receiver = await listener.AcceptTcpClientAsync(budget.Token);
        var frame = new byte[23]; await receiver.GetStream().ReadExactlyAsync(frame, budget.Token);
        await sending;
        Assert.Equal(expected, Convert.ToHexString(frame));
        var tail = new byte[1]; Assert.Equal(0, await receiver.GetStream().ReadAsync(tail, budget.Token));
    }
}
